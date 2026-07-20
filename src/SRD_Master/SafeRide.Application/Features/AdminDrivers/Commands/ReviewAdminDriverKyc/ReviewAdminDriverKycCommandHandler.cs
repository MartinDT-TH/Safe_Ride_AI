using MediatR;
using SafeRide.Application.Common.Interfaces;

namespace SafeRide.Application.Features.AdminDrivers.Commands.ReviewAdminDriverKyc;

public sealed class ReviewAdminDriverKycCommandHandler
    : IRequestHandler<ReviewAdminDriverKycCommand, AdminDriverResponse>
{
    private readonly IAdminDriverAccountService _adminDriverAccountService;

    public ReviewAdminDriverKycCommandHandler(IAdminDriverAccountService adminDriverAccountService)
    {
        _adminDriverAccountService = adminDriverAccountService;
    }

    public Task<AdminDriverResponse> Handle(
        ReviewAdminDriverKycCommand request,
        CancellationToken cancellationToken)
    {
        return _adminDriverAccountService.ReviewKycAsync(
            request.DriverId,
            request.Status,
            request.RejectionReason,
            cancellationToken);
    }
}
