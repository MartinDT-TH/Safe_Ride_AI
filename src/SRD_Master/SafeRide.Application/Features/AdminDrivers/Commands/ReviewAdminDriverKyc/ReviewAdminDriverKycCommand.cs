using MediatR;
using SafeRide.Domain.Enums;

namespace SafeRide.Application.Features.AdminDrivers.Commands.ReviewAdminDriverKyc;

public sealed record ReviewAdminDriverKycCommand(
    Guid DriverId,
    KycStatus Status,
    string? RejectionReason) : IRequest<AdminDriverResponse>;
