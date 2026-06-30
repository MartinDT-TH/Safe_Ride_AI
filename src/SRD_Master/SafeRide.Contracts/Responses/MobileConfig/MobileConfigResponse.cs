namespace SafeRide.Contracts.Responses.MobileConfig;

public sealed record MobileConfigResponse(
    string Version,
    MobileRealtimeConfigResponse Realtime,
    MobileStatusGroupResponse Booking,
    MobileStatusGroupResponse Trip,
    MobileStatusGroupResponse Offer,
    MobileDriverConfigResponse Driver,
    MobileMatchingConfigResponse Matching,
    MobileFeatureConfigResponse Features);

public sealed record MobileRealtimeConfigResponse(
    string HubPath,
    MobileRealtimeEventsResponse Events);

public sealed record MobileRealtimeEventsResponse(
    string BookingSearchingStarted,
    string BookingSearchRadiusExpanded,
    string BookingStatusChanged,
    string BookingDriverAssigned,
    string BookingExpired,
    string BookingCancelled,
    string DriverMatched,
    string DriverLocationUpdated,
    string DriverOfferCreated,
    string DriverOfferReceived,
    string DriverOfferAccepted,
    string DriverOfferRejected,
    string DriverOfferExpired,
    string DriverOfferCancelled,
    string CustomerConfirmedDriverOffer,
    string TripCreated,
    string TripStatusChanged);

public sealed record MobileStatusGroupResponse(
    IReadOnlyList<MobileStatusOptionResponse> Statuses);

public sealed record MobileStatusOptionResponse(
    string Value,
    string Label);

public sealed record MobileDriverConfigResponse(
    IReadOnlyList<MobileStatusOptionResponse> Statuses,
    int LocationUpdateIntervalSeconds);

public sealed record MobileMatchingConfigResponse(
    int SearchingBookingPollIntervalSeconds,
    int NearbyDriversRefreshIntervalSeconds,
    int TripStatusPollIntervalSeconds,
    int DriverLocationUpdateIntervalSeconds);

public sealed record MobileFeatureConfigResponse(
    string MapProvider,
    bool EnableGoogleMap,
    bool EnableVietMap);
