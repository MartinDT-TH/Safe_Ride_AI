import 'dart:async';
import 'dart:collection';
import 'dart:io';
import 'package:flutter/material.dart';
import 'package:dio/dio.dart';
import 'package:http_parser/http_parser.dart';
import 'package:image_picker/image_picker.dart';

import '../../../../../core/constants/app_strings.dart';
import '../../../../../core/network/auth_header.dart';
import '../../../../../core/network/dio_client.dart';
import '../../../../../core/localization/locale_provider.dart';
import '../../../../../core/localization/api_error_localizer.dart';
import '../../../../../core/services/socket_service.dart';
import '../../../../../core/session/session_manager.dart';
import '../../../trip_requests/data/datasources/driver_trip_request_remote_datasource.dart';
import '../../../trip_requests/data/models/driver_trip_request_model.dart';
import '../../../trip_requests/domain/repositories/driver_trip_request_repository.dart';
import '../../../wallet/domain/repositories/driver_wallet_repository.dart';
import '../../../../shared/history/data/models/history_trip.dart';
import '../../../../shared/history/domain/repositories/history_repository.dart';

enum DriverStatus { offline, online }

String imageContentTypeForEvidence(XFile file) {
  final mimeType = file.mimeType?.toLowerCase();
  if (mimeType == 'image/jpeg' ||
      mimeType == 'image/png' ||
      mimeType == 'image/webp') {
    return mimeType!;
  }

  final fileName = file.name.isNotEmpty
      ? file.name
      : file.path.split(RegExp(r'[/\\]')).last;
  final extension = fileName.contains('.')
      ? fileName.split('.').last.toLowerCase()
      : '';
  return switch (extension) {
    'png' => 'image/png',
    'webp' => 'image/webp',
    'jpg' || 'jpeg' => 'image/jpeg',
    _ => mimeType ?? 'application/octet-stream',
  };
}

class _PendingDriverLocationUpdate {
  _PendingDriverLocationUpdate({
    required this.latitude,
    required this.longitude,
    this.clientTimestampUtc,
    this.sequence,
    this.accuracyMeters,
    this.speedMetersPerSecond,
  });

  final double latitude;
  final double longitude;
  final DateTime? clientTimestampUtc;
  final int? sequence;
  final double? accuracyMeters;
  final double? speedMetersPerSecond;

  Map<String, dynamic> toJson() {
    final payload = <String, dynamic>{
      ApiKeys.latitude: latitude,
      ApiKeys.longitude: longitude,
    };
    if (clientTimestampUtc != null) {
      payload[ApiKeys.clientTimestampUtc] = clientTimestampUtc!
          .toUtc()
          .toIso8601String();
    }
    if (sequence != null) payload[ApiKeys.sequence] = sequence;
    if (accuracyMeters != null) {
      payload[ApiKeys.accuracyMeters] = accuracyMeters;
    }
    if (speedMetersPerSecond != null) {
      payload[ApiKeys.speedMetersPerSecond] = speedMetersPerSecond;
    }

    return payload;
  }
}

class DriverDashboardProvider extends ChangeNotifier {
  DriverDashboardProvider({
    SocketService? socketService,
    Dio? dio,
    SessionManager? sessionManager,
    DriverTripRequestRepository? tripRequestRepository,
    DriverWalletRepository? driverWalletRepository,
    HistoryRepository? historyRepository,
  }) : _socketService = socketService ?? SocketService(),
       _dio = dio ?? DioClient().dio,
       _tripRequestRepository = tripRequestRepository,
       _driverWalletRepository = driverWalletRepository,
       _historyRepository = historyRepository {
    _sessionExpiredSubscription = sessionManager?.sessionExpiredStream.listen((
      _,
    ) {
      resetLocalAvailability();
    });
  }

  final SocketService _socketService;
  final Dio _dio;
  final DriverTripRequestRepository? _tripRequestRepository;
  final DriverWalletRepository? _driverWalletRepository;
  final HistoryRepository? _historyRepository;
  String? _accessToken;
  StreamSubscription<void>? _sessionExpiredSubscription;
  static const int _maxPendingLocationUpdates = 20;
  final Queue<_PendingDriverLocationUpdate> _pendingLocationUpdates = Queue();
  bool _isFlushingLocationUpdates = false;

  DriverStatus _status = DriverStatus.offline;
  DriverStatus get status => _status;

  num? _todayIncome;
  num? get todayIncome => _todayIncome;

  int _todayTrips = 0;
  int get todayTrips => _todayTrips;

  bool _isLoadingIncome = false;
  bool get isLoadingIncome => _isLoadingIncome;

  bool _hasLoadedIncome = false;
  bool get hasLoadedIncome => _hasLoadedIncome;

  String? _incomeErrorMessage;
  String? get incomeErrorMessage => _incomeErrorMessage;

  bool _hasNewRequest = false;
  bool get hasNewRequest => _hasNewRequest;

  TripRequest? _currentRequest;
  TripRequest? get currentRequest => _currentRequest;
  final List<TripRequest> _openTripRequests = [];
  UnmodifiableListView<TripRequest> get openTripRequests =>
      UnmodifiableListView(_openTripRequests);

  bool _isResponding = false;
  bool get isResponding => _isResponding;

  bool _isUpdatingTrip = false;
  bool get isUpdatingTrip => _isUpdatingTrip;

