import 'package:flutter/material.dart';

enum DriverStatus { offline, online }

class DriverDashboardProvider extends ChangeNotifier {
  DriverStatus _status = DriverStatus.offline;
  DriverStatus get status => _status;

  double _todayIncome = 500000;
  double get todayIncome => _todayIncome;

  int _todayTrips = 3;
  int get todayTrips => _todayTrips;

  bool _hasNewRequest = false;
  bool get hasNewRequest => _hasNewRequest;

  TripRequest? _currentRequest;
  TripRequest? get currentRequest => _currentRequest;

  void toggleStatus() {
    _status = _status == DriverStatus.offline ? DriverStatus.online : DriverStatus.offline;
    notifyListeners();
  }

  void simulateNewRequest() {
    _currentRequest = TripRequest(
      expectedIncome: 120000,
      pickupDistance: '1.5 km',
      pickupTime: '5 phút',
      pickupAddress: '80 Trần Duy Hưng, Cầu Giấy',
      destinationAddress: 'Sân bay Nội Bài, Sóc Sơn',
    );
    _hasNewRequest = true;
    notifyListeners();
  }

  void acceptRequest() {
    _hasNewRequest = false;
    _currentRequest = null;
    // Handle trip acceptance logic
    notifyListeners();
  }

  void declineRequest() {
    _hasNewRequest = false;
    _currentRequest = null;
    notifyListeners();
  }
}

class TripRequest {
  final double expectedIncome;
  final String pickupDistance;
  final String pickupTime;
  final String pickupAddress;
  final String destinationAddress;

  TripRequest({
    required this.expectedIncome,
    required this.pickupDistance,
    required this.pickupTime,
    required this.pickupAddress,
    required this.destinationAddress,
  });
}
