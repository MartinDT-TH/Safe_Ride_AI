import '../../../../core/constants/app_strings.dart';
import '../models/booking_catalog.dart';

class BookingCatalogDatasource {
  Future<BookingCatalog> getCatalog() async {
    // Replace this catalog when vehicle/service listing endpoints are available.
    return const BookingCatalog(
      services: [
        BookingServiceOption(
          id: 1,
          name: BookingStrings.tripService,
          mode: BookingServiceMode.perTrip,
          description: BookingStrings.tripServiceDescription,
        ),
        BookingServiceOption(
          id: 2,
          name: BookingStrings.hourlyService,
          mode: BookingServiceMode.hourly,
          description: BookingStrings.hourlyServiceDescription,
        ),
      ],
      vehicles: [
        BookingVehicleOption(
          id: 1,
          name: BookingStrings.demoVehicleName,
          plateNumber: BookingStrings.demoPlateNumber,
          color: BookingStrings.demoVehicleColor,
          isMotorbike: false,
        ),
      ],
    );
  }
}
