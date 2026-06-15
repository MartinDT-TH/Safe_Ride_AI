import '../../../../core/constants/app_strings.dart';
import 'booking_location.dart';

enum BookingType { now, scheduled }

class CreateBookingRequest {
  const CreateBookingRequest({
    required this.vehicleId,
    required this.serviceTypeId,
    required this.bookingType,
    required this.pickup,
    required this.destination,
    this.scheduledAt,
    this.specialRequest,
  });

  final int vehicleId;
  final int serviceTypeId;
  final BookingType bookingType;
  final DateTime? scheduledAt;
  final BookingLocation pickup;
  final BookingLocation destination;
  final String? specialRequest;

  Map<String, dynamic> toJson() {
    return {
      ApiKeys.vehicleId: vehicleId,
      ApiKeys.serviceTypeId: serviceTypeId,
      ApiKeys.bookingType: bookingType == BookingType.now
          ? AppValues.bookingNow
          : AppValues.bookingScheduled,
      ApiKeys.scheduledAt: scheduledAt?.toUtc().toIso8601String(),
      ApiKeys.pickupAddress: pickup.address,
      ApiKeys.pickupLatitude: pickup.latitude,
      ApiKeys.pickupLongitude: pickup.longitude,
      ApiKeys.destinationAddress: destination.address,
      ApiKeys.destinationLatitude: destination.latitude,
      ApiKeys.destinationLongitude: destination.longitude,
      ApiKeys.specialRequest: specialRequest?.trim().isEmpty == true
          ? null
          : specialRequest?.trim(),
    };
  }
}
