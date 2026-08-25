import '../../../../../core/localization/locale_provider.dart';

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
          LocaleProvider.currentLocalizations.payDriverToComplete,
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
    this.successfulPaymentAmount = 0,
    this.remainingPayableAmount = 0,
    this.refundObligationAmount = 0,
    this.reconciliationStatus,
    this.refundStatus,
    this.fareEarning,
    this.longDistanceEarning,
    this.longPickupCompensation,
    this.totalPayout,
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
  final double successfulPaymentAmount;
  final double remainingPayableAmount;
  final double refundObligationAmount;
  final String? reconciliationStatus;
  final String? refundStatus;
  final double? fareEarning;
  final double? longDistanceEarning;
  final double? longPickupCompensation;
  final double? totalPayout;

  bool get isSuccess => paymentStatus.toLowerCase() == 'success';
  bool get requiresPayment => remainingPayableAmount > 0;

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
          LocaleProvider.currentLocalizations.payDriverToComplete,
      paidAt: json['paidAt'] == null
          ? null
          : DateTime.tryParse(json['paidAt'].toString()),
      successfulPaymentAmount:
          (json['successfulPaymentAmount'] as num?)?.toDouble() ?? 0,
      remainingPayableAmount:
          (json['remainingPayableAmount'] as num?)?.toDouble() ?? 0,
      refundObligationAmount:
          (json['refundObligationAmount'] as num?)?.toDouble() ?? 0,
      reconciliationStatus: json['reconciliationStatus']?.toString(),
      refundStatus: json['refundStatus']?.toString(),
      fareEarning: (json['fareEarning'] as num?)?.toDouble(),
      longDistanceEarning: (json['longDistanceEarning'] as num?)?.toDouble(),
      longPickupCompensation: (json['longPickupCompensation'] as num?)
          ?.toDouble(),
      totalPayout: (json['totalPayout'] as num?)?.toDouble(),
    );
  }
}