  bool _isWaitingForCustomerConfirmation = false;
  bool get isWaitingForCustomerConfirmation =>
      _isWaitingForCustomerConfirmation;

  bool _isDemoMode = false;
  bool get isDemoMode => _isDemoMode;

  String? _snackbarMessage;
  String? get snackbarMessage => _snackbarMessage;

  void clearSnackbarMessage() {
    if (_snackbarMessage != null) {
      _snackbarMessage = null;
      notifyListeners();
    }
  }

  ActiveDriverTrip? _activeTrip;
  ActiveDriverTrip? get activeTrip => _activeTrip;
  final Map<int, Future<bool>> _activeTripDetailsFetches = {};
  final Set<int> _activeTripDetailsLoaded = {};

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
  double? _lastLatitude;
  double? _lastLongitude;
  double? get demoLat => _demoLat;
  double? get demoLng => _demoLng;

  bool _isLoadingActiveTrip = false;
  bool get isLoadingActiveTrip => _isLoadingActiveTrip;
  bool _isLoadingTripRequests = false;
  bool get isLoadingTripRequests => _isLoadingTripRequests;

  String? _errorMessage;
  String? get errorMessage => _errorMessage;
  String? _tripRequestsErrorMessage;
  String? get tripRequestsErrorMessage => _tripRequestsErrorMessage;
  String? _tripRequestActionErrorCode;
  String? get tripRequestActionErrorCode => _tripRequestActionErrorCode;

  Future<void> initializeRealtime(String accessToken) async {
    if (accessToken.isEmpty) {
      return;
    }

    _accessToken = accessToken;
    unawaited(loadTodayIncome());
    await _socketService.connect();
    _registerRealtimeHandlers();
    try {
      await loadActiveTrip();
    } catch (error) {
      debugPrint('DRIVER_DASHBOARD: load active trip failed: $error');
    }
    try {
      await loadOpenTripRequests();
    } catch (error) {
      debugPrint('DRIVER_DASHBOARD: load trip requests failed: $error');
    }
  }

  Future<void> reloadDashboardAfterConnectionRestored(
    String accessToken,
  ) async {
    if (accessToken.isEmpty) return;

    _accessToken = accessToken;
    try {
      await _socketService.connect();
      _registerRealtimeHandlers();
    } catch (error) {
      debugPrint('DRIVER_DASHBOARD: reconnect realtime failed: $error');
    }

    await Future.wait([
      loadActiveTrip(),
      loadOpenTripRequests(),
      loadTodayIncome(),
    ]);
  }

  Future<void> loadTodayIncome() async {
    final token = _accessToken;
    final walletRepository = _driverWalletRepository;
    final historyRepository = _historyRepository;
    if (token == null ||
        token.isEmpty ||
        walletRepository == null ||
        historyRepository == null) {
      return;
    }
    if (_isLoadingIncome) return;

    _isLoadingIncome = true;
    _incomeErrorMessage = null;
    notifyListeners();
    try {
      final wallet = await walletRepository.getWallet(token, period: 'Day');
      final history = await historyRepository.getBookingHistory(
        token,
        role: AppValues.roleDriver,
      );
      final now = DateTime.now();
      _todayIncome = wallet.income.total;
      _todayTrips = history.where((trip) {
        if (trip.status != HistoryTripStatus.completed || trip.tripId == null) {
          return false;
        }
        final completedAt = trip.time;
        return completedAt.year == now.year &&
            completedAt.month == now.month &&
            completedAt.day == now.day;
      }).length;
      _hasLoadedIncome = true;
    } catch (error) {
      debugPrint('DRIVER_DASHBOARD: Load today income failed: $error');
      _incomeErrorMessage = 'Không thể tải thu nhập hôm nay.';
    } finally {
      _isLoadingIncome = false;
      notifyListeners();
    }
  }

