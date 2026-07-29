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
    return DriverTripRequestModel(
      offerId: (_value(json, 'offerId') as num?)?.toInt() ?? 0,
      bookingId: (_value(json, 'bookingId') as num?)?.toInt() ?? 0,
      offerStatus: _normalizeOfferStatus(_value(json, 'offerStatus')) ?? 'Sent',
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

  static String? _normalizeOfferStatus(Object? value) {
    if (value == null) return null;
    if (value is num) {
      return switch (value.toInt()) {
        0 => 'Sent',
        1 => 'DriverAccepted',
        2 => 'CustomerConfirmed',
        3 => 'Rejected',
        4 => 'Expired',
        5 => 'Cancelled',
        _ => value.toString(),
      };
    }

    final text = value.toString();
    return switch (text) {
      '0' => 'Sent',
      '1' => 'DriverAccepted',
      '2' => 'CustomerConfirmed',
      '3' => 'Rejected',
      '4' => 'Expired',
      '5' => 'Cancelled',
      _ => text,
    };
  }
}
