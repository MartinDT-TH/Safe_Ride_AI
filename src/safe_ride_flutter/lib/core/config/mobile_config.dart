class MobileConfig {
  const MobileConfig({
    required this.version,
    required this.realtime,
    required this.booking,
    required this.trip,
    required this.offer,
    required this.driver,
    required this.matching,
    required this.features,
  });

  final String version;
  final MobileRealtimeConfig realtime;
  final MobileStatusGroup booking;
  final MobileStatusGroup trip;
  final MobileStatusGroup offer;
  final MobileDriverConfig driver;
  final MobileMatchingConfig matching;
  final MobileFeatureConfig features;

  factory MobileConfig.fromJson(Map<String, dynamic> json) {
    final booking = MobileStatusGroup.fromJson(_map(json['booking']));
    final trip = MobileStatusGroup.fromJson(_map(json['trip']));
    final offer = MobileStatusGroup.fromJson(_map(json['offer']));
    final driver = MobileDriverConfig.fromJson(_map(json['driver']));

    return MobileConfig(
      version: json['version']?.toString() ?? fallback.version,
      realtime: MobileRealtimeConfig.fromJson(_map(json['realtime'])),
      booking: booking.statuses.isEmpty ? fallback.booking : booking,
      trip: trip.statuses.isEmpty ? fallback.trip : trip,
      offer: offer.statuses.isEmpty ? fallback.offer : offer,
      driver: driver.statuses.isEmpty ? fallback.driver : driver,
      matching: MobileMatchingConfig.fromJson(_map(json['matching'])),
      features: MobileFeatureConfig.fromJson(_map(json['features'])),
    );
  }

  static const fallback = MobileConfig(
    version: 'fallback',
    realtime: MobileRealtimeConfig.fallback,
    booking: MobileStatusGroup(
      statuses: [
        MobileStatusOption(value: 'PendingSchedule', label: 'Đã đặt lịch'),
        MobileStatusOption(value: 'Searching', label: 'Đang tìm tài xế'),
        MobileStatusOption(value: 'DriverAssigned', label: 'Đã có tài xế'),
        MobileStatusOption(value: 'Cancelled', label: 'Đã hủy'),
        MobileStatusOption(value: 'Expired', label: 'Hết hạn'),
        MobileStatusOption(value: 'Completed', label: 'Hoàn thành'),
      ],
    ),
    trip: MobileStatusGroup(
      statuses: [
        MobileStatusOption(value: 'ACCEPTED', label: 'Tài xế đã nhận chuyến'),
        MobileStatusOption(value: 'DRIVER_ARRIVING', label: 'Tài xế đang đến'),
        MobileStatusOption(value: 'ARRIVED', label: 'Tài xế đã đến'),
        MobileStatusOption(value: 'IN_PROGRESS', label: 'Đang di chuyển'),
        MobileStatusOption(
          value: 'WAITING_RETURN_CONFIRM',
          label: 'Chờ xác nhận nhận lại xe',
        ),
        MobileStatusOption(
          value: 'RETURN_CONFIRMED',
          label: 'Đã xác nhận nhận lại xe',
        ),
        MobileStatusOption(value: 'WAITING_PAYMENT', label: 'Chờ thanh toán'),
        MobileStatusOption(value: 'COMPLETED', label: 'Hoàn thành'),
        MobileStatusOption(value: 'CANCELLED', label: 'Đã hủy'),
      ],
    ),
    offer: MobileStatusGroup(
      statuses: [
        MobileStatusOption(value: 'Sent', label: 'Đã gửi tài xế'),
        MobileStatusOption(value: 'DriverAccepted', label: 'Tài xế đã nhận'),
        MobileStatusOption(
          value: 'CustomerConfirmed',
          label: 'Khách đã xác nhận',
        ),
        MobileStatusOption(value: 'Rejected', label: 'Đã từ chối'),
        MobileStatusOption(value: 'Expired', label: 'Hết hạn'),
        MobileStatusOption(value: 'Cancelled', label: 'Đã hủy'),
      ],
    ),
    driver: MobileDriverConfig(
      statuses: [
        MobileStatusOption(value: 'Online', label: 'Đang hoạt động'),
        MobileStatusOption(value: 'Offline', label: 'Ngoại tuyến'),
        MobileStatusOption(value: 'Busy', label: 'Đang có chuyến'),
      ],
      locationUpdateIntervalSeconds: 3,
    ),
    matching: MobileMatchingConfig(
      searchingBookingPollIntervalSeconds: 3,
      nearbyDriversRefreshIntervalSeconds: 5,
      tripStatusPollIntervalSeconds: 4,
      driverLocationUpdateIntervalSeconds: 3,
    ),
    features: MobileFeatureConfig(
      mapProvider: 'GoogleMaps',
      enableGoogleMap: true,
      enableVietMap: true,
    ),
  );

  static Map<String, dynamic> _map(Object? value) {
    return value is Map ? Map<String, dynamic>.from(value) : const {};
  }
}

