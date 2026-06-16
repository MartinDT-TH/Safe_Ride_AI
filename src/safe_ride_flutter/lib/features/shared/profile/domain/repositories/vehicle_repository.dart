import '../../data/models/vehicle_model.dart';

abstract class VehicleRepository {
  Future<List<VehicleModel>> getVehicles(String accessToken);

  Future<VehicleModel> createVehicle(String accessToken, VehicleModel vehicle);

  Future<VehicleModel> updateVehicle(String accessToken, VehicleModel vehicle);

  Future<void> deleteVehicle(String accessToken, int id);
}

