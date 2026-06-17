enum VehicleType { motorbike, car }

class VehicleModel {
  final int id;
  final String name;
  final String licenseType;
  final String plateNumber;
  final String color;
  final VehicleType type;

  VehicleModel({
    required this.id,
    required this.name,
    this.licenseType = '', // Cho phép mặc định là rỗng để tránh lỗi require ở nhiều nơi
    required this.plateNumber,
    required this.color,
    required this.type,
  });

  factory VehicleModel.fromJson(Map<String, dynamic> json) {
    return VehicleModel(
      id: (json['id'] as num).toInt(),
      name: json['brandModel']?.toString() ?? '',
      licenseType: json['licenseType']?.toString() ?? '',
      plateNumber: json['plateNumber']?.toString() ?? '',
      color: json['color']?.toString() ?? '',
      type: json['vehicleType']?.toString().toLowerCase() == 'car'
          ? VehicleType.car
          : VehicleType.motorbike,
    );
  }

  Map<String, dynamic> toRequestJson() {
    return {
      'brandModel': name.trim(),
      'licenseType': licenseType.trim(),
      'plateNumber': plateNumber.trim(),
      'color': color.trim().isEmpty ? null : color.trim(),
      'vehicleType': type == VehicleType.car ? 'Car' : 'Motorbike',
    };
  }

  VehicleModel copyWith({
    int? id,
    String? name,
    String? licenseType,
    String? plateNumber,
    String? color,
    VehicleType? type,
  }) {
    return VehicleModel(
      id: id ?? this.id,
      name: name ?? this.name,
      licenseType: licenseType ?? this.licenseType,
      plateNumber: plateNumber ?? this.plateNumber,
      color: color ?? this.color,
      type: type ?? this.type,
    );
  }
}

