import '../../../../../core/constants/app_strings.dart';
import 'booking_location.dart';

enum BookingType { now, scheduled }

class CreateBookingRequest {
  const CreateBookingRequest({
    required this.vehicleId,
    required this.serviceTypeId,
    required this.bookingType,
    required this.pickup,
    this.destination,
    this.scheduledAt,
    this.specialRequest,
    this.estimatedHours,
    this.promotionCode,
  });

  final int vehicleId;
  final int serviceTypeId;
  final BookingType bookingType;
  final DateTime? scheduledAt;
  final BookingLocation pickup;
  final BookingLocation? destination;
  final String? specialRequest;
  final int? estimatedHours;
  final String? promotionCode;

  CreateBookingRequest copyWith({
    int? vehicleId,
    int? serviceTypeId,
    BookingType? bookingType,
    DateTime? scheduledAt,
    BookingLocation? pickup,
    BookingLocation? destination,
    String? specialRequest,
    int? estimatedHours,
    String? promotionCode,
  }) {
    return CreateBookingRequest(
      vehicleId: vehicleId ?? this.vehicleId,
      serviceTypeId: serviceTypeId ?? this.serviceTypeId,
      bookingType: bookingType ?? this.bookingType,
      scheduledAt: scheduledAt ?? this.scheduledAt,
      pickup: pickup ?? this.pickup,
      destination: destination ?? this.destination,
      specialRequest: specialRequest ?? this.specialRequest,
      estimatedHours: estimatedHours ?? this.estimatedHours,
      promotionCode: promotionCode ?? this.promotionCode,
    );
  }

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
      ApiKeys.destinationAddress: destination?.address,
      ApiKeys.destinationLatitude: destination?.latitude ?? pickup.latitude,
      ApiKeys.destinationLongitude: destination?.longitude ?? pickup.longitude,
      ApiKeys.specialRequest: specialRequest?.trim().isEmpty == true
          ? null
          : specialRequest?.trim(),
      ApiKeys.estimatedHours: estimatedHours,
      if (promotionCode != null && promotionCode!.trim().isNotEmpty)
        ApiKeys.promotionCode: promotionCode!.trim(),
    };
  }
}

