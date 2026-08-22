import 'dart:async';
import 'dart:math' as math;
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:geolocator/geolocator.dart';
import 'package:intl/intl.dart';

import '../../../../../core/maps/models/map_models.dart';
import '../../../../../core/maps/polyline_decoder.dart';
import '../../../../../core/maps/widgets/map_renderer_widget.dart';
import '../../../../../core/maps/widgets/live_trip_map_widget.dart';
import '../../../../../core/services/location_service.dart';
import '../../../../../core/services/map_api_service.dart';
import '../../../../../core/services/connectivity_service.dart';
import '../../../../../core/services/socket_service.dart';
import '../../../../../core/widgets/current_location_button.dart';
import '../../../../../dependency_injection/injection.dart';

import '../providers/driver_dashboard_provider.dart';
import '../widgets/driver_bottom_nav_bar.dart';
import '../widgets/risk_protection_dialogs.dart';
import '../../../../../core/localization/localization_extensions.dart';
import '../../../../../core/localization/locale_provider.dart';
import '../../../../../core/constants/app_strings.dart';
import '../../../../customer/booking/presentation/providers/booking_provider.dart';
import '../../../../customer/home/presentation/pages/customer_home_page.dart';
import '../../../../shared/history/presentation/pages/history_page.dart';
import '../../../../shared/onboarding/presentation/providers/role_provider.dart';
import '../../../../auth/presentation/providers/auth_provider.dart';
import '../../../../trip_sharing/trip_share_deep_link_coordinator.dart';
import '../../../../shared/call/presentation/pages/in_app_voice_call_page.dart';
import '../../../../shared/call/services/call_tone_player.dart';
import '../../../../shared/profile/presentation/pages/profile_page.dart';
import '../../../../shared/chat/presentation/pages/trip_chat_page.dart';
import '../../../../shared/notifications/presentation/pages/notifications_page.dart';
import '../../../../shared/notifications/presentation/providers/notification_provider.dart';
import '../../../../shared/risk_protection/presentation/pages/accident_details_page.dart';
import 'driver_trip_payment_page.dart';
import 'driver_return_evidence_page.dart';
import '../../../wallet/presentation/pages/driver_wallet_page.dart';

String _formatCurrency(num value) => NumberFormat.currency(
  locale: LocaleProvider.currentLocale.toLanguageTag(),
  symbol: 'VND',
  decimalDigits: 0,
).format(value);

String _formatTodayIncome(num value) => NumberFormat.currency(
  locale: LocaleProvider.currentLocale.toLanguageTag(),
  symbol: 'đ',
  decimalDigits: 0,
).format(value);

class DriverDashboardPage extends StatefulWidget {
  DriverDashboardPage({super.key});

  @override
  State<DriverDashboardPage> createState() => _DriverDashboardPageState();
}

class _RouteProgress {
  _RouteProgress({
    required this.point,
    required this.segmentIndex,
    required this.progress,
    this.distanceMeters = 0,
  });

  final AppLatLng point;
  final int segmentIndex;
  final double progress;
  final double distanceMeters;
}

class _DriverDashboardPageState extends State<DriverDashboardPage> {
  AppMapController? _mapController;
  int _selectedIndex = 0;
  bool _isLocating = false;
  StreamSubscription<Position>? _positionStream;
  StreamSubscription<void>? _connectionReloadSubscription;
  AppLatLng? _driverPosition;
  AppLatLng? _lastReportedPosition;
  DateTime? _lastReportedTime;
  int _locationSequence = 0;
  double _driverHeading = 0;
  final List<AppLatLng> _arrivalRoutePoints = [];
  final List<AppLatLng> _tripRoutePoints = [];
  final MapApiService _mapApiService = MapApiService();
  final SocketService _socketService = getIt<SocketService>();
  DateTime? _lastArrivalRouteRefreshAt;
  AppLatLng? _lastArrivalRouteRefreshOrigin;
  int? _renderedRouteTripId;
  int? _callSignalTripId;
  int? _routeSignalTripId;
  bool _arrivalRouteRefreshInProgress = false;
  bool _incomingCallDialogOpen = false;
  bool _connectionReloadInProgress = false;
  static const double _arrivalRerouteThresholdMeters = 35;
  static const double _arrivalRerouteMinMoveMeters = 80;
  static const double _locationUiJitterThresholdMeters = 5;
  static const Duration _arrivalRouteRefreshInterval = Duration(seconds: 12);