class MobileRealtimeConfig {
  const MobileRealtimeConfig({required this.hubPath, required this.events});

  final String hubPath;
  final MobileRealtimeEvents events;

  factory MobileRealtimeConfig.fromJson(Map<String, dynamic> json) {
    return MobileRealtimeConfig(
      hubPath: json['hubPath']?.toString() ?? fallback.hubPath,
      events: MobileRealtimeEvents.fromJson(MobileConfig._map(json['events'])),
    );
  }

  static const fallback = MobileRealtimeConfig(
    hubPath: '/hubs/saferide',
    events: MobileRealtimeEvents.fallback,
  );
}

class MobileRealtimeEvents {
  const MobileRealtimeEvents({
    required this.bookingSearchingStarted,
    required this.bookingSearchRadiusExpanded,
    required this.bookingStatusChanged,
    required this.bookingDriverAssigned,
    required this.bookingExpired,
    required this.bookingCancelled,
    required this.driverMatched,
    required this.driverLocationUpdated,
    required this.driverOfferCreated,
    required this.driverOfferReceived,
    required this.driverOfferAccepted,
    required this.driverOfferRejected,
    required this.driverOfferExpired,
    required this.driverOfferCancelled,
    required this.customerConfirmedDriverOffer,
    required this.tripCreated,
    required this.tripStatusChanged,
    required this.tripPaymentPending,
    required this.tripPaymentSucceeded,
  });

  final String bookingSearchingStarted;
  final String bookingSearchRadiusExpanded;
  final String bookingStatusChanged;
  final String bookingDriverAssigned;
  final String bookingExpired;
  final String bookingCancelled;
  final String driverMatched;
  final String driverLocationUpdated;
  final String driverOfferCreated;
  final String driverOfferReceived;
  final String driverOfferAccepted;
  final String driverOfferRejected;
  final String driverOfferExpired;
  final String driverOfferCancelled;
  final String customerConfirmedDriverOffer;
  final String tripCreated;
  final String tripStatusChanged;
  final String tripPaymentPending;
  final String tripPaymentSucceeded;

  List<String> get bookingUpdateEvents => [
    bookingSearchingStarted,
    bookingSearchRadiusExpanded,
    driverOfferAccepted,
    driverOfferRejected,
    driverOfferExpired,
    driverOfferCancelled,
    driverMatched,
    bookingDriverAssigned,
    bookingStatusChanged,
    tripCreated,
    tripStatusChanged,
    customerConfirmedDriverOffer,
    bookingExpired,
    bookingCancelled,
  ];

