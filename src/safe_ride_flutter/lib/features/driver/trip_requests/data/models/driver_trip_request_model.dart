class DriverTripRequestModel {
  const DriverTripRequestModel({
    required this.offerId,
    required this.bookingId,
    required this.offerStatus,
    required this.expiresAt,
    required this.expectedIncome,
    required this.pickupAddress,
    required this.destinationAddress,
    this.pickupDistanceKm,
    this.pickupDurationMinutes,
    this.customerConfirmRemainingSeconds,
  });

  final int offerId;
  final int bookingId;
  final String offerStatus;
  final DateTime? expiresAt;
  final double expectedIncome;
  final String pickupAddress;
  final String destinationAddress;
  final double? pickupDistanceKm;
  final int? pickupDurationMinutes;
  final int? customerConfirmRemainingSeconds;

  bool get isSent => offerStatus == 'Sent';
  bool get isDriverAccepted => offerStatus == 'DriverAccepted';

  factory DriverTripRequestModel.fromJson(Map<String, dynamic> json) {
    final offerId = _requiredPositiveInt(_value(json, 'offerId'), 'offerId');
    final bookingId = _requiredPositiveInt(
      _value(json, 'bookingId'),
      'bookingId',
    );
    final offerStatus = _normalizeOfferStatus(_value(json, 'offerStatus'));
    if (offerStatus == null) {
      throw const FormatException('Invalid driver offer status.');
    }

    return DriverTripRequestModel(
      offerId: offerId,
      bookingId: bookingId,
      offerStatus: offerStatus,
      expiresAt: _value(json, 'expiresAt') == null
          ? null
          : DateTime.tryParse(_value(json, 'expiresAt').toString()),
      expectedIncome: (_value(json, 'expectedIncome') as num?)?.toDouble() ?? 0,
      pickupAddress: _value(json, 'pickupAddress')?.toString() ?? '',
      destinationAddress: _value(json, 'destinationAddress')?.toString() ?? '',
      pickupDistanceKm: (_value(json, 'pickupDistanceKm') as num?)?.toDouble(),
      pickupDurationMinutes: (_value(json, 'pickupDurationMinutes') as num?)
          ?.toInt(),
      customerConfirmRemainingSeconds:
          (_value(json, 'customerConfirmRemainingSeconds') as num?)?.toInt(),
    );
  }

  static Object? _value(Map<String, dynamic> data, String key) {
    final pascalKey = key.isEmpty
        ? key
        : '${key[0].toUpperCase()}${key.substring(1)}';
    return data[key] ?? data[pascalKey];
  }

  static int _requiredPositiveInt(Object? value, String fieldName) {
    final parsed = value is num
        ? value.toInt()
        : int.tryParse(value?.toString().trim() ?? '');
    if (parsed == null || parsed <= 0) {
      throw FormatException('Invalid $fieldName.');
    }
    return parsed;
  }

  static String? _normalizeOfferStatus(Object? value) {
    if (value == null) return null;
    final numericValue = value is num
        ? value.toInt()
        : int.tryParse(value.toString().trim());
    if (numericValue != null) {
      return switch (numericValue) {
        0 => 'Sent',
        1 => 'DriverAccepted',
        2 => 'CustomerConfirmed',
        3 => 'Rejected',
        4 => 'Expired',
        5 => 'Cancelled',
        _ => null,
      };
    }

    return switch (value.toString().trim().toLowerCase()) {
      'sent' => 'Sent',
      'driveraccepted' => 'DriverAccepted',
      'customerconfirmed' => 'CustomerConfirmed',
      'rejected' => 'Rejected',
      'expired' => 'Expired',
      'cancelled' || 'canceled' => 'Cancelled',
      _ => null,
    };
  }
}
