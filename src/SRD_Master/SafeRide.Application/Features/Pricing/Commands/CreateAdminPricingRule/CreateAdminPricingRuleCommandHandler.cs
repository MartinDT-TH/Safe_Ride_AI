using MediatR;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Contracts.Responses.Pricing;
using SafeRide.Domain.Entities;

namespace SafeRide.Application.Features.Pricing.Commands.CreateAdminPricingRule;

public sealed class CreateAdminPricingRuleCommandHandler
    : IRequestHandler<CreateAdminPricingRuleCommand, AdminPricingRuleResponse>
{
    private readonly IAdminPricingRuleRepository _pricingRuleRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;

    public CreateAdminPricingRuleCommandHandler(
        IAdminPricingRuleRepository pricingRuleRepository,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork)
    {
        _pricingRuleRepository = pricingRuleRepository;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
    }

    public async Task<AdminPricingRuleResponse> Handle(
        CreateAdminPricingRuleCommand request,
        CancellationToken cancellationToken)
    {
        var serviceType = await _pricingRuleRepository.GetServiceTypeAsync(
            request.ServiceTypeId,
            cancellationToken);

        AdminPricingRuleRules.Validate(
            request.VehicleClass,
            serviceType,
            request.BaseFare,
            request.MinFare,
            request.PricePerKm,
            request.PricePerHour);

        var utcNow = _dateTimeProvider.UtcNow;
        var pricingRule = new PricingRule
        {
            VehicleClass = request.VehicleClass,
            ServiceTypeId = serviceType!.Id,
            ServiceType = serviceType,
            BaseFare = request.BaseFare,
            MinFare = request.MinFare,
            PricePerKm = request.PricePerKm,
            PricePerHour = request.PricePerHour,
            IsActive = request.IsActive,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };

        await _pricingRuleRepository.AddAsync(pricingRule, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _pricingRuleRepository.InvalidateActivePricingRulesCacheAsync(
            cancellationToken);

        return AdminPricingRuleRules.ToResponse(pricingRule);
    }
}