  void _registerRealtimeHandlers() {
    _socketService.onDriverOfferReceived((offer) {
      _currentRequest = TripRequest(
        offerId: offer.offerId,
        bookingId: offer.bookingId,
        expectedIncome: 0,
        pickupDistance: LocaleProvider.currentLocalizations.calculating,
        pickupTime: offer.expiresAt == null
            ? LocaleProvider.currentLocalizations.secondsRemaining(30)
            : LocaleProvider.currentLocalizations.expiresSoon,
        pickupAddress: offer.message,
        destinationAddress:
            LocaleProvider.currentLocalizations.viewTripAfterAccept,
      );
      _hasNewRequest = true;
      notifyListeners();
      loadOpenTripRequests();
    }, key: 'driverDashboardOfferReceived');
    _socketService.onDriverOfferClosed(({offerId, bookingId}) {
      final currentOfferId = _currentRequest?.offerId;
      final currentBookingId = _currentRequest?.bookingId;

      bool isMatch = false;
      if (offerId != null && currentOfferId == offerId) isMatch = true;
      if (bookingId != null && currentBookingId == bookingId) isMatch = true;

      // Robust check: if we are waiting for confirmation and get any closed offer event for this driver,
      // it's likely the one we are waiting for.
      if (isMatch ||
          (offerId == null &&
              bookingId == null &&
              _isWaitingForCustomerConfirmation)) {
        if (currentBookingId != null) {
          _socketService.leaveBooking(currentBookingId);
        }
        _hasNewRequest = false;
        _currentRequest = null;
        _isWaitingForCustomerConfirmation = false;
        _snackbarMessage =
            LocaleProvider.currentLocalizations.customerCancelledDriverRequest;
        notifyListeners();
      }
      if (_activeTrip == null) {
        loadOpenTripRequests();
      }
    }, key: 'driverDashboardOfferClosed');
    _socketService.onTripStatusChanged((update) {
      if (_isTerminalTripStatus(update.tripStatus)) {
        if (_activeTrip?.tripId == update.tripId) {
          _clearActiveTrip();
          notifyListeners();
        }
        if (update.tripStatus == 'COMPLETED') {
          unawaited(loadTodayIncome());
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
        paymentCompleted: sameTrip && oldTrip?.paymentCompleted == true,
      );
      notifyListeners();
      _socketService.joinTrip(update.tripId);
      if (!_hasActiveTripDetails(update.tripId)) {
        _fetchActiveTripDetails(update.bookingId, update.tripId);
      }
    }, key: 'driverDashboard');

    _socketService.onTripPaymentUpdated((update) {
      if (_activeTrip?.tripId != update.tripId) {
        return;
      }

      if (_isTerminalTripStatus(update.tripStatus)) {
        _clearActiveTrip();
        notifyListeners();
        unawaited(loadTodayIncome());
        return;
      }

      if (update.isSuccess) {
        _activeTrip = _activeTrip!.copyWith(
          tripStatus: update.tripStatus,
          paymentCompleted: true,
        );
        notifyListeners();
        return;
      }

      if (update.tripStatus == 'WAITING_PAYMENT') {
        _activeTrip = _activeTrip!.copyWith(tripStatus: 'WAITING_PAYMENT');
        notifyListeners();
      }
    }, key: 'driverDashboardPayment');

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
      _errorMessage = LocaleProvider.currentLocalizations.onlineFailed;
      notifyListeners();
      rethrow;
    }
  }

  Future<void> goOffline({String? accessToken}) async {
    if (accessToken != null && accessToken.trim().isNotEmpty) {
      _accessToken = accessToken;
    }

    final token = _accessToken;
    try {
      if (token != null && token.trim().isNotEmpty) {
        await _dio.post(
          ApiEndpoints.driverOffline,
          options: Options(
            headers: {ApiKeys.authorization: AuthHeader.bearer(token)},
          ),
        );
      }
    } catch (e) {
      debugPrint('Failed to go offline: $e');
    } finally {
      await _disconnectRealtime();
      resetLocalAvailability();
    }
  }

  void resetLocalAvailability() {
    _hasNewRequest = false;
    _currentRequest = null;
    _isResponding = false;
    _isUpdatingTrip = false;
    _isWaitingForCustomerConfirmation = false;
    _isDemoMode = false;
    _demoLat = null;
    _demoLng = null;
    _openTripRequests.clear();
    _pendingLocationUpdates.clear();
    _isFlushingLocationUpdates = false;
    _isLoadingActiveTrip = false;
    _isLoadingTripRequests = false;
    _errorMessage = null;
    _tripRequestsErrorMessage = null;
    _clearActiveTrip();
    _status = DriverStatus.offline;
    notifyListeners();
  }

  Future<void> _disconnectRealtime() async {
    if (_socketService.isConnected) {
      try {
        await _socketService.setDriverOffline();
      } catch (e) {
        debugPrint('Failed to notify socket offline: $e');
      }
    }
    _socketService.removeTripStatusChangedHandler('driverDashboard');
    _socketService.removeTripPaymentUpdatedHandler('driverDashboardPayment');
    _socketService.removeDriverLocationUpdatedHandler('driverDashboardDemo');
    _socketService.removeBookingUpdatedHandler('driverDashboardBooking');
    _socketService.removeDriverOfferReceivedHandler(
      'driverDashboardOfferReceived',
    );
    _socketService.removeDriverOfferClosedHandler('driverDashboardOfferClosed');
    await _socketService.disconnect();
  }

  void simulateNewRequest() {
    _currentRequest = TripRequest(
      offerId: 0,
      bookingId: 0,
      expectedIncome: 120000,
      pickupDistance: '1.5 km',
      pickupTime: LocaleProvider.currentLocalizations.minutesValue(5),
      pickupAddress: '80 Trần Duy Hưng, Cầu Giấy',
      destinationAddress: 'Sân bay Nội Bài, Sóc Sơn',
    );
    _openTripRequests
      ..clear()
      ..add(_currentRequest!);
    _hasNewRequest = true;
    notifyListeners();
  }

