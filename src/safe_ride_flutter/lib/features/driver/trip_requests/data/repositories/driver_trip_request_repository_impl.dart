import '../../domain/repositories/driver_trip_request_repository.dart';
import '../datasources/driver_trip_request_remote_datasource.dart';
import '../models/driver_trip_request_model.dart';

class DriverTripRequestRepositoryImpl implements DriverTripRequestRepository {
  DriverTripRequestRepositoryImpl(this._remoteDatasource);

  final DriverTripRequestRemoteDatasource _remoteDatasource;

  @override
  Future<List<DriverTripRequestModel>> getOpenTripRequests(String accessToken) {
    return _remoteDatasource.getOpenTripRequests(accessToken);
  }
}
