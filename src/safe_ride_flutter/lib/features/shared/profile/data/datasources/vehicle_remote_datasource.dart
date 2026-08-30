import 'package:dio/dio.dart';

import '../../../../../core/network/dio_client.dart';
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

  Future<List<VehicleInsurancePolicyModel>> getInsurancePolicies(
    String accessToken,
    int vehicleId,
  ) async {
    final response = await _dio.get(
      '/vehicles/$vehicleId/insurance-policies',
      options: _authorized(accessToken),
    );
    return (response.data as List<dynamic>)
        .map(
          (item) => VehicleInsurancePolicyModel.fromJson(
            Map<String, dynamic>.from(item as Map),
          ),
        )
        .toList(growable: false);
  }

  Future<VehicleInsurancePolicyModel> saveInsurancePolicy(
    String accessToken,
    VehicleInsurancePolicyModel policy,
  ) async {
    final path =
        '/vehicles/${policy.vehicleId}/insurance-policies'
        '${policy.id == 0 ? '' : '/${policy.id}'}';
    final response = policy.id == 0
        ? await _dio.post(
            path,
            data: policy.toRequestJson(),
            options: _authorized(accessToken),
          )
        : await _dio.put(
            path,
            data: policy.toRequestJson(),
            options: _authorized(accessToken),
          );
    return VehicleInsurancePolicyModel.fromJson(
      Map<String, dynamic>.from(response.data as Map),
    );
  }

  Future<void> deleteInsurancePolicy(
    String accessToken,
    int vehicleId,
    int policyId,
  ) => _dio.delete(
    '/vehicles/$vehicleId/insurance-policies/$policyId',
    options: _authorized(accessToken),
  );

  Future<List<InsuranceDocumentModel>> getInsuranceDocuments(String accessToken, int vehicleId, int policyId) async {
    final response = await _dio.get('/vehicles/$vehicleId/insurance-policies/$policyId/documents', options: _authorized(accessToken));
    return (response.data as List<dynamic>).map((item) => InsuranceDocumentModel.fromJson(Map<String, dynamic>.from(item as Map))).toList(growable: false);
  }

  Future<InsuranceDocumentModel> uploadInsuranceDocument(String accessToken, int vehicleId, int policyId, String path, String documentType) async {
    final fileName = path.split(RegExp(r'[/\\]')).last;
    final response = await _dio.post('/vehicles/$vehicleId/insurance-policies/$policyId/documents', data: FormData.fromMap({'documentType': documentType, 'file': await MultipartFile.fromFile(path, filename: fileName)}), options: _authorized(accessToken));
    return InsuranceDocumentModel.fromJson(Map<String, dynamic>.from(response.data as Map));
  }

  Options _authorized(String accessToken) {
    return Options(headers: {'Authorization': 'Bearer $accessToken'});
  }
}
