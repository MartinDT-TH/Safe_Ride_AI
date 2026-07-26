using MediatR;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Contracts.Responses.Pricing;

namespace SafeRide.Application.Features.Pricing.Commands.UpdateAdminPricingRule;

public sealed class UpdateAdminPricingRuleCommandHandler
    : IRequestHandler<UpdateAdminPricingRuleCommand, AdminPricingRuleResponse>
{
    private readonly IAdminPricingRuleRepository _pricingRuleRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateAdminPricingRuleCommandHandler(
        IAdminPricingRuleRepository pricingRuleRepository,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork)
    {
        _pricingRuleRepository = pricingRuleRepository;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
    }

    public async Task<AdminPricingRuleResponse> Handle(
        UpdateAdminPricingRuleCommand request,
        CancellationToken cancellationToken)
    {
        var pricingRule = await _pricingRuleRepository.GetByIdAsync(
            request.PricingRuleId,
            cancellationToken);
        if (pricingRule is null)
        {
            throw new PricingRuleException(
                "admin_pricing_rule.not_found",
                "Không tìm thấy cấu hình giá.",
                404);
        }

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

        pricingRule.VehicleClass = request.VehicleClass;
        pricingRule.ServiceTypeId = serviceType!.Id;
        pricingRule.ServiceType = serviceType;
        pricingRule.BaseFare = request.BaseFare;
        pricingRule.MinFare = request.MinFare;
        pricingRule.PricePerKm = request.PricePerKm;
        pricingRule.PricePerHour = request.PricePerHour;
        pricingRule.IsActive = request.IsActive;
        pricingRule.UpdatedAt = _dateTimeProvider.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _pricingRuleRepository.InvalidateActivePricingRulesCacheAsync(
            cancellationToken);

        return AdminPricingRuleRules.ToResponse(pricingRule);
    }
}