  Future<void> _goToCurrentLocation() async {
    if (_isLocating) return;
    setState(() {
      _isLocating = true;
    });
    try {
      final locationService = getIt<LocationService>();
      final location = await locationService.getCurrentLocation();
      if (!mounted) return;

      if (_mapController != null) {
        final lat = _driverPosition?.latitude ?? location.latitude;
        final lng = _driverPosition?.longitude ?? location.longitude;
        await _mapController!.animateCamera(
          AppCameraPosition(target: AppLatLng(lat, lng), zoom: 16),
        );
      }
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(context.l10n.currentLocationFailed(e.toString())),
        ),
      );
    } finally {
      if (mounted) {
        setState(() {
          _isLocating = false;
        });
      }
    }
  }

  late DriverDashboardProvider _provider;
  DateTime? _lastCameraFitAt;
  static const _cameraFitInterval = Duration(seconds: 3);

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) async {
      if (!mounted) return;
      final token = context.read<AuthProvider>().token;
      _provider = context.read<DriverDashboardProvider>();
      _provider.addListener(_onProviderUpdated);
      _connectionReloadSubscription ??= getIt<ConnectivityService>()
          .reloadRequests
          .listen((_) {
            unawaited(_reloadDashboardAfterConnectionRestored());
          });
      _onProviderUpdated();
      final switchedToCustomer = await _checkActiveCustomerBooking(token);
      if (switchedToCustomer || !mounted) {
        return;
      }
      if (token != null) {
        _provider.initializeRealtime(token);
        await context.read<NotificationProvider>().initialize(token);
      }
      unawaited(
        getIt<TripShareDeepLinkCoordinator>().processPendingAfterNavigation(),
      );
    });
  }

  Future<void> _reloadDashboardAfterConnectionRestored() async {
    if (!mounted || _connectionReloadInProgress) return;
    final token = context.read<AuthProvider>().token;
    if (token == null || token.isEmpty) return;

    _connectionReloadInProgress = true;
    try {
      await _provider.reloadDashboardAfterConnectionRestored(token);
      if (!mounted) return;
      await context.read<NotificationProvider>().initialize(
        token,
        refreshIfInitialized: true,
      );
    } finally {
      _connectionReloadInProgress = false;
    }
  }

  void _onProviderUpdated() {
    if (!mounted) return;

    final snackbarMessage = _provider.snackbarMessage;
    if (snackbarMessage != null) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (!mounted) return;
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(snackbarMessage),
            behavior: SnackBarBehavior.floating,
          ),
        );
        _provider.clearSnackbarMessage();
      });
    }

    final activeTrip = _provider.activeTrip;

    if (activeTrip != null && _positionStream == null) {
      _startLocationUpdates();
    }

    if (activeTrip != null) {
      _ensureTripCallHandler(activeTrip);
      _ensureTripRouteHandler(activeTrip);
    }

    if (activeTrip == null) {
      _removeTripCallHandler();
      _removeTripRouteHandler();
      if (_arrivalRoutePoints.isNotEmpty ||
          _tripRoutePoints.isNotEmpty ||
          _renderedRouteTripId != null) {
        setState(() {
          _arrivalRoutePoints.clear();
          _tripRoutePoints.clear();
          _renderedRouteTripId = null;
        });
      }
      return;
    }

    var shouldRebuildMap = false;
    if (_renderedRouteTripId != activeTrip.tripId) {
      _arrivalRoutePoints.clear();
      _tripRoutePoints.clear();
      _renderedRouteTripId = activeTrip.tripId;
      shouldRebuildMap = true;
    }

    if (_provider.isDemoMode &&
        _provider.demoLat != null &&
        _provider.demoLng != null) {
      final newPos = AppLatLng(_provider.demoLat!, _provider.demoLng!);
      if (_driverPosition != null) {
        _driverHeading = _calculateHeading(_driverPosition!, newPos);
      }
      _driverPosition = newPos;
      shouldRebuildMap = true;
      _refreshArrivalRouteIfNeeded(newPos);
    }

    if (_arrivalRoutePoints.isEmpty && activeTrip.arrivalPolyline != null) {
      try {
        final pts = decodePolyline(activeTrip.arrivalPolyline!);
        if (pts.isNotEmpty) {
          _arrivalRoutePoints.clear();
          _arrivalRoutePoints.addAll(pts);
          shouldRebuildMap = true;
        }
      } catch (_) {}
    }

    if (_tripRoutePoints.isEmpty && activeTrip.encodedPolyline != null) {
      try {
        final pts = decodePolyline(activeTrip.encodedPolyline!);
        if (pts.isNotEmpty) {
          _tripRoutePoints.clear();
          _tripRoutePoints.addAll(pts);
          shouldRebuildMap = true;
        }
      } catch (_) {}
    }

    if (shouldRebuildMap) {
      setState(() {});
    }

    if (_mapController == null) return;

    final now = DateTime.now();
    if (_lastCameraFitAt != null &&
        now.difference(_lastCameraFitAt!) < _cameraFitInterval) {
      return;
    }
    _lastCameraFitAt = now;

    final driverPos = _driverPosition;

    if (driverPos == null) return;

    List<AppLatLng> focusPoints = [driverPos];

    if (activeTrip.tripStatus == 'ACCEPTED' ||
        activeTrip.tripStatus == 'DRIVER_ARRIVING') {
      if (activeTrip.pickupLat != null && activeTrip.pickupLng != null) {
        focusPoints.add(
          AppLatLng(activeTrip.pickupLat!, activeTrip.pickupLng!),
        );
      }
    } else if (activeTrip.tripStatus == 'IN_PROGRESS' ||
        activeTrip.tripStatus == 'ARRIVED') {
      if (activeTrip.destLat != null && activeTrip.destLng != null) {
        focusPoints.add(AppLatLng(activeTrip.destLat!, activeTrip.destLng!));
      }
    }

    if (focusPoints.length == 1) {
      _mapController!.animateCamera(
        AppCameraPosition(target: focusPoints.first, zoom: 16),
      );
      return;
    }

    double minLat = focusPoints.first.latitude;
    double maxLat = focusPoints.first.latitude;
    double minLng = focusPoints.first.longitude;
    double maxLng = focusPoints.first.longitude;

    for (final pt in focusPoints) {
      if (pt.latitude < minLat) minLat = pt.latitude;
      if (pt.latitude > maxLat) maxLat = pt.latitude;
      if (pt.longitude < minLng) minLng = pt.longitude;
      if (pt.longitude > maxLng) maxLng = pt.longitude;
    }

    _mapController!.animateCameraToBounds(
      AppLatLng(minLat, minLng),
      AppLatLng(maxLat, maxLng),
      60.0,
    );
  }

  @override
  void dispose() {
    unawaited(_connectionReloadSubscription?.cancel());
    _connectionReloadSubscription = null;
    _provider.removeListener(_onProviderUpdated);
    _removeTripCallHandler();
    _removeTripRouteHandler();
    _stopLocationUpdates();
    super.dispose();
  }

  void _ensureTripCallHandler(ActiveDriverTrip activeTrip) {
    if (_callSignalTripId == activeTrip.tripId) return;
    _removeTripCallHandler();
    _callSignalTripId = activeTrip.tripId;
    _socketService.onInAppCallOffer((signal) {
      if (!mounted ||
          signal.tripId != activeTrip.tripId ||
          signal.sdp == null) {
        return;
      }
      _showIncomingCallDialog(signal);
    }, key: _callOfferHandlerKey(activeTrip.tripId));
  }

  void _removeTripCallHandler() {
    final tripId = _callSignalTripId;
    if (tripId == null) return;
    _socketService.removeInAppCallOfferHandler(_callOfferHandlerKey(tripId));
    _callSignalTripId = null;
  }

  void _ensureTripRouteHandler(ActiveDriverTrip activeTrip) {
    if (_routeSignalTripId == activeTrip.tripId) return;
    _removeTripRouteHandler();
    _routeSignalTripId = activeTrip.tripId;
    _socketService.onTripRouteRecalculated((update) {
      if (!mounted ||
          update.tripId != activeTrip.tripId ||
          _provider.activeTrip?.tripId != activeTrip.tripId) {
        return;
      }

      try {
        final points = decodePolyline(update.encodedPolyline);
        if (points.length < 2) return;

        setState(() {
          _tripRoutePoints
            ..clear()
            ..addAll(points);
        });
      } on FormatException {
        debugPrint('DriverDashboard: Invalid recalculated trip polyline.');
      }
    }, key: _tripRouteHandlerKey(activeTrip.tripId));
  }

  void _removeTripRouteHandler() {
    final tripId = _routeSignalTripId;
    if (tripId == null) return;
    _socketService.removeTripRouteRecalculatedHandler(
      _tripRouteHandlerKey(tripId),
    );
    _routeSignalTripId = null;
  }

  Future<void> _startInAppCall(ActiveDriverTrip trip) async {
    final accessToken = context.read<AuthProvider>().token;
    if (accessToken == null || accessToken.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(context.l10n.callUnavailableSessionExpired)),
      );
      return;
    }

    await _socketService.connect(accessToken);
    await _socketService.joinTrip(trip.tripId);
    if (!mounted) return;
    await Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => InAppVoiceCallPage(
          tripId: trip.tripId,
          bookingId: trip.bookingId,
          peerName: context.l10n.customer,
          accessToken: accessToken,
        ),
      ),
    );
  }

  Future<void> _showIncomingCallDialog(InAppCallSignal signal) async {
    if (_incomingCallDialogOpen) return;
    _incomingCallDialogOpen = true;
    final callTonePlayer = CallTonePlayer();
    final endedHandlerKey = 'incomingTone:${signal.tripId}:${signal.callId}';
    BuildContext? incomingDialogContext;
    var endedByPeer = false;
    _socketService.onInAppCallEnded((endedSignal) {
      if (endedSignal.tripId != signal.tripId ||
          endedSignal.callId != signal.callId) {
        return;
      }
      endedByPeer = true;
      final dialogContext = incomingDialogContext;
      if (dialogContext != null && dialogContext.mounted) {
        Navigator.of(dialogContext).pop();
      }
    }, key: endedHandlerKey);
    await callTonePlayer.playIncoming();
    if (!mounted || endedByPeer) {
      _socketService.removeInAppCallEndedHandler(endedHandlerKey);
      await callTonePlayer.dispose();
      _incomingCallDialogOpen = false;
      return;
    }
    final accessToken = context.read<AuthProvider>().token;
    bool? accepted;
    try {
      accepted = await showDialog<bool>(
        context: context,
        barrierDismissible: false,
        builder: (dialogContext) {
          incomingDialogContext = dialogContext;
          return AlertDialog(
            title: Text(context.l10n.incomingCall),
            content: Text(context.l10n.customerCalling),
            actions: [
              TextButton(
                onPressed: () => Navigator.of(dialogContext).pop(false),
                child: Text(context.l10n.decline),
              ),
              FilledButton.icon(
                onPressed: () => Navigator.of(dialogContext).pop(true),
                icon: Icon(Icons.call_rounded),
                label: Text(context.l10n.answer),
              ),
            ],
          );
        },
      );
    } finally {
      _socketService.removeInAppCallEndedHandler(endedHandlerKey);
      await callTonePlayer.dispose();
    }
    _incomingCallDialogOpen = false;

    if (!mounted || accessToken == null || accessToken.isEmpty) return;
    if (endedByPeer) return;
    if (accepted != true) {
      await _socketService.rejectInAppCall(signal);
      return;
    }

    await Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => InAppVoiceCallPage(
          tripId: signal.tripId,
          bookingId: signal.bookingId,
          peerName: context.l10n.customer,
          accessToken: accessToken,
          initialOffer: signal,
        ),
      ),
    );
  }

  static String _callOfferHandlerKey(int tripId) =>
      'driverDashboardCall:$tripId';

  static String _tripRouteHandlerKey(int tripId) =>
      'driverDashboardRoute:$tripId';

  void _startLocationUpdates() {
    _stopLocationUpdates();
    if (_provider.isDemoMode) return;
    _positionStream =
        Geolocator.getPositionStream(
          locationSettings: LocationSettings(
            accuracy: LocationAccuracy.high,
            distanceFilter: 5,
          ),
        ).listen(
          (Position position) {
            _onLocationChanged(position);
          },
          onError: (error) {
            debugPrint('Geolocator stream error: $error');
          },
        );
  }

  Future<void> _publishInitialLocation() async {
    try {
      final locationService = getIt<LocationService>();
      final location = await locationService.getCurrentLocation();
      if (!mounted) return;

      final newPos = AppLatLng(location.latitude, location.longitude);
      setState(() {
        if (_driverPosition != null) {
          _driverHeading = _calculateHeading(_driverPosition!, newPos);
        }
        _driverPosition = newPos;
      });

      final provider = context.read<DriverDashboardProvider>();
      await provider.goOnline(location.latitude, location.longitude);

      _startLocationUpdates();
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(context.l10n.onlineLocationFailed(e.toString())),
        ),
      );
      rethrow;
    }
  }

  void _stopLocationUpdates() {
    _positionStream?.cancel();
    _positionStream = null;
  }

  void _onLocationChanged(Position position) {
    if (!mounted) return;
    final newPos = AppLatLng(position.latitude, position.longitude);
    final currentUiPosition = _driverPosition;
    final shouldUpdateUi =
        currentUiPosition == null ||
        _calculateDirectDistance(currentUiPosition, newPos) * 1000 >=
            _locationUiJitterThresholdMeters;

    if (shouldUpdateUi) {
      setState(() {
        if (currentUiPosition != null) {
          _driverHeading = _calculateHeading(currentUiPosition, newPos);
        }
        _driverPosition = newPos;
      });
    }

    bool shouldReport = false;
    final now = DateTime.now();

    if (_lastReportedPosition == null || _lastReportedTime == null) {
      shouldReport = true;
    } else {
      final dist =
          _calculateDirectDistance(_lastReportedPosition!, newPos) * 1000;
      final timeDiff = now.difference(_lastReportedTime!).inSeconds;

      if (dist >= 10 || timeDiff >= 10) {
        shouldReport = true;
      }
    }

    if (shouldReport) {
      _lastReportedPosition = newPos;
      _lastReportedTime = now;
      final sequence = ++_locationSequence;
      context.read<DriverDashboardProvider>().updateLocation(
        position.latitude,
        position.longitude,
        clientTimestampUtc: position.timestamp,
        sequence: sequence,
        accuracyMeters: position.accuracy.isFinite ? position.accuracy : null,
        speedMetersPerSecond: position.speed.isFinite && position.speed >= 0
            ? position.speed
            : null,
      );
    }

    if (shouldUpdateUi) {
      _refreshArrivalRouteIfNeeded(newPos);
    }
  }

  Future<void> _refreshArrivalRouteIfNeeded(AppLatLng rawPosition) async {
    final activeTrip = _provider.activeTrip;
    if (activeTrip == null ||
        (activeTrip.tripStatus != 'ACCEPTED' &&
            activeTrip.tripStatus != 'DRIVER_ARRIVING')) {
      return;
    }
    if (_arrivalRouteRefreshInProgress) return;

    final now = DateTime.now();
    if (_lastArrivalRouteRefreshAt != null &&
        now.difference(_lastArrivalRouteRefreshAt!) <
            _arrivalRouteRefreshInterval) {
      return;
    }

    if (_lastArrivalRouteRefreshOrigin != null &&
        _calculateDirectDistance(_lastArrivalRouteRefreshOrigin!, rawPosition) *
                1000 <
            _arrivalRerouteMinMoveMeters) {
      return;
    }

    final snap = _findClosestRouteSnap(rawPosition, _arrivalRoutePoints);
    final shouldRefresh =
        _arrivalRoutePoints.length < 2 ||
        snap == null ||
        snap.distanceMeters > _arrivalRerouteThresholdMeters;
    if (!shouldRefresh) return;

    _arrivalRouteRefreshInProgress = true;
    _lastArrivalRouteRefreshAt = now;
    _lastArrivalRouteRefreshOrigin = rawPosition;

    try {
      if (activeTrip.pickupLat == null || activeTrip.pickupLng == null) return;
      final route = await _mapApiService.estimateRoute(
        rawPosition.latitude,
        rawPosition.longitude,
        activeTrip.pickupLat!,
        activeTrip.pickupLng!,
      );
      final points = decodePolyline(route.encodedPolyline);
      if (!mounted || points.length < 2) return;

      setState(() {
        _arrivalRoutePoints.clear();
        _arrivalRoutePoints.addAll(points);
      });
    } catch (e) {
      debugPrint('DriverDashboard: Failed to refresh arrival route: $e');
    } finally {
      _arrivalRouteRefreshInProgress = false;
    }
  }

  double _calculateDirectDistance(AppLatLng start, AppLatLng end) {
    const double earthRadiusKm = 6371.0;
    final lat1 = start.latitude * (math.pi / 180);
    final lon1 = start.longitude * (math.pi / 180);
    final lat2 = end.latitude * (math.pi / 180);
    final lon2 = end.longitude * (math.pi / 180);

    final dLat = lat2 - lat1;
    final dLon = lon2 - lon1;

    final a =
        math.sin(dLat / 2) * math.sin(dLat / 2) +
        math.cos(lat1) *
            math.cos(lat2) *
            math.sin(dLon / 2) *
            math.sin(dLon / 2);
    final c = 2 * math.atan2(math.sqrt(a), math.sqrt(1 - a));

    return earthRadiusKm * c;
  }

  _RouteProgress? _findClosestRouteSnap(
    AppLatLng target,
    List<AppLatLng> route,
  ) {
    if (route.length < 2) return null;
    _RouteProgress? closest;
    for (int i = 0; i < route.length - 1; i++) {
      final snap = _projectPointOnSegment(target, route[i], route[i + 1], i);
      if (closest == null || snap.distanceMeters < closest.distanceMeters) {
        closest = snap;
      }
    }
    return closest;
  }

  _RouteProgress _projectPointOnSegment(
    AppLatLng target,
    AppLatLng start,
    AppLatLng end,
    int segmentIndex,
  ) {
    final metersPerLat = 111320.0;
    final metersPerLng = 111320.0 * math.cos(target.latitude * math.pi / 180);
    final ax = (start.longitude - target.longitude) * metersPerLng;
    final ay = (start.latitude - target.latitude) * metersPerLat;
    final bx = (end.longitude - target.longitude) * metersPerLng;
    final by = (end.latitude - target.latitude) * metersPerLat;
    final abx = bx - ax;
    final aby = by - ay;
    final abLengthSquared = abx * abx + aby * aby;
    final fraction = abLengthSquared == 0
        ? 0.0
        : ((-ax * abx - ay * aby) / abLengthSquared).clamp(0, 1).toDouble();
    final point = AppLatLng(
      start.latitude + (end.latitude - start.latitude) * fraction,
      start.longitude + (end.longitude - start.longitude) * fraction,
    );
    final distanceMeters = _calculateDirectDistance(target, point) * 1000;
    return _RouteProgress(
      point: point,
      segmentIndex: segmentIndex,
      progress: segmentIndex + fraction,
      distanceMeters: distanceMeters,
    );
  }

  double _calculateHeading(AppLatLng start, AppLatLng end) {
    final startLat = start.latitude * math.pi / 180;
    final startLng = start.longitude * math.pi / 180;
    final endLat = end.latitude * math.pi / 180;
    final endLng = end.longitude * math.pi / 180;

    final dLng = endLng - startLng;
    final y = math.sin(dLng) * math.cos(endLat);
    final x =
        math.cos(startLat) * math.sin(endLat) -
        math.sin(startLat) * math.cos(endLat) * math.cos(dLng);
    final brng = math.atan2(y, x);
    return (brng * 180 / math.pi + 360) % 360;
  }

  Future<bool> _checkActiveCustomerBooking(String? accessToken) async {
    final bookingProvider = context.read<BookingProvider>();
    final roleProvider = context.read<RoleProvider>();

    if (bookingProvider.activeBooking != null) {
      debugPrint(
        'DRIVER_DASHBOARD: Active customer booking detected. Forcing switch to customer mode.',
      );
      await _provider.goOffline(accessToken: accessToken);
      if (!mounted) {
        return true;
      }
      roleProvider.setRole(AppValues.roleCustomer);
      Navigator.of(context).pushAndRemoveUntil(
        MaterialPageRoute(builder: (_) => CustomerHomePage()),
        (route) => false,
      );
      return true;
    }

    return false;
  }

  void _openChat(ActiveDriverTrip trip) {
    final auth = context.read<AuthProvider>();
    final currentUserId = auth.userId;

    if (currentUserId == null) {
      ScaffoldMessenger.of(
        context,
      ).showSnackBar(SnackBar(content: Text(context.l10n.chatUnavailable)));
      return;
    }

    Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => TripChatPage(
          tripId: trip.tripId,
          currentUserId: currentUserId,
          receiverName: context.l10n.customer,
          canSendMessage: _canSendChat(trip.tripStatus),
        ),
      ),
    );
  }

  bool _canSendChat(String? status) {
    if (status == null) return true;
    final normalized = status.trim().toUpperCase();
    return normalized != 'CANCELLED' &&
        normalized != 'CANCELED' &&
        normalized != 'EXPIRED';
  }

  @override
  Widget build(BuildContext context) {
    final List<Widget> pages = [
      _buildHomeContent(),
      HistoryPage(),
      _selectedIndex == 2 ? DriverWalletPage() : const SizedBox.shrink(),
      ProfilePage(),
    ];

    return Scaffold(
      body: IndexedStack(index: _selectedIndex, children: pages),
      bottomNavigationBar: DriverBottomNavBar(
        currentIndex: _selectedIndex,
        onTap: (index) {
          setState(() => _selectedIndex = index);
          if (index == 0) {
            context.read<DriverDashboardProvider>().loadTodayIncome();
          }
        },
      ),
    );
  }

  Widget _buildHomeContent() {
    return Stack(
      children: [
        // 1. Map Background
        Selector<DriverDashboardProvider, ActiveDriverTrip?>(
          selector: (_, provider) => provider.activeTrip,
          builder: (context, activeTrip, child) {
            if (activeTrip != null &&
                activeTrip.pickupLat != null &&
                activeTrip.pickupLng != null) {
              final pickup = AppLatLng(
                activeTrip.pickupLat!,
                activeTrip.pickupLng!,
              );
              final destination =
                  activeTrip.destLat != null && activeTrip.destLng != null
                  ? AppLatLng(activeTrip.destLat!, activeTrip.destLng!)
                  : null;
              final isArriving =
                  activeTrip.tripStatus == 'ACCEPTED' ||
                  activeTrip.tripStatus == 'DRIVER_ARRIVING';

              return LiveTripMapWidget(
                trackingState: isArriving
                    ? LiveTripTrackingState.arriving
                    : LiveTripTrackingState.inProgress,
                pickup: pickup,
                destination: destination,
                arrivalRoutePoints: _arrivalRoutePoints,
                tripRoutePoints: _tripRoutePoints,
                driverPosition: _driverPosition,
                driverHeading: _driverHeading,
                padding: const EdgeInsets.only(
                  top: 80,
                  bottom: 320,
                  left: 16,
                  right: 16,
                ),
                onMapCreated: (controller) {
                  _mapController = controller;
                  _goToCurrentLocation();
                },
              );
            }

            final lat =
                _driverPosition?.latitude ?? 16.0544; // Default to Da Nang
            final lng = _driverPosition?.longitude ?? 108.2022;

            return MapRendererWidget(
              initialCameraPosition: AppCameraPosition(
                target: AppLatLng(lat, lng),
                zoom: 15,
              ),
              onMapCreated: (controller) {
                _mapController = controller;
                _goToCurrentLocation();
              },
              myLocationButtonEnabled: false,
              markers: _driverPosition != null
                  ? {
                      AppMarker(
                        id: 'demo_driver',
                        position: _driverPosition!,
                        markerType: AppMarkerType.driver,
                        rotation: _driverHeading,
                      ),
                    }
                  : {},
            );
          },
        ),

        // 2. Top Bar (Income & Notification)
        Positioned(
          top: MediaQuery.viewPaddingOf(context).top + 8,
          left: 16,
          right: 16,
          child: LayoutBuilder(
            builder: (context, constraints) {
              final incomeWidth = math.min(
                160.0,
                math.max(128.0, constraints.maxWidth - 112),
              );
              return SizedBox(
                height: 96,
                child: Stack(
                  alignment: Alignment.topCenter,
                  children: [
                    SizedBox(width: incomeWidth, child: _IncomeHeader()),
                    Align(
                      alignment: Alignment.topRight,
                      child: Column(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          _CircleIconButton(
                            icon: Icons.notifications_none_rounded,
                            hasBadge: context
                                .select<NotificationProvider, bool>(
                                  (provider) => provider.unreadCount > 0,
                                ),
                            onPressed: () {
                              Navigator.of(context).push(
                                MaterialPageRoute(
                                  builder: (_) => NotificationsPage(),
                                ),
                              );
                            },
                          ),
                          if (const bool.fromEnvironment('dart.vm.product') ==
                              false)
                            Selector<DriverDashboardProvider, bool>(
                              selector: (_, provider) => provider.isDemoMode,
                              builder: (context, isDemoMode, _) => IconButton(
                                icon: Icon(
                                  isDemoMode
                                      ? Icons.bug_report
                                      : Icons.bug_report_outlined,
                                  color: isDemoMode ? Colors.red : Colors.grey,
                                ),
                                onPressed: () {
                                  final nextDemoMode = !isDemoMode;
                                  final provider = context
                                      .read<DriverDashboardProvider>();
                                  provider.toggleDemoMode();
                                  if (nextDemoMode) {
                                    _stopLocationUpdates();
                                    ScaffoldMessenger.of(context).showSnackBar(
                                      SnackBar(
                                        content: Text(
                                          context.l10n.gpsSimulationEnabled,
                                        ),
                                      ),
                                    );
                                  } else {
                                    _startLocationUpdates();
                                    ScaffoldMessenger.of(context).showSnackBar(
                                      SnackBar(
                                        content: Text(
                                          context.l10n.gpsSimulationDisabled,
                                        ),
                                      ),
                                    );
                                  }
                                },
                                tooltip: context.l10n.demoGpsMode,
                              ),
                            ),
                        ],
                      ),
                    ),
                  ],
                ),
              );
            },
          ),
        ),

        // 3. Bottom Controls (Online/Offline Toggle & Current Request)
        Positioned(
          bottom: 0,
          left: 0,
          right: 0,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              // Centering Button
              Align(
                alignment: Alignment.centerRight,
                child: Padding(
                  padding: const EdgeInsets.only(right: 16, bottom: 16),
                  child: CurrentLocationButton(
                    onPressed: _goToCurrentLocation,
                    isLoading: _isLocating,
                  ),
                ),
              ),

              // Request Card or Online/Offline Toggle
              Selector<
                DriverDashboardProvider,
                ({
                  ActiveDriverTrip? activeTrip,
                  TripRequest? currentRequest,
                  String? errorMessage,
                  bool hasNewRequest,
                  bool isLoadingActiveTrip,
                  bool isLoadingTripRequests,
                  bool isResponding,
                  bool isUpdatingTrip,
                  bool isWaitingForCustomerConfirmation,
                  String? tripRequestsErrorMessage,
                })
              >(
                selector: (_, provider) => (
                  activeTrip: provider.activeTrip,
                  currentRequest: provider.currentRequest,
                  errorMessage: provider.errorMessage,
                  hasNewRequest: provider.hasNewRequest,
                  isLoadingActiveTrip: provider.isLoadingActiveTrip,
                  isLoadingTripRequests: provider.isLoadingTripRequests,
                  isResponding: provider.isResponding,
                  isUpdatingTrip: provider.isUpdatingTrip,
                  isWaitingForCustomerConfirmation:
                      provider.isWaitingForCustomerConfirmation,
                  tripRequestsErrorMessage: provider.tripRequestsErrorMessage,
                ),
                builder: (context, state, child) {
                  if (state.isLoadingActiveTrip ||
                      (state.isLoadingTripRequests &&
                          state.activeTrip == null &&
                          !state.hasNewRequest &&
                          !state.isWaitingForCustomerConfirmation)) {
                    return Padding(
                      padding: EdgeInsets.only(bottom: 24),
                      child: Center(
                        child: CircularProgressIndicator(
                          color: Color(0xFF006B70),
                        ),
                      ),
                    );
                  }

                  if (state.errorMessage != null && state.activeTrip == null) {
                    return _ErrorLoadingActiveTripCard(
                      errorMessage: state.errorMessage!,
                      onRetry: context
                          .read<DriverDashboardProvider>()
                          .loadActiveTrip,
                    );
                  }

                  if (state.tripRequestsErrorMessage != null &&
                      state.activeTrip == null &&
                      !state.hasNewRequest &&
                      !state.isWaitingForCustomerConfirmation) {
                    return _ErrorLoadingActiveTripCard(
                      errorMessage: state.tripRequestsErrorMessage!,
                      onRetry: context
                          .read<DriverDashboardProvider>()
                          .loadOpenTripRequests,
                    );
                  }

                  if (state.activeTrip != null) {
                    return _ActiveTripCard(
                      trip: state.activeTrip!,
                      isUpdating: state.isUpdatingTrip,
                      onCall: () => _startInAppCall(state.activeTrip!),
                      onChat: () => _openChat(state.activeTrip!),
                    );
                  }
                  if (state.hasNewRequest && state.currentRequest != null) {
                    return _NewRequestCard(
                      request: state.currentRequest!,
                      isResponding: state.isResponding,
                    );
                  }
                  if (state.isWaitingForCustomerConfirmation) {
                    return _WaitingCustomerConfirmationCard();
                  }
                  return _StatusToggle(
                    onGoOnline: _publishInitialLocation,
                    onGoOffline: () async {
                      final provider = context.read<DriverDashboardProvider>();
                      await provider.goOffline();
                      _stopLocationUpdates();
                    },
                  );
                },
              ),

              SizedBox(height: 16),
            ],
          ),
        ),
      ],
    );
  }
}

