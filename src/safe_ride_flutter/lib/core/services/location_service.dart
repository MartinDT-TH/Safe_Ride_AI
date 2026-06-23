import 'package:geocoding/geocoding.dart' as native_geo;
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
      final results = await _mapApiService.getAutocomplete(
        query: normalizedQuery,
        lat: lat,
        lng: lng,
      );
      if (results.isNotEmpty) return results;
      // Fallback: use native geocoding to search by address string
      return await _nativeGeocode(normalizedQuery);
    } catch (_) {
      return await _nativeGeocode(normalizedQuery);
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
    // 1st attempt: backend API
    try {
      final place = await _mapApiService.getReverseGeocode(latitude, longitude);
      if (place.address.isNotEmpty) return place.address;
    } catch (_) {}

    // 2nd attempt: native device geocoder (geocoding package)
    try {
      final placemarks = await native_geo.placemarkFromCoordinates(
        latitude,
        longitude,
      );
      if (placemarks.isNotEmpty) {
        final p = placemarks.first;
        final parts = [
          p.street,
          p.subLocality,
          p.locality,
          p.administrativeArea,
        ].where((s) => s != null && s.isNotEmpty).toList();
        if (parts.isNotEmpty) return parts.join(', ');
      }
    } catch (_) {}

    // Final fallback: raw coordinates
    return '${latitude.toStringAsFixed(5)}, ${longitude.toStringAsFixed(5)}';
  }

  /// Fallback: convert search query into results using native geocoder.
  Future<List<PlaceAutocompleteResult>> _nativeGeocode(String query) async {
    try {
      final locations = await native_geo.locationFromAddress(query);
      if (locations.isEmpty) return [];
      return await Future.wait(
        locations.take(5).map((loc) async {
          String address = query;
          try {
            final placemarks = await native_geo.placemarkFromCoordinates(
              loc.latitude,
              loc.longitude,
            );
            if (placemarks.isNotEmpty) {
              final p = placemarks.first;
              final parts = [
                p.street,
                p.subLocality,
                p.locality,
                p.administrativeArea,
              ].where((s) => s != null && s.isNotEmpty).toList();
              if (parts.isNotEmpty) address = parts.join(', ');
            }
          } catch (_) {}
          return PlaceAutocompleteResult(
            providerPlaceId:
                'native:${loc.latitude.toStringAsFixed(6)},${loc.longitude.toStringAsFixed(6)}',
            primaryText: address,
            secondaryText: '',
            latitude: loc.latitude,
            longitude: loc.longitude,
          );
        }),
      );
    } catch (_) {
      return [];
    }
  }
}

class LocationServiceException implements Exception {
  const LocationServiceException(this.message);

  final String message;

  @override
  String toString() => message;
}
