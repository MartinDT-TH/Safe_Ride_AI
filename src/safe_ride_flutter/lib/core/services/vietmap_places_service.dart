// import 'package:dio/dio.dart';
// import '../config/api_keys_config.dart';
// import '../maps/models/map_api_models.dart';

// class VietMapPlacesService {
//   final Dio _dio = Dio();

//   Future<List<PlaceAutocompleteResult>> getAutocomplete({
//     required String query,
//     double? lat,
//     double? lng,
//   }) async {
//     final Map<String, dynamic> queryParameters = {
//       'text': query,
//       'apikey': ApiKeysConfig.vietMap,
//     };
    
//     if (lat != null && lng != null) {
//       queryParameters['focus'] = '$lat,$lng';
//     }

//     final response = await _dio.get(
//       'https://maps.vietmap.vn/api/search/v3',
//       queryParameters: queryParameters,
//     );

//     final data = response.data;
//     if (data is List) {
//       return data.map((e) => PlaceAutocompleteResult(
//         providerPlaceId: e['ref_id'] as String? ?? e['id'] as String? ?? '',
//         primaryText: e['name'] as String? ?? '',
//         secondaryText: e['address'] as String? ?? '',
//       )).toList();
//     } else if (data is Map<String, dynamic> && data['data'] != null) { // Fallback for some wrapper formats
//       return (data['data'] as List).map((e) => PlaceAutocompleteResult(
//         providerPlaceId: e['ref_id'] as String? ?? e['id'] as String? ?? '',
//         primaryText: e['name'] as String? ?? '',
//         secondaryText: e['address'] as String? ?? '',
//       )).toList();
//     }
//     return [];
//   }

//   Future<List<PlaceDetailResult>> getGeocode(String query) async {
//     // VietMap v3 search endpoint handles geocoding as well.
//     final response = await _dio.get(
//       'https://maps.vietmap.vn/api/search/v3',
//       queryParameters: {
//         'text': query,
//         'apikey': ApiKeysConfig.vietMap,
//       },
//     );

//     final data = response.data;
//     List results = [];
//     if (data is List) {
//       results = data;
//     } else if (data is Map<String, dynamic> && data['data'] != null) {
//       results = data['data'] as List;
//     }

//     return results.map((e) => PlaceDetailResult(
//       latitude: (e['lat'] as num?)?.toDouble() ?? 0.0,
//       longitude: (e['lng'] as num?)?.toDouble() ?? 0.0,
//       address: e['address'] as String? ?? e['name'] as String? ?? '',
//     )).toList();
//   }

//   Future<PlaceDetailResult> getPlaceDetail(String providerPlaceId) async {
//     final response = await _dio.get(
//       'https://maps.vietmap.vn/api/place/v3',
//       queryParameters: {
//         'refid': providerPlaceId,
//         'apikey': ApiKeysConfig.vietMap,
//       },
//     );

//     final data = response.data;
//     if (data != null) {
//       return PlaceDetailResult(
//         latitude: (data['lat'] as num?)?.toDouble() ?? 0.0,
//         longitude: (data['lng'] as num?)?.toDouble() ?? 0.0,
//         address: data['address'] as String? ?? data['name'] as String? ?? '',
//       );
//     }
//     throw Exception('Failed to get VietMap Place details');
//   }

//   Future<PlaceDetailResult> getReverseGeocode(double lat, double lng) async {
//     final response = await _dio.get(
//       'https://maps.vietmap.vn/api/reverse/v3',
//       queryParameters: {
//         'lat': lat,
//         'lng': lng,
//         'apikey': ApiKeysConfig.vietMap,
//       },
//     );

//     final data = response.data;
//     if (data is List && data.isNotEmpty) {
//       final e = data.first;
//       return PlaceDetailResult(
//         latitude: (e['lat'] as num?)?.toDouble() ?? lat,
//         longitude: (e['lng'] as num?)?.toDouble() ?? lng,
//         address: e['address'] as String? ?? e['name'] as String? ?? '',
//       );
//     }
//     throw Exception('Failed to get VietMap Reverse Geocode');
//   }
// }
