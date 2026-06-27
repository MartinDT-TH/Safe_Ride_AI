import '../../domain/repositories/history_repository.dart';
import '../datasources/history_remote_datasource.dart';
import '../models/history_trip.dart';

class HistoryRepositoryImpl implements HistoryRepository {
  HistoryRepositoryImpl(this._remoteDatasource);

  final HistoryRemoteDatasource _remoteDatasource;

  @override
  Future<List<HistoryTrip>> getBookingHistory(
    String accessToken, {
    String? role,
  }) {
    return _remoteDatasource.getBookingHistory(accessToken, role: role);
  }
}
