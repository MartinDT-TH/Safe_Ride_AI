using System.Data;
using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Bookings;
using SafeRide.Application.Features.RiskProtection;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;

namespace SafeRide.Infrastructure.Services;

public sealed class RiskFundLedgerService : IRiskFundLedgerService
{
    public const long MainAccountId = 1;
    private readonly ApplicationDbContext _dbContext;

    public RiskFundLedgerService(ApplicationDbContext dbContext) => _dbContext = dbContext;

    public async Task<RiskFundDashboardResponse> GetDashboardAsync(CancellationToken cancellationToken)
    {
        var balance = await _dbContext.RiskFundAccounts.AsNoTracking()
            .Where(x => x.Id == MainAccountId)
            .Select(x => (decimal?)x.CurrentBalance)
            .SingleOrDefaultAsync(cancellationToken) ?? 0m;
        var transactions = _dbContext.RiskFundTransactions.AsNoTracking();
        var contributions = await SumAsync(transactions, RiskFundTransactionType.CONTRIBUTION, cancellationToken);
        var advances = await SumAsync(transactions, RiskFundTransactionType.CLAIM_ADVANCE, cancellationToken);
        var payouts = await SumAsync(transactions, RiskFundTransactionType.CLAIM_PAYOUT, cancellationToken);
        var recoveries = await transactions
            .Where(x => x.TransactionType == RiskFundTransactionType.DRIVER_RECOVERY
                || x.TransactionType == RiskFundTransactionType.CUSTOMER_RECOVERY
                || x.TransactionType == RiskFundTransactionType.THIRD_PARTY_RECOVERY
                || x.TransactionType == RiskFundTransactionType.INSURANCE_RECOVERY)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        var adjustmentCredits = await transactions
            .Where(x => x.TransactionType == RiskFundTransactionType.ADJUSTMENT
                && x.Direction == LedgerDirection.CREDIT)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        var adjustmentDebits = await transactions
            .Where(x => x.TransactionType == RiskFundTransactionType.ADJUSTMENT
                && x.Direction == LedgerDirection.DEBIT)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        var outstandingRecoveries = await _dbContext.ProtectionClaims.AsNoTracking()
            .SumAsync(x => (decimal?)x.OutstandingRecoveryAmount, cancellationToken) ?? 0m;
        var outstandingExposure = await _dbContext.ProtectionClaims.AsNoTracking()
            .Where(x => x.Status == ProtectionClaimStatus.FUNDED
                || x.Status == ProtectionClaimStatus.RECOVERY_IN_PROGRESS
                || x.Status == ProtectionClaimStatus.SETTLED
                || x.Status == ProtectionClaimStatus.CLOSED)
            .SumAsync(
                x => (decimal?)(x.RiskFundAdvanceAmount
                    - x.RecoveredAmount
                    - x.WrittenOffAdvanceAmount),
                cancellationToken) ?? 0m;
        outstandingExposure = Math.Max(0m, outstandingExposure);
        var pendingInvestigation = await _dbContext.AccidentReports.CountAsync(
            x => x.Status == AccidentStatus.UNDER_REVIEW || x.Status == AccidentStatus.LIABILITY_PENDING,
            cancellationToken);
        var pendingFunding = await _dbContext.ProtectionClaims.CountAsync(
            x => x.Status == ProtectionClaimStatus.PENDING_FUNDING,
            cancellationToken);

        return new(
            balance,
            contributions,
            advances,
            payouts,
            recoveries,
            outstandingRecoveries,
            adjustmentCredits,
            adjustmentDebits,
            outstandingExposure,
            pendingInvestigation,
            pendingFunding);
    }

    public async Task<IReadOnlyList<RiskFundTransactionResponse>> GetTransactionsAsync(
        RiskFundTransactionType? type,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken)
    {
        if (fromUtc.HasValue && toUtc.HasValue && fromUtc.Value > toUtc.Value)
        {
            throw Invalid("Thời điểm bắt đầu không được sau thời điểm kết thúc.");
        }

        var query = ApplyFilters(
            _dbContext.RiskFundTransactions.AsNoTracking(), type, fromUtc, toUtc);

        var transactions = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Take(1000)
            .ToListAsync(cancellationToken);
        return transactions.Select(Map).ToList();
    }

