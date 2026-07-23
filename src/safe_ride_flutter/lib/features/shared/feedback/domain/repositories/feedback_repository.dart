import '../../data/models/driver_rating_summary.dart';

abstract class FeedbackRepository {
  Future<DriverRatingSummary> getDriverRatings(
    String accessToken, {
    required String driverId,
  });
}
