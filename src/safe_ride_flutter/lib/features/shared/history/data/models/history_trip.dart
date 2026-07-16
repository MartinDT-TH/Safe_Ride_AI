enum HistoryTripStatus { completed, cancelled, booked }

class HistoryTrip {
  final int id;
  final String pickup;
  final String destination;
  final DateTime time;
  final double fare;
  final double distanceKm;
  final HistoryTripStatus status;
  final String? driverName;
  final double? driverRating;
  final String? driverAvatar;
  final String vehicleName;
  final bool isMotorbike;
  final bool hasReported;

  HistoryTrip({
    required this.id,
    required this.pickup,
    required this.destination,
    required this.time,
    required this.fare,
    required this.distanceKm,
    required this.status,
    required this.vehicleName,
    this.isMotorbike = false,
    this.hasReported = false,
    this.driverName,
    this.driverRating,
    this.driverAvatar,
  });

  factory HistoryTrip.fromJson(Map<String, dynamic> json) {
    final occurredAt = json['occurredAt']?.toString() ??
        json['completedAt']?.toString() ??
        json['updatedAt']?.toString() ??
        json['scheduledAt']?.toString();
    final estimatedFare = (json['estimatedFare'] as num?)?.toDouble() ?? 0;
    final finalFare = (json['finalFare'] as num?)?.toDouble();
    final bookingStatus =
        (json['bookingStatus'] ?? json['tripStatus'])?.toString().toLowerCase() ??
            '';
    final status = bookingStatus.contains('cancel') ||
            bookingStatus.contains('expire')
        ? HistoryTripStatus.cancelled
        : bookingStatus.contains('complete')
            ? HistoryTripStatus.completed
            : HistoryTripStatus.booked;

    return HistoryTrip(
      id: (json['id'] as num?)?.toInt() ?? 0,
      pickup: json['pickupAddress'] ?? '',
      destination: json['destinationAddress'] ?? '',
      time: DateTime.tryParse(occurredAt ?? '') ?? DateTime.now(),
      fare: finalFare != null && finalFare > 0 ? finalFare : estimatedFare,
      distanceKm: (json['estimatedDistanceKm'] as num?)?.toDouble() ?? 0,
      status: status,
      vehicleName: json['vehicleName'] ?? 'SafeRide',
      isMotorbike: json['isMotorbike'] ?? false,
      hasReported: json['hasReported'] == true,
      driverName: json['driverName'],
      driverRating: (json['driverRating'] as num?)?.toDouble(),
      driverAvatar: json['driverAvatarUrl'] ?? json['driverAvatar'],
    );
  }
}
