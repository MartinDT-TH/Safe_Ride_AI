// import 'package:dio/dio.dart';
// import '../config/api_keys_config.dart';
// import '../maps/models/map_api_models.dart';

// class GooglePlacesService {
//   final Dio _dio = Dio();

//   Future<List<PlaceAutocompleteResult>> getAutocomplete({
//     required String query,
//     double? lat,
//     double? lng,
//   }) async {
//     final Map<String, dynamic> queryParameters = {
//       'input': query,
//       'key': ApiKeysConfig.googleMaps,
//     };
    
//     if (lat != null && lng != null) {
//       queryParameters['location'] = '$lat,$lng';
//       queryParameters['radius'] = 50000; // 50km
//     }
//     final response = await _dio.get(
//       'https://maps.googleapis.com/maps/api/place/autocomplete/json',
//       queryParameters: queryParameters,
//     );

//     final data = response.data;
//     if (data['status'] == 'OK' && data['predictions'] != null) {
//       return (data['predictions'] as List).map((e) => PlaceAutocompleteResult(
//         providerPlaceId: e['place_id'] as String,
//         primaryText: e['structured_formatting']?['main_text'] as String? ?? '',
//         secondaryText: e['structured_formatting']?['secondary_text'] as String? ?? '',
//       )).toList();
//     }
//     return [];
//   }

//   Future<List<PlaceDetailResult>> getGeocode(String query) async {
//     final response = await _dio.get(
//       'https://maps.googleapis.com/maps/api/geocode/json',
//       queryParameters: {
//         'address': query,
//         'key': ApiKeysConfig.googleMaps,
//       },
//     );

//     final data = response.data;
//     if (data['status'] == 'OK' && data['results'] != null) {
//       return (data['results'] as List).map((e) {
//         final location = e['geometry']['location'];
//         return PlaceDetailResult(
//           latitude: (location['lat'] as num).toDouble(),
//           longitude: (location['lng'] as num).toDouble(),
//           address: e['formatted_address'] as String? ?? '',
//         );
//       }).toList();
//     }
//     return [];
//   }

//   Future<PlaceDetailResult> getPlaceDetail(String providerPlaceId) async {
//     final response = await _dio.get(
//       'https://maps.googleapis.com/maps/api/place/details/json',
//       queryParameters: {
//         'place_id': providerPlaceId,
//         'key': ApiKeysConfig.googleMaps,
//       },
//     );

//     final data = response.data;
//     if (data['status'] == 'OK' && data['result'] != null) {
//       final result = data['result'];
//       final location = result['geometry']['location'];
//       return PlaceDetailResult(
//         latitude: (location['lat'] as num).toDouble(),
//         longitude: (location['lng'] as num).toDouble(),
//         address: result['formatted_address'] as String? ?? '',
//       );
//     }
//     throw Exception('Failed to get Google Place details: ${data['status']}');
//   }

//   Future<PlaceDetailResult> getReverseGeocode(double lat, double lng) async {
//     final response = await _dio.get(
//       'https://maps.googleapis.com/maps/api/geocode/json',
//       queryParameters: {
//         'latlng': '$lat,$lng',
//         'key': ApiKeysConfig.googleMaps,
//       },
//     );

//     final data = response.data;
//     if (data['status'] == 'OK' && data['results'] != null && (data['results'] as List).isNotEmpty) {
//       final result = data['results'][0];
//       final location = result['geometry']['location'];
//       return PlaceDetailResult(
//         latitude: (location['lat'] as num).toDouble(),
//         longitude: (location['lng'] as num).toDouble(),
//         address: result['formatted_address'] as String? ?? '',
//       );
//     }
//     throw Exception('Failed to get Google Reverse Geocode: ${data['status']}');
//   }
// }

