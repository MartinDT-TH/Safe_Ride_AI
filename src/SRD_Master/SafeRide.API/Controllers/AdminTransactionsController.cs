using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;
using SafeRide.Domain.Entities;
using System.Data;

namespace SafeRide.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/transactions")]
public sealed class AdminTransactionsController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public AdminTransactionsController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    [ProducesResponseType<AdminTransactionsResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminTransactionsResponse>> GetTransactions(
        [FromQuery] string? status,
        [FromQuery] string? method,
        [FromQuery] DateOnly? date,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var payments = _db.Payments.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!TryParseStatus(status, out var parsedStatus))
                return BadRequest(new ProblemDetails { Detail = "Trạng thái giao dịch không hợp lệ." });
            payments = payments.Where(x => x.PaymentStatus == parsedStatus);
        }
        if (!string.IsNullOrWhiteSpace(method))
        {
            if (!Enum.TryParse<PaymentMethod>(method, true, out var parsedMethod))
                return BadRequest(new ProblemDetails { Detail = "Phương thức thanh toán không hợp lệ." });
            payments = payments.Where(x => x.PaymentMethod == parsedMethod);
        }
        if (date.HasValue)
        {
            var start = date.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var end = start.AddDays(1);
            payments = payments.Where(x => x.CreatedAt >= start && x.CreatedAt < end);
        }

        var totalItems = await payments.CountAsync(cancellationToken);
        var items = await payments
            .OrderByDescending(x => x.PaidAt ?? x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AdminTransactionItem(
                x.Id,
                x.TripId,
                x.Trip.Booking.Customer.FullName ?? "Khách hàng SafeRide",
                MaskPhone(x.Trip.Booking.Customer.PhoneNumber),
                x.Amount,
                x.Currency,
                x.PaymentMethod,
                x.PaymentStatus,
                x.PaidAt ?? x.CreatedAt))
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var currentStart = now.AddDays(-30);
        var previousStart = currentStart.AddDays(-30);
        var stats = await BuildStats(currentStart, previousStart, cancellationToken);

        return Ok(new AdminTransactionsResponse(
            stats,
            items,
            page,
            pageSize,
            totalItems,
            Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize))));
    }

    [HttpGet("withdrawals")]
    [ProducesResponseType<AdminWithdrawalsResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminWithdrawalsResponse>> GetWithdrawals(
        [FromQuery] WithdrawalRequestStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = _db.WithdrawalRequests.AsNoTracking();
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);

        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.Status == WithdrawalRequestStatus.Pending ? 0 : 1)
            .ThenByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AdminWithdrawalItem(
                x.Id,
                x.Wallet.DriverId,
                x.Wallet.Driver.Driver.FullName ?? "Tài xế SafeRide",
                x.Wallet.Driver.Driver.AvatarUrl,
                x.BankName,
                x.BankAccountNumber,
                x.BankAccountName,
                x.Amount,
                x.Status,
                x.CreatedAt,
                x.ProcessedAt,
                x.RejectionReason))
            .ToListAsync(cancellationToken);

        var summary = await _db.WithdrawalRequests.AsNoTracking()
            .GroupBy(_ => 1)
            .Select(group => new AdminWithdrawalStats(
                group.Count(),
                group.Count(x => x.Status == WithdrawalRequestStatus.Pending),
                group.Sum(x => x.Amount)))
            .SingleOrDefaultAsync(cancellationToken)
            ?? new AdminWithdrawalStats(0, 0, 0m);

        return Ok(new AdminWithdrawalsResponse(
            summary, items, page, pageSize, totalItems,
            Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize))));
    }

    [HttpPost("withdrawals/{id:long}/approve")]
    public async Task<IActionResult> ApproveWithdrawal(
        long id,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var request = await _db.WithdrawalRequests
            .Include(x => x.Wallet)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (request is null) return NotFound();
        if (request.Status != WithdrawalRequestStatus.Pending)
            return Conflict(new ProblemDetails { Detail = "Yêu cầu này đã được xử lý." });
        if (request.Wallet.CurrentBalance < request.Amount)
            return Conflict(new ProblemDetails { Detail = "Số dư ví hiện tại không đủ để duyệt." });

        request.Wallet.CurrentBalance -= request.Amount;
        request.Status = WithdrawalRequestStatus.Approved;
        request.ProcessedAt = DateTime.UtcNow;
        _db.WalletTransactions.Add(new WalletTransaction
        {
            WalletId = request.WalletId,
            TransactionType = WalletTransactionType.Withdrawal,
            Amount = request.Amount,
            Description = $"Rút tiền #{request.Id} về {request.BankName}",
            CreatedAt = request.ProcessedAt.Value
        });
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("withdrawals/{id:long}/reject")]
    public async Task<IActionResult> RejectWithdrawal(
        long id,
        [FromBody] RejectWithdrawalRequest? body,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var request = await _db.WithdrawalRequests
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (request is null) return NotFound();
        if (request.Status != WithdrawalRequestStatus.Pending)
            return Conflict(new ProblemDetails { Detail = "Yêu cầu này đã được xử lý." });

        request.Status = WithdrawalRequestStatus.Rejected;
        request.RejectionReason = string.IsNullOrWhiteSpace(body?.Reason)
            ? "Từ chối bởi quản trị viên"
            : body.Reason.Trim()[..Math.Min(body.Reason.Trim().Length, 255)];
        request.ProcessedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return NoContent();
    }

    private async Task<AdminTransactionStats> BuildStats(
        DateTime currentStart,
        DateTime previousStart,
        CancellationToken cancellationToken)
    {
        var current = await _db.Payments.AsNoTracking()
            .Where(x => x.CreatedAt >= currentStart)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Revenue = group.Where(x => x.PaymentStatus == PaymentStatus.Success).Sum(x => (decimal?)x.Amount) ?? 0m,
                Success = group.Count(x => x.PaymentStatus == PaymentStatus.Success),
                Failed = group.Count(x => x.PaymentStatus == PaymentStatus.Failed)
            })
            .SingleOrDefaultAsync(cancellationToken);
        var previous = await _db.Payments.AsNoTracking()
            .Where(x => x.CreatedAt >= previousStart && x.CreatedAt < currentStart)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Revenue = group.Where(x => x.PaymentStatus == PaymentStatus.Success).Sum(x => (decimal?)x.Amount) ?? 0m,
                Success = group.Count(x => x.PaymentStatus == PaymentStatus.Success),
                Failed = group.Count(x => x.PaymentStatus == PaymentStatus.Failed)
            })
            .SingleOrDefaultAsync(cancellationToken);
        var pendingWithdrawals = await _db.WithdrawalRequests.AsNoTracking()
            .CountAsync(x => x.Status == WithdrawalRequestStatus.Pending, cancellationToken);

        return new AdminTransactionStats(
            current?.Revenue ?? 0m,
            current?.Success ?? 0,
            current?.Failed ?? 0,
            pendingWithdrawals,
            Growth(current?.Revenue ?? 0m, previous?.Revenue ?? 0m),
            Growth(current?.Success ?? 0, previous?.Success ?? 0),
            Growth(current?.Failed ?? 0, previous?.Failed ?? 0));
    }

    private static bool TryParseStatus(string value, out PaymentStatus status)
    {
        status = value.Trim().ToLowerInvariant() switch
        {
            "success" => PaymentStatus.Success,
            "pending" => PaymentStatus.Pending,
            "failed" => PaymentStatus.Failed,
            _ => (PaymentStatus)(-1)
        };
        return (int)status >= 0;
    }

    private static string MaskPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone) || phone.Length < 7) return "—";
        return $"{phone[..3]}***{phone[^4..]}";
    }

    private static decimal? Growth(decimal current, decimal previous) =>
        previous == 0 ? current == 0 ? 0 : null : Math.Round((current - previous) / previous * 100m, 1);
}

