import 'dart:async';
import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:provider/provider.dart';
import '../../../../../core/maps/polyline_decoder.dart';
import '../../../../../core/maps/models/map_models.dart';
import '../../../../../core/maps/widgets/live_trip_map_widget.dart';
import '../../../../../core/services/map_api_service.dart';
import '../../../../../core/services/socket_service.dart';
import '../../../../../dependency_injection/injection.dart';
import '../../../../auth/presentation/providers/auth_provider.dart';
import '../../data/models/booking_catalog.dart';
import '../../data/models/booking_location.dart';
import '../../data/models/booking_response.dart';
import '../providers/booking_provider.dart';
import '../widgets/booking_cancel_flow.dart';

import 'trip_summary_page.dart';

enum TripTrackingState { arriving, inProgress }

class TripTrackingPage extends StatefulWidget {
  const TripTrackingPage({
    super.key,
    required this.state,
    required this.booking,
    required this.pickup,
    this.destination,
    this.vehicle,
    this.onSwitchTab,
  });

  final TripTrackingState state;
  final BookingResponse booking;
  final BookingLocation pickup;
  final BookingLocation? destination;
  final BookingVehicleOption? vehicle;
  final ValueChanged<int>? onSwitchTab;

  @override
  State<TripTrackingPage> createState() => _TripTrackingPageState();
}