  factory MobileRealtimeEvents.fromJson(Map<String, dynamic> json) {
    return MobileRealtimeEvents(
      bookingSearchingStarted: _read(
        json,
        'bookingSearchingStarted',
        fallback.bookingSearchingStarted,
      ),
      bookingSearchRadiusExpanded: _read(
        json,
        'bookingSearchRadiusExpanded',
        fallback.bookingSearchRadiusExpanded,
      ),
      bookingStatusChanged: _read(
        json,
        'bookingStatusChanged',
        fallback.bookingStatusChanged,
      ),
      bookingDriverAssigned: _read(
        json,
        'bookingDriverAssigned',
        fallback.bookingDriverAssigned,
      ),
      bookingExpired: _read(json, 'bookingExpired', fallback.bookingExpired),
      bookingCancelled: _read(
        json,
        'bookingCancelled',
        fallback.bookingCancelled,
      ),
      driverMatched: _read(json, 'driverMatched', fallback.driverMatched),
      driverLocationUpdated: _read(
        json,
        'driverLocationUpdated',
        fallback.driverLocationUpdated,
      ),
      driverOfferCreated: _read(
        json,
        'driverOfferCreated',
        fallback.driverOfferCreated,
      ),
      driverOfferReceived: _read(
        json,
        'driverOfferReceived',
        fallback.driverOfferReceived,
      ),
      driverOfferAccepted: _read(
        json,
        'driverOfferAccepted',
        fallback.driverOfferAccepted,
      ),
      driverOfferRejected: _read(
        json,
        'driverOfferRejected',
        fallback.driverOfferRejected,
      ),
      driverOfferExpired: _read(
        json,
        'driverOfferExpired',
        fallback.driverOfferExpired,
      ),
      driverOfferCancelled: _read(
        json,
        'driverOfferCancelled',
        fallback.driverOfferCancelled,
      ),
      customerConfirmedDriverOffer: _read(
        json,
        'customerConfirmedDriverOffer',
        fallback.customerConfirmedDriverOffer,
      ),
      tripCreated: _read(json, 'tripCreated', fallback.tripCreated),
      tripStatusChanged: _read(
        json,
        'tripStatusChanged',
        fallback.tripStatusChanged,
      ),
      tripPaymentPending: _read(
        json,
        'tripPaymentPending',
        fallback.tripPaymentPending,
      ),
      tripPaymentSucceeded: _read(
        json,
        'tripPaymentSucceeded',
        fallback.tripPaymentSucceeded,
      ),
    );
  }

  static const fallback = MobileRealtimeEvents(
    bookingSearchingStarted: 'BookingSearchingStarted',
    bookingSearchRadiusExpanded: 'BookingSearchRadiusExpanded',
    bookingStatusChanged: 'BookingStatusChanged',
    bookingDriverAssigned: 'BookingDriverAssigned',
    bookingExpired: 'BookingExpired',
    bookingCancelled: 'BookingCancelled',
    driverMatched: 'DriverMatched',
    driverLocationUpdated: 'DriverLocationUpdated',
    driverOfferCreated: 'DriverOfferCreated',
    driverOfferReceived: 'ReceiveDriverOffer',
    driverOfferAccepted: 'DriverOfferAccepted',
    driverOfferRejected: 'DriverOfferRejected',
    driverOfferExpired: 'DriverOfferExpired',
    driverOfferCancelled: 'DriverOfferCancelled',
    customerConfirmedDriverOffer: 'CustomerConfirmedDriverOffer',
    tripCreated: 'TripCreated',
    tripStatusChanged: 'TripStatusChanged',
    tripPaymentPending: 'TripPaymentPending',
    tripPaymentSucceeded: 'TripPaymentSucceeded',
  );

  static String _read(Map<String, dynamic> json, String key, String fallback) {
    final value = json[key];
    return value == null || value.toString().isEmpty
        ? fallback
        : value.toString();
  }
}

class MobileStatusGroup {
  const MobileStatusGroup({required this.statuses});

  final List<MobileStatusOption> statuses;

  factory MobileStatusGroup.fromJson(Map<String, dynamic> json) {
    final rawStatuses = json['statuses'];
    if (rawStatuses is! List) {
      return const MobileStatusGroup(statuses: []);
    }

    return MobileStatusGroup(
      statuses: rawStatuses
          .whereType<Map>()
          .map(
            (value) =>
                MobileStatusOption.fromJson(Map<String, dynamic>.from(value)),
          )
          .toList(),
    );
  }

