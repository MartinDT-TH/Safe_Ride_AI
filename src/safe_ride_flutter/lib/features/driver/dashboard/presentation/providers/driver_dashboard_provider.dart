import 'dart:math' as math;
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

  bool _isLoadingActiveTrip = false;
  bool get isLoadingActiveTrip => _isLoadingActiveTrip;

  String? _errorMessage;
  String? get errorMessage => _errorMessage;

  // Hack Demo State
  double? _demoLat;
  double? get demoLat => _demoLat;
  double? _demoLng;
  double? get demoLng => _demoLng;
  double _demoHeading = 0;
  double get demoHeading => _demoHeading;

  double _calculateDemoHeading(double startLat, double startLng, double endLat, double endLng) {
    final sLat = startLat * math.pi / 180;
    final sLng = startLng * math.pi / 180;
    final eLat = endLat * math.pi / 180;
    final eLng = endLng * math.pi / 180;

    final dLng = eLng - sLng;
    final y = math.sin(dLng) * math.cos(eLat);
    final x = math.cos(sLat) * math.sin(eLat) -
        math.sin(sLat) * math.cos(eLat) * math.cos(dLng);
    final brng = math.atan2(y, x);
    return (brng * 180 / math.pi + 360) % 360;
  }

  Future<void> initializeRealtime(String accessToken) async {
    if (accessToken.isEmpty) {
      return;
    }

    if (_accessToken == accessToken) {
      _socketService.onBookingUpdated((update) {
      if (update.status == 'DriverAssigned' || update.tripId != null) {
        if (update.tripId != null) {
          final oldTrip = _activeTrip;
          _activeTrip = ActiveDriverTrip(
            bookingId: update.bookingId,
            tripId: update.tripId!,
            tripStatus: update.tripStatus ?? 'ACCEPTED',
            pickupLat: oldTrip?.pickupLat,
            pickupLng: oldTrip?.pickupLng,
            destLat: oldTrip?.destLat,
            destLng: oldTrip?.destLng,
            encodedPolyline: oldTrip?.encodedPolyline,
            arrivalPolyline: oldTrip?.arrivalPolyline,
          );
          notifyListeners();
          if (oldTrip?.encodedPolyline == null) {
            _fetchActiveTripDetails(update.bookingId, update.tripId!);
          }
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

      final oldTrip = _activeTrip;
      _activeTrip = ActiveDriverTrip(
        bookingId: update.bookingId,
        tripId: update.tripId,
        tripStatus: update.tripStatus,
        pickupLat: oldTrip?.pickupLat,
        pickupLng: oldTrip?.pickupLng,
        destLat: oldTrip?.destLat,
        destLng: oldTrip?.destLng,
        encodedPolyline: oldTrip?.encodedPolyline,
        arrivalPolyline: oldTrip?.arrivalPolyline,
      );
      notifyListeners();
      if (oldTrip?.encodedPolyline == null) {
        _fetchActiveTripDetails(update.bookingId, update.tripId);
      }
    }, key: 'driverDashboard');
    _socketService.onDriverLocationUpdated((update) {
      if (_demoLat != null && _demoLng != null) {
        _demoHeading = _calculateDemoHeading(_demoLat!, _demoLng!, update.latitude, update.longitude);
      }
      _demoLat = update.latitude;
      _demoLng = update.longitude;
      notifyListeners();
    });
    _socketService.onBookingUpdated((update) {
      if (update.status == 'DriverAssigned' || update.tripId != null) {
        if (update.tripId != null) {
          final oldTrip = _activeTrip;
          _activeTrip = ActiveDriverTrip(
            bookingId: update.bookingId,
            tripId: update.tripId!,
            tripStatus: update.tripStatus ?? 'ACCEPTED',
            pickupLat: oldTrip?.pickupLat,
            pickupLng: oldTrip?.pickupLng,
            destLat: oldTrip?.destLat,
            destLng: oldTrip?.destLng,
            encodedPolyline: oldTrip?.encodedPolyline,
            arrivalPolyline: oldTrip?.arrivalPolyline,
          );
          notifyListeners();
          if (oldTrip?.encodedPolyline == null) {
            _fetchActiveTripDetails(update.bookingId, update.tripId!);
          }
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
    if (_status == DriverStatus.offline) {
      if (_socketService.isConnected) {
        _socketService.setDriverOffline();
      }
    }
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
          final oldTrip = _activeTrip;
          _activeTrip = ActiveDriverTrip(
            bookingId: update.bookingId,
            tripId: update.tripId!,
            tripStatus: update.tripStatus ?? 'ACCEPTED',
            pickupLat: oldTrip?.pickupLat,
            pickupLng: oldTrip?.pickupLng,
            destLat: oldTrip?.destLat,
            destLng: oldTrip?.destLng,
            encodedPolyline: oldTrip?.encodedPolyline,
            arrivalPolyline: oldTrip?.arrivalPolyline,
          );
          notifyListeners();
          if (oldTrip?.encodedPolyline == null) {
            _fetchActiveTripDetails(update.bookingId, update.tripId!);
          }
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
          final oldTrip = _activeTrip;
          _activeTrip = ActiveDriverTrip(
            bookingId: update.bookingId,
            tripId: update.tripId!,
            tripStatus: update.tripStatus ?? 'ACCEPTED',
            pickupLat: oldTrip?.pickupLat,
            pickupLng: oldTrip?.pickupLng,
            destLat: oldTrip?.destLat,
            destLng: oldTrip?.destLng,
            encodedPolyline: oldTrip?.encodedPolyline,
            arrivalPolyline: oldTrip?.arrivalPolyline,
          );
          notifyListeners();
          if (oldTrip?.encodedPolyline == null) {
            _fetchActiveTripDetails(update.bookingId, update.tripId!);
          }
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

  Future<void> publishOnlineLocation(double lat, double lng) async {
    if (_socketService.isConnected) {
      await _socketService.setDriverOnline(lat, lng);
    }
    final token = _accessToken;
    if (token == null) return;
    try {
      await _dio.patch(
        ApiEndpoints.driverLocation,
        data: {ApiKeys.latitude: lat, ApiKeys.longitude: lng},
        options: Options(headers: {ApiKeys.authorization: AuthHeader.bearer(token)}),
      );
    } catch (e) {
      debugPrint('Failed to publish online location: $e');
    }
  }

  Future<void> updateLocation(double lat, double lng) async {
    if (_socketService.isConnected) {
      await _socketService.updateDriverLocation(lat, lng);
    }
    final token = _accessToken;
    if (token == null) return;
    try {
      await _dio.patch(
        ApiEndpoints.driverLocation,
        data: {ApiKeys.latitude: lat, ApiKeys.longitude: lng},
        options: Options(headers: {ApiKeys.authorization: AuthHeader.bearer(token)}),
      );
    } catch (e) {
      debugPrint('Failed to update driver location: $e');
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

    _isLoadingActiveTrip = true;
    _errorMessage = null;
    notifyListeners();

    try {
      final response = await _dio.get(
        ApiEndpoints.driverActiveTrip,
        options: Options(headers: {ApiKeys.authorization: AuthHeader.bearer(token)}),
      );
      if (response.statusCode == 204 || response.data == null) {
        _activeTrip = null;
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
          // Fetch extra details immediately before setting loading to false
          await _fetchActiveTripDetailsSync(bookingId.toInt(), tripId.toInt());
        }
      }
    } catch (e) {
      debugPrint('Error loading active trip: $e');
      _errorMessage = 'Không thể tải dữ liệu chuyến đi hiện tại. Vui lòng thử lại.';
    } finally {
      _isLoadingActiveTrip = false;
      notifyListeners();
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
          final oldTrip = _activeTrip;
          _activeTrip = ActiveDriverTrip(
            bookingId: update.bookingId,
            tripId: update.tripId!,
            tripStatus: update.tripStatus ?? 'ACCEPTED',
            pickupLat: oldTrip?.pickupLat,
            pickupLng: oldTrip?.pickupLng,
            destLat: oldTrip?.destLat,
            destLng: oldTrip?.destLng,
            encodedPolyline: oldTrip?.encodedPolyline,
            arrivalPolyline: oldTrip?.arrivalPolyline,
          );
          notifyListeners();
          if (oldTrip?.encodedPolyline == null) {
            _fetchActiveTripDetails(update.bookingId, update.tripId!);
          }
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

  Future<void> _fetchActiveTripDetailsSync(int bookingId, int tripId) async {
    final token = _accessToken;
    if (token == null || _activeTrip == null || _activeTrip!.tripId != tripId) return;

    try {
      final response = await _dio.get(
        ApiEndpoints.driverActiveTrip,
        options: Options(headers: {ApiKeys.authorization: AuthHeader.bearer(token)}),
      );
      if (response.data is Map && _activeTrip?.tripId == tripId) {
        final bData = Map<String, dynamic>.from(response.data as Map);
        double? pickupLat = (bData['pickupLat'] as num?)?.toDouble();
        double? pickupLng = (bData['pickupLng'] as num?)?.toDouble();
        double? destLat = (bData['destLat'] as num?)?.toDouble();
        double? destLng = (bData['destLng'] as num?)?.toDouble();
        final encodedPoly = bData['encodedPolyline'] as String?;
        String? arrivalPoly = bData['arrivalPolyline'] as String?;

        // Hack Demo: Fetch arrival polyline if missing and we have demo coordinates or active trip
        if (arrivalPoly == null && pickupLat != null && pickupLng != null) {
          try {
            // Use demo coordinates if available, otherwise use a fallback (Da Nang) to fetch a route
            final oLat = _demoLat ?? 16.0544;
            final oLng = _demoLng ?? 108.2022;
            
            final routeResp = await _dio.post(
              'maps/routes/estimate',
              data: {
                'originLat': oLat,
                'originLng': oLng,
                'destinationLat': pickupLat,
                'destinationLng': pickupLng,
                'travelMode': 1
              },
            );
            if (routeResp.data != null && routeResp.data is Map) {
              arrivalPoly = routeResp.data['encodedPolyline'] as String?;
            }
          } catch (e) {
            debugPrint('Failed to fetch demo arrival polyline: $e');
          }
        }

        _activeTrip = _activeTrip!.copyWith(
          pickupLat: pickupLat,
          pickupLng: pickupLng,
          destLat: destLat,
          destLng: destLng,
          encodedPolyline: encodedPoly,
          arrivalPolyline: arrivalPoly,
        );
      }
    } catch (e) {
      debugPrint('Failed to load active trip booking details: $e');
    }
  }

  void _fetchActiveTripDetails(int bookingId, int tripId) {
    _fetchActiveTripDetailsSync(bookingId, tripId).then((_) {
      notifyListeners();
    });
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
    this.pickupLat,
    this.pickupLng,
    this.destLat,
    this.destLng,
    this.encodedPolyline,
    this.arrivalPolyline,
  });

  final int bookingId;
  final int tripId;
  final String tripStatus;
  final double? pickupLat;
  final double? pickupLng;
  final double? destLat;
  final double? destLng;
  final String? encodedPolyline;
  final String? arrivalPolyline;

  ActiveDriverTrip copyWith({
    String? tripStatus,
    double? pickupLat,
    double? pickupLng,
    double? destLat,
    double? destLng,
    String? encodedPolyline,
    String? arrivalPolyline,
  }) {
    return ActiveDriverTrip(
      bookingId: bookingId,
      tripId: tripId,
      tripStatus: tripStatus ?? this.tripStatus,
      pickupLat: pickupLat ?? this.pickupLat,
      pickupLng: pickupLng ?? this.pickupLng,
      destLat: destLat ?? this.destLat,
      destLng: destLng ?? this.destLng,
      encodedPolyline: encodedPolyline ?? this.encodedPolyline,
      arrivalPolyline: arrivalPolyline ?? this.arrivalPolyline,
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