class _TripTrackingPageState extends State<TripTrackingPage>
    with TickerProviderStateMixin {
  AppMapController? _mapController;
  late AnimationController _pulseController;
  final SocketService _socketService = getIt<SocketService>();
  final MapApiService _mapApiService = MapApiService();
  final List<AppLatLng> _arrivalRoutePoints = [];
  final List<AppLatLng> _tripRoutePoints = [];
  AppLatLng? _driverPosition;
  double _driverHeading = 0;
  DateTime? _lastCameraFitAt;
  DateTime? _lastArrivalRouteRefreshAt;
  AppLatLng? _lastArrivalRouteRefreshOrigin;
  int? _joinedTripId;
  Timer? _tripStatusPollingTimer;
  bool _summaryOpened = false;
  bool _isCompletingTrip = false;
  bool _arrivalRouteRefreshInProgress = false;
  late String? _currentTripStatus;
  static const _tealColor = Color(0xFF006B70);
  static const double _arrivalRerouteThresholdMeters = 35;
  static const double _arrivalRerouteMinMoveMeters = 80;
  static const Duration _cameraFitInterval = Duration(seconds: 3);
  static const Duration _arrivalRouteRefreshInterval = Duration(seconds: 12);

  TripTrackingState get _trackingState {
    return _currentTripStatus == 'IN_PROGRESS' ||
            _currentTripStatus == 'COMPLETED'
        ? TripTrackingState.inProgress
        : TripTrackingState.arriving;
  }

  @override
  void initState() {
    super.initState();
    _pulseController = AnimationController(
      vsync: this,
      duration: const Duration(seconds: 2),
    )..repeat(reverse: true);
    _currentTripStatus =
        widget.booking.tripStatus ??
        (widget.state == TripTrackingState.inProgress
            ? 'IN_PROGRESS'
            : 'DRIVER_ARRIVING');
    _initializeRoutes();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) return;
      _openSummaryIfCompleted(widget.booking.tripStatus);
      unawaited(_connectTripSocket());
      _startTripStatusPolling();
    });
  }

  @override
  void didUpdateWidget(covariant TripTrackingPage oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.booking.tripStatus != widget.booking.tripStatus) {
      _currentTripStatus = widget.booking.tripStatus ?? _currentTripStatus;
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (!mounted) return;
        _openSummaryIfCompleted(widget.booking.tripStatus);
      });
    }

    if (oldWidget.booking.tripId == null && widget.booking.tripId != null) {
      unawaited(_connectTripSocket());
    }

    if ((_arrivalRoutePoints.isEmpty &&
            widget.booking.arrivalPolyline != null) ||
        (_tripRoutePoints.isEmpty &&
            widget.booking.encodedPolyline.isNotEmpty)) {
      _initializeRoutes();
    }
  }

  @override
  void dispose() {
    _tripStatusPollingTimer?.cancel();
    _pulseController.dispose();
    final joinedTripId = _joinedTripId;
    if (joinedTripId != null) {
      _cleanupSocketHandlers(joinedTripId);
    }
    super.dispose();
  }

  Future<void> _cleanupSocketHandlers(int tripId) async {
    try {
      _socketService.removeDriverLocationUpdatedHandler(
        _driverLocationHandlerKey(tripId),
      );
      _socketService.removeTripStatusChangedHandler(
        _tripStatusHandlerKey(tripId),
      );
      await _socketService.leaveTrip(tripId);
    } catch (e) {
      debugPrint('Tracking: Error during socket cleanup: $e');
    }
  }

  void _initializeRoutes() {
    final arrivalPolyline = widget.booking.arrivalPolyline;
    if (arrivalPolyline != null && arrivalPolyline.isNotEmpty) {
      try {
        final points = decodePolyline(arrivalPolyline);
        if (points.isNotEmpty) {
          _arrivalRoutePoints.clear();
          _arrivalRoutePoints.addAll(points);
        }
      } on FormatException {
        _arrivalRoutePoints.clear();
      }
    }

    if (widget.booking.encodedPolyline.isNotEmpty) {
      try {
        final points = decodePolyline(widget.booking.encodedPolyline);
        if (points.isNotEmpty) {
          _tripRoutePoints.clear();
          _tripRoutePoints.addAll(points);
        }
      } on FormatException {
        _tripRoutePoints.clear();
      }
    }

    if (_arrivalRoutePoints.isNotEmpty && _driverPosition == null) {
      _driverPosition = _arrivalRoutePoints.first;
    } else if (_tripRoutePoints.isNotEmpty && _driverPosition == null) {
      _driverPosition = _tripRoutePoints.first;
    }
  }

  Future<void> _connectTripSocket() async {
    final tripId = widget.booking.tripId;
    final accessToken = context.read<AuthProvider>().token;
    if (tripId == null || accessToken == null || accessToken.isEmpty) {
      return;
    }

    try {
      await _socketService.connect(accessToken);
      if (!mounted) return;
      debugPrint('Tracking: Connected to Socket for Trip $tripId');

      _socketService.onDriverLocationUpdated((update) {
        if (!mounted || update.tripId != tripId) {
          return;
        }

        final rawPosition = AppLatLng(update.latitude, update.longitude);
        setState(() {
          if (_driverPosition != null) {
            _driverHeading = _calculateHeading(_driverPosition!, rawPosition);
          }
          _driverPosition = rawPosition;
        });

        _fitMapToVisibleRoute(throttled: true);
        unawaited(_refreshArrivalRouteIfNeeded(rawPosition));
      }, key: _driverLocationHandlerKey(tripId));

      _socketService.onTripStatusChanged((update) {
        if (!mounted || update.tripId != tripId) {
          return;
        }

        context.read<BookingProvider>().updateActiveTripStatus(
          bookingId: update.bookingId,
          tripStatus: update.tripStatus,
        );
        setState(() {
          _currentTripStatus = update.tripStatus;
        });

        if (_isCompletedStatus(update.tripStatus)) {
          _openSummaryPage();
        } else if (update.tripStatus == 'CANCELLED') {
          _showMessage('Chuyến đi đã được hủy.');
          Navigator.of(context).popUntil((route) => route.isFirst);
        }
      }, key: _tripStatusHandlerKey(tripId));

      await _socketService.joinTrip(tripId);
      debugPrint('Tracking: Joined Trip Group $tripId');
      _joinedTripId = tripId;
    } catch (e) {
      debugPrint('Tracking Error: $e');
      if (mounted) {
        _showMessage('Không thể kết nối theo dõi vị trí tài xế. Đang thử lại...');
        Future.delayed(const Duration(seconds: 3), () {
          if (mounted) _connectTripSocket();
        });
      }
    }
  }

  double _calculateHeading(AppLatLng start, AppLatLng end) {
    final lat1 = start.latitude * (math.pi / 180);
    final lon1 = start.longitude * (math.pi / 180);
    final lat2 = end.latitude * (math.pi / 180);
    final lon2 = end.longitude * (math.pi / 180);

    final dLon = lon2 - lon1;
    final y = math.sin(dLon) * math.cos(lat2);
    final x =
        math.cos(lat1) * math.sin(lat2) -
        math.sin(lat1) * math.cos(lat2) * math.cos(dLon);

    final radians = math.atan2(y, x);
    return (radians * 180 / math.pi + 360) % 360;
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

  int _getArrivalDurationMinutes() {
    if (_arrivalRoutePoints.isEmpty) {
      if (_driverPosition != null) {
        final distanceKm = _calculateDirectDistance(
          _driverPosition!,
          AppLatLng(widget.pickup.latitude, widget.pickup.longitude),
        );
        final minutes = (distanceKm / 25 * 60).round();
        return minutes > 0 ? minutes : 1;
      }
      return 3;
    }

    final driverPos = _driverPosition ?? _arrivalRoutePoints.first;
    int closestIndex = 0;
    double minDistance = double.infinity;
    for (int i = 0; i < _arrivalRoutePoints.length; i++) {
      final dist = _calculateDirectDistance(driverPos, _arrivalRoutePoints[i]);
      if (dist < minDistance) {
        minDistance = dist;
        closestIndex = i;
      }
    }

    double remainingDistanceKm = 0;
    for (int i = closestIndex; i < _arrivalRoutePoints.length - 1; i++) {
      remainingDistanceKm += _calculateDirectDistance(
        _arrivalRoutePoints[i],
        _arrivalRoutePoints[i + 1],
      );
    }

    final minutes = (remainingDistanceKm / 25 * 60).round();
    return minutes > 0 ? minutes : 1;
  }

  int _getTripDurationMinutes() {
    if (_tripRoutePoints.isEmpty) {
      return widget.booking.estimatedDurationMinutes > 0
          ? widget.booking.estimatedDurationMinutes
          : 12;
    }

    final driverPos = _driverPosition ?? _tripRoutePoints.first;
    int closestIndex = 0;
    double minDistance = double.infinity;
    for (int i = 0; i < _tripRoutePoints.length; i++) {
      final dist = _calculateDirectDistance(driverPos, _tripRoutePoints[i]);
      if (dist < minDistance) {
        minDistance = dist;
        closestIndex = i;
      }
    }

    double remainingDistanceKm = 0;
    for (int i = closestIndex; i < _tripRoutePoints.length - 1; i++) {
      remainingDistanceKm += _calculateDirectDistance(
        _tripRoutePoints[i],
        _tripRoutePoints[i + 1],
      );
    }

    if (remainingDistanceKm < 0.05) return 1;

    final minutes = (remainingDistanceKm / 30 * 60).round();
    return minutes > 0 ? minutes : 1;
  }

  void _showMessage(String message) {
    if (!mounted) return;
    ScaffoldMessenger.of(context)
      ..hideCurrentSnackBar()
      ..showSnackBar(
        SnackBar(
          content: Text(message),
          behavior: SnackBarBehavior.floating,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(10),
          ),
          margin: const EdgeInsets.all(16),
        ),
      );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Stack(
        children: [_buildMap(), _buildTopHeader(), _buildBottomPanel()],
      ),
    );
  }

  Widget _buildMap() {
    return LiveTripMapWidget(
      trackingState: _trackingState == TripTrackingState.arriving
          ? LiveTripTrackingState.arriving
          : LiveTripTrackingState.inProgress,
      pickup: AppLatLng(widget.pickup.latitude, widget.pickup.longitude),
      destination: widget.destination != null
          ? AppLatLng(widget.destination!.latitude, widget.destination!.longitude)
          : null,
      arrivalRoutePoints: _arrivalRoutePoints,
      tripRoutePoints: _tripRoutePoints,
      driverPosition: _driverPosition,
      driverHeading: _driverHeading,
      padding: const EdgeInsets.only(
        top: 130,
        bottom: 320,
        left: 24,
        right: 24,
      ),
      onMapCreated: (controller) {
        _mapController = controller;
        _fitMapToVisibleRoute();
      },
    );
  }

  Future<void> _refreshArrivalRouteIfNeeded(AppLatLng rawPosition) async {
    if (_trackingState != TripTrackingState.arriving ||
        _arrivalRouteRefreshInProgress) {
      return;
    }

    final now = DateTime.now();
    final lastRefreshAt = _lastArrivalRouteRefreshAt;
    if (lastRefreshAt != null &&
        now.difference(lastRefreshAt) < _arrivalRouteRefreshInterval) {
      return;
    }

    final lastOrigin = _lastArrivalRouteRefreshOrigin;
    if (lastOrigin != null &&
        _calculateDirectDistance(lastOrigin, rawPosition) * 1000 <
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
      final pickup = AppLatLng(widget.pickup.latitude, widget.pickup.longitude);
      final route = await _mapApiService.estimateRoute(
        rawPosition.latitude,
        rawPosition.longitude,
        pickup.latitude,
        pickup.longitude,
      );
      final points = decodePolyline(route.encodedPolyline);
      if (!mounted ||
          _trackingState != TripTrackingState.arriving ||
          points.length < 2) {
        return;
      }

      setState(() {
        _arrivalRoutePoints
          ..clear()
          ..addAll(points);
        if (_driverPosition != null) {
          _driverHeading = _calculateHeading(_driverPosition!, rawPosition);
        }
        _driverPosition = rawPosition;
      });
    } catch (e) {
      debugPrint('Tracking: Failed to refresh arrival route: $e');
    } finally {
      _arrivalRouteRefreshInProgress = false;
    }
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
    final point = _interpolate(start, end, fraction);
    final distanceMeters = _calculateDirectDistance(target, point) * 1000;

    return _RouteProgress(
      point: point,
      segmentIndex: segmentIndex,
      progress: segmentIndex + fraction,
      distanceMeters: distanceMeters,
    );
  }

  AppLatLng _interpolate(AppLatLng start, AppLatLng end, double fraction) {
    return AppLatLng(
      start.latitude + (end.latitude - start.latitude) * fraction,
      start.longitude + (end.longitude - start.longitude) * fraction,
    );
  }

  void _fitMapToVisibleRoute({bool throttled = false}) {
    if (_mapController == null || !mounted) return;
    if (throttled) {
      final now = DateTime.now();
      final lastFit = _lastCameraFitAt;
      if (lastFit != null && now.difference(lastFit) < _cameraFitInterval) {
        return;
      }
      _lastCameraFitAt = now;
    }

    final driverPosition = _driverPosition;
    final pickupPos = AppLatLng(
      widget.pickup.latitude,
      widget.pickup.longitude,
    );
    final destination = widget.destination;

    List<AppLatLng> focusPoints = [];
    if (_trackingState == TripTrackingState.arriving &&
        driverPosition != null) {
      // Arriving: keep driver + pickup visible
      focusPoints = [driverPosition, pickupPos];
    } else if (_trackingState == TripTrackingState.inProgress &&
        driverPosition != null &&
        destination != null) {
      // InProgress: keep driver + destination visible (tight follow)
      focusPoints = [
        driverPosition,
        AppLatLng(destination.latitude, destination.longitude),
      ];
    } else {
      focusPoints = [pickupPos];
      if (destination != null) {
        focusPoints.add(AppLatLng(destination.latitude, destination.longitude));
      }
      if (driverPosition != null) focusPoints.add(driverPosition);
    }

    if (focusPoints.length < 2) {
      if (focusPoints.length == 1) {
        final p = focusPoints.first;
        // Create a small bounding box around the single point to force bounds padding calculations
        focusPoints = [
          AppLatLng(p.latitude - 0.001, p.longitude - 0.001),
          AppLatLng(p.latitude + 0.001, p.longitude + 0.001),
        ];
      } else {
        return;
      }
    }

    final bounds = _boundsFor(focusPoints);
    final paddingVal = _trackingState == TripTrackingState.inProgress
        ? 60.0
        : 80.0;
    unawaited(
      _mapController!.animateCameraToBounds(
        bounds.$1,
        bounds.$2,
        paddingVal,
        top: 130,
        bottom: 320,
        left: 24,
        right: 24,
      ),
    );
  }

  (AppLatLng, AppLatLng) _boundsFor(List<AppLatLng> points) {
    var minLat = points.first.latitude;
    var maxLat = points.first.latitude;
    var minLng = points.first.longitude;
    var maxLng = points.first.longitude;

    for (final point in points.skip(1)) {
      minLat = math.min(minLat, point.latitude);
      maxLat = math.max(maxLat, point.latitude);
      minLng = math.min(minLng, point.longitude);
      maxLng = math.max(maxLng, point.longitude);
    }

    return (AppLatLng(minLat, minLng), AppLatLng(maxLat, maxLng));
  }

  Widget _buildTopHeader() {
    final bool isArriving = _trackingState == TripTrackingState.arriving;

    return SafeArea(
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
        child: Column(
          children: [
            Row(
              children: [
                _CircleIconButton(
                  icon: Icons.home_rounded,
                  onPressed: () {
                    if (Navigator.of(context).canPop()) {
                      Navigator.of(context).popUntil((route) => route.isFirst);
                    }
                    widget.onSwitchTab?.call(0);
                  },
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Container(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 16,
                      vertical: 12,
                    ),
                    decoration: BoxDecoration(
                      color: Colors.white,
                      borderRadius: BorderRadius.circular(30),
                      boxShadow: [
                        BoxShadow(
                          color: Colors.black.withValues(alpha: 0.12),
                          blurRadius: 16,
                          offset: const Offset(0, 4),
                        ),
                      ],
                    ),
                    child: Row(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        _buildLiveIndicator(),
                        const SizedBox(width: 8),
                        Flexible(
                          child: Text(
                            _currentTripStatus == 'ARRIVED'
                                ? 'Tài xế đã đến điểm đón'
                                : isArriving
                                ? 'Tài xế đang đến • ${_getArrivalDurationMinutes()} phút'
                                : 'Đang di chuyển • ${_getTripDurationMinutes()} phút',
                            style: const TextStyle(
                              fontWeight: FontWeight.w800,
                              fontSize: 13,
                              color: _tealColor,
                            ),
                            overflow: TextOverflow.ellipsis,
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
                const SizedBox(width: 12),
                const Opacity(
                  opacity: 0,
                  child: _CircleIconButton(
                    icon: Icons.arrow_back,
                    onPressed: null,
                  ),
                ),
              ],
            ),
            if (!isArriving && widget.destination != null) ...[
              const SizedBox(height: 12),
              Container(
                padding: const EdgeInsets.symmetric(
                  horizontal: 18,
                  vertical: 10,
                ),
                decoration: BoxDecoration(
                  color: Colors.white,
                  borderRadius: BorderRadius.circular(24),
                  boxShadow: [
                    BoxShadow(
                      color: Colors.black.withValues(alpha: 0.08),
                      blurRadius: 12,
                      offset: const Offset(0, 4),
                    ),
                  ],
                ),
                child: Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    const Icon(
                      Icons.location_on_rounded,
                      color: Colors.redAccent,
                      size: 18,
                    ),
                    const SizedBox(width: 8),
                    Flexible(
                      child: Text(
                        widget.destination!.address,
                        style: const TextStyle(
                          fontWeight: FontWeight.w700,
                          fontSize: 14,
                          color: Color(0xFF1D2939),
                        ),
                        overflow: TextOverflow.ellipsis,
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }

  Widget _buildLiveIndicator() {
    return FadeTransition(
      opacity: _pulseController,
      child: Container(
        width: 10,
        height: 10,
        decoration: const BoxDecoration(
          color: Colors.red,
          shape: BoxShape.circle,
          boxShadow: [
            BoxShadow(color: Colors.redAccent, blurRadius: 4, spreadRadius: 2),
          ],
        ),
      ),
    );
  }

  Widget _buildBottomPanel() {
    final bool isArriving = _trackingState == TripTrackingState.arriving;
    final offer = widget.booking.driverOffer;
    final vehicle = widget.vehicle;
    final plateParts = vehicle?.plateNumber.split('-') ?? const [];

    return Positioned(
      bottom: 0,
      left: 0,
      right: 0,
      child: Container(
        padding: const EdgeInsets.fromLTRB(24, 12, 24, 32),
        decoration: const BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.vertical(top: Radius.circular(32)),
          boxShadow: [
            BoxShadow(
              color: Colors.black12,
              blurRadius: 24,
              offset: Offset(0, -10),
            ),
          ],
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Container(
              width: 40,
              height: 4,
              margin: const EdgeInsets.only(bottom: 24),
              decoration: BoxDecoration(
                color: Colors.grey[200],
                borderRadius: BorderRadius.circular(2),
              ),
            ),
            if (!isArriving) ...[
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  const Row(
                    children: [
                      Icon(
                        Icons.check_circle_rounded,
                        color: _tealColor,
                        size: 22,
                      ),
                      SizedBox(width: 10),
                      Text(
                        'Bạn đang đi đúng lộ trình',
                        style: TextStyle(
                          fontWeight: FontWeight.w800,
                          fontSize: 15,
                          color: Colors.black87,
                        ),
                      ),
                    ],
                  ),
                  _SosButton(
                    onTap: () => _showMessage('Đã gửi tín hiệu SOS khẩn cấp!'),
                  ),
                ],
              ),
              const SizedBox(height: 24),
            ],
            Row(
              children: [
                Stack(
                  children: [
                    CircleAvatar(
                      radius: 30,
                      backgroundColor: Colors.grey[200],
                      backgroundImage: offer?.driverAvatarUrl != null
                          ? NetworkImage(offer!.driverAvatarUrl!)
                          : null,
                      child: offer?.driverAvatarUrl == null
                          ? const Icon(
                              Icons.person,
                              size: 30,
                              color: Colors.grey,
                            )
                          : null,
                    ),
                    Positioned(
                      bottom: 0,
                      right: 0,
                      child: Container(
                        padding: const EdgeInsets.symmetric(
                          horizontal: 6,
                          vertical: 3,
                        ),
                        decoration: BoxDecoration(
                          color: Colors.white,
                          borderRadius: BorderRadius.circular(12),
                          boxShadow: const [
                            BoxShadow(color: Colors.black12, blurRadius: 4),
                          ],
                        ),
                        child: Row(
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            const Icon(
                              Icons.star_rounded,
                              color: Colors.amber,
                              size: 14,
                            ),
                            Text(
                              offer == null ? ' --' : ' ${offer.rating}',
                              style: const TextStyle(
                                fontSize: 11,
                                fontWeight: FontWeight.w800,
                              ),
                            ),
                          ],
                        ),
                      ),
                    ),
                  ],
                ),
                const SizedBox(width: 16),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        offer?.driverName ?? 'Tài xế SafeRide',
                        style: const TextStyle(
                          fontSize: 18,
                          fontWeight: FontWeight.w900,
                          color: Color(0xFF1A1A1A),
                        ),
                      ),
                      const SizedBox(height: 4),
                      Text(
                        vehicle == null
                            ? 'Đang cập nhật xe'
                            : '${vehicle.name} - ${vehicle.color}',
                        style: TextStyle(
                          color: Colors.grey[600],
                          fontSize: 13,
                          fontWeight: FontWeight.w500,
                        ),
                      ),
                    ],
                  ),
                ),
                Container(
                  padding: const EdgeInsets.symmetric(
                    horizontal: 14,
                    vertical: 10,
                  ),
                  decoration: BoxDecoration(
                    color: const Color(0xFFF2F4F7),
                    borderRadius: BorderRadius.circular(12),
                  ),
                  child: Column(
                    children: [
                      Text(
                        plateParts.isNotEmpty ? plateParts.first.trim() : '--',
                        style: const TextStyle(
                          fontSize: 12,
                          fontWeight: FontWeight.w600,
                          color: Color(0xFF667085),
                        ),
                      ),
                      Text(
                        plateParts.length > 1 ? plateParts.last.trim() : '--',
                        style: const TextStyle(
                          fontSize: 18,
                          fontWeight: FontWeight.w900,
                          color: Color(0xFF1D2939),
                        ),
                      ),
                    ],
                  ),
                ),
              ],
            ),
            const SizedBox(height: 24),
            if (isArriving) ...[
              Row(
                children: [
                  Expanded(
                    child: _ActionButton(
                      icon: Icons.chat_bubble_rounded,
                      label: 'Nhắn tin',
                      onPressed: () =>
                          _showMessage('Chức năng nhắn tin đang phát triển'),
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    flex: 2,
                    child: ElevatedButton.icon(
                      onPressed: () => _showMessage('Đang kết nối cuộc gọi...'),
                      icon: const Icon(Icons.phone_in_talk_rounded),
                      label: const Text('Gọi điện'),
                      style: ElevatedButton.styleFrom(
                        backgroundColor: _tealColor,
                        foregroundColor: Colors.white,
                        elevation: 0,
                        padding: const EdgeInsets.symmetric(vertical: 16),
                        shape: RoundedRectangleBorder(
                          borderRadius: BorderRadius.circular(14),
                        ),
                        textStyle: const TextStyle(
                          fontWeight: FontWeight.w900,
                          fontSize: 16,
                        ),
                      ),
                    ),
                  ),
                  const SizedBox(width: 12),
                  _SosButton(
                    isCircle: true,
                    onTap: () => _showMessage('Đã gửi tín hiệu SOS khẩn cấp!'),
                  ),
                ],
              ),
              const SizedBox(height: 16),
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  InkWell(
                    onTap: _showShareModal,
                    borderRadius: BorderRadius.circular(8),
                    child: const Padding(
                      padding: EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                      child: Row(
                        children: [
                          Icon(
                            Icons.share_outlined,
                            color: Colors.black54,
                            size: 20,
                          ),
                          SizedBox(width: 8),
                          Text(
                            'Chia sẻ',
                            style: TextStyle(
                              color: Colors.black87,
                              fontWeight: FontWeight.w600,
                              fontSize: 14,
                            ),
                          ),
                        ],
                      ),
                    ),
                  ),
                  TextButton(
                    onPressed: () =>
                        handleBookingBack(context, booking: widget.booking),
                    child: const Text(
                      'Hủy chuyến',
                      style: TextStyle(
                        color: Color(0xFFE53935),
                        fontWeight: FontWeight.w700,
                        fontSize: 14,
                      ),
                    ),
                  ),
                ],
              ),
            ] else ...[
              Row(
                children: [
                  Expanded(
                    child: _CircleActionButton(
                      icon: Icons.share_rounded,
                      onPressed: _showShareModal,
                      label: 'Chia sẻ',
                    ),
                  ),
                  const SizedBox(width: 16),
                  Expanded(
                    child: _CircleActionButton(
                      icon: Icons.phone_in_talk_rounded,
                      onPressed: () {},
                      label: 'Gọi điện',
                    ),
                  ),
                ],
              ),
              if (_currentTripStatus == 'IN_PROGRESS') ...[
                const SizedBox(height: 16),
                SizedBox(
                  width: double.infinity,
                  child: ElevatedButton.icon(
                    onPressed: _isCompletingTrip ? null : _completeTrip,
                    icon: _isCompletingTrip
                        ? const SizedBox(
                            width: 18,
                            height: 18,
                            child: CircularProgressIndicator(strokeWidth: 2),
                          )
                        : const Icon(Icons.check_circle_rounded),
                    label: Text(
                      _isCompletingTrip
                          ? 'Đang kết thúc...'
                          : 'Kết thúc chuyến',
                    ),
                    style: ElevatedButton.styleFrom(
                      backgroundColor: _tealColor,
                      foregroundColor: Colors.white,
                      padding: const EdgeInsets.symmetric(vertical: 14),
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(14),
                      ),
                    ),
                  ),
                ),
              ],
            ],
          ],
        ),
      ),
    );
  }

  void _showShareModal() {
    showDialog(
      context: context,
      builder: (context) => const Center(child: ShareTripModal()),
    );
  }

  void _startTripStatusPolling() {
    _tripStatusPollingTimer?.cancel();
    _tripStatusPollingTimer = Timer.periodic(const Duration(seconds: 4), (_) {
      unawaited(_refreshTripStatus());
    });
  }

  Future<void> _refreshTripStatus() async {
    if (!mounted || _summaryOpened) return;

    final accessToken = context.read<AuthProvider>().token;
    if (accessToken == null || accessToken.isEmpty) return;

    final booking = await context
        .read<BookingProvider>()
        .refreshActiveBookingDetails(
          accessToken,
          bookingId: widget.booking.bookingId,
        );
    if (!mounted || booking == null) return;

    if (_isCompletedStatus(booking.tripStatus)) {
      await _openSummaryPage(booking);
    } else if (booking.tripStatus == 'CANCELLED' ||
        booking.bookingStatus == 'Cancelled') {
      _showMessage('Chuyến đi đã được hủy.');
      Navigator.of(context).popUntil((route) => route.isFirst);
    } else {
      setState(() {
        _currentTripStatus = booking.tripStatus ?? _currentTripStatus;
      });
    }
  }

  Future<void> _completeTrip() async {
    final tripId = widget.booking.tripId;
    final accessToken = context.read<AuthProvider>().token;
    if (tripId == null || accessToken == null || accessToken.isEmpty) {
      _showMessage('Không thể kết thúc chuyến lúc này.');
      return;
    }

    setState(() => _isCompletingTrip = true);
    final ok = await context.read<BookingProvider>().completeTrip(
      accessToken,
      tripId: tripId,
    );
    if (!mounted) return;

    setState(() => _isCompletingTrip = false);
    if (!ok) {
      _showMessage(
        context.read<BookingProvider>().errorMessage ??
            'Không thể kết thúc chuyến. Vui lòng thử lại.',
      );
      return;
    }

    await _refreshTripStatus();
  }

  void _openSummaryIfCompleted(String? tripStatus) {
    if (_isCompletedStatus(tripStatus)) {
      unawaited(_openSummaryPage());
    }
  }

  Future<void> _openSummaryPage([BookingResponse? completedBooking]) async {
    if (_summaryOpened || !mounted) return;
    _summaryOpened = true;
    _tripStatusPollingTimer?.cancel();

    final bookingProvider = context.read<BookingProvider>();
    final booking =
        completedBooking ?? bookingProvider.activeBooking ?? widget.booking;

    await Navigator.of(context).pushAndRemoveUntil(
      MaterialPageRoute(builder: (_) => TripSummaryPage(booking: booking)),
      (route) => false,
    );
  }

  static String _tripStatusHandlerKey(int tripId) => 'tripTracking:$tripId';
  static String _driverLocationHandlerKey(int tripId) =>
      'tripTrackingLocation:$tripId';
  static bool _isCompletedStatus(String? status) =>
      status == 'COMPLETED' || status == '4';
}

