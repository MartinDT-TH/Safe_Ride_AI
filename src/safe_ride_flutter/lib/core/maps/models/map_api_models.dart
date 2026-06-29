import '../../../features/customer/booking/data/models/booking_location.dart';

class RouteEstimateResult {
  final double distanceKm;
  final int durationMinutes;
  final String encodedPolyline;

  RouteEstimateResult({
    required this.distanceKm,
    required this.durationMinutes,
    required this.encodedPolyline,
  });

  factory RouteEstimateResult.fromJson(Map<String, dynamic> json) {
    return RouteEstimateResult(
      distanceKm: (json['distanceKm'] as num).toDouble(),
      durationMinutes: json['durationMinutes'] as int,
      encodedPolyline: json['encodedPolyline'] as String? ?? '',
    );
  }
}

class PlaceAutocompleteResult {
  final String providerPlaceId;
  final String primaryText;
  final String secondaryText;
  final double? latitude;
  final double? longitude;

  PlaceAutocompleteResult({
    required this.providerPlaceId,
    required this.primaryText,
    required this.secondaryText,
    this.latitude,
    this.longitude,
  });

  factory PlaceAutocompleteResult.fromJson(Map<String, dynamic> json) {
    return PlaceAutocompleteResult(
      providerPlaceId:
          json['providerPlaceId'] as String? ?? json['id'] as String? ?? '',
      primaryText:
          json['primaryText'] as String? ?? json['name'] as String? ?? '',
      secondaryText:
          json['secondaryText'] as String? ?? json['address'] as String? ?? '',
      latitude: (json['lat'] as num?)?.toDouble(),
      longitude: (json['lng'] as num?)?.toDouble(),
    );
  }
}

class PlaceDetailResult {
  final double latitude;
  final double longitude;
  final String address;

  PlaceDetailResult({
    required this.latitude,
    required this.longitude,
    required this.address,
  });

  factory PlaceDetailResult.fromJson(Map<String, dynamic> json) {
    return PlaceDetailResult(
      latitude: (json['lat'] as num?)?.toDouble() ?? 0.0,
      longitude: (json['lng'] as num?)?.toDouble() ?? 0.0,
      address: json['address'] as String? ?? '',
    );
  }

  BookingLocation toBookingLocation() {
    return BookingLocation(
      address: address,
      latitude: latitude,
      longitude: longitude,
    );
  }
}
