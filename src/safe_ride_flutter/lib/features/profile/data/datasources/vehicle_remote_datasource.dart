import 'package:dio/dio.dart';

import '../../../../core/network/dio_client.dart';
import '../models/vehicle_model.dart';

class VehicleRemoteDatasource {
  final Dio _dio;

  VehicleRemoteDatasource({Dio? dio}) : _dio = dio ?? DioClient().dio;

  Future<List<VehicleModel>> getVehicles(String accessToken) async {
    final response = await _dio.get(
      '/vehicles',
      options: _authorized(accessToken),
    );
    final data = response.data as List<dynamic>;
    return data
        .map(
          (item) =>
              VehicleModel.fromJson(Map<String, dynamic>.from(item as Map)),
        )
        .toList();
  }

  Future<VehicleModel> createVehicle(
    String accessToken,
    VehicleModel vehicle,
  ) async {
    final response = await _dio.post(
      '/vehicles',
      data: vehicle.toRequestJson(),
      options: _authorized(accessToken),
    );
    return VehicleModel.fromJson(
      Map<String, dynamic>.from(response.data as Map),
    );
  }

  Future<VehicleModel> updateVehicle(
    String accessToken,
    VehicleModel vehicle,
  ) async {
    final response = await _dio.put(
      '/vehicles/${vehicle.id}',
      data: vehicle.toRequestJson(),
      options: _authorized(accessToken),
    );
    return VehicleModel.fromJson(
      Map<String, dynamic>.from(response.data as Map),
    );
  }

  Future<void> deleteVehicle(String accessToken, int id) {
    return _dio.delete('/vehicles/$id', options: _authorized(accessToken));
  }

  Options _authorized(String accessToken) {
    return Options(headers: {'Authorization': 'Bearer $accessToken'});
  }
}
