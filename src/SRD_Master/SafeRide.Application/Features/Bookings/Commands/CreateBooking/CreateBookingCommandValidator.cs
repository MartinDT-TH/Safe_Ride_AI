using FluentValidation;

namespace SafeRide.Application.Features.Bookings.Commands.CreateBooking;

public sealed class CreateBookingCommandValidator
    : AbstractValidator<CreateBookingCommand>
{
    public CreateBookingCommandValidator()
    {
        RuleFor(command => command.CustomerId)
            .NotEmpty();

        RuleFor(command => command.VehicleId)
            .GreaterThan(0);

        RuleFor(command => command.PickupLatitude)
            .InclusiveBetween(-90, 90);

        RuleFor(command => command.PickupLongitude)
            .InclusiveBetween(-180, 180);

        When(HasDestination, () =>
        {
            RuleFor(command => command.DestinationLatitude)
                .InclusiveBetween(-90, 90);

            RuleFor(command => command.DestinationLongitude)
                .InclusiveBetween(-180, 180);
        });
    }

    private static bool HasDestination(CreateBookingCommand command)
    {
        return !string.IsNullOrWhiteSpace(command.DestinationAddress)
            || command.DestinationLatitude != default
            || command.DestinationLongitude != default;
    }
}
