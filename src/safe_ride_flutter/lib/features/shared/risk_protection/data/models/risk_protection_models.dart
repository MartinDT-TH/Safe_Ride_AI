class RiskProtectionAccident {
  const RiskProtectionAccident({
    required this.id,
    required this.tripId,
    required this.category,
    required this.status,
    required this.occurredAt,
    required this.description,
    required this.evidence,
    this.policeReportReference,
    this.claim,
    this.assessment,
  });

  final int id;
  final int tripId;
  final String category;
  final String status;
  final DateTime occurredAt;
  final String description;
  final String? policeReportReference;
  final List<AccidentEvidenceItem> evidence;
  final ProtectionClaimSummary? claim;
  final LiabilityAssessmentSummary? assessment;

  factory RiskProtectionAccident.fromJson(Map<String, dynamic> json) {
    final claimJson = json['claim'];
    final assessmentJson = json['liabilityAssessment'];
    return RiskProtectionAccident(
      id: _asInt(json['id']),
      tripId: _asInt(json['tripId']),
      category: json['category']?.toString() ?? '',
      status: json['status']?.toString() ?? '',
      occurredAt: _asDate(json['occurredAtUtc']),
      description: json['description']?.toString() ?? '',
      policeReportReference: json['policeReportReference']?.toString(),
      evidence: (json['evidence'] as List? ?? const [])
          .whereType<Map>()
          .map(
            (item) =>
                AccidentEvidenceItem.fromJson(item.cast<String, dynamic>()),
          )
          .toList(growable: false),
      claim: claimJson is Map
          ? ProtectionClaimSummary.fromJson(claimJson.cast<String, dynamic>())
          : _claimFromLegacyFields(json),
      assessment: assessmentJson is Map
          ? LiabilityAssessmentSummary.fromJson(
              assessmentJson.cast<String, dynamic>(),
            )
          : null,
    );
  }

  static ProtectionClaimSummary? _claimFromLegacyFields(
    Map<String, dynamic> json,
  ) {
    final id = _asNullableInt(json['claimId']);
    if (id == null) return null;
    return ProtectionClaimSummary(
      id: id,
      status: json['claimStatus']?.toString() ?? '',
    );
  }
}

class AccidentEvidenceItem {
  const AccidentEvidenceItem({
    required this.id,
    required this.type,
    required this.fileUrl,
    required this.createdAt,
    this.originalFileName,
    this.description,
  });

  final int id;
  final String type;
  final String fileUrl;
  final String? originalFileName;
  final String? description;
  final DateTime createdAt;

  factory AccidentEvidenceItem.fromJson(Map<String, dynamic> json) =>
      AccidentEvidenceItem(
        id: _asInt(json['id']),
        type: json['evidenceType']?.toString() ?? '',
        fileUrl: json['fileUrl']?.toString() ?? '',
        originalFileName: json['originalFileName']?.toString(),
        description: json['description']?.toString(),
        createdAt: _asDate(json['createdAtUtc']),
      );
}

class LiabilityAssessmentSummary {
  const LiabilityAssessmentSummary({
    required this.status,
    required this.driverFaultPercentage,
    required this.customerFaultPercentage,
    required this.thirdPartyFaultPercentage,
    required this.vehicleFailurePercentage,
    required this.objectiveCausePercentage,
    required this.driverFaultLevel,
    this.disputeReason,
  });

  final String status;
  final double driverFaultPercentage;
  final double customerFaultPercentage;
  final double thirdPartyFaultPercentage;
  final double vehicleFailurePercentage;
  final double objectiveCausePercentage;
  final String driverFaultLevel;
  final String? disputeReason;

  factory LiabilityAssessmentSummary.fromJson(Map<String, dynamic> json) =>
      LiabilityAssessmentSummary(
        status: json['status']?.toString() ?? '',
        driverFaultPercentage: _asDouble(json['driverFaultPercentage']),
        customerFaultPercentage: _asDouble(json['customerFaultPercentage']),
        thirdPartyFaultPercentage: _asDouble(json['thirdPartyFaultPercentage']),
        vehicleFailurePercentage: _asDouble(json['vehicleFailurePercentage']),
        objectiveCausePercentage: _asDouble(json['objectiveCausePercentage']),
        driverFaultLevel: json['driverFaultLevel']?.toString() ?? '',
        disputeReason: json['disputeReason']?.toString(),
      );
}

