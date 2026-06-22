import 'package:flutter/material.dart';
import 'package:dio/dio.dart';

import '../../../../../core/constants/app_strings.dart';
import '../../../../../core/network/auth_header.dart';
import '../../../../../core/network/dio_client.dart';
import '../../../../../core/services/socket_service.dart';

enum DriverStatus { offline, online }

class DriverDashboardProvider extends ChangeNotifier {
  DriverDashboardProvider({SocketService? socketService, Dio? dio})
    : _socketService = socketService ?? SocketService(),
      _dio = dio ?? DioClient().dio;

  final SocketService _socketService;
  final Dio _dio;
  String? _accessToken;

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

  bool _isResponding = false;
  bool get isResponding => _isResponding;

  bool _isUpdatingTrip = false;
  bool get isUpdatingTrip => _isUpdatingTrip;

  ActiveDriverTrip? _activeTrip;
  ActiveDriverTrip? get activeTrip => _activeTrip;

  Future<void> initializeRealtime(String accessToken) async {
    if (accessToken.isEmpty) {
      return;
    }

    if (_accessToken == accessToken) {
      _socketService.onBookingUpdated((update) {
      if (update.status == 'DriverAssigned' || update.tripId != null) {
        if (update.tripId != null) {
          _activeTrip = ActiveDriverTrip(
            bookingId: update.bookingId,
            tripId: update.tripId!,
            tripStatus: update.tripStatus ?? 'ACCEPTED',
          );
          notifyListeners();
        }
      } else if (update.status == 'Cancelled' || update.status == 'Expired') {
        if (_activeTrip?.bookingId == update.bookingId) {
          _activeTrip = null;
          notifyListeners();
        }
      }
    }, key: 'driverDashboardBooking');
    try {
        await loadActiveTrip();
      } catch (error) {
        debugPrint('DRIVER_DASHBOARD: reload active trip failed: $error');
      }
      return;
    }

    _accessToken = accessToken;
    await _socketService.connect(accessToken);
    _socketService.onDriverOfferReceived((offer) {
      _currentRequest = TripRequest(
        offerId: offer.offerId,
        bookingId: offer.bookingId,
        expectedIncome: 0,
        pickupDistance: 'Đang tính',
        pickupTime: offer.expiresAt == null ? '30 giây' : 'Sắp hết hạn',
        pickupAddress: offer.message,
        destinationAddress: 'Mở chi tiết chuyến sau khi nhận',
      );
      _hasNewRequest = true;
      notifyListeners();
    });
    _socketService.onDriverOfferClosed((offerId) {
      if (_currentRequest?.offerId == offerId) {
        _hasNewRequest = false;
        _currentRequest = null;
        notifyListeners();
      }
    });
    _socketService.onTripStatusChanged((update) {
      if (update.tripStatus == 'COMPLETED' ||
          update.tripStatus == 'CANCELLED') {
        if (_activeTrip?.tripId == update.tripId) {
          _activeTrip = null;
          notifyListeners();
        }
        return;
      }

      _activeTrip = ActiveDriverTrip(
        bookingId: update.bookingId,
        tripId: update.tripId,
        tripStatus: update.tripStatus,
      );
      notifyListeners();
    }, key: 'driverDashboard');
    _socketService.onBookingUpdated((update) {
      if (update.status == 'DriverAssigned' || update.tripId != null) {
        if (update.tripId != null) {
          _activeTrip = ActiveDriverTrip(
            bookingId: update.bookingId,
            tripId: update.tripId!,
            tripStatus: update.tripStatus ?? 'ACCEPTED',
          );
          notifyListeners();
        }
      } else if (update.status == 'Cancelled' || update.status == 'Expired') {
        if (_activeTrip?.bookingId == update.bookingId) {
          _activeTrip = null;
          notifyListeners();
        }
      }
    }, key: 'driverDashboardBooking');
    try {
      await loadActiveTrip();
    } catch (error) {
      debugPrint('DRIVER_DASHBOARD: load active trip failed: $error');
    }
  }

