enum ActivityTripStatus { completed, cancelled }

class ActivityTrip {
  final int id;
  final String pickup;
  final String destination;
  final DateTime time;
  final double fare;
  final double distanceKm;
  final ActivityTripStatus status;
  final String? driverName;
  final double? driverRating;
  final String? driverAvatar;
  final String vehicleName;
  final bool isMotorbike;

  ActivityTrip({
    required this.id,
    required this.pickup,
    required this.destination,
    required this.time,
    required this.fare,
    required this.distanceKm,
    required this.status,
    required this.vehicleName,
    this.isMotorbike = false,
    this.driverName,
    this.driverRating,
    this.driverAvatar,
  });

  factory ActivityTrip.fromJson(Map<String, dynamic> json) {
    return ActivityTrip(
      id: json['id'] as int,
      pickup: json['pickupAddress'] ?? '',
      destination: json['destinationAddress'] ?? '',
      time: DateTime.parse(json['scheduledAt'] ?? DateTime.now().toIso8601String()),
      fare: (json['estimatedFare'] as num?)?.toDouble() ?? 0,
      distanceKm: (json['estimatedDistanceKm'] as num?)?.toDouble() ?? 0,
      status: json['bookingStatus'] == 'Cancelled' 
          ? ActivityTripStatus.cancelled 
          : ActivityTripStatus.completed,
      vehicleName: json['vehicleName'] ?? 'SafeRide',
      isMotorbike: json['isMotorbike'] ?? false,
      driverName: json['driverName'],
      driverRating: (json['driverRating'] as num?)?.toDouble(),
      driverAvatar: json['driverAvatar'],
    );
  }
}
