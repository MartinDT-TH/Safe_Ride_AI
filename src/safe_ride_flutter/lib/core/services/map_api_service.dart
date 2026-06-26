import '../network/dio_client.dart';
import '../maps/models/map_api_models.dart';

class MapApiService {
  final _dio = DioClient().dio;

  Future<List<PlaceAutocompleteResult>> getAutocomplete({
    required String query,
    double? lat,
    double? lng,
  }) async {
    final Map<String, dynamic> queryParameters = {'query': query};
    if (lat != null && lng != null) {
      queryParameters['lat'] = lat;
      queryParameters['lng'] = lng;
    }
    final response = await _dio.get(
      '/maps/autocomplete',
      queryParameters: queryParameters,
    );
    final data = response.data;
    if (data is List) {
      return data.map((e) => PlaceAutocompleteResult.fromJson(e)).toList();
    }
    return [];
  }

  Future<List<PlaceDetailResult>> getGeocode(String query) async {
    final response = await _dio.get(
      '/maps/geocode',
      queryParameters: {'query': query},
    );
    final data = response.data;
    if (data is List) {
      return data.map((e) => PlaceDetailResult.fromJson(e)).toList();
    } else if (data is Map<String, dynamic>) {
       return [PlaceDetailResult.fromJson(data)];
    }
    return [];
  }

  Future<PlaceDetailResult> getPlaceDetail(String providerPlaceId) async {
    final response = await _dio.get(
      '/maps/place-detail',
      queryParameters: {'providerPlaceId': providerPlaceId},
    );
    return PlaceDetailResult.fromJson(response.data);
  }

  Future<PlaceDetailResult> getReverseGeocode(double lat, double lng) async {
    final response = await _dio.get(
      '/maps/reverse',
      queryParameters: {
        'lat': lat,
        'lng': lng,
      },
    );
    return PlaceDetailResult.fromJson(response.data);
  }

  // estimateRoute still calls the backend because backend does routing
  Future<RouteEstimateResult> estimateRoute(
    double pickupLat,
    double pickupLng,
    double destLat,
    double destLng,
  ) async {
    final response = await _dio.post(
      '/maps/routes/estimate',
      data: {
        'originLat': pickupLat,
        'originLng': pickupLng,
        'destinationLat': destLat,
        'destinationLng': destLng,
      },
    );

    return RouteEstimateResult.fromJson(response.data);
  }
}