  Future<void> acceptRequest() async {
    final request = _currentRequest;
    final token = _accessToken;
    if (_isResponding) {
      return;
    }
    if (request == null) {
      _snackbarMessage = LocaleProvider.currentLocalizations.acceptTripFailed;
      notifyListeners();
      unawaited(loadOpenTripRequests());
      return;
    }
    if (token == null || token.isEmpty) {
      _snackbarMessage = LocaleProvider.currentLocalizations.sessionExpired;
      notifyListeners();
      return;
    }
    if (request.offerId <= 0 || request.bookingId <= 0) {
      _snackbarMessage =
          LocaleProvider.currentLocalizations.tripRequestsLoadFailed;
      notifyListeners();
      await loadOpenTripRequests();
      return;
    }

    _isResponding = true;
    _tripRequestActionErrorCode = null;
    notifyListeners();
    _socketService.onBookingUpdated(
      _handleBookingUpdate,
      key: 'driverDashboardBooking',
    );
    try {
      // Join before accepting when possible; the HTTP response remains the recovery
      // source if the assignment event races with group membership.
      try {
        await _socketService.joinBooking(request.bookingId);
      } catch (error) {
        debugPrint('Failed to join booking before accepting: $error');
      }
      final response = await _dio.post(
        ApiEndpoints.acceptDriverOffer(request.offerId),
        options: Options(
          headers: {ApiKeys.authorization: AuthHeader.bearer(token)},
        ),
      );

      _errorMessage = null;
      final data = response.data is Map
          ? Map<String, dynamic>.from(response.data as Map)
          : const <String, dynamic>{};
      final bookingStatus = _normalizeBookingStatus(
        data[ApiKeys.bookingStatus] ?? data['BookingStatus'],
      );
      final tripId = _parsePositiveInt(data[ApiKeys.tripId] ?? data['TripId']);
      final tripStatus = _normalizeTripStatus(
        data[ApiKeys.tripStatus] ?? data['TripStatus'],
      );
      final driverOfferRaw = data[ApiKeys.driverOffer] ?? data['DriverOffer'];
      final driverOffer = driverOfferRaw is Map
          ? Map<String, dynamic>.from(driverOfferRaw)
          : const <String, dynamic>{};
      final offerStatus = _normalizeOfferStatus(
        driverOffer[ApiKeys.offerStatus] ?? driverOffer['OfferStatus'],
      );

      if (bookingStatus == 'DriverAssigned' && tripId != null) {
        final tripIdValue = tripId;
        _hasNewRequest = false;
        _currentRequest = null;
        _isWaitingForCustomerConfirmation = false;
        _openTripRequests.clear();
        _activeTrip = ActiveDriverTrip(
          bookingId: request.bookingId,
          tripId: tripIdValue,
          tripStatus: tripStatus ?? 'ACCEPTED',
        );
        try {
          await _socketService.leaveBooking(request.bookingId);
          await _socketService.joinTrip(tripIdValue);
        } catch (error) {
          debugPrint(
            'Failed to update realtime groups after assignment: $error',
          );
        }
        await _fetchActiveTripDetailsSync(request.bookingId, tripIdValue);
      } else if (bookingStatus == 'Searching' &&
          offerStatus == 'DriverAccepted' &&
          tripId == null) {
        _isWaitingForCustomerConfirmation = true;
        try {
          await _socketService.joinBooking(request.bookingId);
        } catch (error) {
          debugPrint('Failed to join booking while waiting: $error');
        }
      } else {
        // Recover from an unexpected/stale response using the durable active-trip API.
        _isWaitingForCustomerConfirmation = false;
        await loadActiveTrip();
      }

      unawaited(loadOpenTripRequests());
    } on DioException catch (error) {
      final failure = _readTripRequestActionFailure(error);
      debugPrint(
        'Failed to accept request: status=${error.response?.statusCode} '
        'code=${failure.code}',
      );
      _tripRequestActionErrorCode = failure.code;
      _snackbarMessage = failure.message;
      await _reconcileAfterAcceptFailure();
    } catch (error) {
      debugPrint('Failed to accept request: $error');
      _tripRequestActionErrorCode = null;
      _snackbarMessage = LocaleProvider.currentLocalizations.acceptTripFailed;
      await _reconcileAfterAcceptFailure();
    } finally {
      _isResponding = false;
      notifyListeners();
    }
  }

  ({String? code, String message}) _readTripRequestActionFailure(
    DioException error,
  ) {
    final data = error.response?.data;
    final code = data is Map ? data[ApiKeys.code]?.toString() : null;
    final detail = data is Map
        ? (data[ApiKeys.detail] ?? data[ApiKeys.message])?.toString()
        : null;
    return (
      code: code,
      message: ApiErrorLocalizer.translate(
        LocaleProvider.currentLocalizations,
        code: code,
        fallback:
            detail ?? LocaleProvider.currentLocalizations.acceptTripFailed,
      ),
    );
  }

  Future<void> _reconcileAfterAcceptFailure() async {
    await loadOpenTripRequests();
    if (_currentRequest == null &&
        !_isWaitingForCustomerConfirmation &&
        _activeTrip == null) {
      await loadActiveTrip();
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
      loadOpenTripRequests();
    } catch (e) {
      debugPrint('Failed to decline request: $e');
      _errorMessage = LocaleProvider.currentLocalizations.declineTripFailed;
    } finally {
      _hasNewRequest = false;
      _currentRequest = null;
      _isResponding = false;
      notifyListeners();
    }
  }