    public async Task ExportTransactionsAsync(
        RiskFundTransactionType? type,
        DateTime? fromUtc,
        DateTime? toUtc,
        Stream output,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(output);
        ValidateFilter(fromUtc, toUtc);

        await using var writer = new StreamWriter(
            output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), leaveOpen: true);
        await writer.WriteLineAsync(
            "Id,CreatedAtUtc,Type,Direction,Amount,BalanceBefore,BalanceAfter,TripId,ClaimId,RecoveryId,ActorUserId,ExternalReference,EvidenceUrl,Reason,IdempotencyKey");

        const int pageSize = 500;
        DateTime? cursorCreatedAt = null;
        long cursorId = 0;
        while (true)
        {
            var query = ApplyFilters(
                _dbContext.RiskFundTransactions.AsNoTracking(), type, fromUtc, toUtc);
            if (cursorCreatedAt.HasValue)
            {
                var createdAt = cursorCreatedAt.Value;
                query = query.Where(x => x.CreatedAtUtc > createdAt
                    || (x.CreatedAtUtc == createdAt && x.Id > cursorId));
            }

            var page = await query
                .OrderBy(x => x.CreatedAtUtc)
                .ThenBy(x => x.Id)
                .Take(pageSize)
                .ToListAsync(cancellationToken);
            foreach (var transaction in page)
            {
                await writer.WriteLineAsync(string.Join(',', new[]
                {
                    Csv(transaction.Id),
                    Csv(transaction.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture)),
                    Csv(transaction.TransactionType),
                    Csv(transaction.Direction),
                    Csv(transaction.Amount),
                    Csv(transaction.BalanceBefore),
                    Csv(transaction.BalanceAfter),
                    Csv(transaction.TripId),
                    Csv(transaction.ProtectionClaimId),
                    Csv(transaction.ClaimRecoveryId),
                    Csv(transaction.PerformedByUserId),
                    Csv(transaction.ExternalReference),
                    Csv(transaction.EvidenceUrl),
                    Csv(transaction.Reason),
                    Csv(transaction.IdempotencyKey)
                }));
            }

