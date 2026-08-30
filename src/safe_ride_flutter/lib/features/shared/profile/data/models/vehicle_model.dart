enum VehicleType { motorbike, car }

class InsuranceDocumentModel {
  const InsuranceDocumentModel({required this.id, required this.documentType, required this.originalFileName, required this.contentType, required this.fileSizeBytes, required this.uploadedAtUtc});
  final int id;
  final String documentType;
  final String originalFileName;
  final String contentType;
  final int fileSizeBytes;
  final DateTime uploadedAtUtc;
  factory InsuranceDocumentModel.fromJson(Map<String, dynamic> json) => InsuranceDocumentModel(
    id: (json['id'] as num).toInt(), documentType: json['documentType'].toString(), originalFileName: json['originalFileName'].toString(),
    contentType: json['contentType'].toString(), fileSizeBytes: (json['fileSizeBytes'] as num).toInt(), uploadedAtUtc: DateTime.parse(json['uploadedAtUtc'].toString()));
}

class VehicleInsurancePolicyModel {
  const VehicleInsurancePolicyModel({
    required this.id,
    required this.vehicleId,
    required this.insuranceType,
    required this.provider,
    required this.policyNumber,
    required this.effectiveFromUtc,
    required this.expiresAtUtc,
    required this.coverageAmount,
    required this.deductible,
    required this.verificationStatus,
    this.documentUrl,
  });

  final int id;
  final int vehicleId;
  final String insuranceType;
  final String provider;
  final String policyNumber;
  final DateTime effectiveFromUtc;
  final DateTime expiresAtUtc;
  final double coverageAmount;
  final double deductible;
  final String? documentUrl;
  final String verificationStatus;

  factory VehicleInsurancePolicyModel.fromJson(Map<String, dynamic> json) =>
      VehicleInsurancePolicyModel(
        id: (json['id'] as num).toInt(),
        vehicleId: (json['vehicleId'] as num).toInt(),
        insuranceType: json['insuranceType']?.toString() ?? 'OTHER',
        provider: json['provider']?.toString() ?? '',
        policyNumber: json['policyNumber']?.toString() ?? '',
        effectiveFromUtc: DateTime.parse(json['effectiveFromUtc'].toString()),
        expiresAtUtc: DateTime.parse(json['expiresAtUtc'].toString()),
        coverageAmount: (json['coverageAmount'] as num?)?.toDouble() ?? 0,
        deductible: (json['deductible'] as num?)?.toDouble() ?? 0,
        documentUrl: json['documentUrl']?.toString(),
        verificationStatus: json['verificationStatus']?.toString() ?? 'PENDING',
      );

  Map<String, dynamic> toRequestJson() => {
    'insuranceType': insuranceType,
    'provider': provider.trim(),
    'policyNumber': policyNumber.trim(),
    'effectiveFromUtc': effectiveFromUtc.toUtc().toIso8601String(),
    'expiresAtUtc': expiresAtUtc.toUtc().toIso8601String(),
    'coverageAmount': coverageAmount,
    'deductible': deductible,
    'documentUrl': documentUrl?.trim().isEmpty == true
        ? null
        : documentUrl?.trim(),
  };
}

class VehicleModel {
  final int id;
  final String name;
  final String plateNumber;
  final String color;
  final VehicleType type;
  final int? engineCapacityCc;
  final String requiredLicenseClass;

  VehicleModel({
    required this.id,
    required this.name,
    required this.plateNumber,
    required this.color,
    required this.type,
    this.engineCapacityCc,
    this.requiredLicenseClass = '',
  });

  factory VehicleModel.fromJson(Map<String, dynamic> json) {
    return VehicleModel(
      id: (json['id'] as num).toInt(),
      name: json['brandModel']?.toString() ?? '',
      plateNumber: json['plateNumber']?.toString() ?? '',
      color: json['color']?.toString() ?? '',
      type: json['vehicleType']?.toString().toLowerCase() == 'car'
          ? VehicleType.car
          : VehicleType.motorbike,
      engineCapacityCc: (json['engineCapacityCc'] as num?)?.toInt(),
      requiredLicenseClass: json['requiredLicenseClass']?.toString() ?? '',
    );
  }

  Map<String, dynamic> toRequestJson() {
    return {
      'brandModel': name.trim(),
      'plateNumber': plateNumber.trim(),
      'color': color.trim().isEmpty ? null : color.trim(),
      'vehicleType': type == VehicleType.car ? 'Car' : 'Motorbike',
      'engineCapacityCc': type == VehicleType.motorbike
          ? engineCapacityCc
          : null,
    };
  }

  VehicleModel copyWith({
    int? id,
    String? name,
    String? licenseType,
    String? plateNumber,
    String? color,
    VehicleType? type,
    int? engineCapacityCc,
    String? requiredLicenseClass,
  }) {
    return VehicleModel(
      id: id ?? this.id,
      name: name ?? this.name,
      plateNumber: plateNumber ?? this.plateNumber,
      color: color ?? this.color,
      type: type ?? this.type,
      engineCapacityCc: engineCapacityCc ?? this.engineCapacityCc,
      requiredLicenseClass: requiredLicenseClass ?? this.requiredLicenseClass,
    );
  }
}
