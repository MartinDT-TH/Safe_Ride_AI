enum VehicleType { motorbike, car }

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

