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
    // Handle both camelCase and PascalCase just in case
    return NearbyDriver(
      driverId: (json['driverId'] ?? json['DriverId'])?.toString() ?? '',
      latitude: (json['latitude'] ?? json['Latitude'] as num?)?.toDouble() ?? 0,
      longitude: (json['longitude'] ?? json['Longitude'] as num?)?.toDouble() ?? 0,
    );
  }
}
