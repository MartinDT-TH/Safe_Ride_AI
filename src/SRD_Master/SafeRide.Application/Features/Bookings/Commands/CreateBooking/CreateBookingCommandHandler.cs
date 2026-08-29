using MediatR;
using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;
using SafeRide.Application.Common.Exceptions;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Models;
using SafeRide.Application.Common.Realtime;
using SafeRide.Application.Features.Promotions;
using SafeRide.Application.Features.Bookings.Services;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;

namespace SafeRide.Application.Features.Bookings.Commands.CreateBooking;

public sealed class CreateBookingCommandHandler
    : IRequestHandler<CreateBookingCommand, CreateBookingResponse>
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IMapRoutingService _mapRoutingService;
    private readonly IFareEstimationService _fareEstimationService;
    private readonly IBookingMatchingService _matchingService;
    private readonly IVehicleLicenseRequirementService _vehicleLicenseRequirementService;
    private readonly IRealtimeNotificationService _realtimeNotificationService;
    private readonly IPromotionRepository _promotionRepository;
    private readonly IMatchingPolicyProvider _matchingPolicyProvider;
    private readonly IBookingLifecycleJobScheduler _jobScheduler;
    private readonly IPromotionUnlockRuleStore _promotionUnlockRuleStore;
    private readonly ICustomerBookingPrivilegeService _customerBookingPrivilegeService;
    private readonly DriverCompensationOptions _compensationOptions;

    public CreateBookingCommandHandler(
        IBookingRepository bookingRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider,
        IMapRoutingService mapRoutingService,
        IFareEstimationService fareEstimationService,
        IBookingMatchingService matchingService,
        IVehicleLicenseRequirementService vehicleLicenseRequirementService,
        IRealtimeNotificationService realtimeNotificationService,
        IPromotionRepository promotionRepository,
        IPromotionUnlockRuleStore promotionUnlockRuleStore,
        IMatchingPolicyProvider matchingPolicyProvider,
        IBookingLifecycleJobScheduler jobScheduler,
        IOptions<DriverCompensationOptions> compensationOptions,
        ICustomerBookingPrivilegeService customerBookingPrivilegeService)
    {
        _bookingRepository = bookingRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
        _mapRoutingService = mapRoutingService;
        _fareEstimationService = fareEstimationService;
        _matchingService = matchingService;
        _vehicleLicenseRequirementService = vehicleLicenseRequirementService;
        _realtimeNotificationService = realtimeNotificationService;
        _promotionRepository = promotionRepository;
        _promotionUnlockRuleStore = promotionUnlockRuleStore;
        _matchingPolicyProvider = matchingPolicyProvider;
        _jobScheduler = jobScheduler;
        _compensationOptions = compensationOptions.Value;
        _customerBookingPrivilegeService = customerBookingPrivilegeService;
    }

    public async Task<CreateBookingResponse> Handle(
        CreateBookingCommand request,
        CancellationToken cancellationToken)
    {
        var utcNow = _dateTimeProvider.UtcNow;
        await _customerBookingPrivilegeService.EnsureCanCreateAsync(
            request.CustomerId,
            request.BookingType,
            utcNow,
            cancellationToken);
        // Flow: validate booking type, pickup data, and duplicate active now-booking before loading related state.
        var surgeEvaluationTime = BookingScheduleRules.ResolveSurgeEvaluationTime(
            request.BookingType,
            request.ScheduledAt,
            utcNow);
        ValidatePickup(request);
        await EnsureNoActiveNowBookingAsync(request, cancellationToken);

        // Flow: validate the customer-owned vehicle and its license requirement before pricing.
        var vehicle = await _bookingRepository.GetCustomerVehicleAsync(
            request.VehicleId,
            request.CustomerId,
            cancellationToken);
        if (vehicle is null)
        {
            throw new BookingException(
                "booking.vehicle_not_found",
                "Không tìm thấy xe hợp lệ của bạn.",
                404);
        }

        ValidateVehicleLicenseRequirement(vehicle);

        var pricingRule = await _bookingRepository.GetPricingRuleAsync(
            request.ServiceTypeId,
            request.VehicleId,
            cancellationToken);
        if (pricingRule is null)
        {
            throw new BookingException(
                "booking.pricing_rule_not_found",
                "Không tìm thấy cấu hình giá phù hợp cho dịch vụ và xe đã chọn.",
                400);
        }

        var activeSurgeRule = await _bookingRepository.GetActiveSurgePricingRuleAsync(
            surgeEvaluationTime,
            cancellationToken);
        var surgeMultiplier = activeSurgeRule?.SurgeMultiplier ?? 1m;

        // Flow: calculate fare from hourly duration or route distance, preserving route data for distance trips.
        var isHourly = pricingRule.PricePerHour.HasValue;
        decimal estimatedDistanceKm;
        int estimatedDurationMinutes;
        BookingFareBreakdown fareBreakdown;
        string? routePolyline;
        string? destinationAddress;
        Point? destinationLocation;

        if (isHourly)
        {
            var estimatedHours = request.EstimatedHours;
            if (!estimatedHours.HasValue || estimatedHours is < 1 or > 24)
            {
                throw new BookingException(
                    "booking.invalid_estimated_hours",
                    "Số giờ thuê dự kiến phải từ 1 đến 24 giờ.",
                    400);
            }

            estimatedDistanceKm = 0m;
            estimatedDurationMinutes = estimatedHours.Value * 60;
            fareBreakdown = _fareEstimationService.CalculateBookingFare(
                pricingRule,
                estimatedDistanceKm,
                estimatedDurationMinutes,
                surgeMultiplier,
                _compensationOptions);
            routePolyline = null;
            destinationAddress = null;
            destinationLocation = null;
        }
        else
        {
            ValidateDestination(request);
            RouteEstimateResult route;
            try
            {
                route = await _mapRoutingService.GetRouteEstimateAsync(
                    new RouteEstimateRequest
                    {
                        Origin = new LocationPoint(request.PickupLatitude, request.PickupLongitude),
                        Destination = new LocationPoint(
                            request.DestinationLatitude,
                            request.DestinationLongitude),
                        Provider = MapProvider.Auto,
                        TravelMode = MapTravelMode.Car,
                        IncludePolyline = true,
                        RequestSource = "CreateBooking"
                    },
                    cancellationToken);
            }
            catch (MapServiceException exception)
            {
                throw new BookingException(
                    "booking.route_estimation_failed",
                    exception.Message,
                    502);
            }

            if (string.IsNullOrWhiteSpace(route.EncodedPolyline))
            {
                throw new BookingException(
                    "booking.route_polyline_missing",
                    "Không thể tính tuyến đường. Vui lòng kiểm tra lại điểm đón và điểm đến.",
                    502);
            }

            estimatedDistanceKm = (decimal)route.DistanceKm;
            estimatedDurationMinutes = route.DurationMinutes;
            fareBreakdown = _fareEstimationService.CalculateBookingFare(
                pricingRule,
                estimatedDistanceKm,
                estimatedDurationMinutes,
                surgeMultiplier,
                _compensationOptions);
            routePolyline = route.EncodedPolyline;
            destinationAddress = request.DestinationAddress!.Trim();
            destinationLocation = CreatePoint(
                request.DestinationLatitude,
                request.DestinationLongitude);
        }

        var estimatedFare = fareBreakdown.EstimatedFare;

        var bookingStatus = request.BookingType == BookingType.Now
            ? BookingStatus.Searching
            : BookingStatus.PendingSchedule;

        // Flow: create the booking aggregate and attach promotion usage intent before the first save.
        var booking = new Booking
        {
            CustomerId = request.CustomerId,
            VehicleId = request.VehicleId,
            ServiceTypeId = request.ServiceTypeId,
            BookingType = request.BookingType,
            BookingStatus = bookingStatus,
            ScheduledAt = request.ScheduledAt,
            PickupAddress = request.PickupAddress.Trim(),
            PickupLocation = CreatePoint(request.PickupLatitude, request.PickupLongitude),
            DestinationAddress = destinationAddress,
            DestinationLocation = destinationLocation,
            EstimatedDistanceKm = estimatedDistanceKm,
            EstimatedDurationMinutes = estimatedDurationMinutes,
            EstimatedFare = estimatedFare,
            RoutePolyline = routePolyline,
            SpecialRequest = NormalizeOptionalText(request.SpecialRequest),
            PricingRuleId = pricingRule.Id,
            SurgePricingRuleId = activeSurgeRule?.Id,
            PricingSnapshotVersion = Booking.CurrentPricingSnapshotVersion,
            AcceptedBaseFare = pricingRule.BaseFare,
            AcceptedMinimumServiceFare = pricingRule.MinFare,
            AcceptedPricePerKm = pricingRule.PricePerKm,
            AcceptedPricePerHour = pricingRule.PricePerHour,
            AcceptedSurgeMultiplier = surgeMultiplier,
            SurgeEvaluationTime = surgeEvaluationTime,
            NormalFare = fareBreakdown.NormalFare,
            SurgedFare = fareBreakdown.SurgedFare,
            SurgeAmount = fareBreakdown.SurgeAmount,
            AcceptedLongDistanceThresholdKm = Convert.ToDecimal(
                _compensationOptions.LongDistanceThresholdKm),
            AcceptedLongDistanceOptInThresholdKm = Convert.ToDecimal(
                _compensationOptions.LongDistanceOptInThresholdKm),
            AcceptedMaximumTripDistanceKm = Convert.ToDecimal(
                _compensationOptions.MaximumTripDistanceKm),
            AcceptedLongDistanceRatePerKm = _compensationOptions.LongDistanceRatePerKm,
            LongDistanceComponent = fareBreakdown.LongDistanceComponent,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };

        var promotionResult = await ApplyPromotionIfRequestedAsync(
            booking,
            request.PromotionCode,
            estimatedFare,
            utcNow,
            cancellationToken);

        await _bookingRepository.AddAsync(booking, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        // Flow: publish customer-visible status, then start matching and lifecycle jobs for now bookings.
        await _realtimeNotificationService.PublishBookingStatusChangedAsync(
            new BookingStatusChangedEvent(
                booking.BookingId,
                booking.CustomerId,
                booking.BookingStatus,
                utcNow),
            cancellationToken);
        if (request.BookingType == BookingType.Now)
        {
            await _realtimeNotificationService.PublishBookingSearchingStartedAsync(
                new BookingSearchingStartedEvent(
                    booking.BookingId,
                    booking.CustomerId,
                    _matchingPolicyProvider.Current.InitialRadiusKm,
                    utcNow,
                    $"SafeRide đang tìm tài xế gần bạn trong bán kính {_matchingPolicyProvider.Current.InitialRadiusKm:0.#}km."),
                cancellationToken);
        }

        if (request.BookingType == BookingType.Now)
        {
            await _matchingService.StartMatchingAsync(
                booking.BookingId,
                cancellationToken);

            // Schedule Hangfire delayed jobs for lifecycle management.
            var options = _matchingPolicyProvider.Current;
            _jobScheduler.ScheduleExpandRadius(
                booking.BookingId,
                TimeSpan.FromMinutes(options.ExpandAfterMinutes));
            _jobScheduler.ScheduleExpireBooking(
                booking.BookingId,
                TimeSpan.FromMinutes(options.BookingExpireAfterMinutes));
        }

        var matchingSnapshot = _matchingPolicyProvider.GetSnapshot(
            booking,
            utcNow);

        var message = request.BookingType == BookingType.Now
            ? "Đặt chuyến thành công. Hệ thống đang tìm tài xế."
            : isHourly
                ? "Tạo yêu cầu thuê theo giờ thành công."
                : "Đặt trước chuyến đi thành công.";

        return new CreateBookingResponse(
            booking.BookingId,
            booking.BookingType,
            booking.BookingStatus,
            booking.ScheduledAt,
            (double)estimatedDistanceKm,
            estimatedDurationMinutes,
            booking.EstimatedFare,
            estimatedFare,
            promotionResult.PromotionCode,
            promotionResult.DiscountAmount,
            Math.Max(0m, estimatedFare - promotionResult.DiscountAmount),
            routePolyline,
            message,
            TripId: null,
            DriverOffer: null,
            TripStatus: null,
            CurrentSearchRadiusKm: matchingSnapshot.CurrentSearchRadiusKm,
            ExpiresAt: matchingSnapshot.ExpiresAt,
            EstimatedRemainingSeconds: matchingSnapshot.EstimatedRemainingSeconds,
            MatchingMessage: matchingSnapshot.MatchingMessage);
    }

    private async Task EnsureNoActiveNowBookingAsync(
        CreateBookingCommand request,
        CancellationToken cancellationToken)
    {
        if (request.BookingType != BookingType.Now)
        {
            return;
        }

        var activeBooking = await _bookingRepository.GetActiveNowBookingAsync(
            request.CustomerId,
            cancellationToken);
        if (activeBooking is null)
        {
            return;
        }

        throw new BookingException(
            "booking.active_now_exists",
            "Bạn đang có chuyến đang hoạt động. Vui lòng theo dõi chuyến ở mục Hoạt động.",
            409);
    }

    private static void ValidatePickup(CreateBookingCommand request)
    {
        ValidateCoordinate(
            request.PickupLatitude,
            request.PickupLongitude,
            "Điểm đón");

        if (string.IsNullOrWhiteSpace(request.PickupAddress))
        {
            throw new BookingException(
                "booking.pickup_required",
                "Địa chỉ điểm đón là bắt buộc.",
                400);
        }
    }

    private void ValidateVehicleLicenseRequirement(Vehicle vehicle)
    {
        if (_vehicleLicenseRequirementService.HasValidRequirement(vehicle))
        {
            return;
        }

        throw new BookingException(
            "booking.invalid_vehicle_license_requirement",
            "Không xác định được hạng bằng lái cần thiết cho xe đã chọn.",
            400);
    }

    private static void ValidateDestination(CreateBookingCommand request)
    {
        ValidateCoordinate(
            request.DestinationLatitude,
            request.DestinationLongitude,
            "Điểm đến");

        if (string.IsNullOrWhiteSpace(request.DestinationAddress))
        {
            throw new BookingException(
                "booking.destination_required",
                "Địa chỉ điểm đến là bắt buộc.",
                400);
        }

        if (request.PickupLatitude == request.DestinationLatitude
            && request.PickupLongitude == request.DestinationLongitude)
        {
            throw new BookingException(
                "booking.same_locations",
                "Điểm đón và điểm đến phải khác nhau.",
                400);
        }
    }

    private static void ValidateCoordinate(
        double latitude,
        double longitude,
        string locationName)
    {
        if (!double.IsFinite(latitude)
            || !double.IsFinite(longitude)
            || latitude is < -90 or > 90
            || longitude is < -180 or > 180)
        {
            throw new BookingException(
                "booking.invalid_coordinates",
                $"{locationName} có tọa độ không hợp lệ.",
                400);
        }
    }

    private static Point CreatePoint(double latitude, double longitude)
    {
        return new Point(longitude, latitude) { SRID = 4326 };
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private async Task<CreateBookingPromotionResult> ApplyPromotionIfRequestedAsync(
        Booking booking,
        string? promotionCode,
        decimal originalFare,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(promotionCode))
        {
            return new CreateBookingPromotionResult(null, 0m);
        }

        var normalizedCode = PromotionApplicationRules.NormalizePromotionCode(
            promotionCode);
        var promotion = await _promotionRepository.GetPromotionByCodeAsync(
            normalizedCode,
            cancellationToken);
        if (promotion is null)
        {
            throw new PromotionException(
                "promotion.not_found",
                "Mã khuyến mãi không tồn tại.",
                404);
        }

        PromotionApplicationRules.ValidateAvailability(promotion, utcNow);
        PromotionApplicationRules.ValidateMinimumOrderValue(
            originalFare,
            promotion.MinimumOrderValue);
        await ValidateCustomerUsageAsync(
            booking.CustomerId,
            promotion.Id,
            promotion.UsageLimitPerUser,
            cancellationToken);
        await ValidateCompletedTripsAsync(
            booking.CustomerId,
            promotion.PromotionCode,
            cancellationToken);

        var discountAmount = PromotionApplicationRules.CalculateDiscountAmount(
            promotion,
            originalFare);
        booking.BookingPromotions.Add(new BookingPromotion
        {
            Promotion = promotion,
            PromotionId = promotion.Id,
            DiscountAmount = discountAmount,
            CreatedAt = utcNow
        });

        return new CreateBookingPromotionResult(
            promotion.PromotionCode,
            discountAmount);
    }

    private async Task ValidateCustomerUsageAsync(
        Guid customerId,
        long promotionId,
        int usageLimitPerUser,
        CancellationToken cancellationToken)
    {
        var usageCount = await _promotionRepository.CountCustomerPromotionUsageAsync(
            customerId,
            promotionId,
            cancellationToken);
        PromotionApplicationRules.ValidateCustomerUsageLimit(
            usageCount,
            usageLimitPerUser);
    }

    private async Task ValidateCompletedTripsAsync(
        Guid customerId,
        string promotionCode,
        CancellationToken cancellationToken)
    {
        var requiredCompletedTrips = await _promotionUnlockRuleStore
            .GetRequiredCompletedTripsAsync(promotionCode, cancellationToken);
        if (requiredCompletedTrips <= 0)
        {
            return;
        }

        var completedTrips = await _promotionRepository
            .CountCustomerCompletedTripsAsync(customerId, cancellationToken);
        PromotionUnlockRules.ValidateCompletedTrips(
            completedTrips,
            requiredCompletedTrips);
    }

    private sealed record CreateBookingPromotionResult(
        string? PromotionCode,
        decimal DiscountAmount);
}