  Future<void> loadOpenTripRequests() async {
    final token = _accessToken;
    final repository = _tripRequestRepository;
    if (token == null || token.isEmpty || repository == null) {
      _openTripRequests.clear();
      _tripRequestsErrorMessage = null;
      _isLoadingTripRequests = false;
      notifyListeners();
      return;
    }

    if (_isLoadingTripRequests) {
      return;
    }

    _isLoadingTripRequests = true;
    _tripRequestsErrorMessage = null;
    notifyListeners();

    try {
      final requests = await repository.getOpenTripRequests(token);
      _openTripRequests
        ..clear()
        ..addAll(requests.map(_mapTripRequest));
      _applyTripRequestState(requests);
    } on DriverTripRequestApiException catch (exception) {
      _tripRequestsErrorMessage = ApiErrorLocalizer.translate(
        LocaleProvider.currentLocalizations,
        fallback: exception.message,
      );
    } catch (e) {
      debugPrint('Failed to load trip requests: $e');
      _tripRequestsErrorMessage =
          LocaleProvider.currentLocalizations.tripRequestsLoadFailed;
    } finally {
      _isLoadingTripRequests = false;
      notifyListeners();
    }
  }

  Future<void> updateLocation(
    double lat,
    double lng, {
    DateTime? clientTimestampUtc,
    int? sequence,
    double? accuracyMeters,
    double? speedMetersPerSecond,
  }) async {
    _lastLatitude = lat;
    _lastLongitude = lng;
    final update = _PendingDriverLocationUpdate(
      latitude: lat,
      longitude: lng,
      clientTimestampUtc: clientTimestampUtc,
      sequence: sequence,
      accuracyMeters: accuracyMeters,
      speedMetersPerSecond: speedMetersPerSecond,
    );

    await _flushPendingLocationUpdates();
    final sent = await _sendLocationUpdate(update);
    if (!sent) {
      _enqueueLocationUpdate(update);
    } else {
      await _flushPendingLocationUpdates();
    }
  }

  Future<bool> _sendLocationUpdate(_PendingDriverLocationUpdate update) async {
    if (_socketService.isConnected) {
      try {
        await _socketService.updateDriverLocation(
          update.latitude,
          update.longitude,
          clientTimestampUtc: update.clientTimestampUtc,
          sequence: update.sequence,
          accuracyMeters: update.accuracyMeters,
          speedMetersPerSecond: update.speedMetersPerSecond,
        );
        return true;
      } catch (e) {
        debugPrint('Failed to update driver location over socket: $e');
      }
    }

    final token = _accessToken;
    if (token == null) return false;
    try {
      await _dio.patch(
        ApiEndpoints.driverLocation,
        data: update.toJson(),
        options: Options(
          headers: {ApiKeys.authorization: AuthHeader.bearer(token)},
        ),
      );
      return true;
    } catch (e) {
      debugPrint('Failed to update driver location: $e');
      return false;
    }
  }

  void _enqueueLocationUpdate(_PendingDriverLocationUpdate update) {
    if (update.sequence != null &&
        _pendingLocationUpdates.any(
          (item) => item.sequence == update.sequence,
        )) {
      return;
    }

    while (_pendingLocationUpdates.length >= _maxPendingLocationUpdates) {
      _pendingLocationUpdates.removeFirst();
    }
    _pendingLocationUpdates.addLast(update);
  }

