import 'dart:async';
import 'dart:io';
import 'package:flutter/material.dart';
import 'package:dio/dio.dart';
import 'package:http_parser/http_parser.dart';

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

  // debug: Mock value until driver income summary API is available.
  double _todayIncome = 500000;
  double get todayIncome => _todayIncome;

  // debug: Mock value until driver trip summary API is available.
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

  bool _isDemoMode = false;
  bool get isDemoMode => _isDemoMode;

  ActiveDriverTrip? _activeTrip;
  ActiveDriverTrip? get activeTrip => _activeTrip;
  int? _completedTripAwaitingPaymentId;
  final Map<int, Future<bool>> _activeTripDetailsFetches = {};
  final Set<int> _activeTripDetailsLoaded = {};

  int? takeCompletedTripAwaitingPayment() {
    final tripId = _completedTripAwaitingPaymentId;
    _completedTripAwaitingPaymentId = null;
    return tripId;
  }

  void toggleDemoMode() {
    _isDemoMode = !_isDemoMode;
    if (_isDemoMode) {
      _socketService.onDriverLocationUpdated((update) {
        if (_activeTrip != null && update.tripId == _activeTrip!.tripId) {
          _demoLat = update.latitude;
          _demoLng = update.longitude;
          notifyListeners();
        }
      }, key: 'driverDashboardDemo');
    } else {
      _demoLat = null;
      _demoLng = null;
      _socketService.removeDriverLocationUpdatedHandler('driverDashboardDemo');
    }
    notifyListeners();
  }

  double? _demoLat;
  double? _demoLng;
  double? get demoLat => _demoLat;
  double? get demoLng => _demoLng;

  bool _isLoadingActiveTrip = false;
  bool get isLoadingActiveTrip => _isLoadingActiveTrip;

  String? _errorMessage;
  String? get errorMessage => _errorMessage;

  Future<void> initializeRealtime(String accessToken) async {
    if (accessToken.isEmpty) {
      return;
    }

    _accessToken = accessToken;
    await _socketService.connect(accessToken);
    _registerRealtimeHandlers();
    try {
      await loadActiveTrip();
    } catch (error) {
      debugPrint('DRIVER_DASHBOARD: load active trip failed: $error');
    }
  }

  void _registerRealtimeHandlers() {
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
    }, key: 'driverDashboardOfferReceived');
    _socketService.onDriverOfferClosed((offerId) {
      if (_currentRequest?.offerId == offerId) {
        _hasNewRequest = false;
        _currentRequest = null;
        notifyListeners();
      }
    }, key: 'driverDashboardOfferClosed');
    _socketService.onTripStatusChanged((update) {
      if (update.tripStatus == 'COMPLETED' ||
          update.tripStatus == 'CANCELLED') {
        if (_activeTrip?.tripId == update.tripId) {
          if (update.tripStatus == 'COMPLETED') {
            _completedTripAwaitingPaymentId = update.tripId;
          }
          _clearActiveTrip();
          notifyListeners();
        }
        return;
      }

      final oldTrip = _activeTrip;
      final sameTrip = oldTrip?.tripId == update.tripId;
      _activeTrip = ActiveDriverTrip(
        bookingId: update.bookingId,
        tripId: update.tripId,
        tripStatus: update.tripStatus,
        pickupLat: sameTrip ? oldTrip?.pickupLat : null,
        pickupLng: sameTrip ? oldTrip?.pickupLng : null,
        destLat: sameTrip ? oldTrip?.destLat : null,
        destLng: sameTrip ? oldTrip?.destLng : null,
        encodedPolyline: sameTrip ? oldTrip?.encodedPolyline : null,
        arrivalPolyline: sameTrip ? oldTrip?.arrivalPolyline : null,
      );
      notifyListeners();
      _socketService.joinTrip(update.tripId);
      if (!_hasActiveTripDetails(update.tripId)) {
        _fetchActiveTripDetails(update.bookingId, update.tripId);
      }
    }, key: 'driverDashboard');

    _socketService.onBookingUpdated(
      _handleBookingUpdate,
      key: 'driverDashboardBooking',
    );
  }

  Future<void> goOnline(double lat, double lng) async {
    final token = _accessToken;
    if (token == null) return;
    try {
      await _dio.post(
        ApiEndpoints.driverOnline,
        data: {ApiKeys.latitude: lat, ApiKeys.longitude: lng},
        options: Options(
          headers: {ApiKeys.authorization: AuthHeader.bearer(token)},
        ),
      );
      _status = DriverStatus.online;
      _errorMessage = null;

      await initializeRealtime(token);

      notifyListeners();
    } catch (e) {
      debugPrint('Failed to go online: $e');
      _errorMessage = 'Không thể online. Vui lòng thử lại.';
      notifyListeners();
      throw e;
    }
  }

  Future<void> goOffline() async {
    final token = _accessToken;
    if (token == null) return;
    try {
      await _dio.post(
        ApiEndpoints.driverOffline,
        options: Options(
          headers: {ApiKeys.authorization: AuthHeader.bearer(token)},
        ),
      );
    } catch (e) {
      debugPrint('Failed to go offline: $e');
    } finally {
      if (_socketService.isConnected) {
        await _socketService.setDriverOffline();
      }
      _socketService.removeTripStatusChangedHandler('driverDashboard');
      _socketService.removeDriverLocationUpdatedHandler('driverDashboardDemo');
      _socketService.removeBookingUpdatedHandler('driverDashboardBooking');
      _socketService.removeDriverOfferReceivedHandler(
        'driverDashboardOfferReceived',
      );
      _socketService.removeDriverOfferClosedHandler(
        'driverDashboardOfferClosed',
      );
      await _socketService.disconnect();
      _hasNewRequest = false;
      _currentRequest = null;
      _clearActiveTrip();
      _status = DriverStatus.offline;
      notifyListeners();
    }
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
    _socketService.onBookingUpdated(
      _handleBookingUpdate,
      key: 'driverDashboardBooking',
    );
    try {
      await _dio.post(
        ApiEndpoints.acceptDriverOffer(request.offerId),
        options: Options(
          headers: {ApiKeys.authorization: AuthHeader.bearer(token)},
        ),
      );
      // Removed immediate trip creation logic.
      // Waiting for SignalR 'BookingDriverAssigned' event.
      _errorMessage = null;
    } catch (e) {
      debugPrint('Failed to accept request: $e');
      _errorMessage = 'Không thể nhận chuyến. Vui lòng thử lại.';
    } finally {
      _hasNewRequest = false;
      _currentRequest = null;
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
    try {
      await _dio.post(
        ApiEndpoints.rejectDriverOffer(request.offerId),
        options: Options(
          headers: {ApiKeys.authorization: AuthHeader.bearer(token)},
        ),
      );
      _errorMessage = null;
    } catch (e) {
      debugPrint('Failed to decline request: $e');
      _errorMessage = 'Không thể từ chối chuyến. Vui lòng thử lại.';
    } finally {
      _hasNewRequest = false;
      _currentRequest = null;
      _isResponding = false;
      notifyListeners();
    }
  }

  Future<void> updateLocation(double lat, double lng) async {
    if (_socketService.isConnected) {
      await _socketService.updateDriverLocation(lat, lng);
      return;
    }
    final token = _accessToken;
    if (token == null) return;
    try {
      await _dio.patch(
        ApiEndpoints.driverLocation,
        data: {ApiKeys.latitude: lat, ApiKeys.longitude: lng},
        options: Options(
          headers: {ApiKeys.authorization: AuthHeader.bearer(token)},
        ),
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

  Future<bool> startTrip() {
    return updateTripStatus('IN_PROGRESS');
  }

  Future<bool> cancelActiveTrip() {
    return updateTripStatus('CANCELLED');
  }

  Future<bool> completeActiveTrip() async {
    final trip = _activeTrip;
    final token = _accessToken;
    if (trip == null || token == null || _isUpdatingTrip) {
      return false;
    }

    _isUpdatingTrip = true;
    notifyListeners();
    try {
      await _dio.post(
        ApiEndpoints.completeTrip(trip.tripId),
        options: Options(
          headers: {ApiKeys.authorization: AuthHeader.bearer(token)},
        ),
      );
      _completedTripAwaitingPaymentId = trip.tripId;
      _clearActiveTrip();
      return true;
    } catch (e) {
      debugPrint('Failed to complete trip: $e');
      throw e;
    } finally {
      _isUpdatingTrip = false;
      notifyListeners();
    }
  }

  /// Ends an IN_PROGRESS trip — moves it to WAITING_RETURN_CONFIRM.
  /// Returns true on success. Does NOT set COMPLETED.
  Future<bool> endTripAsync() async {
    final trip = _activeTrip;
    final token = _accessToken;
    if (trip == null || token == null || _isUpdatingTrip) {
      return false;
    }

    _isUpdatingTrip = true;
    notifyListeners();
    try {
      await _dio.post(
        ApiEndpoints.endTrip(trip.tripId),
        options: Options(
          headers: {ApiKeys.authorization: AuthHeader.bearer(token)},
        ),
      );
      _activeTrip = trip.copyWith(tripStatus: 'WAITING_RETURN_CONFIRM');
      return true;
    } catch (e) {
      debugPrint('Failed to end trip: $e');
      rethrow;
    } finally {
      _isUpdatingTrip = false;
      notifyListeners();
    }
  }

  /// Driver submits 1–3 evidence photos to confirm vehicle return on behalf of customer.
  /// Moves trip from WAITING_RETURN_CONFIRM → RETURN_CONFIRMED.
  Future<void> confirmReturnByDriver({
    required int tripId,
    required List<File> evidenceFiles,
    String? note,
  }) async {
    final token = _accessToken;
    if (token == null) throw Exception('Not authenticated');
    if (evidenceFiles.isEmpty || evidenceFiles.length > 3) {
      throw Exception('Cần từ 1 đến 3 ảnh bằng chứng.');
    }

    final formFields = <String, dynamic>{};
    if (note != null && note.trim().isNotEmpty) {
      formFields['note'] = note.trim();
    }

    // Attach all evidence photos; the backend reads all files from the form.
    final multipartFiles = <MultipartFile>[];
    for (final file in evidenceFiles) {
      final fileName = file.path.split(RegExp(r'[/\\]')).last;
      final ext = fileName.split('.').last.toLowerCase();
      final mimeType = switch (ext) {
        'png' => AppValues.pngMimeType,
        'webp' => AppValues.webpMimeType,
        _ => AppValues.jpegMimeType,
      };
      multipartFiles.add(
        await MultipartFile.fromFile(
          file.path,
          filename: fileName,
          contentType: MediaType.parse(mimeType),
        ),
      );
    }
    formFields['evidence'] = multipartFiles;

    await _dio.post(
      ApiEndpoints.driverReturnConfirmation(tripId),
      data: FormData.fromMap(formFields),
      options: Options(
        headers: {ApiKeys.authorization: AuthHeader.bearer(token)},
        contentType: AppValues.multipartFormData,
      ),
    );

    // Reflect locally — SignalR TripStatusChanged will arrive shortly and confirm.
    if (_activeTrip?.tripId == tripId) {
      _activeTrip = _activeTrip!.copyWith(tripStatus: 'RETURN_CONFIRMED');
      notifyListeners();
    }
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
        options: Options(
          headers: {ApiKeys.authorization: AuthHeader.bearer(token)},
        ),
      );
      if (response.statusCode == 204 || response.data == null) {
        _clearActiveTrip();
        return;
      }

      if (response.data is Map) {
        final data = Map<String, dynamic>.from(response.data as Map);
        final bookingId =
            (data[ApiKeys.bookingId] ?? data['BookingId']) as num?;
        final tripId = (data[ApiKeys.tripId] ?? data['TripId']) as num?;
        final tripStatus = _normalizeTripStatus(
          data[ApiKeys.tripStatus] ?? data['TripStatus'],
        );
        if (bookingId != null && tripId != null && tripStatus != null) {
          final oldTrip = _activeTrip;
          final tripIdValue = tripId.toInt();
          final sameTrip = oldTrip?.tripId == tripIdValue;
          _activeTrip = ActiveDriverTrip(
            bookingId: bookingId.toInt(),
            tripId: tripIdValue,
            tripStatus: tripStatus,
            pickupLat: sameTrip ? oldTrip?.pickupLat : null,
            pickupLng: sameTrip ? oldTrip?.pickupLng : null,
            destLat: sameTrip ? oldTrip?.destLat : null,
            destLng: sameTrip ? oldTrip?.destLng : null,
            encodedPolyline: sameTrip ? oldTrip?.encodedPolyline : null,
            arrivalPolyline: sameTrip ? oldTrip?.arrivalPolyline : null,
          );
          _socketService.joinTrip(tripIdValue);
          if (!_hasActiveTripDetails(tripIdValue)) {
            await _fetchActiveTripDetailsSync(bookingId.toInt(), tripIdValue);
          }
        }
      }
    } catch (e) {
      debugPrint('Error loading active trip: $e');
      _errorMessage =
          'Không thể tải dữ liệu chuyến đi hiện tại. Vui lòng thử lại.';
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
    _socketService.onBookingUpdated(
      _handleBookingUpdate,
      key: 'driverDashboardBooking',
    );
    try {
      await _dio.patch(
        ApiEndpoints.tripStatus(trip.tripId),
        data: {ApiKeys.tripStatus: tripStatus},
        options: Options(
          headers: {ApiKeys.authorization: AuthHeader.bearer(token)},
        ),
      );

      if (tripStatus == 'COMPLETED' || tripStatus == 'CANCELLED') {
        if (tripStatus == 'COMPLETED') {
          _completedTripAwaitingPaymentId = trip.tripId;
        }
        _clearActiveTrip();
      } else {
        _activeTrip = trip.copyWith(tripStatus: tripStatus);
      }
      return true;
    } finally {
      _isUpdatingTrip = false;
      notifyListeners();
    }
  }

  void _handleBookingUpdate(dynamic update) {
    if (update.status == 'DriverAssigned' || update.tripId != null) {
      if (update.tripId != null) {
        final oldTrip = _activeTrip;
        final sameTrip = oldTrip?.tripId == update.tripId!;
        _activeTrip = ActiveDriverTrip(
          bookingId: update.bookingId,
          tripId: update.tripId!,
          tripStatus: update.tripStatus ?? 'ACCEPTED',
          pickupLat: sameTrip ? oldTrip?.pickupLat : null,
          pickupLng: sameTrip ? oldTrip?.pickupLng : null,
          destLat: sameTrip ? oldTrip?.destLat : null,
          destLng: sameTrip ? oldTrip?.destLng : null,
          encodedPolyline: sameTrip ? oldTrip?.encodedPolyline : null,
          arrivalPolyline: sameTrip ? oldTrip?.arrivalPolyline : null,
        );
        notifyListeners();
        _socketService.joinTrip(update.tripId!);
        if (!_hasActiveTripDetails(update.tripId!)) {
          _fetchActiveTripDetails(update.bookingId, update.tripId!);
        }
      }
    } else if (update.status == 'Cancelled' || update.status == 'Expired') {
      if (_activeTrip?.bookingId == update.bookingId) {
        _clearActiveTrip();
        notifyListeners();
      }
    }
  }

  Future<bool> _fetchActiveTripDetailsSync(
    int bookingId,
    int tripId, [
    int retries = 3,
  ]) async {
    final token = _accessToken;
    final trip = _activeTrip;
    if (token == null ||
        trip == null ||
        trip.tripId != tripId ||
        trip.bookingId != bookingId ||
        _hasActiveTripDetails(tripId)) {
      return false;
    }

    final existingFetch = _activeTripDetailsFetches[tripId];
    if (existingFetch != null) {
      return existingFetch;
    }

    final fetch = _loadActiveTripDetails(token, bookingId, tripId, retries);
    _activeTripDetailsFetches[tripId] = fetch;
    try {
      return await fetch;
    } finally {
      if (identical(_activeTripDetailsFetches[tripId], fetch)) {
        _activeTripDetailsFetches.remove(tripId);
      }
    }
  }

  void _fetchActiveTripDetails(int bookingId, int tripId) {
    _fetchActiveTripDetailsSync(bookingId, tripId).then((changed) {
      if (changed) {
        notifyListeners();
      }
    });
  }

  Future<bool> _loadActiveTripDetails(
    String token,
    int bookingId,
    int tripId,
    int retries,
  ) async {
    try {
      for (
        var remainingRetries = retries;
        remainingRetries >= 0;
        remainingRetries--
      ) {
        final activeTrip = _activeTrip;
        if (activeTrip == null ||
            activeTrip.tripId != tripId ||
            activeTrip.bookingId != bookingId) {
          return false;
        }

        final response = await _dio.get(
          ApiEndpoints.driverActiveTrip,
          options: Options(
            headers: {ApiKeys.authorization: AuthHeader.bearer(token)},
          ),
        );

        if (response.statusCode == 204 && remainingRetries > 0) {
          await Future.delayed(const Duration(seconds: 1));
          continue;
        }

        if (response.data != null && response.data is Map) {
          final bData = Map<String, dynamic>.from(response.data as Map);
          final pickupLat = (bData['pickupLat'] as num?)?.toDouble();
          final pickupLng = (bData['pickupLng'] as num?)?.toDouble();
          final destLat = (bData['destLat'] as num?)?.toDouble();
          final destLng = (bData['destLng'] as num?)?.toDouble();
          final encodedPoly = bData['encodedPolyline'] as String?;
          final arrivalPoly = bData['arrivalPolyline'] as String?;

          if (_activeTrip?.tripId != tripId ||
              _activeTrip?.bookingId != bookingId) {
            return false;
          }

          _activeTrip = _activeTrip!.copyWith(
            pickupLat: pickupLat,
            pickupLng: pickupLng,
            destLat: destLat,
            destLng: destLng,
            encodedPolyline: encodedPoly,
            arrivalPolyline: arrivalPoly,
          );
          _activeTripDetailsLoaded.add(tripId);
          return true;
        }

        return false;
      }
    } catch (e) {
      debugPrint('Failed to load active trip booking details: $e');
    }

    return false;
  }

  bool _hasActiveTripDetails(int tripId) {
    if (_activeTripDetailsLoaded.contains(tripId)) {
      return true;
    }

    final trip = _activeTrip;
    if (trip == null || trip.tripId != tripId) {
      return false;
    }

    return trip.pickupLat != null ||
        trip.pickupLng != null ||
        trip.destLat != null ||
        trip.destLng != null ||
        trip.encodedPolyline != null ||
        trip.arrivalPolyline != null;
  }

  void _clearActiveTrip() {
    _activeTrip = null;
    _activeTripDetailsLoaded.clear();
    _activeTripDetailsFetches.clear();
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
        4 => 'WAITING_RETURN_CONFIRM',
        5 => 'RETURN_CONFIRMED',
        6 => 'COMPLETED',
        7 => 'CANCELLED',
        _ => value.toString(),
      };
    }

    final text = value.toString();
    return switch (text) {
      '0' => 'ACCEPTED',
      '1' => 'DRIVER_ARRIVING',
      '2' => 'ARRIVED',
      '3' => 'IN_PROGRESS',
      '4' => 'WAITING_RETURN_CONFIRM',
      '5' => 'RETURN_CONFIRMED',
      '6' => 'COMPLETED',
      '7' => 'CANCELLED',
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