class _RouteProgress {
  final AppLatLng point;
  final int segmentIndex;
  final double progress;
  final double distanceMeters;

  const _RouteProgress({
    required this.point,
    required this.segmentIndex,
    required this.progress,
    this.distanceMeters = 0,
  });
}

class _CircleIconButton extends StatelessWidget {
  final IconData icon;
  final VoidCallback? onPressed;
  const _CircleIconButton({required this.icon, this.onPressed});

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        color: Colors.white,
        shape: BoxShape.circle,
        boxShadow: [
          BoxShadow(
            color: Colors.black.withValues(alpha: 0.08),
            blurRadius: 10,
            offset: const Offset(0, 4),
          ),
        ],
      ),
      child: IconButton(
        icon: Icon(icon, color: Colors.black87),
        onPressed: onPressed,
      ),
    );
  }
}

class _ActionButton extends StatelessWidget {
  final IconData icon;
  final String label;
  final VoidCallback onPressed;
  const _ActionButton({
    required this.icon,
    required this.label,
    required this.onPressed,
  });

  @override
  Widget build(BuildContext context) {
    return OutlinedButton.icon(
      onPressed: onPressed,
      icon: Icon(icon, size: 20),
      label: Text(label),
      style: OutlinedButton.styleFrom(
        foregroundColor: const Color(0xFF1A1A1A),
        side: BorderSide(color: Colors.grey[200]!, width: 1.5),
        padding: const EdgeInsets.symmetric(vertical: 16),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
        textStyle: const TextStyle(fontWeight: FontWeight.w800, fontSize: 14),
      ),
    );
  }
}