  String labelFor(String value, {String? fallback}) {
    for (final status in statuses) {
      if (status.value == value) return status.label;
    }
    return fallback ?? value;
  }
}

class MobileStatusOption {
  const MobileStatusOption({required this.value, required this.label});

  final String value;
  final String label;

  factory MobileStatusOption.fromJson(Map<String, dynamic> json) {
    return MobileStatusOption(
      value: json['value']?.toString() ?? '',
      label: json['label']?.toString() ?? '',
    );
  }
}

class MobileDriverConfig {
  const MobileDriverConfig({
    required this.statuses,
    required this.locationUpdateIntervalSeconds,
  });

  final List<MobileStatusOption> statuses;
  final int locationUpdateIntervalSeconds;

  factory MobileDriverConfig.fromJson(Map<String, dynamic> json) {
    return MobileDriverConfig(
      statuses: MobileStatusGroup.fromJson(json).statuses,
      locationUpdateIntervalSeconds: _intValue(
        json['locationUpdateIntervalSeconds'],
        MobileConfig.fallback.driver.locationUpdateIntervalSeconds,
      ),
    );
  }
}

class MobileMatchingConfig {
  const MobileMatchingConfig({
    required this.searchingBookingPollIntervalSeconds,
    required this.nearbyDriversRefreshIntervalSeconds,
    required this.tripStatusPollIntervalSeconds,
    required this.driverLocationUpdateIntervalSeconds,
  });

  final int searchingBookingPollIntervalSeconds;
  final int nearbyDriversRefreshIntervalSeconds;
  final int tripStatusPollIntervalSeconds;
  final int driverLocationUpdateIntervalSeconds;

  factory MobileMatchingConfig.fromJson(Map<String, dynamic> json) {
    return MobileMatchingConfig(
      searchingBookingPollIntervalSeconds: _intValue(
        json['searchingBookingPollIntervalSeconds'],
        MobileConfig.fallback.matching.searchingBookingPollIntervalSeconds,
      ),
      nearbyDriversRefreshIntervalSeconds: _intValue(
        json['nearbyDriversRefreshIntervalSeconds'],
        MobileConfig.fallback.matching.nearbyDriversRefreshIntervalSeconds,
      ),
      tripStatusPollIntervalSeconds: _intValue(
        json['tripStatusPollIntervalSeconds'],
        MobileConfig.fallback.matching.tripStatusPollIntervalSeconds,
      ),
      driverLocationUpdateIntervalSeconds: _intValue(
        json['driverLocationUpdateIntervalSeconds'],
        MobileConfig.fallback.matching.driverLocationUpdateIntervalSeconds,
      ),
    );
  }
}

class MobileFeatureConfig {
  const MobileFeatureConfig({
    required this.mapProvider,
    required this.enableGoogleMap,
    required this.enableVietMap,
  });

  final String mapProvider;
  final bool enableGoogleMap;
  final bool enableVietMap;

  factory MobileFeatureConfig.fromJson(Map<String, dynamic> json) {
    return MobileFeatureConfig(
      mapProvider:
          json['mapProvider']?.toString() ??
          MobileConfig.fallback.features.mapProvider,
      enableGoogleMap: _boolValue(
        json['enableGoogleMap'],
        MobileConfig.fallback.features.enableGoogleMap,
      ),
      enableVietMap: _boolValue(
        json['enableVietMap'],
        MobileConfig.fallback.features.enableVietMap,
      ),
    );
  }
}

int _intValue(Object? value, int fallback) {
  if (value is int && value > 0) return value;
  if (value is num && value > 0) return value.toInt();
  final parsed = int.tryParse(value?.toString() ?? '');
  return parsed != null && parsed > 0 ? parsed : fallback;
}

bool _boolValue(Object? value, bool fallback) {
  if (value is bool) return value;
  if (value is String) {
    return switch (value.toLowerCase()) {
      'true' => true,
      'false' => false,
      _ => fallback,
    };
  }
  return fallback;
}