  void toggleStatus() {
    _status = _status == DriverStatus.offline
        ? DriverStatus.online
        : DriverStatus.offline;
    notifyListeners();
  }

  void simulateNewRequest() {
    _currentRequest = TripRequest(
      offerId: 0,
      bookingId: 0,
      expectedIncome: 120000,
      pickupDistance: '1.5 km',
      pickupTime: '5 phút',
      pickupAddress: '80 Trần Duy Hưng, Cầu Giấy',
      destinationAddress: 'Sân bay Nội Bài, Sóc Sơn',
    );
    _hasNewRequest = true;
    notifyListeners();
  }

  Future<void> acceptRequest() async {
    final request = _currentRequest;
    final token = _accessToken;
    if (request == null || token == null || _isResponding) {
      return;
    }

    _isResponding = true;
    notifyListeners();
    _socketService.onBookingUpdated((update) {
      if (update.status == 'DriverAssigned' || update.tripId != null) {
        if (update.tripId != null) {
          _activeTrip = ActiveDriverTrip(
            bookingId: update.bookingId,
            tripId: update.tripId!,
            tripStatus: update.tripStatus ?? 'ACCEPTED',
          );
          notifyListeners();
        }
      } else if (update.status == 'Cancelled' || update.status == 'Expired') {
        if (_activeTrip?.bookingId == update.bookingId) {
          _activeTrip = null;
          notifyListeners();
        }
      }
    }, key: 'driverDashboardBooking');
    try {
      await _dio.post(
        ApiEndpoints.acceptDriverOffer(request.offerId),
        options: Options(
          headers: {ApiKeys.authorization: AuthHeader.bearer(token)},
        ),
      );
      // Removed immediate trip creation logic. 
      // Waiting for SignalR 'BookingDriverAssigned' event.
      _hasNewRequest = false;
      _currentRequest = null;
    } finally {
      _isResponding = false;
      notifyListeners();
    }
  }

  Future<void> declineRequest() async {
    final request = _currentRequest;
    final token = _accessToken;
    if (request == null || token == null || _isResponding) {
      return;
    }

    _isResponding = true;
    notifyListeners();
    _socketService.onBookingUpdated((update) {
      if (update.status == 'DriverAssigned' || update.tripId != null) {
        if (update.tripId != null) {
          _activeTrip = ActiveDriverTrip(
            bookingId: update.bookingId,
            tripId: update.tripId!,
            tripStatus: update.tripStatus ?? 'ACCEPTED',
          );
          notifyListeners();
        }
      } else if (update.status == 'Cancelled' || update.status == 'Expired') {
        if (_activeTrip?.bookingId == update.bookingId) {
          _activeTrip = null;
          notifyListeners();
        }
      }
    }, key: 'driverDashboardBooking');
    try {
      await _dio.post(
        ApiEndpoints.rejectDriverOffer(request.offerId),
        options: Options(
          headers: {ApiKeys.authorization: AuthHeader.bearer(token)},
        ),
      );
      _hasNewRequest = false;
      _currentRequest = null;
    } finally {
      _isResponding = false;
      notifyListeners();
    }
  }

  Future<bool> startArriving() {
    return updateTripStatus('DRIVER_ARRIVING');
  }

  Future<bool> markArrived() {
    return updateTripStatus('ARRIVED');
  }

  Future<bool> cancelActiveTrip() {
    return updateTripStatus('CANCELLED');
  }

  Future<bool> completeActiveTrip() {
    return updateTripStatus('COMPLETED');
  }

