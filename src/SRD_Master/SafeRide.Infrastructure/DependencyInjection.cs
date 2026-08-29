using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SafeRide.Application.Features.Auth.Services;
using SafeRide.Application.Common.Models;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.RiskProtection;
using SafeRide.Application.Features.Admin.Revenue;
using SafeRide.Domain.Entities;
using SafeRide.Infrastructure.Authentication;
using SafeRide.Infrastructure.AiChat;
using SafeRide.Infrastructure.BackgroundJobs;
using SafeRide.Infrastructure.ExternalServices;
using SafeRide.Infrastructure.ExternalServices.GoogleMaps;
using SafeRide.Infrastructure.ExternalServices.OpenRouteService;
using SafeRide.Infrastructure.ExternalServices.VietMap;
using SafeRide.Infrastructure.ExternalServices.NoOp;
using SafeRide.Infrastructure.ExternalServices.PayOS;
using SafeRide.Infrastructure.Persistence;
using SafeRide.Infrastructure.Redis;
using SafeRide.Infrastructure.Repositories;
using SafeRide.Infrastructure.Services;
using SafeRide.Infrastructure.Services.AccountBans;
using SafeRide.Infrastructure.Simulator;
using System.Text;
using SafeRide.Infrastructure.ExternalServices.Cloudinary;
using SafeRide.Infrastructure.TripChat;

namespace SafeRide.Infrastructure;

