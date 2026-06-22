import 'package:geolocator/geolocator.dart';

import '../constants/app_strings.dart';
import '../../features/customer/booking/data/models/booking_location.dart';
import '../maps/models/map_api_models.dart';
import 'map_api_service.dart';

class LocationService {
  final MapApiService _mapApiService;

  LocationService({MapApiService? mapApiService})
    : _mapApiService = mapApiService ?? MapApiService();

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

    final lastKnown = await Geolocator.getLastKnownPosition();
    if (lastKnown != null) {
      final age = DateTime.now().difference(lastKnown.timestamp);
      if (age.inMinutes <= 5) {
        return await _toBookingLocation(lastKnown);
      }
    }

    final position =
        await Geolocator.getCurrentPosition(
          locationSettings: const LocationSettings(
            accuracy: LocationAccuracy.high,
            timeLimit: Duration(seconds: 12),
          ),
        ).catchError((_) async {
          final lastKnown = await Geolocator.getLastKnownPosition();
          if (lastKnown != null) return lastKnown;
          throw const LocationServiceException(
            LocationStrings.locationNotFound,
          );
        });
    return await _toBookingLocation(position);
  }

  Future<BookingLocation> _toBookingLocation(Position position) async {
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
      final locations = await _mapApiService.getGeocode(normalizedAddress);
      if (locations.isEmpty) {
        throw const LocationServiceException(LocationStrings.locationNotFound);
      }

      final location = locations.first;
      return BookingLocation(
        address: location.address.isNotEmpty
            ? location.address
            : normalizedAddress,
        latitude: location.latitude,
        longitude: location.longitude,
      );
    } catch (_) {
      throw const LocationServiceException(LocationStrings.locationNotFound);
    }
  }

  Future<BookingLocation> resolvePlaceId(String placeId) async {
    try {
      final place = await _mapApiService.getPlaceDetail(placeId);
      return BookingLocation(
        address: place.address,
        latitude: place.latitude,
        longitude: place.longitude,
      );
    } catch (_) {
      throw const LocationServiceException(LocationStrings.locationNotFound);
    }
  }

  Future<List<PlaceAutocompleteResult>> autocompleteAddress(
    String query, {
    double? lat,
    double? lng,
  }) async {
    final normalizedQuery = query.trim();
    if (normalizedQuery.isEmpty) return [];
    try {
      return await _mapApiService.getAutocomplete(
        query: normalizedQuery,
        lat: lat,
        lng: lng,
      );
    } catch (_) {
      return [];
    }
  }

  Future<BookingLocation> resolveCoordinates(
    double latitude,
    double longitude,
  ) async {
    return BookingLocation(
      address: await _reverseGeocode(latitude, longitude),
      latitude: latitude,
      longitude: longitude,
    );
  }

  Future<String> _reverseGeocode(double latitude, double longitude) async {
    try {
      final place = await _mapApiService.getReverseGeocode(latitude, longitude);
      if (place.address.isEmpty) {
        return '${latitude.toStringAsFixed(4)}, ${longitude.toStringAsFixed(4)}';
      }
      return place.address;
    } catch (_) {
      return '${latitude.toStringAsFixed(4)}, ${longitude.toStringAsFixed(4)}';
    }
  }
}

class LocationServiceException implements Exception {
  const LocationServiceException(this.message);

  final String message;

  @override
  String toString() => message;
}
