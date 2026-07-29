import 'package:flutter/material.dart';

import '../../data/models/history_trip.dart';
import '../../data/models/trip_details_view_data.dart';
import '../../domain/repositories/trip_details_repository.dart';

class TripDetailsProvider extends ChangeNotifier {
  final TripDetailsRepository _repository;
  final HistoryTrip _historyTrip;

  bool _isLoading = false;
  String? _errorMessage;
  bool _hasLoadedRemoteDetails = false;
  TripDetailsViewData _tripDetailsViewData;

  TripDetailsProvider._internal(
    this._repository,
    this._historyTrip,
    this._tripDetailsViewData,
  );

  factory TripDetailsProvider.create(
    TripDetailsRepository repository,
    HistoryTrip historyTrip,
  ) {
    return TripDetailsProvider._internal(
      repository,
      historyTrip,
      TripDetailsViewData(historyTrip: historyTrip),
    );
  }

  bool get isLoading => _isLoading;
  String? get errorMessage => _errorMessage;
  bool get hasLoadedRemoteDetails => _hasLoadedRemoteDetails;
  TripDetailsViewData get tripDetails => _tripDetailsViewData;

  Future<void> loadDetails(String? accessToken) async {
    _isLoading = true;
    _errorMessage = null;
    notifyListeners();

    try {
      if (accessToken == null || accessToken.isEmpty) {
        throw const TripDetailsRepositoryException(
          'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.',
        );
      }

      final booking = await _repository.getTripDetails(
        accessToken,
        bookingId: _historyTrip.id,
      );
      _tripDetailsViewData = TripDetailsViewData(
        historyTrip: _historyTrip,
        booking: booking,
      );
      _hasLoadedRemoteDetails = true;
    } on TripDetailsRepositoryException catch (exception) {
      _errorMessage = exception.message;
      if (!_hasLoadedRemoteDetails) {
        _tripDetailsViewData = TripDetailsViewData(historyTrip: _historyTrip);
      }
    } catch (_) {
      _errorMessage = 'Không thể tải chi tiết chuyến đi. Vui lòng thử lại.';
      if (!_hasLoadedRemoteDetails) {
        _tripDetailsViewData = TripDetailsViewData(historyTrip: _historyTrip);
      }
    } finally {
      _isLoading = false;
      notifyListeners();
    }
  }
}
