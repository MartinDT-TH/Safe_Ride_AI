class QrPaymentResult {
  const QrPaymentResult({
    required this.tripId,
    required this.paymentId,
    required this.orderCode,
    required this.amount,
    required this.currency,
    required this.paymentStatus,
    required this.tripStatus,
    required this.message,
    this.qrCode,
    this.checkoutUrl,
    this.createdAt,
  });

  final int tripId;
  final int paymentId;
  final String orderCode;
  final double amount;
  final String currency;
  final String paymentStatus;
  final String tripStatus;
  final String message;
  final String? qrCode;
  final String? checkoutUrl;
  final DateTime? createdAt;

  bool get isSuccess => paymentStatus.toLowerCase() == 'success';

  factory QrPaymentResult.fromJson(Map<String, dynamic> json) {
    return QrPaymentResult(
      tripId: (json['tripId'] as num?)?.toInt() ?? 0,
      paymentId: (json['paymentId'] as num?)?.toInt() ?? 0,
      orderCode: json['orderCode']?.toString() ?? '',
      amount: (json['amount'] as num?)?.toDouble() ?? 0,
      currency: json['currency']?.toString() ?? 'VND',
      paymentStatus: json['paymentStatus']?.toString() ?? 'Pending',
      tripStatus: json['tripStatus']?.toString() ?? 'WAITING_PAYMENT',
      message:
          json['message']?.toString() ??
          'Vui lòng thanh toán cho tài xế để hoàn tất chuyến đi.',
      qrCode: json['qrCode']?.toString(),
      checkoutUrl: json['checkoutUrl']?.toString(),
      createdAt: json['createdAt'] == null
          ? null
          : DateTime.tryParse(json['createdAt'].toString()),
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
    required this.tripStatus,
    required this.message,
    this.paymentId,
    this.paymentMethod,
    this.paidAt,
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
  final String tripStatus;
  final String message;
  final DateTime? paidAt;

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
      tripStatus: json['tripStatus']?.toString() ?? 'WAITING_PAYMENT',
      message:
          json['message']?.toString() ??
          'Vui lòng thanh toán cho tài xế để hoàn tất chuyến đi.',
      paidAt: json['paidAt'] == null
          ? null
          : DateTime.tryParse(json['paidAt'].toString()),
    );
  }
}
