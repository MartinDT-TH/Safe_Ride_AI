import '../../data/models/vehicle_model.dart';

abstract class VehicleRepository {
  Future<List<VehicleModel>> getVehicles(String accessToken);

  Future<VehicleModel> createVehicle(String accessToken, VehicleModel vehicle);

  Future<VehicleModel> updateVehicle(String accessToken, VehicleModel vehicle);

  Future<void> deleteVehicle(String accessToken, int id);

  Future<List<VehicleInsurancePolicyModel>> getInsurancePolicies(
    String accessToken,
    int vehicleId,
  );

  Future<VehicleInsurancePolicyModel> saveInsurancePolicy(
    String accessToken,
    VehicleInsurancePolicyModel policy,
  );

  Future<void> deleteInsurancePolicy(
    String accessToken,
    int vehicleId,
    int policyId,
  );
  Future<List<InsuranceDocumentModel>> getInsuranceDocuments(String accessToken, int vehicleId, int policyId);
  Future<InsuranceDocumentModel> uploadInsuranceDocument(String accessToken, int vehicleId, int policyId, String path, String documentType);
}