public static class DependencyInjection
{
    private static bool IsValidTripSharingAppLink(string value, bool isDevelopment)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || string.IsNullOrWhiteSpace(uri.Host))
        {
            return false;
        }

        if (string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return isDevelopment
            && !string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var backgroundJobsEnabled = configuration.GetValue<bool>("BackgroundJobs:Enabled");

        services
            .AddOptions<AiChatOptions>()
            .Bind(configuration.GetSection(AiChatOptions.SectionName))
            .PostConfigure(options =>
                options.MongoConnectionString =
                    configuration.GetConnectionString("MongoDB") ?? "")
            .Validate(
                options => options.TripChatTranslationTimeoutSeconds > 0,
                "AiChat:TripChatTranslationTimeoutSeconds must be greater than zero.")
            .Validate(
                options => options.TripChatTranslationMaxRetries is >= 0 and <= 3,
                "AiChat:TripChatTranslationMaxRetries must be between zero and three.");
        services.AddHttpClient<IAiChatService, AiChatService>(client =>
        {
            client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
        });
        services.AddHttpClient<ITextTranslationService, GeminiTextTranslationService>(client =>
        {
            client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
        });
        services.AddHostedService<AiChatMongoInitializer>();

        services.AddHttpClient<ITripChatTranslationService, GeminiTripChatTranslationService>(
            (provider, client) =>
            {
                var options = provider
                    .GetRequiredService<IOptions<AiChatOptions>>()
                    .Value;
                client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
                client.Timeout = TimeSpan.FromSeconds(
                    options.TripChatTranslationTimeoutSeconds);
            });

        services.AddDbContext<ApplicationDbContext>(
            options => options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sqlOptions => sqlOptions
                    .UseNetTopologySuite()
                    .EnableRetryOnFailure()));

        services
            .AddIdentity<AspNetUser, AspNetRole>(options =>
            {
                options.User.RequireUniqueEmail = false;
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        services
            .AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(x => x.SecretKey != "CHANGE_ME", "Jwt:SecretKey must be configured.")
            .ValidateOnStart();
        services
            .AddOptions<GoogleAuthOptions>()
            .Bind(configuration.GetSection(GoogleAuthOptions.SectionName));
        services
            .AddOptions<TripContinuationOptions>()
            .Bind(configuration.GetSection(TripContinuationOptions.SectionName))
            .Validate(options => options.ExpiredRefreshGraceMinutes > 0, "Authentication:TripContinuation:ExpiredRefreshGraceMinutes must be greater than zero.")
            .Validate(options => options.AccessTokenMinutes > 0, "Authentication:TripContinuation:AccessTokenMinutes must be greater than zero.")
            .Validate(options => options.RefreshTokenMinutes > 0, "Authentication:TripContinuation:RefreshTokenMinutes must be greater than zero.")
            .Validate(options => options.AbsoluteMaxHoursFromTripStart > 0, "Authentication:TripContinuation:AbsoluteMaxHoursFromTripStart must be greater than zero.")
            .Validate(options => options.AbsoluteMaxHoursFromBookingCreation > 0, "Authentication:TripContinuation:AbsoluteMaxHoursFromBookingCreation must be greater than zero.")
            .Validate(options => options.PostCompletionRatingGraceMinutes > 0, "Authentication:TripContinuation:PostCompletionRatingGraceMinutes must be greater than zero.")
            .ValidateOnStart();
        services
            .AddOptions<CloudinaryOptions>()
            .Bind(configuration.GetSection(CloudinaryOptions.SectionName));
        services
            .AddOptions<PayOsOptions>()
            .Bind(configuration.GetSection(PayOsOptions.SectionName));
        var googleMapsIsPrimaryProvider = string.Equals(
            configuration["MapServices:PrimaryProvider"],
            "GoogleMaps",
            StringComparison.OrdinalIgnoreCase);
        services
            .AddOptions<GoogleMapsOptions>()
            .Bind(configuration.GetSection(GoogleMapsOptions.SectionName))
            .Validate(
                options => !googleMapsIsPrimaryProvider || !string.IsNullOrWhiteSpace(options.ApiKey),
                "MapServices:GoogleMaps:ApiKey must be configured when GoogleMaps is the primary provider.")
            .Validate(
                options => !googleMapsIsPrimaryProvider
                    || (Uri.TryCreate(options.RoutesApiUrl, UriKind.Absolute, out var uri)
                        && uri.Scheme == Uri.UriSchemeHttps),
                "MapServices:GoogleMaps:RoutesApiUrl must be an absolute HTTPS URI when GoogleMaps is the primary provider.")
            .Validate(
                options => !googleMapsIsPrimaryProvider || options.TimeoutSeconds > 0,
                "MapServices:GoogleMaps:TimeoutSeconds must be greater than zero when GoogleMaps is the primary provider.")
            .ValidateOnStart();
        services
            .AddOptions<OpenRouteServiceOptions>()
            .Bind(configuration.GetSection(OpenRouteServiceOptions.SectionName))
            .Validate(options => options.TimeoutSeconds > 0, "MapServices:OpenRouteService:TimeoutSeconds must be greater than zero.");
            // NOTE: ValidateOnStart removed — OpenRouteService is a fallback provider.

        services
            .AddOptions<MatchingOptions>()
            .Bind(configuration.GetSection(MatchingOptions.SectionName))
            .Validate(options => options.InitialRadiusKm > 0, "BackgroundJobs:MatchingOptions:InitialRadiusKm must be greater than zero.")
            .Validate(options => options.ExpandedRadiusKm >= options.InitialRadiusKm, "BackgroundJobs:MatchingOptions:ExpandedRadiusKm must be greater than or equal to InitialRadiusKm.")
            .Validate(options => options.ExpandAfterMinutes > 0, "BackgroundJobs:MatchingOptions:ExpandAfterMinutes must be greater than zero.")
            .Validate(options => options.BookingExpireAfterMinutes > options.ExpandAfterMinutes, "BackgroundJobs:MatchingOptions:BookingExpireAfterMinutes must be greater than ExpandAfterMinutes.")
            .Validate(options => options.OfferExpireSeconds > 0, "BackgroundJobs:MatchingOptions:OfferExpireSeconds must be greater than zero.")
            .Validate(options => options.CustomerConfirmExpireSeconds > 0, "BackgroundJobs:MatchingOptions:CustomerConfirmExpireSeconds must be greater than zero.")
            .Validate(options => options.MatchingTickSeconds > 0, "BackgroundJobs:MatchingOptions:MatchingTickSeconds must be greater than zero.")
            .Validate(options => options.CandidateLimit > 0, "BackgroundJobs:MatchingOptions:CandidateLimit must be greater than zero.")
            .ValidateOnStart();

        services
            .AddOptions<DriverCompensationOptions>()
            .Bind(configuration.GetSection(DriverCompensationOptions.SectionName))
            .Validate(options => options.LongPickupThresholdKm > 0, "DriverCompensation:LongPickupThresholdKm must be greater than zero.")
            .Validate(options => options.LongPickupThresholdKm <= options.LongPickupOptInThresholdKm, "DriverCompensation:LongPickupOptInThresholdKm must be greater than or equal to LongPickupThresholdKm.")
            .Validate(options => options.LongDistanceThresholdKm > 0, "DriverCompensation:LongDistanceThresholdKm must be greater than zero.")
            .Validate(options => options.LongDistanceThresholdKm <= options.LongDistanceOptInThresholdKm, "DriverCompensation:LongDistanceOptInThresholdKm must be greater than or equal to LongDistanceThresholdKm.")
            .Validate(options => options.LongDistanceOptInThresholdKm <= options.MaximumTripDistanceKm, "DriverCompensation:MaximumTripDistanceKm must be greater than or equal to LongDistanceOptInThresholdKm.")
            .Validate(options => options.LongPickupRatePerKm >= 0, "DriverCompensation:LongPickupRatePerKm must be greater than or equal to zero.")
            .Validate(options => options.LongDistanceRatePerKm >= 0, "DriverCompensation:LongDistanceRatePerKm must be greater than or equal to zero.")
            .Validate(options => options.DestinationReachedThresholdMeters > 0, "DriverCompensation:DestinationReachedThresholdMeters must be greater than zero.")
            .ValidateOnStart();

        services
            .AddOptions<ScheduledBookingMatchingOptions>()
            .Bind(configuration.GetSection(ScheduledBookingMatchingOptions.SectionName))
            .Validate(options => options.StartMatchingBeforeMinutes > 0, "BackgroundJobs:ScheduledBookingMatching:StartMatchingBeforeMinutes must be greater than zero.")
            .Validate(options => options.PollingIntervalSeconds > 0, "BackgroundJobs:ScheduledBookingMatching:PollingIntervalSeconds must be greater than zero.")
            .ValidateOnStart();

        services
            .AddOptions<CustomerNoShowOptions>()
            .Bind(configuration.GetSection(CustomerNoShowOptions.SectionName))
            .Validate(options => options.NoShowWaitMinutes > 0, "CustomerNoShow:NoShowWaitMinutes must be greater than zero.")
            .Validate(options => options.ArrivalRadiusMeters > 0, "CustomerNoShow:ArrivalRadiusMeters must be greater than zero.")
            .Validate(options => options.DriverLocationFreshnessSeconds > 0, "CustomerNoShow:DriverLocationFreshnessSeconds must be greater than zero.")
            .Validate(options => options.DriverSupportMinPickupDistanceKm > 0, "CustomerNoShow:DriverSupportMinPickupDistanceKm must be greater than zero.")
            .Validate(options => options.DriverNoShowSupportAmount >= 0, "CustomerNoShow:DriverNoShowSupportAmount must be non-negative.")
            .Validate(options => options.BehaviorWindowDays > 0, "CustomerNoShow:BehaviorWindowDays must be greater than zero.")
            .Validate(options => options.ScheduleRestrictionDaysFirst > 0, "CustomerNoShow:ScheduleRestrictionDaysFirst must be greater than zero.")
            .Validate(options => options.ScheduleRestrictionDaysPersistent > 0, "CustomerNoShow:ScheduleRestrictionDaysPersistent must be greater than zero.")
            .Validate(options => options.InstantCooldownHoursPersistent > 0, "CustomerNoShow:InstantCooldownHoursPersistent must be greater than zero.")
            .ValidateOnStart();

        services
            .AddOptions<ExpandSearchingRadiusJobOptions>()
            .Bind(configuration.GetSection(ExpandSearchingRadiusJobOptions.SectionName))
            .Validate(options => options.RadiusExpandedNotificationTtlMinutes > 0, "BackgroundJobs:ExpandSearchingRadius:RadiusExpandedNotificationTtlMinutes must be greater than zero.")
            .ValidateOnStart();

        services
            .AddOptions<CleanupStaleDriverLocationJobOptions>()
            .Bind(configuration.GetSection(CleanupStaleDriverLocationJobOptions.SectionName))
            .Validate(options => options.StaleAfterMinutes > 0, "BackgroundJobs:CleanupStaleDriverLocation:StaleAfterMinutes must be greater than zero.")
            .Validate(options => options.BatchSize > 0, "BackgroundJobs:CleanupStaleDriverLocation:BatchSize must be greater than zero.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.CronExpression), "BackgroundJobs:CleanupStaleDriverLocation:CronExpression must be configured.")
            .ValidateOnStart();

        services
            .AddOptions<BookingLifecycleJobSchedulerOptions>()
            .Bind(configuration.GetSection(BookingLifecycleJobSchedulerOptions.SectionName))
            .Validate(options => options.JobIdTtlHours > 0, "BackgroundJobs:BookingLifecycleJobScheduler:JobIdTtlHours must be greater than zero.")
            .ValidateOnStart();

        services
            .AddOptions<SimulatorOptions>()
            .Bind(configuration.GetSection(SimulatorOptions.SectionName))
            .Validate(options => options.MockDriverTtlRefreshSeconds > 0, "SimulatorOptions:MockDriverTtlRefreshSeconds must be greater than zero.")
            .Validate(options => options.MockBookingIntervalSeconds > 0, "SimulatorOptions:MockBookingIntervalSeconds must be greater than zero.")
            .Validate(options => options.MaxConcurrentMockBookings >= 0, "SimulatorOptions:MaxConcurrentMockBookings must be >= 0.")
            .Validate(options => options.MockBookingBaseLat >= -90 && options.MockBookingBaseLat <= 90, "SimulatorOptions:MockBookingBaseLat must be between -90 and 90.")
            .Validate(options => options.MockBookingBaseLng >= -180 && options.MockBookingBaseLng <= 180, "SimulatorOptions:MockBookingBaseLng must be between -180 and 180.")
            .ValidateOnStart();

        services
            .AddOptions<DriverRealtimeOptions>()
            .Bind(configuration.GetSection(DriverRealtimeOptions.SectionName))
            .Validate(options => options.DriverLocationTtlMinutes > 0, "DriverRealtime:DriverLocationTtlMinutes must be greater than zero.")
            .Validate(options => options.DriverOnlineTtlMinutes > 0, "DriverRealtime:DriverOnlineTtlMinutes must be greater than zero.")
            .Validate(options => options.DriverHeartbeatDbUpdateIntervalSeconds > 0, "DriverRealtime:DriverHeartbeatDbUpdateIntervalSeconds must be greater than zero.")
            .ValidateOnStart();

        services
            .AddOptions<TripTrackingOptions>()
            .Bind(configuration.GetSection(TripTrackingOptions.SectionName))
            .Validate(options => options.TripLiveTtlHours > 0, "TripTracking:TripLiveTtlHours must be greater than zero.")
            .Validate(options => options.DriverStatusTtlMinutes > 0, "TripTracking:DriverStatusTtlMinutes must be greater than zero.")
            .Validate(options => options.TrackingTtlHours > 0, "TripTracking:TrackingTtlHours must be greater than zero.")
            .Validate(options => options.MaxPathPoints > 0, "TripTracking:MaxPathPoints must be greater than zero.")
            .Validate(options => options.AccumulatorJitterThresholdMeters >= 0, "TripTracking:AccumulatorJitterThresholdMeters must be greater than or equal to zero.")
            .Validate(options => options.PathSampleDistanceMeters >= 0, "TripTracking:PathSampleDistanceMeters must be greater than or equal to zero.")
            .Validate(options => options.PathSampleIntervalSeconds > 0, "TripTracking:PathSampleIntervalSeconds must be greater than zero.")
            .Validate(options => options.MaxInferredSpeedKmh > 0, "TripTracking:MaxInferredSpeedKmh must be greater than zero.")
            .Validate(options => options.MaxAccuracyMeters > 0, "TripTracking:MaxAccuracyMeters must be greater than zero.")
            .Validate(options => options.FinalizeLockSeconds > 0, "TripTracking:FinalizeLockSeconds must be greater than zero.")
            .Validate(options => options.RouteDeviationThresholdMeters > 0, "TripTracking:RouteDeviationThresholdMeters must be greater than zero.")
            .Validate(options => options.RouteDeviationRequiredSamples > 0, "TripTracking:RouteDeviationRequiredSamples must be greater than zero.")
            .Validate(options => options.RouteDeviationStateTtlMinutes > 0, "TripTracking:RouteDeviationStateTtlMinutes must be greater than zero.")
            .Validate(options => options.RouteRerouteCooldownSeconds > 0, "TripTracking:RouteRerouteCooldownSeconds must be greater than zero.")
            .Validate(options => options.CustomerDeviationAlertCooldownMinutes > 0, "TripTracking:CustomerDeviationAlertCooldownMinutes must be greater than zero.")
            .Validate(options => options.ActiveRouteTtlHours > 0, "TripTracking:ActiveRouteTtlHours must be greater than zero.")
            .Validate(options => options.ReverseProgressThresholdMeters > 0, "TripTracking:ReverseProgressThresholdMeters must be greater than zero.")
            .Validate(options => options.ReverseRequiredSamples > 0, "TripTracking:ReverseRequiredSamples must be greater than zero.")
            .Validate(options => options.CustomerAlertDistanceIncreaseMeters > 0, "TripTracking:CustomerAlertDistanceIncreaseMeters must be greater than zero.")
            .ValidateOnStart();

        services
            .AddOptions<TripSharingOptions>()
            .Bind(configuration.GetSection(TripSharingOptions.SectionName))
            .Validate(options => IsValidTripSharingAppLink(
                    options.AppLinkBaseUrl,
                    environment.IsDevelopment()),
                "TripSharing:AppLinkBaseUrl must be an absolute HTTPS URL in production, or an explicitly configured custom scheme in development.")
            .Validate(options => options.DefaultExpirationHours > 0, "TripSharing:DefaultExpirationHours must be greater than zero.")
            .Validate(options => options.CompletedGraceMinutes > 0, "TripSharing:CompletedGraceMinutes must be greater than zero.")
            .Validate(options => options.CancelledGraceMinutes > 0, "TripSharing:CancelledGraceMinutes must be greater than zero.")
            .ValidateOnStart();

        // ── Hangfire ───────────────────────────────────────────────────────────────
        if (backgroundJobsEnabled)
        {
            services.AddHangfire(config => config
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseSqlServerStorage(
                    configuration.GetConnectionString("DefaultConnection"),
                    new SqlServerStorageOptions
                    {
                        CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                        SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                        QueuePollInterval = TimeSpan.Zero,
                        UseRecommendedIsolationLevel = true,
                        DisableGlobalLocks = true
                    }));
            services.AddHangfireServer();
            services.AddScoped<IBookingLifecycleJobScheduler, HangfireBookingLifecycleJobScheduler>();
            services.AddScoped<ITripShareExpiryScheduler, HangfireTripShareExpiryScheduler>();
        }
        else
        {
            services.AddScoped<IBookingLifecycleJobScheduler, NoOpBookingLifecycleJobScheduler>();
            services.AddScoped<ITripShareExpiryScheduler, NoOpTripShareExpiryScheduler>();
        }
        // ──────────────────────────────────────────────────────────────────────────

        services.AddSingleton<RedisService>();
        services.AddSingleton<InMemoryRedisService>();
        services.AddSingleton<IRedisService>(provider =>
            new ResilientRedisService(
                provider.GetRequiredService<RedisService>(),
                provider.GetRequiredService<InMemoryRedisService>(),
                provider.GetRequiredService<ILogger<ResilientRedisService>>()));
        services.AddSingleton<ICloudinaryImageService, CloudinaryImageService>();
        services.AddSingleton<IIdentityDocumentStorage, CloudinaryIdentityDocumentStorage>();
        services.AddSingleton<ITripReturnEvidenceStorage, CloudinaryTripReturnEvidenceStorage>();
        services.AddSingleton<IAccidentEvidenceStorage, CloudinaryAccidentEvidenceStorage>();
        if (environment.IsDevelopment() || environment.IsEnvironment("Testing"))
        {
            services.AddSingleton<IFileSafetyScanner, NonProductionFileSafetyScanner>();
        }
        else
        {
            services.AddSingleton<IFileSafetyScanner, UnconfiguredFileSafetyScanner>();
        }
        services.AddSingleton<IEvidenceFileValidator, EvidenceFileValidator>();
        services.AddSingleton<IPreTripVehicleCheckEvidenceStorage, CloudinaryPreTripVehicleCheckEvidenceStorage>();
        services.AddSingleton<ISafetyTerminationEvidenceStorage, CloudinarySafetyTerminationEvidenceStorage>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IGoogleTokenVerifier, GoogleTokenVerifier>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ITripSessionQueryService, TripSessionQueryService>();
        services.AddScoped<ITripContinuationAccessService, TripContinuationAccessService>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IAdminPricingRuleRepository, AdminPricingRuleRepository>();
        services.AddScoped<IPromotionRepository, PromotionRepository>();
        services.AddScoped<IAdminPromotionRepository, PromotionRepository>();
        services.AddScoped<IPromotionUnlockRuleStore, PromotionUnlockRuleStore>();
        services.AddScoped<IRatingRepository, RatingRepository>();
        services.AddScoped<IReportRepository, ReportRepository>();
        services.AddScoped<ISOSAlertRepository, SOSAlertRepository>();
        services.AddScoped<IAdminCustomerAccountService, AdminCustomerAccountService>();
        services.AddScoped<IAdminDriverAccountService, AdminDriverAccountService>();
        services.AddScoped<IAccountBanManagementService, AccountBanService>();
        services.AddScoped<IAccountBanEvaluationService, AccountBanService>();
        services.AddScoped<IAccountRestrictionService, AccountBanService>();
        services.AddScoped<IUserSessionRevocationService, UserSessionRevocationService>();
        services.AddScoped<IAdminBookingManagementService, AdminBookingManagementService>();
        services.AddScoped<IAdminTripManagementService, AdminTripManagementService>();
        services.AddScoped<IAdminNotificationManagementService, AdminNotificationManagementService>();
        services.AddScoped<IStaffPaymentStatusService, StaffPaymentStatusService>();
        services.AddScoped<IStaffNotificationRequestService, StaffNotificationRequestService>();
        services.AddScoped<IUserNotificationService, UserNotificationService>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<IMatchingPolicyProvider, MatchingPolicyProvider>();
        services.AddScoped<IBookingMatchingService, BookingMatchingService>();
        services.AddScoped<IBookingAssignmentService, BookingAssignmentService>();
        services.AddScoped<IDriverQueryService, DriverQueryService>();
        services.AddScoped<IDriverMatchingPreferencesService, DriverMatchingPreferencesService>();
        services.AddScoped<IDriverWalletService, DriverWalletService>();
        services.AddScoped<IDriverRealtimeService, DriverRealtimeService>();
        services.AddScoped<TripFareFinalizationService>();
        services.AddSingleton<ITripCommissionCalculator, TripCommissionCalculator>();
        services.AddSingleton<IClaimSettlementCalculator, TripCommissionCalculator>();
        services.AddScoped<IRiskProtectionPolicyProvider, RiskProtectionPolicyProvider>();
        services.AddScoped<IPreTripVehicleCheckService, PreTripVehicleCheckService>();
        services.AddScoped<IVehicleInsurancePolicyService, VehicleInsurancePolicyService>();
        services.AddScoped<ISafetyReportService, SafetyReportService>();
        services.AddScoped<RiskFundLedgerService>();
        services.AddScoped<IRiskFundLedgerService>(provider => provider.GetRequiredService<RiskFundLedgerService>());
        services.AddScoped<ITripFinancialSettlementService, TripFinancialSettlementService>();
        services.AddScoped<ISafetyPaymentReconciliationService, SafetyPaymentReconciliationService>();
        services.AddScoped<IInsuranceProvider, MockInsuranceProvider>();
        services.AddScoped<IAccidentManagementService, AccidentManagementService>();
        services.AddScoped<IAdminRevenueQueryService, AdminRevenueQueryService>();
        services.AddScoped<TripPaymentSettlementService>();
        services.AddScoped<ITripStatusService, TripStatusService>();
        services.AddScoped<ITripArrivalVerificationService, TripArrivalVerificationService>();
        services.AddScoped<ITripCustomerNoShowReminderService, TripCustomerNoShowReminderService>();
        services.AddScoped<ICustomerNoShowEligibilityService, CustomerNoShowEligibilityService>();
        services.AddScoped<ITripSharingService, TripSharingService>();
        services.AddScoped<ITripChatService, TripChatService>();
        services.AddSingleton<ITripChatContentFilter, TripChatContentFilter>();
        services.AddHttpClient<ISpeedSmsService, InfobipSmsService>();
        services.AddHttpClient<IPaymentService, PayOsPaymentService>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<PayOsOptions>>().Value;
            var baseUrl = string.IsNullOrWhiteSpace(options.BaseUrl)
                ? "https://api-merchant.payos.vn"
                : options.BaseUrl;

            client.BaseAddress = new Uri(baseUrl);
            if (!string.IsNullOrWhiteSpace(options.ClientId))
            {
                client.DefaultRequestHeaders.Add("x-client-id", options.ClientId);
            }

            if (!string.IsNullOrWhiteSpace(options.ApiKey))
            {
                client.DefaultRequestHeaders.Add("x-api-key", options.ApiKey);
            }
        });

        // ── Map Services ───────────────────────────────────────────────────────────
        // VietMap options (always registered regardless of primary provider)
        services
            .AddOptions<VietMapOptions>()
            .Bind(configuration.GetSection(VietMapOptions.SectionName))
            .Validate(options => options.TimeoutSeconds > 0, "MapServices:VietMap:TimeoutSeconds must be greater than zero.");

        var primaryMapProvider = configuration["MapServices:PrimaryProvider"];

        if (string.Equals(primaryMapProvider, "OpenRouteService", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient<IMapRoutingService, OpenRouteServiceRoutingService>(client =>
            {
                var timeoutSeconds = configuration.GetValue<int>("MapServices:OpenRouteService:TimeoutSeconds", 20);
                client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
            });
            if(!configuration.GetValue<bool>("MapServices:TurnGeocodingOffForOpenRouteServiceFallback")) 
            {
                services.AddHttpClient<IMapGeocodingService, OpenRouteServiceGeocodingService>(client =>
                {
                    var timeoutSeconds = configuration.GetValue<int>("MapServices:OpenRouteService:TimeoutSeconds", 20);
                    client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
                });
            }
            else
            {
                services.AddHttpClient<IMapGeocodingService, VietMapGeocodingService>(client =>
                {
                    var timeoutSeconds = configuration.GetValue<int>(
                        "MapServices:VietMap:TimeoutSeconds",
                        15);
                    client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
                });
            }
            
        }
        else if (string.Equals(primaryMapProvider, "GoogleMaps", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient<IMapRoutingService, GoogleMapsRoutingService>(client =>
            {
                var timeoutSeconds = configuration.GetValue<int>("MapServices:GoogleMaps:TimeoutSeconds", 15);
                client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
            });
            // Fallback to VietMap for Geocoding until GoogleMapsGeocodingService is implemented
            services.AddHttpClient<IMapGeocodingService, VietMapGeocodingService>(client =>
            {
                var timeoutSeconds = configuration.GetValue<int>("MapServices:VietMap:TimeoutSeconds", 15);
                client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
            });
        }
        else
        {
            // Default: VietMap
            services.AddHttpClient<IMapRoutingService, VietMapRoutingService>(client =>
            {
                var timeoutSeconds = configuration.GetValue<int>("MapServices:VietMap:TimeoutSeconds", 15);
                client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
            });
            services.AddHttpClient<IMapGeocodingService, VietMapGeocodingService>(client =>
            {
                var timeoutSeconds = configuration.GetValue<int>("MapServices:VietMap:TimeoutSeconds", 15);
                client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
            });
        }
        // ──────────────────────────────────────────────────────────────────────────

        if (backgroundJobsEnabled && environment.IsDevelopment())
        {
            if (configuration.GetValue<bool>("Simulator:EnableMockDrivers"))
            {
                services.AddHostedService<MockDriverOfferAcceptorService>();
            }

            if (configuration.GetValue<bool>("Simulator:EnableMockCustomerService"))
            {
                services.AddHostedService<MockCustomerSimulatorService>();
            }

            if (configuration.GetValue<bool>("Simulator:EnableMockBookingGenerator"))
            {
                services.AddHostedService<MockBookingGeneratorService>();
            }
        }

        if (backgroundJobsEnabled && !environment.IsEnvironment("Testing"))
        {
            services.AddHostedService<ScheduledBookingMatchingJob>();
            services.AddHostedService<BookingMatchingBackgroundService>();
        }

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = !environment.IsDevelopment();
                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILoggerFactory>()
                            .CreateLogger("JwtAuthentication");
                        logger.LogWarning(
                            context.Exception,
                            "JWT authentication failed for {Method} {Path}. FailureType={FailureType} TraceId={TraceId}.",
                            context.Request.Method,
                            context.Request.Path,
                            context.Exception.GetType().Name,
                            context.HttpContext.TraceIdentifier);
                        return Task.CompletedTask;
                    },
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrWhiteSpace(accessToken)
                            && path.StartsWithSegments("/hubs"))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    },
                    OnChallenge = async context =>
                    {
                        var authorizationHeaders = context.Request.Headers.Authorization;
                        var hasBearerHeader = authorizationHeaders.Any(value =>
                            value?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true);
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILoggerFactory>()
                            .CreateLogger("JwtAuthentication");
                        logger.LogWarning(
                            "JWT challenge for {Method} {Path}. HasBearerHeader={HasBearerHeader} AuthorizationHeaderCount={AuthorizationHeaderCount} FailureType={FailureType} TraceId={TraceId}.",
                            context.Request.Method,
                            context.Request.Path,
                            hasBearerHeader,
                            authorizationHeaders.Count,
                            context.AuthenticateFailure?.GetType().Name ?? "none",
                            context.HttpContext.TraceIdentifier);

                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/problem+json";
                        var problem = new ProblemDetails
                        {
                            Status = StatusCodes.Status401Unauthorized,
                            Title = "Unauthorized",
                            Detail = "Access token không hợp lệ hoặc đã hết hạn.",
                            Instance = context.Request.Path
                        };
                        problem.Extensions["code"] = "auth.access_token_invalid";
                        problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
                        await context.Response.WriteAsJsonAsync(problem);
                    },
                    OnForbidden = async context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        context.Response.ContentType = "application/problem+json";
                        var problem = new ProblemDetails
                        {
                            Status = StatusCodes.Status403Forbidden,
                            Title = "Forbidden",
                            Detail = "Bạn không có quyền truy cập tài nguyên này.",
                            Instance = context.Request.Path
                        };
                        problem.Extensions["code"] = context.HttpContext.Items.TryGetValue("AuthErrorCode", out var code)
                            ? code
                            : "auth.forbidden";
                        problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
                        if (context.HttpContext.Items.TryGetValue("AuthErrorDetail", out var detail)
                            && detail is string detailText
                            && !string.IsNullOrWhiteSpace(detailText))
                        {
                            problem.Detail = detailText;
                        }
                        await context.Response.WriteAsJsonAsync(problem);
                    }
                };
            });

        services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((options, accessor) =>
            {
                var jwt = accessor.Value;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwt.SecretKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });

        services.AddAuthorization();
        return services;
    }

}