class _CircleActionButton extends StatelessWidget {
  final IconData icon;
  final VoidCallback onPressed;
  final String? label;
  const _CircleActionButton({
    required this.icon,
    required this.onPressed,
    this.label,
  });

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onPressed,
      borderRadius: BorderRadius.circular(30),
      child: Container(
        padding: const EdgeInsets.symmetric(vertical: 14),
        decoration: BoxDecoration(
          color: const Color(0xFFEAF4F4),
          borderRadius: BorderRadius.circular(30),
        ),
        child: Row(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Icon(icon, color: const Color(0xFF006B70), size: 22),
            if (label != null) ...[
              const SizedBox(width: 8),
              Text(
                label!,
                style: const TextStyle(
                  color: Color(0xFF006B70),
                  fontWeight: FontWeight.w800,
                  fontSize: 15,
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class _SosButton extends StatelessWidget {
  final bool isCircle;
  final VoidCallback? onTap;
  const _SosButton({this.isCircle = false, this.onTap});

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: onTap,
      child: Container(
        padding: isCircle
            ? const EdgeInsets.all(16)
            : const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
        decoration: BoxDecoration(
          color: const Color(0xFFE53935),
          shape: isCircle ? BoxShape.circle : BoxShape.rectangle,
          borderRadius: isCircle ? null : BorderRadius.circular(20),
          boxShadow: [
            BoxShadow(
              color: const Color(0xFFE53935).withValues(alpha: 0.3),
              blurRadius: 8,
              offset: const Offset(0, 4),
            ),
          ],
        ),
        child: const Text(
          'SOS',
          style: TextStyle(
            color: Colors.white,
            fontWeight: FontWeight.w900,
            fontSize: 12,
          ),
        ),
      ),
    );
  }
}

class ShareTripModal extends StatelessWidget {
  const ShareTripModal({super.key});
  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.transparent,
      child: Container(
        margin: const EdgeInsets.all(28),
        padding: const EdgeInsets.all(28),
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(24),
          boxShadow: [
            BoxShadow(
              color: Colors.black.withValues(alpha: 0.15),
              blurRadius: 30,
              offset: const Offset(0, 10),
            ),
          ],
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Text(
              'Chia sẻ lộ trình',
              style: TextStyle(
                fontSize: 20,
                fontWeight: FontWeight.w900,
                color: Color(0xFF1A1A1A),
              ),
            ),
            const SizedBox(height: 16),
            const Text(
              'Gửi link bên dưới cho người thân để theo dõi chuyến đi của bạn theo thời gian thực.',
              textAlign: TextAlign.center,
              style: TextStyle(
                color: Color(0xFF667085),
                fontSize: 15,
                fontWeight: FontWeight.w500,
                height: 1.5,
              ),
            ),
            const SizedBox(height: 28),
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
              decoration: BoxDecoration(
                color: const Color(0xFFF2F4F7),
                borderRadius: BorderRadius.circular(14),
                border: Border.all(color: const Color(0xFFEAECF0)),
              ),
              child: Row(
                children: [
                  const Expanded(
                    child: Text(
                      'saferide.vn/track/SR94210',
                      style: TextStyle(
                        fontSize: 15,
                        color: Color(0xFF1D2939),
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                  ),
                  const SizedBox(width: 8),
                  InkWell(
                    onTap: () {
                      Clipboard.setData(
                        const ClipboardData(text: 'saferide.vn/track/SR94210'),
                      );
                      ScaffoldMessenger.of(context).showSnackBar(
                        const SnackBar(
                          content: Text('Đã sao chép liên kết'),
                          behavior: SnackBarBehavior.floating,
                        ),
                      );
                    },
                    child: const Icon(
                      Icons.copy_rounded,
                      size: 20,
                      color: Color(0xFF006B70),
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 28),
            SizedBox(
              width: double.infinity,
              child: ElevatedButton(
                onPressed: () => Navigator.pop(context),
                style: ElevatedButton.styleFrom(
                  backgroundColor: const Color(0xFF006B70),
                  foregroundColor: Colors.white,
                  elevation: 0,
                  padding: const EdgeInsets.symmetric(vertical: 18),
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(30),
                  ),
                ),
                child: const Text(
                  'Đóng',
                  style: TextStyle(fontWeight: FontWeight.w900, fontSize: 16),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
