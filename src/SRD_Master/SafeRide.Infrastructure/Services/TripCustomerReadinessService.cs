using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Realtime;
using SafeRide.Application.Features.Bookings;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;

namespace SafeRide.Infrastructure.Services;

public sealed class TripCustomerReadinessService : ITripCustomerReadinessService
{
    private const string ReadyMessage = "Tôi đã sẵn sàng";
    private const string ComingMessage = "Tôi đang đến";
    private readonly ApplicationDbContext _dbContext;
    private readonly IRealtimeNotificationService _realtime;

    public TripCustomerReadinessService(
        ApplicationDbContext dbContext,
        IRealtimeNotificationService realtime)
    {
        _dbContext = dbContext;
        _realtime = realtime;
    }

    public async Task ReportAsync(Guid customerId, long tripId, string message, CancellationToken cancellationToken)
    {
        var trip = await _dbContext.Trips
            .AsNoTracking()
            .Include(x => x.Booking)
            .SingleOrDefaultAsync(x => x.Id == tripId, cancellationToken)
            ?? throw new BookingException("trip.not_found", "Không tìm thấy chuyến đi.", StatusCodes.Status404NotFound);

        if (trip.Booking.CustomerId != customerId)
            throw new BookingException("trip.customer_readiness_forbidden", "Bạn không có quyền báo trạng thái cho chuyến đi này.", StatusCodes.Status403Forbidden);
        if (trip.TripStatus is not (TripStatus.DRIVER_ARRIVING or TripStatus.ARRIVED))
            throw new BookingException("trip.customer_readiness_invalid_status", "Chỉ có thể báo trạng thái khi tài xế đang đến hoặc đã đến điểm đón.", StatusCodes.Status409Conflict);
        if (message is not ReadyMessage and not ComingMessage)
            throw new BookingException("trip.customer_readiness_invalid_message", "Nội dung báo trạng thái không hợp lệ.", StatusCodes.Status400BadRequest);

        var driverMessage = message == ReadyMessage
            ? "Khách hàng đã báo: Tôi đã sẵn sàng."
            : "Khách hàng đang đến điểm đón.";
        await _realtime.PublishCustomerReadinessReportedAsync(
            new CustomerReadinessReportedEvent(
                trip.Id, trip.BookingId, customerId, trip.DriverId, driverMessage, DateTime.UtcNow),
            cancellationToken);
    }
}
