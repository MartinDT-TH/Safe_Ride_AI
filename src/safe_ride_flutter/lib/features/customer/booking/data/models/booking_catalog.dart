enum BookingServiceMode { perTrip, hourly }

class BookingServiceOption {
  const BookingServiceOption({
    required this.id,
    required this.name,
    required this.mode,
    required this.description,
  });

  final int id;
  final String name;
  final BookingServiceMode mode;
  final String description;

  factory BookingServiceOption.fromJson(Map<String, dynamic> json) {
    final mode = switch (json['mode']?.toString()) {
      'hourly' => BookingServiceMode.hourly,
      _ => BookingServiceMode.perTrip,
    };

    return BookingServiceOption(
      id: json['id'] as int,
      name: json['name']?.toString() ?? '',
      mode: mode,
      description: json['description']?.toString() ?? '',
    );
  }
}

class BookingVehicleOption {
  const BookingVehicleOption({
    required this.id,
    required this.name,
    required this.plateNumber,
    required this.color,
    required this.isMotorbike,
  });

  final int id;
  final String name;
  final String plateNumber;
  final String color;
  final bool isMotorbike;

  factory BookingVehicleOption.fromJson(Map<String, dynamic> json) {
    return BookingVehicleOption(
      id: json['id'] as int,
      name: json['name']?.toString() ?? '',
      plateNumber: json['plateNumber']?.toString() ?? '',
      color: json['color']?.toString() ?? '',
      isMotorbike: json['isMotorbike'] as bool? ?? false,
    );
  }
}

class BookingCatalog {
  const BookingCatalog({required this.services, required this.vehicles});

  final List<BookingServiceOption> services;
  final List<BookingVehicleOption> vehicles;

  factory BookingCatalog.fromJson(Map<String, dynamic> json) {
    final services = json['services'] as List? ?? const [];
    final vehicles = json['vehicles'] as List? ?? const [];

    return BookingCatalog(
      services: services
          .map(
            (item) => BookingServiceOption.fromJson(
              Map<String, dynamic>.from(item as Map),
            ),
          )
          .toList(),
      vehicles: vehicles
          .map(
            (item) => BookingVehicleOption.fromJson(
              Map<String, dynamic>.from(item as Map),
            ),
          )
          .toList(),
    );
  }
}
