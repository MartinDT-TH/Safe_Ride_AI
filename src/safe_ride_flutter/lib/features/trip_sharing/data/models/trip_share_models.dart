class TripShareRecipient {
  const TripShareRecipient({
    required this.userId,
    required this.fullName,
    required this.maskedPhoneNumber,
    this.avatarUrl,
  });

  final String userId;
  final String fullName;
  final String? avatarUrl;
  final String maskedPhoneNumber;

  factory TripShareRecipient.fromJson(Map<String, dynamic> json) =>
      TripShareRecipient(
        userId: json['userId']?.toString() ?? '',
        fullName: json['fullName']?.toString() ?? 'Người dùng SafeRide',
        avatarUrl: json['avatarUrl']?.toString(),
        maskedPhoneNumber: json['maskedPhoneNumber']?.toString() ?? '***',
      );
}

class CreatedTripShare {
  const CreatedTripShare({
    required this.tripShareId,
    required this.recipient,
    required this.shareUrl,
    required this.expiresAt,
  });

  final int tripShareId;
  final TripShareRecipient recipient;
  final String shareUrl;
  final DateTime expiresAt;

  factory CreatedTripShare.fromJson(Map<String, dynamic> json) =>
      CreatedTripShare(
        tripShareId: (json['tripShareId'] as num).toInt(),
        recipient: TripShareRecipient.fromJson(
          Map<String, dynamic>.from(json['recipient'] as Map),
        ),
        shareUrl: json['shareUrl'] as String,
        expiresAt: DateTime.parse(json['expiresAt'] as String).toUtc(),
      );
}

class TripShareListItem {
  const TripShareListItem({
    required this.tripShareId,
    required this.recipient,
    required this.expiresAt,
    required this.isActive,
    this.openedAt,
    this.revokedAt,
  });

  final int tripShareId;
  final TripShareRecipient recipient;
  final DateTime? openedAt;
  final DateTime expiresAt;
  final DateTime? revokedAt;
  final bool isActive;

  factory TripShareListItem.fromJson(Map<String, dynamic> json) =>
      TripShareListItem(
        tripShareId: (json['tripShareId'] as num).toInt(),
        recipient: TripShareRecipient.fromJson(
          Map<String, dynamic>.from(json['recipient'] as Map),
        ),
        openedAt: _date(json['openedAt']),
        expiresAt: DateTime.parse(json['expiresAt'] as String).toUtc(),
        revokedAt: _date(json['revokedAt']),
        isActive: json['isActive'] == true,
      );
}

class ResolvedTripShare {
  const ResolvedTripShare({
    required this.tripShareId,
    required this.tripId,
    required this.tripStatus,
  });

  final int tripShareId;
  final int tripId;
  final String tripStatus;

  factory ResolvedTripShare.fromJson(Map<String, dynamic> json) =>
      ResolvedTripShare(
        tripShareId: (json['tripShareId'] as num).toInt(),
        tripId: (json['tripId'] as num).toInt(),
        tripStatus: json['tripStatus']?.toString() ?? '',
      );
}

class ReceivedTripShare {
  const ReceivedTripShare({
    required this.tripShareId,
    required this.tripStatus,
    required this.sharedByName,
    required this.expiresAt,
    required this.isActive,
    this.sharedByAvatarUrl,
    this.openedAt,
  });

  final int tripShareId;
  final String tripStatus;
  final String sharedByName;
  final String? sharedByAvatarUrl;
  final DateTime? openedAt;
  final DateTime expiresAt;
  final bool isActive;

  factory ReceivedTripShare.fromJson(Map<String, dynamic> json) {
    final sharedBy = Map<String, dynamic>.from(
      json['sharedBy'] as Map? ?? const {},
    );
    return ReceivedTripShare(
      tripShareId: (json['tripShareId'] as num).toInt(),
      tripStatus: json['tripStatus']?.toString() ?? '',
      sharedByName: sharedBy['fullName']?.toString() ?? 'Người dùng SafeRide',
      sharedByAvatarUrl: sharedBy['avatarUrl']?.toString(),
      openedAt: _date(json['openedAt']),
      expiresAt: DateTime.parse(json['expiresAt'] as String).toUtc(),
      isActive: json['isActive'] == true,
    );
  }
}

