import 'package:flutter/material.dart';

import '../../../../../core/constants/app_strings.dart';
import '../../data/datasources/history_remote_datasource.dart';
import '../../data/models/history_trip.dart';
import '../../domain/repositories/history_repository.dart';

enum HistoryFilter { all, completed, cancelled, booked }

class HistoryProvider extends ChangeNotifier {
  HistoryProvider(this._repository);

  final HistoryRepository _repository;

  List<HistoryTrip> _allTrips = [];
  bool _isLoading = false;
  String? _errorMessage;
  HistoryFilter _currentFilter = HistoryFilter.all;

  List<HistoryTrip> get trips {
    switch (_currentFilter) {
      case HistoryFilter.completed:
        return _allTrips
            .where((trip) => trip.status == HistoryTripStatus.completed)
            .toList();
      case HistoryFilter.cancelled:
        return _allTrips
            .where((trip) => trip.status == HistoryTripStatus.cancelled)
            .toList();
      case HistoryFilter.booked:
        return _allTrips
            .where((trip) => trip.status == HistoryTripStatus.booked)
            .toList();
      case HistoryFilter.all:
        return _allTrips;
    }
  }

  bool get isLoading => _isLoading;
  String? get errorMessage => _errorMessage;
  HistoryFilter get currentFilter => _currentFilter;

  void setFilter(HistoryFilter filter) {
    _currentFilter = filter;
    notifyListeners();
  }

  Future<void> loadHistory(
    String? accessToken, {
    String? role,
  }) async {
    _isLoading = true;
    _errorMessage = null;
    notifyListeners();

    try {
      if (accessToken == null || accessToken.isEmpty) {
        _allTrips = [];
        _errorMessage = BookingStrings.sessionExpired;
        return;
      }

      _allTrips = await _repository.getBookingHistory(accessToken, role: role);
    } on HistoryApiException catch (exception) {
      _errorMessage = exception.message;
    } catch (_) {
      _errorMessage = AppStrings.genericError;
    } finally {
      _isLoading = false;
      notifyListeners();
    }
  }
}
