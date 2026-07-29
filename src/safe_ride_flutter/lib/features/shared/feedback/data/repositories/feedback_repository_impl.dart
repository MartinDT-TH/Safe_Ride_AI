import '../../domain/repositories/feedback_repository.dart';
import '../datasources/feedback_remote_datasource.dart';
import '../models/driver_rating_summary.dart';

class FeedbackRepositoryImpl implements FeedbackRepository {
  final FeedbackRemoteDatasource _remoteDatasource;

  FeedbackRepositoryImpl(this._remoteDatasource);

  @override
  Future<DriverRatingSummary> getDriverRatings(
    String accessToken, {
    required String driverId,
  }) {
    return _remoteDatasource.getDriverRatings(accessToken, driverId: driverId);
  }
}