class SharedTripPoint {
  const SharedTripPoint({
    required this.latitude,
    required this.longitude,
    this.address,
  });
  final double latitude;
  final double longitude;
  final String? address;

  factory SharedTripPoint.fromJson(Map<String, dynamic> json) =>
      SharedTripPoint(
        latitude: (json['latitude'] as num).toDouble(),
        longitude: (json['longitude'] as num).toDouble(),
        address: json['address']?.toString(),
      );
}

class SharedTripTracking {
  const SharedTripTracking({
    required this.tripShareId,
    required this.tripStatus,
    required this.pickup,
    required this.driverName,
    required this.vehicleBrandModel,
    required this.maskedPlateNumber,
    this.destination,
    this.currentDriverLocation,
    this.lastLocationUpdate,
    this.routePolyline,
    this.driverAvatarUrl,
    this.driverRating,
    this.vehicleColor,
  });

  final int tripShareId;
  final String tripStatus;
  final SharedTripPoint pickup;
  final SharedTripPoint? destination;
  final SharedTripPoint? currentDriverLocation;
  final DateTime? lastLocationUpdate;
  final String? routePolyline;
  final String driverName;
  final String? driverAvatarUrl;
  final double? driverRating;
  final String vehicleBrandModel;
  final String? vehicleColor;
  final String maskedPlateNumber;

  factory SharedTripTracking.fromJson(Map<String, dynamic> json) {
    final driver = Map<String, dynamic>.from(json['driver'] as Map);
    final vehicle = Map<String, dynamic>.from(json['vehicle'] as Map);
    return SharedTripTracking(
      tripShareId: (json['tripShareId'] as num).toInt(),
      tripStatus: json['tripStatus']?.toString() ?? '',
      pickup: SharedTripPoint.fromJson(
        Map<String, dynamic>.from(json['pickup'] as Map),
      ),
      destination: _point(json['destination']),
      currentDriverLocation: _point(json['currentDriverLocation']),
      lastLocationUpdate: _date(json['lastLocationUpdate']),
      routePolyline: json['routePolyline']?.toString(),
      driverName: driver['fullName']?.toString() ?? 'Tài xế SafeRide',
      driverAvatarUrl: driver['avatarUrl']?.toString(),
      driverRating: (driver['rating'] as num?)?.toDouble(),
      vehicleBrandModel: vehicle['brandModel']?.toString() ?? '',
      vehicleColor: vehicle['color']?.toString(),
      maskedPlateNumber: vehicle['maskedPlateNumber']?.toString() ?? '***',
    );
  }

  SharedTripTracking copyWith({
    String? tripStatus,
    SharedTripPoint? currentDriverLocation,
    DateTime? lastLocationUpdate,
  }) => SharedTripTracking(
    tripShareId: tripShareId,
    tripStatus: tripStatus ?? this.tripStatus,
    pickup: pickup,
    destination: destination,
    currentDriverLocation: currentDriverLocation ?? this.currentDriverLocation,
    lastLocationUpdate: lastLocationUpdate ?? this.lastLocationUpdate,
    routePolyline: routePolyline,
    driverName: driverName,
    driverAvatarUrl: driverAvatarUrl,
    driverRating: driverRating,
    vehicleBrandModel: vehicleBrandModel,
    vehicleColor: vehicleColor,
    maskedPlateNumber: maskedPlateNumber,
  );
}

DateTime? _date(Object? value) =>
    value == null ? null : DateTime.tryParse(value.toString())?.toUtc();

SharedTripPoint? _point(Object? value) => value is Map
    ? SharedTripPoint.fromJson(Map<String, dynamic>.from(value))
    : null;
