import '../../data/models/driver_trip_request_model.dart';

abstract class DriverTripRequestRepository {
  Future<List<DriverTripRequestModel>> getOpenTripRequests(String accessToken);
}
