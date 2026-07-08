namespace SafeRide.Application.Features.Auth;

public enum TripContinuationOperation
{
    ActiveTripRead,
    BookingRead,
    BookingCancel,
    DriverLocation,
    TripStatusUpdate,
    TripReturnConfirmation,
    TripPayment,
    TripRating,
    SignalRJoinTrip,
    SignalRJoinBooking
}
