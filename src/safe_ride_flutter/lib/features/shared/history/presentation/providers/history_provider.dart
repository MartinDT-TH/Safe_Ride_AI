import 'package:flutter/material.dart';
import '../../data/models/history_trip.dart';
import '../../data/datasources/history_remote_datasource.dart';

enum HistoryFilter { all, completed, cancelled }

class HistoryProvider extends ChangeNotifier {
  final HistoryRemoteDatasource _datasource = HistoryRemoteDatasource();
  
  List<HistoryTrip> _allTrips = [];
  bool _isLoading = false;
  HistoryFilter _currentFilter = HistoryFilter.all;

  List<HistoryTrip> get trips {
    switch (_currentFilter) {
      case HistoryFilter.completed:
        return _allTrips.where((t) => t.status == HistoryTripStatus.completed).toList();
      case HistoryFilter.cancelled:
        return _allTrips.where((t) => t.status == HistoryTripStatus.cancelled).toList();
      case HistoryFilter.all:
      
        return _allTrips;
    }
  }

  bool get isLoading => _isLoading;
  HistoryFilter get currentFilter => _currentFilter;

  void setFilter(HistoryFilter filter) {
    _currentFilter = filter;
    notifyListeners();
  }

  Future<void> loadHistory(String? accessToken) async {
    _isLoading = true;
    notifyListeners();

    try {
      // Logic: If API code is commented/returns empty, we use mock data
      if (accessToken != null) {
        _allTrips = await _datasource.getBookingHistory(accessToken);
      }

      if (_allTrips.isEmpty) {
        _loadMockData();
      }
    } catch (e) {
      _loadMockData();
    } finally {
      _isLoading = false;
      notifyListeners();
    }
  }

  void _loadMockData() {
    _allTrips = [
      HistoryTrip(
        id: 1,
        pickup: 'Landmark 81, Quận Bình Thạnh',
        destination: 'Bitexco Financial Tower, Quận 1',
        time: DateTime(2026, 5, 24, 15, 30),
        fare: 95000,
        distanceKm: 4.2,
        status: HistoryTripStatus.completed,
        vehicleName: 'SafeRide Plus',
        driverName: 'Nguyễn Văn A',
        driverRating: 4.9,
        driverAvatar: 'https://i.pravatar.cc/150?u=a',
      ),
      HistoryTrip(
        id: 2,
        pickup: 'Chung cư Vinhomes Central Park',
        destination: 'Sân bay Tân Sơn Nhất (Ga đi)',
        time: DateTime(2026, 5, 23, 8, 45),
        fare: 62000,
        distanceKm: 6.8,
        status: HistoryTripStatus.completed,
        vehicleName: 'SafeRide Eco',
        driverName: 'Trần Minh B',
        driverRating: 4.8,
        driverAvatar: 'https://i.pravatar.cc/150?u=b',
      ),
      HistoryTrip(
        id: 3,
        pickup: 'Phố đi bộ Nguyễn Huệ',
        destination: 'Thảo Điền, Quận 2',
        time: DateTime(2026, 5, 22, 19, 15),
        fare: 0,
        distanceKm: 5.5,
        status: HistoryTripStatus.cancelled,
        vehicleName: 'SafeRide Bike',
        isMotorbike: true,
      ),
    ];
  }
}