public sealed record AdminTransactionsResponse(
    AdminTransactionStats Stats,
    IReadOnlyCollection<AdminTransactionItem> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);

public sealed record AdminTransactionStats(
    decimal TotalRevenue,
    int SuccessfulTransactions,
    int FailedTransactions,
    int PendingWithdrawals,
    decimal? RevenueGrowthPercent,
    decimal? SuccessGrowthPercent,
    decimal? FailedGrowthPercent);

public sealed record AdminTransactionItem(
    long Id,
    long TripId,
    string CustomerName,
    string MaskedPhone,
    decimal Amount,
    string Currency,
    PaymentMethod Method,
    PaymentStatus Status,
    DateTime PerformedAt);

public sealed record AdminWithdrawalsResponse(
    AdminWithdrawalStats Stats,
    IReadOnlyCollection<AdminWithdrawalItem> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);
public sealed record AdminWithdrawalStats(int TotalRequests, int PendingRequests, decimal TotalAmount);
public sealed record AdminWithdrawalItem(long Id, Guid DriverId, string DriverName, string? AvatarUrl,
    string BankName, string BankAccountNumber, string BankAccountName, decimal Amount,
    WithdrawalRequestStatus Status, DateTime CreatedAt, DateTime? ProcessedAt, string? RejectionReason);
public sealed record RejectWithdrawalRequest(string? Reason);
