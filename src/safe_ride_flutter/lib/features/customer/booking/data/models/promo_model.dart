class PromoModel {
  final int promotionId;
  final String promotionCode;
  final String discountType;
  final double discountValue;
  final DateTime? startDate;
  final DateTime? endDate;
  final double minimumOrderValue;
  final double maximumDiscountValue;
  final int usageLimitPerUser;
  final int remainingUsageCount;
  final String shortDescription;
  final int requiredCompletedTrips;
  final int customerCompletedTrips;
  final int remainingTripsToUnlock;
  final bool isUnlocked;
  final String? unlockMessage;

  const PromoModel({
    required this.promotionId,
    required this.promotionCode,
    required this.discountType,
    required this.discountValue,
    this.startDate,
    this.endDate,
    this.minimumOrderValue = 0,
    this.maximumDiscountValue = 0,
    this.usageLimitPerUser = 1,
    this.remainingUsageCount = 0,
    required this.shortDescription,
    this.requiredCompletedTrips = 0,
    this.customerCompletedTrips = 0,
    this.remainingTripsToUnlock = 0,
    this.isUnlocked = true,
    this.unlockMessage,
  });

  factory PromoModel.fromJson(Map<String, dynamic> json) {
    return PromoModel(
      promotionId: (json['promotionId'] as num?)?.toInt() ?? 0,
      promotionCode: json['promotionCode']?.toString() ?? '',
      discountType: json['discountType']?.toString() ?? '',
      discountValue: (json['discountValue'] as num?)?.toDouble() ?? 0,
      startDate: json['startDate'] == null
          ? null
          : DateTime.tryParse(json['startDate'].toString()),
      endDate: json['endDate'] == null
          ? null
          : DateTime.tryParse(json['endDate'].toString()),
      minimumOrderValue: (json['minimumOrderValue'] as num?)?.toDouble() ?? 0,
      maximumDiscountValue:
          (json['maximumDiscountValue'] as num?)?.toDouble() ?? 0,
      usageLimitPerUser: (json['usageLimitPerUser'] as num?)?.toInt() ?? 1,
      remainingUsageCount: (json['remainingUsageCount'] as num?)?.toInt() ?? 0,
      shortDescription: json['shortDescription']?.toString() ?? '',
      requiredCompletedTrips:
          (json['requiredCompletedTrips'] as num?)?.toInt() ?? 0,
      customerCompletedTrips:
          (json['customerCompletedTrips'] as num?)?.toInt() ?? 0,
      remainingTripsToUnlock:
          (json['remainingTripsToUnlock'] as num?)?.toInt() ?? 0,
      isUnlocked: json['isUnlocked'] as bool? ?? true,
      unlockMessage: json['unlockMessage']?.toString(),
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'promotionId': promotionId,
      'promotionCode': promotionCode,
      'discountType': discountType,
      'discountValue': discountValue,
      'startDate': startDate?.toIso8601String(),
      'endDate': endDate?.toIso8601String(),
      'minimumOrderValue': minimumOrderValue,
      'maximumDiscountValue': maximumDiscountValue,
      'usageLimitPerUser': usageLimitPerUser,
      'remainingUsageCount': remainingUsageCount,
      'shortDescription': shortDescription,
      'requiredCompletedTrips': requiredCompletedTrips,
      'customerCompletedTrips': customerCompletedTrips,
      'remainingTripsToUnlock': remainingTripsToUnlock,
      'isUnlocked': isUnlocked,
      'unlockMessage': unlockMessage,
    };
  }

  // Legacy support for older code if any
  String get code => promotionCode;
  String get description => shortDescription;

  String get resolvedUnlockMessage => unlockMessage?.trim().isNotEmpty == true
      ? unlockMessage!.trim()
      : 'Bạn cần hoàn thành thêm $remainingTripsToUnlock chuyến để sử dụng mã khuyến mãi này.';
}
