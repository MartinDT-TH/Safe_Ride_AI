using MediatR;

namespace SafeRide.Application.Features.AdminDrivers.Commands.UnlockAdminDriver;

public sealed record UnlockAdminDriverCommand(
    Guid DriverId) : IRequest<AdminDriverResponse>;
