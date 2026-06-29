import '../../data/models/history_trip.dart';

abstract class HistoryRepository {
  Future<List<HistoryTrip>> getBookingHistory(
    String accessToken, {
    String? role,
  });
}
