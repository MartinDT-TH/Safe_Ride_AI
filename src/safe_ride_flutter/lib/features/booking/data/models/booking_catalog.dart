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
}

class BookingCatalog {
  const BookingCatalog({required this.services, required this.vehicles});

  final List<BookingServiceOption> services;
  final List<BookingVehicleOption> vehicles;
}