  Future<void> _flushPendingLocationUpdates() async {
    if (_isFlushingLocationUpdates || _pendingLocationUpdates.isEmpty) {
      return;
    }

    _isFlushingLocationUpdates = true;
    try {
      while (_pendingLocationUpdates.isNotEmpty) {
        final update = _pendingLocationUpdates.first;
        final sent = await _sendLocationUpdate(update);
        if (!sent) {
          break;
        }
        _pendingLocationUpdates.removeFirst();
      }
    } finally {
      _isFlushingLocationUpdates = false;
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

  Future<bool> submitPreTripVehicleCheck({
    required bool brakeResponsePassed,
    required bool frontRearLightsPassed,
    required bool turnSignalsPassed,
    required bool visibleTiresPassed,
    required bool dashboardWarningPassed,
    required bool windshieldVisibilityPassed,
    required bool noMajorVisibleIssue,
    String? faultType,
    String? note,
    XFile? evidence,
  }) async {
    final trip = _activeTrip;
    final token = _accessToken;
    if (trip == null || token == null || _isUpdatingTrip) return false;
    _isUpdatingTrip = true;
    _errorMessage = null;
    notifyListeners();
    try {
      final data = evidence == null
          ? <String, dynamic>{
              'brakeResponsePassed': brakeResponsePassed,
              'frontRearLightsPassed': frontRearLightsPassed,
              'turnSignalsPassed': turnSignalsPassed,
              'visibleTiresPassed': visibleTiresPassed,
              'dashboardWarningPassed': dashboardWarningPassed,
              'windshieldVisibilityPassed': windshieldVisibilityPassed,
              'noMajorVisibleIssue': noMajorVisibleIssue,
              'faultType': faultType,
              'note': note?.trim(),
            }
          : FormData.fromMap({
              'brakeResponsePassed': brakeResponsePassed,
              'frontRearLightsPassed': frontRearLightsPassed,
              'turnSignalsPassed': turnSignalsPassed,
              'visibleTiresPassed': visibleTiresPassed,
              'dashboardWarningPassed': dashboardWarningPassed,
              'windshieldVisibilityPassed': windshieldVisibilityPassed,
              'noMajorVisibleIssue': noMajorVisibleIssue,
              'faultType': ?faultType,
              if (note?.trim().isNotEmpty == true) 'note': note!.trim(),
              'evidence': await MultipartFile.fromFile(
                evidence.path,
                filename: evidence.name,
                contentType: MediaType.parse(
                  imageContentTypeForEvidence(evidence),
                ),
              ),
            });
      await _dio.post(
        ApiEndpoints.preTripVehicleChecks(trip.tripId),
        data: data,
        options: Options(
          headers: {ApiKeys.authorization: AuthHeader.bearer(token)},
        ),
      );
      return true;
    } on DioException catch (error) {
      _captureTripActionError(error);
      rethrow;
    } catch (_) {
      _errorMessage = LocaleProvider.currentLocalizations.preTripCheckFailed;
      rethrow;
    } finally {
      _isUpdatingTrip = false;
      notifyListeners();
    }
  }

  Future<bool> safetyTerminate(String reason, {XFile? evidence}) async {
    final trip = _activeTrip;
    final token = _accessToken;
    if (trip == null || token == null || _isUpdatingTrip) return false;
    _isUpdatingTrip = true;
    _errorMessage = null;
    notifyListeners();
    try {
      await _flushPendingLocationUpdates();
      final data = evidence == null
          ? <String, dynamic>{'reason': reason.trim()}
          : FormData.fromMap({
              'reason': reason.trim(),
              'evidence': await MultipartFile.fromFile(
                evidence.path,
                filename: evidence.name,
                contentType: MediaType.parse(
                  imageContentTypeForEvidence(evidence),
                ),
              ),
            });
      await _dio.post(
        ApiEndpoints.safetyTermination(trip.tripId),
        data: data,
        options: Options(
          headers: {ApiKeys.authorization: AuthHeader.bearer(token)},
        ),
      );
      _clearActiveTrip();
      return true;
    } on DioException catch (error) {
      _captureTripActionError(error);
      rethrow;
    } catch (_) {
      _errorMessage =
          LocaleProvider.currentLocalizations.safetyTerminationFailed;
      rethrow;
    } finally {
      _isUpdatingTrip = false;
      notifyListeners();
    }
  }

  void _captureTripActionError(DioException error) {
    final data = error.response?.data;
    final code = data is Map ? data[ApiKeys.code]?.toString() : null;
    final detail = data is Map
        ? (data[ApiKeys.detail] ?? data[ApiKeys.message])?.toString()
        : null;
    _errorMessage = ApiErrorLocalizer.translate(
      LocaleProvider.currentLocalizations,
      code: code,
      fallback: detail ?? LocaleProvider.currentLocalizations.genericError,
    );
  }

  Future<bool> submitSafetyReport({
    required String reportType,
    required String reasonCode,
    required String description,
    required bool escalationRequested,
  }) async {
    final trip = _activeTrip;
    final token = _accessToken;
    if (trip == null || token == null || _isUpdatingTrip) return false;
    if (escalationRequested &&
        (_lastLatitude == null || _lastLongitude == null)) {
      throw StateError('Current location is required for SOS escalation.');
    }
    _isUpdatingTrip = true;
    notifyListeners();
    try {
      await _dio.post(
        ApiEndpoints.safetyReports(trip.tripId),
        data: {
          'reportType': reportType,
          'reasonCode': reasonCode.trim(),
          'description': description.trim(),
          'latitude': _lastLatitude,
          'longitude': _lastLongitude,
          'escalationRequested': escalationRequested,
        },
        options: Options(
          headers: {ApiKeys.authorization: AuthHeader.bearer(token)},
        ),
      );
      return true;
    } finally {
      _isUpdatingTrip = false;
      notifyListeners();
    }
  }

  Future<int?> reportAccident(String description) async {
    final trip = _activeTrip;
    final token = _accessToken;
    if (trip == null || token == null || _isUpdatingTrip) return null;
    _isUpdatingTrip = true;
    _errorMessage = null;
    notifyListeners();
    try {
      final response = await _dio.post(
        ApiEndpoints.tripAccidents(trip.tripId),
        data: {
          'category': 'MULTIPLE',
          'occurredAtUtc': DateTime.now().toUtc().toIso8601String(),
          'description': description.trim(),
        },
        options: Options(
          headers: {ApiKeys.authorization: AuthHeader.bearer(token)},
        ),
      );
      final data = response.data;
      return data is Map ? (data['id'] as num?)?.toInt() : null;
    } on DioException catch (error) {
      final data = error.response?.data;
      final code = data is Map ? data[ApiKeys.code]?.toString() : null;
      final detail = data is Map
          ? (data[ApiKeys.detail] ?? data[ApiKeys.message])?.toString()
          : null;
      _errorMessage = ApiErrorLocalizer.translate(
        LocaleProvider.currentLocalizations,
        code: code,
        fallback: detail ?? LocaleProvider.currentLocalizations.genericError,
      );
      rethrow;
    } catch (_) {
      _errorMessage = LocaleProvider.currentLocalizations.genericError;
      rethrow;
    } finally {
      _isUpdatingTrip = false;
      notifyListeners();
    }
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
      _clearActiveTrip();
      return true;
    } catch (e) {
      debugPrint('Failed to complete trip: $e');
      rethrow;
    } finally {
      _isUpdatingTrip = false;
      notifyListeners();
    }
  }

  /// Ends an IN_PROGRESS trip and advances it to the payment stage.
  Future<bool> endTripAsync() async {
    final trip = _activeTrip;
    final token = _accessToken;
    if (trip == null || token == null || _isUpdatingTrip) {
      return false;
    }

    _isUpdatingTrip = true;
    notifyListeners();
    try {
      // Make the latest queued GPS points visible to fare finalization before
      // the backend closes the trip tracking snapshot.
      await _flushPendingLocationUpdates();
      await _dio.post(
        ApiEndpoints.endTrip(trip.tripId),
        options: Options(
          headers: {ApiKeys.authorization: AuthHeader.bearer(token)},
        ),
      );
      await loadActiveTrip();
      _snackbarMessage = LocaleProvider.currentLocalizations.waitingForPayment;
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
      throw Exception(
        LocaleProvider.currentLocalizations.evidencePhotoCountError,
      );
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
    await loadActiveTrip();
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
        final paymentCompleted =
            data['paymentCompleted'] == true ||
            data['PaymentCompleted'] == true;
        if (bookingId != null && tripId != null && tripStatus != null) {
          if (_isTerminalTripStatus(tripStatus)) {
            _clearActiveTrip();
            unawaited(_socketService.leaveTrip(tripId.toInt()));
            return;
          }

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
            paymentCompleted:
                paymentCompleted ||
                (sameTrip && oldTrip?.paymentCompleted == true),
          );
          _socketService.joinTrip(tripIdValue);
          if (!_hasActiveTripDetails(tripIdValue)) {
            await _fetchActiveTripDetailsSync(bookingId.toInt(), tripIdValue);
          }
        }
      }
    } catch (e) {
      debugPrint('Error loading active trip: $e');
      _errorMessage = LocaleProvider.currentLocalizations.activeTripLoadFailed;
    } finally {
      _isLoadingActiveTrip = false;
      notifyListeners();
    }
  }

  void markTripPaymentCompleted(int tripId) {
    if (_activeTrip?.tripId != tripId) {
      return;
    }
    _activeTrip = _activeTrip!.copyWith(paymentCompleted: true);
    notifyListeners();
  }

  Future<bool> updateTripStatus(String tripStatus) async {
    final trip = _activeTrip;
    final token = _accessToken;
    if (trip == null || token == null || _isUpdatingTrip) {
      return false;
    }

    _isUpdatingTrip = true;
    _errorMessage = null;
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

      if (_isTerminalTripStatus(tripStatus)) {
        _clearActiveTrip();
        unawaited(_socketService.leaveTrip(trip.tripId));
        unawaited(loadOpenTripRequests());
      } else {
        _activeTrip = trip.copyWith(tripStatus: tripStatus);
      }
      return true;
    } on DioException catch (error) {
      _captureTripActionError(error);
      rethrow;
    } catch (_) {
      _errorMessage =
          LocaleProvider.currentLocalizations.tripStatusUpdateFailed;
      rethrow;
    } finally {
      _isUpdatingTrip = false;
      notifyListeners();
    }
  }

  void _handleBookingUpdate(dynamic update) {
    final isTerminal =
        _isTerminalBookingStatus(update.status?.toString()) ||
        _isTerminalTripStatus(update.tripStatus?.toString());
    if (isTerminal) {
      var changed = false;
      if (_activeTrip?.bookingId == update.bookingId) {
        final tripId = _activeTrip!.tripId;
        _socketService.leaveBooking(update.bookingId);
        _clearActiveTrip();
        unawaited(_socketService.leaveTrip(tripId));
        changed = true;
      }
      if (_currentRequest?.bookingId == update.bookingId) {
        _socketService.leaveBooking(update.bookingId);
        _hasNewRequest = false;
        _currentRequest = null;
        _isWaitingForCustomerConfirmation = false;
        if (update.status == 'Cancelled') {
          _snackbarMessage = LocaleProvider
              .currentLocalizations
              .customerCancelledDriverRequest;
        }
        changed = true;
      }
      if (changed) {
        notifyListeners();
      }
      unawaited(loadOpenTripRequests());
      return;
    } else if (update.status == 'DriverAssigned' || update.tripId != null) {
      if (update.tripId != null) {
        _socketService.leaveBooking(update.bookingId);
        _hasNewRequest = false;
        _currentRequest = null;
        _isWaitingForCustomerConfirmation = false;
        _openTripRequests.clear();
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
          paymentCompleted: sameTrip && oldTrip?.paymentCompleted == true,
        );
        notifyListeners();
        _socketService.joinTrip(update.tripId!);
        if (!_hasActiveTripDetails(update.tripId!)) {
          _fetchActiveTripDetails(update.bookingId, update.tripId!);
        }
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
          await Future.delayed(Duration(seconds: 1));
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

  void _applyTripRequestState(List<DriverTripRequestModel> requests) {
    if (_activeTrip != null) {
      return;
    }

    DriverTripRequestModel? waitingRequest;
    DriverTripRequestModel? sentRequest;
    for (final request in requests) {
      if (request.isDriverAccepted) {
        waitingRequest ??= request;
        continue;
      }
      if (request.isSent) {
        sentRequest ??= request;
      }
    }

    final selectedRequest = waitingRequest ?? sentRequest;
    if (selectedRequest == null) {
      _hasNewRequest = false;
      _currentRequest = null;
      _isWaitingForCustomerConfirmation = false;
      return;
    }

    _currentRequest = _mapTripRequest(selectedRequest);
    _hasNewRequest = selectedRequest.isSent;
    _isWaitingForCustomerConfirmation = selectedRequest.isDriverAccepted;
  }

  TripRequest _mapTripRequest(DriverTripRequestModel request) {
    return TripRequest(
      offerId: request.offerId,
      bookingId: request.bookingId,
      expectedIncome: request.expectedIncome,
      pickupDistance: _formatPickupDistance(request.pickupDistanceKm),
      pickupTime: _formatPickupTime(
        request.pickupDurationMinutes,
        request.expiresAt,
      ),
      pickupAddress: request.pickupAddress,
      destinationAddress: request.destinationAddress.trim().isEmpty
          ? LocaleProvider.currentLocalizations.noDestination
          : request.destinationAddress,
    );
  }

  static String _formatPickupDistance(double? distanceKm) {
    if (distanceKm == null || distanceKm <= 0) {
      return LocaleProvider.currentLocalizations.calculating;
    }

    if (distanceKm < 1) {
      return '${(distanceKm * 1000).round()} m';
    }

    final rounded = distanceKm >= 10 || distanceKm == distanceKm.roundToDouble()
        ? distanceKm.toStringAsFixed(0)
        : distanceKm.toStringAsFixed(1);
    return '$rounded km';
  }

  static String _formatPickupTime(
    int? pickupDurationMinutes,
    DateTime? expiresAt,
  ) {
    if (pickupDurationMinutes != null && pickupDurationMinutes > 0) {
      return LocaleProvider.currentLocalizations.minutesValue(
        pickupDurationMinutes,
      );
    }

    if (expiresAt != null) {
      return LocaleProvider.currentLocalizations.expiresSoon;
    }

    return LocaleProvider.currentLocalizations.calculating;
  }

  static int? _parsePositiveInt(Object? value) {
    final parsed = value is num
        ? value.toInt()
        : int.tryParse(value?.toString().trim() ?? '');
    return parsed != null && parsed > 0 ? parsed : null;
  }

  static String? _normalizeBookingStatus(Object? value) {
    if (value == null) return null;
    final numericValue = value is num
        ? value.toInt()
        : int.tryParse(value.toString().trim());
    if (numericValue != null) {
      return switch (numericValue) {
        0 => 'PendingSchedule',
        1 => 'Searching',
        2 => 'DriverAssigned',
        3 => 'Cancelled',
        4 => 'Expired',
        5 => 'Completed',
        _ => null,
      };
    }

    return switch (value.toString().trim().toLowerCase()) {
      'pendingschedule' => 'PendingSchedule',
      'searching' => 'Searching',
      'driverassigned' => 'DriverAssigned',
      'cancelled' || 'canceled' => 'Cancelled',
      'expired' => 'Expired',
      'completed' => 'Completed',
      _ => null,
    };
  }

  static String? _normalizeOfferStatus(Object? value) {
    if (value == null) return null;
    final numericValue = value is num
        ? value.toInt()
        : int.tryParse(value.toString().trim());
    if (numericValue != null) {
      return switch (numericValue) {
        0 => 'Sent',
        1 => 'DriverAccepted',
        2 => 'CustomerConfirmed',
        3 => 'Rejected',
        4 => 'Expired',
        5 => 'Cancelled',
        _ => null,
      };
    }

    return switch (value.toString().trim().toLowerCase()) {
      'sent' => 'Sent',
      'driveraccepted' => 'DriverAccepted',
      'customerconfirmed' => 'CustomerConfirmed',
      'rejected' => 'Rejected',
      'expired' => 'Expired',
      'cancelled' || 'canceled' => 'Cancelled',
      _ => null,
    };
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
        6 => 'WAITING_PAYMENT',
        7 => 'COMPLETED',
        8 => 'CANCELLED',
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
      '6' => 'WAITING_PAYMENT',
      '7' => 'COMPLETED',
      '8' => 'CANCELLED',
      _ => text,
    };
  }

  static bool _isTerminalTripStatus(String? value) {
    final normalized = value?.trim().toUpperCase();
    return normalized == 'COMPLETED' ||
        normalized == 'CANCELLED' ||
        normalized == 'CANCELED';
  }

  static bool _isTerminalBookingStatus(String? value) {
    final normalized = value?.trim().toUpperCase();
    return normalized == 'COMPLETED' ||
        normalized == 'CANCELLED' ||
        normalized == 'CANCELED' ||
        normalized == 'EXPIRED';
  }

  @override
  void dispose() {
    _sessionExpiredSubscription?.cancel();
    super.dispose();
  }
}

class ActiveDriverTrip {
  ActiveDriverTrip({
    required this.bookingId,
    required this.tripId,
    required this.tripStatus,
    this.pickupLat,
    this.pickupLng,
    this.destLat,
    this.destLng,
    this.encodedPolyline,
    this.arrivalPolyline,
    this.paymentCompleted = false,
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
  final bool paymentCompleted;

  ActiveDriverTrip copyWith({
    String? tripStatus,
    double? pickupLat,
    double? pickupLng,
    double? destLat,
    double? destLng,
    String? encodedPolyline,
    String? arrivalPolyline,
    bool? paymentCompleted,
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
      paymentCompleted: paymentCompleted ?? this.paymentCompleted,
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
