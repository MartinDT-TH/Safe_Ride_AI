import '../../domain/repositories/vehicle_repository.dart';
import '../datasources/vehicle_remote_datasource.dart';
import '../models/vehicle_model.dart';

class VehicleRepositoryImpl implements VehicleRepository {
  final VehicleRemoteDatasource _datasource;

  VehicleRepositoryImpl(this._datasource);

  @override
  Future<List<VehicleModel>> getVehicles(String accessToken) {
    return _datasource.getVehicles(accessToken);
  }

  @override
  Future<VehicleModel> createVehicle(String accessToken, VehicleModel vehicle) {
    return _datasource.createVehicle(accessToken, vehicle);
  }

  @override
  Future<VehicleModel> updateVehicle(String accessToken, VehicleModel vehicle) {
    return _datasource.updateVehicle(accessToken, vehicle);
  }

  @override
  Future<void> deleteVehicle(String accessToken, int id) {
    return _datasource.deleteVehicle(accessToken, id);
  }
}
