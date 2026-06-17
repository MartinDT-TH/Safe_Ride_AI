class NearbyDriver {
  const NearbyDriver({
    required this.driverId,
    required this.latitude,
    required this.longitude,
  });

  final String driverId;
  final double latitude;
  final double longitude;

  factory NearbyDriver.fromJson(Map<String, dynamic> json) {
    return NearbyDriver(
      driverId: json['driverId']?.toString() ?? '',
      latitude: (json['latitude'] as num?)?.toDouble() ?? 0,
      longitude: (json['longitude'] as num?)?.toDouble() ?? 0,
    );
  }
}
