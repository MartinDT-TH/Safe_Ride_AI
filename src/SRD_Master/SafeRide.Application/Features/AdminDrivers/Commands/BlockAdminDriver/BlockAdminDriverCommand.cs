using MediatR;

namespace SafeRide.Application.Features.AdminDrivers.Commands.BlockAdminDriver;

public sealed record BlockAdminDriverCommand(
    Guid DriverId,
    string? Reason) : IRequest<AdminDriverResponse>;
