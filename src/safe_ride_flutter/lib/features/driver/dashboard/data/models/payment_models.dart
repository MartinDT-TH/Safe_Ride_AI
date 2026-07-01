class QrPaymentResult {
  const QrPaymentResult({
    required this.tripId,
    required this.paymentId,
    required this.orderCode,
    required this.amount,
    required this.currency,
    required this.paymentStatus,
    this.qrCode,
    this.checkoutUrl,
  });

  final int tripId;
  final int paymentId;
  final String orderCode;
  final double amount;
  final String currency;
  final String paymentStatus;
  final String? qrCode;
  final String? checkoutUrl;

  bool get isSuccess => paymentStatus.toLowerCase() == 'success';

  factory QrPaymentResult.fromJson(Map<String, dynamic> json) {
    return QrPaymentResult(
      tripId: (json['tripId'] as num?)?.toInt() ?? 0,
      paymentId: (json['paymentId'] as num?)?.toInt() ?? 0,
      orderCode: json['orderCode']?.toString() ?? '',
      amount: (json['amount'] as num?)?.toDouble() ?? 0,
      currency: json['currency']?.toString() ?? 'VND',
      paymentStatus: json['paymentStatus']?.toString() ?? 'Pending',
      qrCode: json['qrCode']?.toString(),
      checkoutUrl: json['checkoutUrl']?.toString(),
    );
  }
}

class PaymentStatusResult {
  const PaymentStatusResult({
    required this.tripId,
    required this.paymentStatus,
    required this.amount,
    required this.originalFare,
    required this.finalFare,
    required this.driverShare,
    required this.platformShare,
    required this.currency,
    this.paymentId,
    this.paymentMethod,
  });

  final int tripId;
  final int? paymentId;
  final String? paymentMethod;
  final String paymentStatus;
  final double amount;
  final double originalFare;
  final double finalFare;
  final double driverShare;
  final double platformShare;
  final String currency;

  bool get isSuccess => paymentStatus.toLowerCase() == 'success';

  factory PaymentStatusResult.fromJson(Map<String, dynamic> json) {
    return PaymentStatusResult(
      tripId: (json['tripId'] as num?)?.toInt() ?? 0,
      paymentId: (json['paymentId'] as num?)?.toInt(),
      paymentMethod: json['paymentMethod']?.toString(),
      paymentStatus: json['paymentStatus']?.toString() ?? 'Pending',
      amount: (json['amount'] as num?)?.toDouble() ?? 0,
      originalFare: (json['originalFare'] as num?)?.toDouble() ?? 0,
      finalFare: (json['finalFare'] as num?)?.toDouble() ?? 0,
      driverShare: (json['driverShare'] as num?)?.toDouble() ?? 0,
      platformShare: (json['platformShare'] as num?)?.toDouble() ?? 0,
      currency: json['currency']?.toString() ?? 'VND',
    );
  }
}