class _ErrorLoadingActiveTripCard extends StatelessWidget {
  final String errorMessage;
  final VoidCallback onRetry;

  _ErrorLoadingActiveTripCard({
    required this.errorMessage,
    required this.onRetry,
  });

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 20),
      child: Container(
        padding: const EdgeInsets.all(20),
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(24),
          boxShadow: [
            BoxShadow(
              color: Colors.black.withValues(alpha: 0.16),
              blurRadius: 20,
              offset: Offset(0, 10),
            ),
          ],
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(Icons.cloud_off_rounded, size: 48, color: Colors.grey),
            SizedBox(height: 12),
            Text(
              context.l10n.serverConnectionErrorTitle,
              style: TextStyle(
                fontSize: 16,
                fontWeight: FontWeight.bold,
                color: Color(0xFF1A1A1A),
              ),
            ),
            SizedBox(height: 8),
            Text(
              errorMessage,
              textAlign: TextAlign.center,
              style: TextStyle(fontSize: 14, color: Colors.black54),
            ),
            SizedBox(height: 20),
            SizedBox(
              width: double.infinity,
              child: ElevatedButton.icon(
                onPressed: onRetry,
                icon: Icon(Icons.refresh_rounded),
                label: Text(
                  context.l10n.tryAgain,
                  style: TextStyle(fontWeight: FontWeight.bold),
                ),
                style: ElevatedButton.styleFrom(
                  backgroundColor: Color(0xFF006B70),
                  foregroundColor: Colors.white,
                  padding: const EdgeInsets.symmetric(vertical: 14),
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(12),
                  ),
                  elevation: 0,
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _ActiveTripCard extends StatelessWidget {
  _ActiveTripCard({
    required this.trip,
    required this.isUpdating,
    required this.onCall,
    required this.onChat,
  });

  final ActiveDriverTrip trip;
  final bool isUpdating;
  final VoidCallback onCall;
  final VoidCallback onChat;

  @override
  Widget build(BuildContext context) {
    final status = trip.tripStatus;
    final canCancel =
        status == 'ACCEPTED' ||
        status == 'DRIVER_ARRIVING' ||
        status == 'ARRIVED';
    final isWaitingReturn = status == 'WAITING_RETURN_CONFIRM';
    final isReturnConfirmed = status == 'RETURN_CONFIRMED';
    final isWaitingPayment = status == 'WAITING_PAYMENT';

    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 20),
      child: Container(
        padding: const EdgeInsets.all(20),
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(24),
          boxShadow: [
            BoxShadow(
              color: Colors.black.withValues(alpha: 0.16),
              blurRadius: 20,
              offset: Offset(0, 10),
            ),
          ],
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Container(
                  padding: const EdgeInsets.all(10),
                  decoration: BoxDecoration(
                    color: Color(0xFFE8F2F2),
                    shape: BoxShape.circle,
                  ),
                  child: Icon(
                    Icons.route_rounded,
                    color: Color(0xFF006B70),
                    size: 22,
                  ),
                ),
                SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        context.l10n.activeTrip,
                        style: TextStyle(
                          fontSize: 18,
                          fontWeight: FontWeight.bold,
                          color: Colors.black87,
                        ),
                      ),
                      SizedBox(height: 4),
                      Text(
                        _statusLabel(context, status),
                        style: TextStyle(
                          fontSize: 13,
                          fontWeight: FontWeight.w600,
                          color: Color(0xFF667085),
                        ),
                      ),
                    ],
                  ),
                ),
              ],
            ),
            SizedBox(height: 20),
            Row(
              children: [
                Expanded(
                  child: OutlinedButton.icon(
                    onPressed: onChat,
                    icon: Icon(Icons.chat_bubble_outline_rounded),
                    label: Text(context.l10n.message),
                    style: OutlinedButton.styleFrom(
                      foregroundColor: Color(0xFF006B70),
                      side: BorderSide(color: Color(0xFF006B70), width: 1.5),
                      padding: const EdgeInsets.symmetric(vertical: 14),
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(12),
                      ),
                    ),
                  ),
                ),
                SizedBox(width: 12),
                Expanded(
                  child: OutlinedButton.icon(
                    onPressed: onCall,
                    icon: Icon(Icons.phone_in_talk_rounded),
                    label: Text(context.l10n.callCustomer),
                    style: OutlinedButton.styleFrom(
                      foregroundColor: Color(0xFF006B70),
                      side: BorderSide(color: Color(0xFF006B70), width: 1.5),
                      padding: const EdgeInsets.symmetric(vertical: 14),
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(12),
                      ),
                    ),
                  ),
                ),
              ],
            ),
            SizedBox(height: 12),
            if (status == 'ACCEPTED')
              SizedBox(
                width: double.infinity,
                child: ElevatedButton.icon(
                  onPressed: isUpdating
                      ? null
                      : () => _runTripAction(
                          context,
                          () => context
                              .read<DriverDashboardProvider>()
                              .startArriving(),
                        ),
                  icon: Icon(Icons.navigation_rounded),
                  label: Text(
                    isUpdating
                        ? context.l10n.processing
                        : context.l10n.startPickup,
                  ),
                  style: _primaryButtonStyle(),
                ),
              )
            else if (status == 'DRIVER_ARRIVING')
              Row(
                children: [
                  if (canCancel) ...[
                    Expanded(
                      child: OutlinedButton.icon(
                        onPressed: isUpdating
                            ? null
                            : () => _runTripAction(
                                context,
                                () => context
                                    .read<DriverDashboardProvider>()
                                    .cancelActiveTrip(),
                              ),
                        icon: Icon(Icons.close_rounded),
                        label: Text(context.l10n.cancelBooking),
                        style: OutlinedButton.styleFrom(
                          foregroundColor: Color(0xFFE53935),
                          side: BorderSide(color: Color(0xFFE53935)),
                          padding: const EdgeInsets.symmetric(vertical: 14),
                          shape: RoundedRectangleBorder(
                            borderRadius: BorderRadius.circular(12),
                          ),
                        ),
                      ),
                    ),
                    SizedBox(width: 12),
                  ],
                  Expanded(
                    child: ElevatedButton.icon(
                      onPressed: isUpdating
                          ? null
                          : () => _runTripAction(
                              context,
                              () => context
                                  .read<DriverDashboardProvider>()
                                  .markArrived(),
                            ),
                      icon: Icon(Icons.flag_rounded),
                      label: Text(context.l10n.driverArrived),
                      style: _primaryButtonStyle(),
                    ),
                  ),
                ],
              )
            else if (status == 'ARRIVED')
              Row(
                children: [
                  if (canCancel) ...[
                    Expanded(
                      child: OutlinedButton.icon(
                        onPressed: isUpdating
                            ? null
                            : () => _runTripAction(
                                context,
                                () => context
                                    .read<DriverDashboardProvider>()
                                    .cancelActiveTrip(),
                              ),
                        icon: Icon(Icons.close_rounded),
                        label: Text(context.l10n.cancelBooking),
                        style: OutlinedButton.styleFrom(
                          foregroundColor: Color(0xFFE53935),
                          side: BorderSide(color: Color(0xFFE53935)),
                          padding: const EdgeInsets.symmetric(vertical: 14),
                          shape: RoundedRectangleBorder(
                            borderRadius: BorderRadius.circular(12),
                          ),
                        ),
                      ),
                    ),
                    SizedBox(width: 12),
                  ],
                  Expanded(
                    child: ElevatedButton.icon(
                      onPressed: isUpdating
                          ? null
                          : () => _startTripAfterSafetyCheck(context),
                      icon: Icon(Icons.play_arrow_rounded),
                      label: Text(context.l10n.startTrip),
                      style: _primaryButtonStyle(),
                    ),
                  ),
                ],
              )
            else if (status == 'IN_PROGRESS') ...[
              Row(
                children: [
                  Expanded(
                    child: OutlinedButton.icon(
                      onPressed: isUpdating
                          ? null
                          : () => _reportAccident(context),
                      icon: const Icon(Icons.car_crash_outlined),
                      label: Text(context.l10n.reportAccident),
                    ),
                  ),
                  const SizedBox(width: 10),
                  Expanded(
                    child: OutlinedButton.icon(
                      onPressed: isUpdating
                          ? null
                          : () => _safetyTerminate(context, trip.tripId),
                      icon: const Icon(Icons.health_and_safety_outlined),
                      label: Text(context.l10n.safetyTermination),
                      style: OutlinedButton.styleFrom(
                        foregroundColor: const Color(0xFFC2410C),
                      ),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 10),
              SizedBox(
                width: double.infinity,
                child: OutlinedButton.icon(
                  onPressed: isUpdating
                      ? null
                      : () => _submitSafetyReport(context),
                  icon: const Icon(Icons.report_problem_outlined),
                  label: Text(context.l10n.safetyReportTitle),
                  style: OutlinedButton.styleFrom(
                    foregroundColor: const Color(0xFFB45309),
                  ),
                ),
              ),
              const SizedBox(height: 10),
              SizedBox(
                width: double.infinity,
                child: ElevatedButton.icon(
                  onPressed: isUpdating
                      ? null
                      : () => _runTripAction(
                          context,
                          () => context
                              .read<DriverDashboardProvider>()
                              .endTripAsync(),
                        ),
                  icon: const Icon(Icons.flag_rounded),
                  label: Text(
                    isUpdating ? context.l10n.processing : context.l10n.endTrip,
                  ),
                  style: _primaryButtonStyle(),
                ),
              ),
            ] else if (isWaitingReturn)
              trip.paymentCompleted
                  ? _buildWaitingReturnSection(context, trip.tripId, isUpdating)
                  : _buildWaitingPaymentSection(context, trip.tripId)
            else if (isReturnConfirmed)
              _buildReturnConfirmedSection(context, isUpdating)
            else if (isWaitingPayment)
              _buildWaitingPaymentSection(context, trip.tripId),
            if (canCancel) ...[
              const SizedBox(height: 10),
              SizedBox(
                width: double.infinity,
                child: OutlinedButton.icon(
                  onPressed: isUpdating
                      ? null
                      : () => _safetyTerminate(
                          context,
                          trip.tripId,
                          requiresPayment: false,
                        ),
                  icon: const Icon(Icons.health_and_safety_outlined),
                  label: Text(context.l10n.safetyTermination),
                  style: OutlinedButton.styleFrom(
                    foregroundColor: const Color(0xFFC2410C),
                  ),
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }

  static ButtonStyle _primaryButtonStyle() {
    return ElevatedButton.styleFrom(
      backgroundColor: Color(0xFF006B70),
      foregroundColor: Colors.white,
      padding: const EdgeInsets.symmetric(vertical: 14),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
    );
  }

  // ─────────── WAITING_RETURN_CONFIRM section ──────────────────────────

  static Widget _buildWaitingReturnSection(
    BuildContext context,
    int tripId,
    bool isUpdating,
  ) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        // Status banner
        Container(
          padding: const EdgeInsets.all(14),
          decoration: BoxDecoration(
            color: Color(0xFFFFF8E1),
            borderRadius: BorderRadius.circular(14),
            border: Border.all(color: Color(0xFFFFCC02).withValues(alpha: 0.5)),
          ),
          child: Row(
            children: [
              Icon(
                Icons.hourglass_top_rounded,
                color: Color(0xFFF9A825),
                size: 20,
              ),
              SizedBox(width: 10),
              Expanded(
                child: Text(
                  context.l10n.waitingCustomerReturnConfirmation,
                  style: TextStyle(
                    color: Color(0xFF7B5800),
                    fontSize: 13,
                    height: 1.5,
                  ),
                ),
              ),
            ],
          ),
        ),
        SizedBox(height: 14),

        // Driver substitute confirm button
        SizedBox(
          width: double.infinity,
          child: OutlinedButton.icon(
            onPressed: isUpdating
                ? null
                : () {
                    Navigator.push(
                      context,
                      MaterialPageRoute(
                        builder: (_) =>
                            DriverReturnEvidencePage(tripId: tripId),
                      ),
                    );
                  },
            icon: Icon(Icons.add_photo_alternate_rounded),
            label: Text(context.l10n.confirmReturnWithEvidence),
            style: OutlinedButton.styleFrom(
              foregroundColor: Color(0xFF006B70),
              side: BorderSide(color: Color(0xFF006B70), width: 1.5),
              padding: const EdgeInsets.symmetric(vertical: 14),
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(12),
              ),
            ),
          ),
        ),
      ],
    );
  }

  // ─────────── RETURN_CONFIRMED banner ─────────────────────────────────

  static Widget _buildReturnConfirmedSection(
    BuildContext context,
    bool isUpdating,
  ) {
    return Column(
      children: [
        Container(
          padding: const EdgeInsets.all(16),
          decoration: BoxDecoration(
            color: Color(0xFFE8F7F0),
            borderRadius: BorderRadius.circular(14),
            border: Border.all(
              color: Color(0xFF0A8F62).withValues(alpha: 0.3),
            ),
          ),
          child: Row(
            children: [
              Icon(
                Icons.check_circle_rounded,
                color: Color(0xFF0A8F62),
                size: 22,
              ),
              SizedBox(width: 12),
              Expanded(
                child: Text(
                  context.l10n.returnConfirmedCompleting,
                  style: TextStyle(
                    color: Color(0xFF0A5C3E),
                    fontSize: 14,
                    fontWeight: FontWeight.w600,
                  ),
                ),
              ),
            ],
          ),
        ),
        SizedBox(height: 12),
        SizedBox(
          width: double.infinity,
          child: OutlinedButton.icon(
            onPressed: isUpdating
                ? null
                : () => _runTripAction(
                    context,
                    () => context
                        .read<DriverDashboardProvider>()
                        .completeActiveTrip(),
                  ),
            icon: Icon(Icons.sync_rounded),
            label: Text(
              isUpdating ? context.l10n.processing : context.l10n.checkAgain,
            ),
          ),
        ),
      ],
    );
  }

  static Widget _buildWaitingPaymentSection(BuildContext context, int tripId) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Container(
          padding: const EdgeInsets.all(16),
          decoration: BoxDecoration(
            color: Color(0xFFE8F2F2),
            borderRadius: BorderRadius.circular(14),
            border: Border.all(color: Color(0xFF006B70).withValues(alpha: 0.3)),
          ),
          child: Row(
            children: [
              Icon(Icons.payments_rounded, color: Color(0xFF006B70), size: 22),
              SizedBox(width: 12),
              Expanded(
                child: Text(
                  context.l10n.waitForPayment,
                  style: TextStyle(
                    color: Color(0xFF00545A),
                    fontSize: 14,
                    fontWeight: FontWeight.w600,
                  ),
                ),
              ),
            ],
          ),
        ),
        SizedBox(height: 14),
        SizedBox(
          width: double.infinity,
          child: ElevatedButton.icon(
            onPressed: () async {
              final completed = await Navigator.of(context).push<bool>(
                MaterialPageRoute(
                  builder: (_) => DriverTripPaymentPage(tripId: tripId),
                ),
              );
              if (!context.mounted || completed != true) {
                return;
              }
              final provider = context.read<DriverDashboardProvider>();
              provider.markTripPaymentCompleted(tripId);
              await provider.loadActiveTrip();
            },
            icon: Icon(Icons.receipt_long_rounded),
            label: Text(context.l10n.confirmPayment),
            style: _primaryButtonStyle(),
          ),
        ),
      ],
    );
  }

  static String _statusLabel(BuildContext context, String status) {
    return switch (status) {
      'ACCEPTED' => context.l10n.statusAccepted,
      'DRIVER_ARRIVING' => context.l10n.statusDriverArriving,
      'ARRIVED' => context.l10n.statusArrived,
      'IN_PROGRESS' => context.l10n.statusInProgress,
      'WAITING_RETURN_CONFIRM' => context.l10n.waitingReturnConfirmation,
      'RETURN_CONFIRMED' => context.l10n.returnConfirmedStatus,
      'WAITING_PAYMENT' => context.l10n.waitForPayment,
      _ => status,
    };
  }

  static Future<void> _runTripAction(
    BuildContext context,
    Future<bool> Function() action, {
    String? successMessage,
    VoidCallback? onSuccess,
  }) async {
    try {
      final ok = await action();
      if (!context.mounted || !ok) {
        return;
      }
      onSuccess?.call();
      if (successMessage == null) {
        return;
      }
      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(SnackBar(content: Text(successMessage)));
    } catch (_) {
      if (!context.mounted) return;
      final provider = context.read<DriverDashboardProvider>();
      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(
          SnackBar(
            content: Text(
              provider.errorMessage ?? context.l10n.tripStatusUpdateFailed,
            ),
          ),
        );
    }
  }

  static Future<void> _startTripAfterSafetyCheck(BuildContext context) async {
    final check = await showPreTripSafetyCheckDialog(context);
    if (!context.mounted || check == null) return;
    await _runTripAction(context, () async {
      final provider = context.read<DriverDashboardProvider>();
      final submitted = await provider.submitPreTripVehicleCheck(
        brakeResponsePassed: check.values[0],
        frontRearLightsPassed: check.values[1],
        turnSignalsPassed: check.values[2],
        visibleTiresPassed: check.values[3],
        dashboardWarningPassed: check.values[4],
        windshieldVisibilityPassed: check.values[5],
        noMajorVisibleIssue: check.values[6],
        faultType: check.faultType,
        note: check.note,
        evidence: check.evidence,
      );
      if (!submitted) return false;
      if (!check.allPassed) {
        if (context.mounted) {
          ScaffoldMessenger.of(context)
            ..hideCurrentSnackBar()
            ..showSnackBar(
              SnackBar(content: Text(context.l10n.allChecksRequired)),
            );
        }
        return true;
      }
      return provider.startTrip();
    });
  }

  static Future<void> _reportAccident(BuildContext context) async {
    final description = await showAccidentReportDialog(context);
    if (!context.mounted || description == null) return;
    final provider = context.read<DriverDashboardProvider>();
    try {
      final accidentId = await provider.reportAccident(description);
      if (!context.mounted) return;
      if (accidentId == null) {
        throw StateError('Missing accident id');
      }
      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(SnackBar(content: Text(context.l10n.accidentReported)));
      await Navigator.of(context).push(
        MaterialPageRoute(
          builder: (_) => AccidentDetailsPage(accidentId: accidentId),
        ),
      );
    } catch (_) {
      if (!context.mounted) return;
      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(
          SnackBar(
            content: Text(provider.errorMessage ?? context.l10n.genericError),
          ),
        );
    }
  }

  static Future<void> _submitSafetyReport(BuildContext context) async {
    final report = await showSafetyReportDialog(context);
    if (!context.mounted || report == null) return;
    try {
      final submitted = await context
          .read<DriverDashboardProvider>()
          .submitSafetyReport(
            reportType: report.reportType,
            reasonCode: report.reasonCode,
            description: report.description,
            escalationRequested: report.escalationRequested,
          );
      if (!context.mounted || !submitted) return;
      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(
          SnackBar(content: Text(context.l10n.safetyReportSubmitted)),
        );
    } catch (_) {
      if (!context.mounted) return;
      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(
          SnackBar(content: Text(context.l10n.safetyReportFailed)),
        );
    }
  }

  static Future<void> _safetyTerminate(
    BuildContext context,
    int tripId, {
    bool requiresPayment = true,
  }) async {
    final result = await showSafetyTerminationDialog(context);
    if (!context.mounted || result == null) return;
    final navigator = Navigator.of(context);
    final messenger = ScaffoldMessenger.of(context);
    final provider = context.read<DriverDashboardProvider>();
    final fallbackMessage = context.l10n.safetyTerminationFailed;
    try {
      final terminated = await provider.safetyTerminate(
        result.reason,
        evidence: result.evidence,
      );
      if (!terminated) return;
      if (!requiresPayment) return;
      if (!navigator.mounted) return;
      await navigator.push<bool>(
        MaterialPageRoute(
          builder: (_) => DriverTripPaymentPage(tripId: tripId),
        ),
      );
    } catch (_) {
      if (!messenger.mounted) return;
      messenger
        ..hideCurrentSnackBar()
        ..showSnackBar(
          SnackBar(content: Text(provider.errorMessage ?? fallbackMessage)),
        );
    }
  }
}