class ProtectionClaimSummary {
  const ProtectionClaimSummary({
    required this.id,
    required this.status,
    this.insuranceStatus = '',
    this.eligibleDamageAmount = 0,
    this.insuranceApprovedAmount = 0,
    this.riskFundAdvanceAmount = 0,
    this.riskFundPermanentLossAmount = 0,
    this.driverLiabilityAmount = 0,
    this.customerLiabilityAmount = 0,
    this.thirdPartyLiabilityAmount = 0,
    this.totalPaidToClaimant = 0,
    this.recoveredAmount = 0,
    this.outstandingRecoveryAmount = 0,
  });

  final int id;
  final String status;
  final String insuranceStatus;
  final double eligibleDamageAmount;
  final double insuranceApprovedAmount;
  final double riskFundAdvanceAmount;
  final double riskFundPermanentLossAmount;
  final double driverLiabilityAmount;
  final double customerLiabilityAmount;
  final double thirdPartyLiabilityAmount;
  final double totalPaidToClaimant;
  final double recoveredAmount;
  final double outstandingRecoveryAmount;

  factory ProtectionClaimSummary.fromJson(Map<String, dynamic> json) =>
      ProtectionClaimSummary(
        id: _asInt(json['id']),
        status: json['status']?.toString() ?? '',
        insuranceStatus: json['insuranceStatus']?.toString() ?? '',
        eligibleDamageAmount: _asDouble(json['eligibleDamageAmount']),
        insuranceApprovedAmount: _asDouble(json['insuranceApprovedAmount']),
        riskFundAdvanceAmount: _asDouble(json['riskFundAdvanceAmount']),
        riskFundPermanentLossAmount: _asDouble(
          json['riskFundPermanentLossAmount'],
        ),
        driverLiabilityAmount: _asDouble(json['driverLiabilityAmount']),
        customerLiabilityAmount: _asDouble(json['customerLiabilityAmount']),
        thirdPartyLiabilityAmount: _asDouble(json['thirdPartyLiabilityAmount']),
        totalPaidToClaimant: _asDouble(json['totalPaidToClaimant']),
        recoveredAmount: _asDouble(json['recoveredAmount']),
        outstandingRecoveryAmount: _asDouble(json['outstandingRecoveryAmount']),
      );
}

class DriverLiabilityItem {
  const DriverLiabilityItem({
    required this.id,
    required this.claimId,
    required this.attributableDamage,
    required this.faultLevel,
    required this.confirmedAmount,
    required this.paidAmount,
    required this.outstandingAmount,
    required this.status,
    required this.recoveries,
    this.accidentId,
    this.claimStatus,
  });

  final int id;
  final int claimId;
  final int? accidentId;
  final String? claimStatus;
  final double attributableDamage;
  final String faultLevel;
  final double confirmedAmount;
  final double paidAmount;
  final double outstandingAmount;
  final String status;
  final List<RecoveryHistoryItem> recoveries;

  factory DriverLiabilityItem.fromJson(Map<String, dynamic> json) =>
      DriverLiabilityItem(
        id: _asInt(json['id']),
        claimId: _asInt(json['protectionClaimId']),
        accidentId: _asNullableInt(json['accidentReportId']),
        claimStatus: json['claimStatus']?.toString(),
        attributableDamage: _asDouble(json['driverAttributableEligibleDamage']),
        faultLevel: json['faultLevel']?.toString() ?? '',
        confirmedAmount: _asDouble(json['confirmedAmount']),
        paidAmount: _asDouble(json['paidAmount']),
        outstandingAmount: _asDouble(json['outstandingAmount']),
        status: json['status']?.toString() ?? '',
        recoveries: (json['recoveries'] as List? ?? const [])
            .whereType<Map>()
            .map(
              (item) =>
                  RecoveryHistoryItem.fromJson(item.cast<String, dynamic>()),
            )
            .toList(growable: false),
      );
}

class RecoveryHistoryItem {
  const RecoveryHistoryItem({
    required this.id,
    required this.amount,
    required this.maskedReference,
    required this.recordedAt,
  });

  final int id;
  final double amount;
  final String maskedReference;
  final DateTime recordedAt;

  factory RecoveryHistoryItem.fromJson(Map<String, dynamic> json) =>
      RecoveryHistoryItem(
        id: _asInt(json['id']),
        amount: _asDouble(json['amount']),
        maskedReference: json['maskedPaymentReference']?.toString() ?? '',
        recordedAt: _asDate(json['recordedAtUtc']),
      );
}

int _asInt(dynamic value) =>
    value is num ? value.toInt() : int.tryParse('$value') ?? 0;
int? _asNullableInt(dynamic value) => value == null ? null : _asInt(value);
double _asDouble(dynamic value) =>
    value is num ? value.toDouble() : double.tryParse('$value') ?? 0;
DateTime _asDate(dynamic value) =>
    DateTime.tryParse('$value')?.toLocal() ??
    DateTime.fromMillisecondsSinceEpoch(0);