            await writer.FlushAsync(cancellationToken);
            if (page.Count < pageSize) break;
            cursorCreatedAt = page[^1].CreatedAtUtc;
            cursorId = page[^1].Id;
        }
    }

    public async Task<RiskFundMutationResponse> ApplyOpeningBalanceAsync(
        Guid adminUserId,
        RiskFundMutationRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Direction != LedgerDirection.CREDIT)
        {
            throw Invalid("Số dư đầu kỳ phải là giao dịch ghi có.");
        }

        var result = await ApplyCoreAsync(
            RiskFundTransactionType.OPENING_BALANCE,
            request.Direction,
            request.Amount,
            null,
            null,
            null,
            adminUserId,
            request.ExternalReference,
            request.EvidenceUrl,
            request.Reason,
            request.IdempotencyKey,
            cancellationToken);
        return ToAdministrativeResponse(result);
    }

    public async Task<RiskFundMutationResponse> ApplyAdjustmentAsync(
        Guid adminUserId,
        RiskFundMutationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await ApplyCoreAsync(
            RiskFundTransactionType.ADJUSTMENT,
            request.Direction,
            request.Amount,
            null,
            null,
            null,
            adminUserId,
            request.ExternalReference,
            request.EvidenceUrl,
            request.Reason,
            request.IdempotencyKey,
            cancellationToken);
        return ToAdministrativeResponse(result);
    }

    public async Task<bool> ApplyAsync(
        RiskFundTransactionType type,
        LedgerDirection direction,
        decimal amount,
        long? tripId,
        long? claimId,
        long? recoveryId,
        Guid? actorUserId,
        string? externalReference,
        string? evidenceUrl,
        string reason,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var result = await ApplyCoreAsync(
            type,
            direction,
            amount,
            tripId,
            claimId,
            recoveryId,
            actorUserId,
            externalReference,
            evidenceUrl,
            reason,
            idempotencyKey,
            cancellationToken);
        return result.Outcome == ApplyOutcome.Applied;
    }

    public async Task<bool> ApplyClaimFundingAsync(
        long claimId,
        decimal recoverableAdvance,
        decimal finalPayout,
        Guid staffUserId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        recoverableAdvance = decimal.Round(recoverableAdvance, 2, MidpointRounding.AwayFromZero);
        finalPayout = decimal.Round(finalPayout, 2, MidpointRounding.AwayFromZero);
        var parts = new List<(RiskFundTransactionType Type, decimal Amount, string Key)>();
        if (recoverableAdvance > 0)
            parts.Add((RiskFundTransactionType.CLAIM_ADVANCE, recoverableAdvance,
                finalPayout > 0 ? $"{idempotencyKey}:advance" : idempotencyKey));
        if (finalPayout > 0)
            parts.Add((RiskFundTransactionType.CLAIM_PAYOUT, finalPayout,
                recoverableAdvance > 0 ? $"{idempotencyKey}:payout" : idempotencyKey));
        if (parts.Count == 0) return true;
        if (_dbContext.Database.IsRelational()
            && _dbContext.Database.CurrentTransaction is null)
            throw new InvalidOperationException(
                "Claim funding must run inside the serializable claim transaction.");

        var account = await LoadAccountForUpdateAsync(cancellationToken);
        var keys = parts.Select(x => x.Key).ToArray();
        var existing = await _dbContext.RiskFundTransactions.AsNoTracking()
            .Where(x => keys.Contains(x.IdempotencyKey))
            .ToListAsync(cancellationToken);
        if (existing.Count > 0)
        {
            var isExactReplay = existing.Count == parts.Count
                && parts.All(part => existing.Any(transaction =>
                    transaction.IdempotencyKey == part.Key
                    && transaction.ProtectionClaimId == claimId
                    && transaction.TransactionType == part.Type
                    && transaction.Direction == LedgerDirection.DEBIT
                    && transaction.Amount == part.Amount));
            if (!isExactReplay)
                throw Conflict(
                    "risk_protection.funding_idempotency_conflict",
                    "Idempotency key Ä‘Ã£ Ä‘Æ°á»£c dÃ¹ng cho má»™t lá»‡nh cáº¥p vá»‘n khÃ¡c.");
            return true;
        }

        var total = parts.Sum(x => x.Amount);
        if (account is null || account.CurrentBalance < total) return false;

        var balance = account.CurrentBalance;
        var now = DateTime.UtcNow;
        foreach (var part in parts)
        {
            var after = balance - part.Amount;
            _dbContext.RiskFundTransactions.Add(new RiskFundTransaction
            {
                RiskFundAccount = account,
                TransactionType = part.Type,
                Direction = LedgerDirection.DEBIT,
                Amount = part.Amount,
                BalanceBefore = balance,
                BalanceAfter = after,
                ProtectionClaimId = claimId,
                PerformedByUserId = staffUserId,
                ExternalReference = $"CLM-{claimId}",
                Reason = part.Type == RiskFundTransactionType.CLAIM_ADVANCE
                    ? "Recoverable SafeRide claim advance"
                    : "Final SafeRide claim payout",
                IdempotencyKey = part.Key,
                CreatedAtUtc = now
            });
            balance = after;
        }
        account.CurrentBalance = balance;
        account.UpdatedAtUtc = now;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<LedgerApplyResult> ApplyCoreAsync(
        RiskFundTransactionType type,
        LedgerDirection direction,
        decimal amount,
        long? tripId,
        long? claimId,
        long? recoveryId,
        Guid? actorUserId,
        string? externalReference,
        string? evidenceUrl,
        string reason,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!_dbContext.Database.IsRelational())
        {
            return await ApplyCoreWithinAmbientAsync(
                type, direction, amount, tripId, claimId, recoveryId, actorUserId,
                externalReference, evidenceUrl, reason, idempotencyKey, cancellationToken);
        }

        if (_dbContext.Database.CurrentTransaction is not null)
        {
            if (_dbContext.Database.CurrentTransaction.GetDbTransaction().IsolationLevel
                != IsolationLevel.Serializable)
            {
                throw new InvalidOperationException(
                    "Risk Fund mutations require an existing serializable transaction.");
            }

            return await ApplyCoreWithinAmbientAsync(
                type, direction, amount, tripId, claimId, recoveryId, actorUserId,
                externalReference, evidenceUrl, reason, idempotencyKey, cancellationToken);
        }

        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            _dbContext.ChangeTracker.Clear();
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            var result = await ApplyCoreWithinAmbientAsync(
                type, direction, amount, tripId, claimId, recoveryId, actorUserId,
                externalReference, evidenceUrl, reason, idempotencyKey, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }

    private async Task<LedgerApplyResult> ApplyCoreWithinAmbientAsync(
        RiskFundTransactionType type,
        LedgerDirection direction,
        decimal amount,
        long? tripId,
        long? claimId,
        long? recoveryId,
        Guid? actorUserId,
        string? externalReference,
        string? evidenceUrl,
        string reason,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        amount = decimal.Round(amount, 0, MidpointRounding.AwayFromZero);
        reason = NormalizeRequired(reason, 1000, "Lý do");
        idempotencyKey = NormalizeRequired(idempotencyKey, 100, "Idempotency key");
        externalReference = NormalizeOptional(externalReference, 200, "Tham chiếu giao dịch");
        evidenceUrl = NormalizeOptional(evidenceUrl, 1000, "Bằng chứng");
        ValidateMutation(
            type,
            direction,
            amount,
            tripId,
            claimId,
            recoveryId,
            actorUserId,
            externalReference,
            evidenceUrl);

        var ownsTransaction = _dbContext.Database.IsRelational()
            && _dbContext.Database.CurrentTransaction is null;
        IDbContextTransaction? transaction = null;
        if (ownsTransaction)
        {
            transaction = await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
        }

        try
        {
            // Every mutation locks the singleton projection before reading idempotency
            // state. A concurrent retry therefore observes the transaction committed by
            // the first writer instead of racing into the unique index.
            var account = await LoadAccountForUpdateAsync(cancellationToken);
            var existing = await _dbContext.RiskFundTransactions.AsNoTracking()
                .SingleOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);
            if (existing is not null)
            {
                EnsureSameMutation(
                    existing,
                    type,
                    direction,
                    amount,
                    tripId,
                    claimId,
                    recoveryId,
                    actorUserId,
                    externalReference,
                    evidenceUrl,
                    reason);
                if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                return new LedgerApplyResult(ApplyOutcome.Replayed, existing);
            }

            if (type == RiskFundTransactionType.CONTRIBUTION && tripId.HasValue)
            {
                var existingContribution = await _dbContext.RiskFundTransactions.AsNoTracking()
                    .SingleOrDefaultAsync(
                        x => x.TripId == tripId.Value
                            && x.TransactionType == RiskFundTransactionType.CONTRIBUTION,
                        cancellationToken);
                if (existingContribution is not null)
                {
                    EnsureSameMutationExceptIdempotency(
                        existingContribution,
                        type,
                        direction,
                        amount,
                        tripId,
                        claimId,
                        recoveryId,
                        actorUserId,
                        externalReference,
                        evidenceUrl,
                        reason);
                    if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                    return new LedgerApplyResult(ApplyOutcome.Replayed, existingContribution);
                }
            }

            if (type == RiskFundTransactionType.OPENING_BALANCE
                && await _dbContext.RiskFundTransactions.AnyAsync(
                    x => x.RiskFundAccountId == MainAccountId,
                    cancellationToken))
            {
                throw Conflict(
                    "risk_fund.opening_balance_exists",
                    "Chỉ được ghi số dư đầu kỳ trước giao dịch đầu tiên của quỹ.");
            }

            if (account is null && direction == LedgerDirection.DEBIT)
            {
                if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                return new LedgerApplyResult(ApplyOutcome.InsufficientBalance, null);
            }

            if (account is null)
            {
                account = new RiskFundAccount
                {
                    Id = MainAccountId,
                    CurrentBalance = 0m,
                    UpdatedAtUtc = DateTime.UtcNow
                };
                _dbContext.RiskFundAccounts.Add(account);
            }

            var balanceBefore = account.CurrentBalance;
            var balanceAfter = direction == LedgerDirection.CREDIT
                ? balanceBefore + amount
                : balanceBefore - amount;
            if (balanceAfter < 0)
            {
                if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                return new LedgerApplyResult(ApplyOutcome.InsufficientBalance, null);
            }

            var now = DateTime.UtcNow;
            account.CurrentBalance = balanceAfter;
            account.UpdatedAtUtc = now;
            var ledgerTransaction = new RiskFundTransaction
            {
                RiskFundAccount = account,
                TransactionType = type,
                Direction = direction,
                Amount = amount,
                BalanceBefore = balanceBefore,
                BalanceAfter = balanceAfter,
                TripId = tripId,
                ProtectionClaimId = claimId,
                ClaimRecoveryId = recoveryId,
                PerformedByUserId = actorUserId,
                ExternalReference = externalReference,
                EvidenceUrl = evidenceUrl,
                Reason = reason,
                IdempotencyKey = idempotencyKey,
                CreatedAtUtc = now
            };
            _dbContext.RiskFundTransactions.Add(ledgerTransaction);
            await _dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return new LedgerApplyResult(ApplyOutcome.Applied, ledgerTransaction);
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            if (transaction is not null) await transaction.DisposeAsync();
        }
    }

    private static void ValidateMutation(
        RiskFundTransactionType type,
        LedgerDirection direction,
        decimal amount,
        long? tripId,
        long? claimId,
        long? recoveryId,
        Guid? actorUserId,
        string? externalReference,
        string? evidenceUrl)
    {
        if (amount <= 0) throw Invalid("Số tiền giao dịch phải lớn hơn 0.");

        var expectedDirection = type switch
        {
            RiskFundTransactionType.OPENING_BALANCE
                or RiskFundTransactionType.CONTRIBUTION
                or RiskFundTransactionType.DRIVER_RECOVERY
                or RiskFundTransactionType.CUSTOMER_RECOVERY
                or RiskFundTransactionType.THIRD_PARTY_RECOVERY
                or RiskFundTransactionType.INSURANCE_RECOVERY => LedgerDirection.CREDIT,
            RiskFundTransactionType.CLAIM_ADVANCE
                or RiskFundTransactionType.CLAIM_PAYOUT => LedgerDirection.DEBIT,
            RiskFundTransactionType.ADJUSTMENT => direction,
            _ => throw Invalid("Loại giao dịch Risk Fund không hợp lệ.")
        };
        if (direction != expectedDirection)
        {
            throw Invalid("Chiều ghi sổ không phù hợp với loại giao dịch Risk Fund.");
        }

        var linksAreValid = type switch
        {
            RiskFundTransactionType.OPENING_BALANCE or RiskFundTransactionType.ADJUSTMENT =>
                tripId is null && claimId is null && recoveryId is null,
            RiskFundTransactionType.CONTRIBUTION =>
                tripId.HasValue && claimId is null && recoveryId is null,
            RiskFundTransactionType.CLAIM_ADVANCE or RiskFundTransactionType.CLAIM_PAYOUT =>
                tripId is null && claimId.HasValue && recoveryId is null,
            RiskFundTransactionType.DRIVER_RECOVERY
                or RiskFundTransactionType.CUSTOMER_RECOVERY
                or RiskFundTransactionType.THIRD_PARTY_RECOVERY
                or RiskFundTransactionType.INSURANCE_RECOVERY =>
                tripId is null && claimId.HasValue && recoveryId.HasValue,
            _ => false
        };
        if (!linksAreValid)
        {
            throw Invalid("Liên kết Trip, Claim hoặc Recovery không phù hợp với loại giao dịch.");
        }

        if (type != RiskFundTransactionType.CONTRIBUTION
            && (!actorUserId.HasValue || actorUserId.Value == Guid.Empty))
        {
            throw Invalid("Người thực hiện giao dịch là bắt buộc.");
        }

        if (type is RiskFundTransactionType.OPENING_BALANCE or RiskFundTransactionType.ADJUSTMENT
            && (externalReference is null || evidenceUrl is null))
        {
            throw Invalid("Giao dịch quản trị phải có tham chiếu và bằng chứng.");
        }
    }

    private static void EnsureSameMutation(
        RiskFundTransaction existing,
        RiskFundTransactionType type,
        LedgerDirection direction,
        decimal amount,
        long? tripId,
        long? claimId,
        long? recoveryId,
        Guid? actorUserId,
        string? externalReference,
        string? evidenceUrl,
        string reason)
    {
        if (IsSameMutation(
                existing,
                type,
                direction,
                amount,
                tripId,
                claimId,
                recoveryId,
                actorUserId,
                externalReference,
                evidenceUrl,
                reason))
        {
            return;
        }

        throw Conflict(
            "risk_fund.idempotency_conflict",
            "Idempotency key đã được dùng cho một giao dịch Risk Fund khác.");
    }

    private static void EnsureSameMutationExceptIdempotency(
        RiskFundTransaction existing,
        RiskFundTransactionType type,
        LedgerDirection direction,
        decimal amount,
        long? tripId,
        long? claimId,
        long? recoveryId,
        Guid? actorUserId,
        string? externalReference,
        string? evidenceUrl,
        string reason)
    {
        if (IsSameMutation(
                existing,
                type,
                direction,
                amount,
                tripId,
                claimId,
                recoveryId,
                actorUserId,
                externalReference,
                evidenceUrl,
                reason))
        {
            return;
        }

        throw Conflict(
            "risk_fund.contribution_conflict",
            "Trip đã có một khoản đóng góp Risk Fund với dữ liệu khác.");
    }

    private static bool IsSameMutation(
        RiskFundTransaction existing,
        RiskFundTransactionType type,
        LedgerDirection direction,
        decimal amount,
        long? tripId,
        long? claimId,
        long? recoveryId,
        Guid? actorUserId,
        string? externalReference,
        string? evidenceUrl,
        string reason)
        => existing.TransactionType == type
            && existing.Direction == direction
            && existing.Amount == amount
            && existing.TripId == tripId
            && existing.ProtectionClaimId == claimId
            && existing.ClaimRecoveryId == recoveryId
            && existing.PerformedByUserId == actorUserId
            && existing.ExternalReference == externalReference
            && existing.EvidenceUrl == evidenceUrl
            && existing.Reason == reason;

    private static RiskFundMutationResponse ToAdministrativeResponse(LedgerApplyResult result)
    {
        if (result.Outcome == ApplyOutcome.InsufficientBalance)
        {
            throw Conflict(
                "risk_fund.insufficient_balance",
                "Số dư Risk Fund không đủ để thực hiện toàn bộ giao dịch.");
        }

        return new RiskFundMutationResponse(
            result.Outcome == ApplyOutcome.Applied,
            Map(result.Transaction!));
    }

    private static RiskFundTransactionResponse Map(RiskFundTransaction transaction) => new(
        transaction.Id,
        transaction.TransactionType,
        transaction.Direction,
        transaction.Amount,
        transaction.BalanceBefore,
        transaction.BalanceAfter,
        transaction.TripId,
        transaction.ProtectionClaimId,
        transaction.ClaimRecoveryId,
        transaction.PerformedByUserId,
        transaction.ExternalReference,
        transaction.EvidenceUrl,
        transaction.Reason,
        transaction.IdempotencyKey,
        transaction.CreatedAtUtc);

    private static string NormalizeRequired(string value, int maxLength, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw Invalid($"{fieldName} là bắt buộc.");
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw Invalid($"{fieldName} không được vượt quá {maxLength} ký tự.");
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw Invalid($"{fieldName} không được vượt quá {maxLength} ký tự.");
        }

        return normalized;
    }

    private Task<RiskFundAccount?> LoadAccountForUpdateAsync(CancellationToken cancellationToken)
    {
        if (_dbContext.Database.IsSqlServer())
        {
            return _dbContext.RiskFundAccounts
                .FromSqlInterpolated($"SELECT * FROM [RiskFundAccounts] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {MainAccountId}")
                .SingleOrDefaultAsync(cancellationToken);
        }

        return _dbContext.RiskFundAccounts
            .SingleOrDefaultAsync(x => x.Id == MainAccountId, cancellationToken);
    }

    private static async Task<decimal> SumAsync(
        IQueryable<RiskFundTransaction> source,
        RiskFundTransactionType type,
        CancellationToken cancellationToken)
        => await source.Where(x => x.TransactionType == type)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;

    private static IQueryable<RiskFundTransaction> ApplyFilters(
        IQueryable<RiskFundTransaction> query,
        RiskFundTransactionType? type,
        DateTime? fromUtc,
        DateTime? toUtc)
    {
        if (type.HasValue) query = query.Where(x => x.TransactionType == type.Value);
        if (fromUtc.HasValue) query = query.Where(x => x.CreatedAtUtc >= fromUtc.Value);
        if (toUtc.HasValue) query = query.Where(x => x.CreatedAtUtc <= toUtc.Value);
        return query;
    }

    private static void ValidateFilter(DateTime? fromUtc, DateTime? toUtc)
    {
        if (fromUtc.HasValue && toUtc.HasValue && fromUtc.Value > toUtc.Value)
            throw Invalid("Thời điểm bắt đầu không được sau thời điểm kết thúc.");
    }

    private static string Csv(object? value)
    {
        var text = value switch
        {
            null => string.Empty,
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
        return $"\"{text.Replace("\"", "\"\"")}\"";
    }

    private static BookingException Invalid(string detail) => new(
        "risk_fund.invalid_transaction",
        detail,
        StatusCodes.Status400BadRequest);

    private static BookingException Conflict(string code, string detail) => new(
        code,
        detail,
        StatusCodes.Status409Conflict);

    private enum ApplyOutcome
    {
        Applied,
        Replayed,
        InsufficientBalance
    }

    private sealed record LedgerApplyResult(
        ApplyOutcome Outcome,
        RiskFundTransaction? Transaction);
}