  Future<void> loadActiveTrip() async {
    final token = _accessToken;
    if (token == null || token.isEmpty) {
      return;
    }

    final response = await _dio.get(
      '/drivers/trips/active',
      options: Options(headers: {ApiKeys.authorization: AuthHeader.bearer(token)}),
    );
    if (response.statusCode == 204 || response.data == null) {
      _activeTrip = null;
      notifyListeners();
      return;
    }

    if (response.data is Map) {
      final data = Map<String, dynamic>.from(response.data as Map);
      final bookingId = (data[ApiKeys.bookingId] ?? data['BookingId']) as num?;
      final tripId = (data[ApiKeys.tripId] ?? data['TripId']) as num?;
      final tripStatus = _normalizeTripStatus(
        data[ApiKeys.tripStatus] ?? data['TripStatus'],
      );
      if (bookingId != null && tripId != null && tripStatus != null) {
        _activeTrip = ActiveDriverTrip(
          bookingId: bookingId.toInt(),
          tripId: tripId.toInt(),
          tripStatus: tripStatus,
        );
        notifyListeners();
      }
    }
  }

  Future<bool> updateTripStatus(String tripStatus) async {
    final trip = _activeTrip;
    final token = _accessToken;
    if (trip == null || token == null || _isUpdatingTrip) {
      return false;
    }

    _isUpdatingTrip = true;
    notifyListeners();
    _socketService.onBookingUpdated((update) {
      if (update.status == 'DriverAssigned' || update.tripId != null) {
        if (update.tripId != null) {
          _activeTrip = ActiveDriverTrip(
            bookingId: update.bookingId,
            tripId: update.tripId!,
            tripStatus: update.tripStatus ?? 'ACCEPTED',
          );
          notifyListeners();
        }
      } else if (update.status == 'Cancelled' || update.status == 'Expired') {
        if (_activeTrip?.bookingId == update.bookingId) {
          _activeTrip = null;
          notifyListeners();
        }
      }
    }, key: 'driverDashboardBooking');
    try {
      await _dio.patch(
        ApiEndpoints.tripStatus(trip.tripId),
        data: {ApiKeys.tripStatus: tripStatus},
        options: Options(
          headers: {ApiKeys.authorization: AuthHeader.bearer(token)},
        ),
      );

      if (tripStatus == 'COMPLETED' || tripStatus == 'CANCELLED') {
        _activeTrip = null;
      } else {
        _activeTrip = trip.copyWith(tripStatus: tripStatus);
      }
      return true;
    } finally {
      _isUpdatingTrip = false;
      notifyListeners();
    }
  }

  static String? _normalizeTripStatus(Object? value) {
    if (value == null) {
      return null;
    }

    if (value is num) {
      return switch (value.toInt()) {
        0 => 'ACCEPTED',
        1 => 'DRIVER_ARRIVING',
        2 => 'ARRIVED',
        3 => 'IN_PROGRESS',
        4 => 'COMPLETED',
        5 => 'CANCELLED',
        _ => value.toString(),
      };
    }

    final text = value.toString();
    return switch (text) {
      '0' => 'ACCEPTED',
      '1' => 'DRIVER_ARRIVING',
      '2' => 'ARRIVED',
      '3' => 'IN_PROGRESS',
      '4' => 'COMPLETED',
      '5' => 'CANCELLED',
      _ => text,
    };
  }
}

class ActiveDriverTrip {
  const ActiveDriverTrip({
    required this.bookingId,
    required this.tripId,
    required this.tripStatus,
  });

  final int bookingId;
  final int tripId;
  final String tripStatus;

  ActiveDriverTrip copyWith({String? tripStatus}) {
    return ActiveDriverTrip(
      bookingId: bookingId,
      tripId: tripId,
      tripStatus: tripStatus ?? this.tripStatus,
    );
  }
}

class TripRequest {
  final int offerId;
  final int bookingId;
  final double expectedIncome;
  final String pickupDistance;
  final String pickupTime;
  final String pickupAddress;
  final String destinationAddress;

  TripRequest({
    required this.offerId,
    required this.bookingId,
    required this.expectedIncome,
    required this.pickupDistance,
    required this.pickupTime,
    required this.pickupAddress,
    required this.destinationAddress,
  });
}