class _CircleIconButton extends StatelessWidget {
  final IconData icon;
  final VoidCallback onPressed;
  final bool hasBadge;

  _CircleIconButton({
    required this.icon,
    required this.onPressed,
    this.hasBadge = false,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        color: Colors.white,
        shape: BoxShape.circle,
        boxShadow: [
          BoxShadow(
            color: Colors.black.withValues(alpha: 0.1),
            blurRadius: 8,
            offset: Offset(0, 2),
          ),
        ],
      ),
      child: Stack(
        children: [
          IconButton(
            icon: Icon(icon, color: Colors.black87),
            onPressed: onPressed,
          ),
          if (hasBadge)
            Positioned(
              top: 10,
              right: 10,
              child: Container(
                width: 8,
                height: 8,
                decoration: BoxDecoration(
                  color: Colors.red,
                  shape: BoxShape.circle,
                ),
              ),
            ),
        ],
      ),
    );
  }
}

class _IncomeHeader extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    return Selector<
      DriverDashboardProvider,
      ({num? income, int trips, bool loading, bool loaded, bool hasError})
    >(
      selector: (_, provider) => (
        income: provider.todayIncome,
        trips: provider.todayTrips,
        loading: provider.isLoadingIncome,
        loaded: provider.hasLoadedIncome,
        hasError: provider.incomeErrorMessage != null,
      ),
      builder: (context, summary, child) {
        return Container(
          padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 8),
          decoration: BoxDecoration(
            color: Colors.white,
            borderRadius: BorderRadius.circular(30),
            boxShadow: [
              BoxShadow(
                color: Colors.black.withValues(alpha: 0.1),
                blurRadius: 10,
                offset: Offset(0, 4),
              ),
            ],
          ),
          child: _buildContent(context, summary),
        );
      },
    );
  }

  Widget _buildContent(
    BuildContext context,
    ({num? income, int trips, bool loading, bool loaded, bool hasError})
    summary,
  ) {
    if (!summary.loaded && !summary.hasError) {
      return Text(
        'Đang tải thu nhập...',
        textAlign: TextAlign.center,
        style: TextStyle(fontSize: 13, color: Color(0xFF667085)),
      );
    }
    if (summary.hasError && !summary.loaded) {
      return Text(
        'Không thể tải thu nhập hôm nay.',
        textAlign: TextAlign.center,
        style: TextStyle(fontSize: 12, color: Color(0xFFB42318)),
      );
    }

    final income = summary.income ?? 0;
    final detail = income > 0
        ? '${summary.trips} chuyến'
        : 'Chưa có thu nhập hôm nay.';
    return Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        Text(
          context.l10n.todayIncomeUpper,
          style: TextStyle(
            fontSize: 10,
            fontWeight: FontWeight.bold,
            color: Colors.grey,
          ),
        ),
        SizedBox(height: 3),
        Row(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Text(
              _formatTodayIncome(income),
              maxLines: 1,
              style: TextStyle(
                fontSize: 18,
                fontWeight: FontWeight.bold,
                color: Color(0xFF006B70),
              ),
            ),
            SizedBox(width: 8),
            Flexible(
              child: Text(
                detail,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(
                  fontSize: 9,
                  fontWeight: FontWeight.w600,
                  color: Color(0xFF667085),
                ),
              ),
            ),
          ],
        ),
      ],
    );
  }
}

