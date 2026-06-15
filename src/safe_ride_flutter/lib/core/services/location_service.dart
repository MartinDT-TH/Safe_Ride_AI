import 'package:geocoding/geocoding.dart';
import 'package:geolocator/geolocator.dart';

import '../constants/app_strings.dart';
import '../../features/booking/data/models/booking_location.dart';

class LocationService {
  Future<BookingLocation> getCurrentLocation() async {
    if (!await Geolocator.isLocationServiceEnabled()) {
      throw const LocationServiceException(LocationStrings.serviceDisabled);
    }

    var permission = await Geolocator.checkPermission();
    if (permission == LocationPermission.denied) {
      permission = await Geolocator.requestPermission();
    }

    if (permission == LocationPermission.denied ||
        permission == LocationPermission.deniedForever) {
      throw const LocationServiceException(LocationStrings.permissionRequired);
    }

    final position = await Geolocator.getCurrentPosition(
      locationSettings: const LocationSettings(accuracy: LocationAccuracy.high),
    );
    final address = await _reverseGeocode(
      position.latitude,
      position.longitude,
    );

    return BookingLocation(
      address: address,
      latitude: position.latitude,
      longitude: position.longitude,
    );
  }

  Future<BookingLocation> resolveAddress(String address) async {
    final normalizedAddress = address.trim();
    if (normalizedAddress.isEmpty) {
      throw const LocationServiceException(LocationStrings.destinationRequired);
    }

    try {
      final locations = await locationFromAddress(normalizedAddress);
      if (locations.isEmpty) {
        throw const LocationServiceException(LocationStrings.locationNotFound);
      }

      final location = locations.first;
      return BookingLocation(
        address: normalizedAddress,
        latitude: location.latitude,
        longitude: location.longitude,
      );
    } on NoResultFoundException {
      throw const LocationServiceException(LocationStrings.locationNotFound);
    }
  }

  Future<String> _reverseGeocode(double latitude, double longitude) async {
    try {
      final placemarks = await placemarkFromCoordinates(latitude, longitude);
      if (placemarks.isEmpty) return LocationStrings.currentLocation;

      final place = placemarks.first;
      return [
        place.street,
        place.subAdministrativeArea,
        place.administrativeArea,
      ].whereType<String>().where((part) => part.trim().isNotEmpty).join(', ');
    } catch (_) {
      return LocationStrings.currentLocation;
    }
  }
}

class LocationServiceException implements Exception {
  const LocationServiceException(this.message);

  final String message;

  @override
  String toString() => message;
}