class _StatusToggle extends StatefulWidget {
  final Future<void> Function() onGoOnline;
  final Future<void> Function() onGoOffline;

  _StatusToggle({required this.onGoOnline, required this.onGoOffline});

  @override
  State<_StatusToggle> createState() => _StatusToggleState();
}

class _StatusToggleState extends State<_StatusToggle> {
  bool _isLoading = false;

  Future<void> _handleToggle(bool isOnline) async {
    if (_isLoading) return;
    setState(() => _isLoading = true);
    try {
      if (isOnline) {
        await widget.onGoOffline();
      } else {
        await widget.onGoOnline();
      }
    } finally {
      if (mounted) {
        setState(() => _isLoading = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Selector<DriverDashboardProvider, bool>(
      selector: (_, provider) => provider.status == DriverStatus.online,
      builder: (context, isOnline, child) {
        return Padding(
          padding: const EdgeInsets.symmetric(horizontal: 24),
          child: Container(
            height: 60,
            decoration: BoxDecoration(
              color: Colors.white,
              borderRadius: BorderRadius.circular(35),
              boxShadow: [
                BoxShadow(
                  color: Colors.black.withValues(alpha: 0.1),
                  blurRadius: 10,
                  offset: Offset(0, 4),
                ),
              ],
            ),
            child: Stack(
              children: [
                Row(
                  children: [
                    Expanded(
                      child: GestureDetector(
                        onTap: (!isOnline || _isLoading)
                            ? null
                            : () => _handleToggle(isOnline),
                        child: Center(
                          child: Text(
                            'Offline',
                            style: TextStyle(
                              fontSize: 16,
                              fontWeight: FontWeight.bold,
                              color: !isOnline ? Colors.black87 : Colors.grey,
                            ),
                          ),
                        ),
                      ),
                    ),
                    Expanded(
                      child: GestureDetector(
                        onTap: (isOnline || _isLoading)
                            ? null
                            : () => _handleToggle(isOnline),
                        child: Container(
                          margin: const EdgeInsets.all(4),
                          decoration: BoxDecoration(
                            color: isOnline
                                ? Color(0xFF006B70)
                                : Colors.transparent,
                            borderRadius: BorderRadius.circular(30),
                          ),
                          child: Center(
                            child: Row(
                              mainAxisAlignment: MainAxisAlignment.center,
                              children: [
                                if (isOnline) ...[
                                  Container(
                                    width: 8,
                                    height: 8,
                                    decoration: BoxDecoration(
                                      color: Colors.cyanAccent,
                                      shape: BoxShape.circle,
                                    ),
                                  ),
                                  SizedBox(width: 8),
                                ],
                                Text(
                                  'Online',
                                  style: TextStyle(
                                    fontSize: 16,
                                    fontWeight: FontWeight.bold,
                                    color: isOnline
                                        ? Colors.white
                                        : Colors.grey,
                                  ),
                                ),
                              ],
                            ),
                          ),
                        ),
                      ),
                    ),
                  ],
                ),
                if (_isLoading)
                  Center(
                    child: CircularProgressIndicator(color: Color(0xFF006B70)),
                  ),
              ],
            ),
          ),
        );
      },
    );
  }
}

class _WaitingCustomerConfirmationCard extends StatelessWidget {
  _WaitingCustomerConfirmationCard();

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 20),
      child: Container(
        padding: const EdgeInsets.all(24),
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(24),
          boxShadow: [
            BoxShadow(
              color: Colors.black.withValues(alpha: 0.12),
              blurRadius: 24,
              offset: Offset(0, 8),
            ),
          ],
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            SizedBox(
              width: 40,
              height: 40,
              child: CircularProgressIndicator(
                color: Color(0xFF006B70),
                strokeWidth: 3,
              ),
            ),
            SizedBox(height: 24),
            Text(
              context.l10n.waitingConfirmation,
              style: TextStyle(
                fontSize: 20,
                fontWeight: FontWeight.bold,
                color: Color(0xFF1D2939),
              ),
            ),
            SizedBox(height: 8),
            Text(
              context.l10n.waitingCustomerDriverConfirmation,
              textAlign: TextAlign.center,
              style: TextStyle(
                fontSize: 14,
                color: Color(0xFF667085),
                height: 1.5,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _NewRequestCard extends StatelessWidget {
  final TripRequest request;
  final bool isResponding;

  _NewRequestCard({required this.request, required this.isResponding});

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 20),
      child: Container(
        padding: const EdgeInsets.all(20),
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(24),
          boxShadow: [
            BoxShadow(
              color: Colors.black.withValues(alpha: 0.2),
              blurRadius: 20,
              offset: Offset(0, 10),
            ),
          ],
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Container(
                  padding: const EdgeInsets.all(8),
                  decoration: BoxDecoration(
                    color: Color(0xFF006B70),
                    shape: BoxShape.circle,
                  ),
                  child: Icon(Icons.check, color: Colors.white, size: 20),
                ),
                SizedBox(width: 12),
                Text(
                  context.l10n.newTripAvailable,
                  style: TextStyle(
                    fontSize: 18,
                    fontWeight: FontWeight.bold,
                    color: Colors.black87,
                  ),
                ),
              ],
            ),
            SizedBox(height: 20),
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      context.l10n.expectedIncomeUpper,
                      style: TextStyle(
                        fontSize: 10,
                        color: Colors.grey,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                    Text(
                      _formatCurrency(request.expectedIncome),
                      style: TextStyle(
                        fontSize: 22,
                        fontWeight: FontWeight.bold,
                        color: Color(0xFF006B70),
                      ),
                    ),
                  ],
                ),
                Column(
                  crossAxisAlignment: CrossAxisAlignment.end,
                  children: [
                    Text(
                      context.l10n.pickupCustomerUpper,
                      style: TextStyle(
                        fontSize: 10,
                        color: Colors.grey,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                    Text(
                      '${request.pickupDistance} (${request.pickupTime})',
                      style: TextStyle(
                        fontSize: 14,
                        fontWeight: FontWeight.bold,
                        color: Colors.black87,
                      ),
                    ),
                  ],
                ),
              ],
            ),
            SizedBox(height: 20),
            _AddressItem(
              icon: Icons.radio_button_checked,
              iconColor: Colors.teal,
              label: context.l10n.pickupPointA,
              address: request.pickupAddress,
            ),
            Padding(
              padding: EdgeInsets.only(left: 11),
              child: SizedBox(
                height: 20,
                child: VerticalDivider(
                  width: 2,
                  thickness: 1,
                  color: Colors.grey,
                ),
              ),
            ),
            _AddressItem(
              icon: Icons.location_on,
              iconColor: Colors.red,
              label: context.l10n.destinationPointB,
              address: request.destinationAddress,
            ),
            SizedBox(height: 24),
            Row(
              children: [
                Expanded(
                  child: OutlinedButton(
                    onPressed: isResponding
                        ? null
                        : () => context
                              .read<DriverDashboardProvider>()
                              .declineRequest(),
                    style: OutlinedButton.styleFrom(
                      padding: const EdgeInsets.symmetric(vertical: 16),
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(12),
                      ),
                    ),
                    child: Text(context.l10n.decline),
                  ),
                ),
                SizedBox(width: 16),
                Expanded(
                  child: ElevatedButton(
                    onPressed: isResponding
                        ? null
                        : () => context
                              .read<DriverDashboardProvider>()
                              .acceptRequest(),
                    style: ElevatedButton.styleFrom(
                      backgroundColor: Color(0xFF006B70),
                      foregroundColor: Colors.white,
                      padding: const EdgeInsets.symmetric(vertical: 16),
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(12),
                      ),
                    ),
                    child: Text(
                      isResponding
                          ? context.l10n.processing
                          : context.l10n.accept,
                    ),
                  ),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

class _AddressItem extends StatelessWidget {
  final IconData icon;
  final Color iconColor;
  final String label;
  final String address;

  _AddressItem({
    required this.icon,
    required this.iconColor,
    required this.label,
    required this.address,
  });

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Icon(icon, color: iconColor, size: 22),
        SizedBox(width: 12),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                label,
                style: TextStyle(
                  fontSize: 10,
                  color: Colors.grey,
                  fontWeight: FontWeight.bold,
                ),
              ),
              Text(
                address,
                style: TextStyle(fontSize: 14, color: Colors.black87),
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
              ),
            ],
          ),
        ),
      ],
    );
  }
}

// _BottomNavBar logic removed, now using DriverBottomNavBar widget
