import 'dart:async';
import 'dart:math' as math;
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:geolocator/geolocator.dart';

import '../../../../../core/maps/models/map_models.dart';
import '../../../../../core/maps/polyline_decoder.dart';
import '../../../../../core/maps/widgets/map_renderer_widget.dart';
import '../../../../../core/maps/widgets/live_trip_map_widget.dart';
import '../../../../../core/services/location_service.dart';
import '../../../../../core/services/map_api_service.dart';
import '../../../../../core/widgets/current_location_button.dart';
import '../../../../../dependency_injection/injection.dart';

import '../providers/driver_dashboard_provider.dart';
import '../widgets/driver_bottom_nav_bar.dart';
import '../../../../../core/constants/app_strings.dart';
import '../../../../customer/booking/presentation/providers/booking_provider.dart';
import '../../../../customer/home/presentation/pages/customer_home_page.dart';
import '../../../../shared/history/presentation/pages/history_page.dart';
import '../../../../shared/onboarding/presentation/providers/role_provider.dart';
import '../../../../auth/presentation/providers/auth_provider.dart';
import '../../../../shared/profile/presentation/pages/profile_page.dart';
import 'driver_trip_payment_page.dart';
import 'driver_return_evidence_page.dart';

class DriverDashboardPage extends StatefulWidget {
  const DriverDashboardPage({super.key});

  @override
  State<DriverDashboardPage> createState() => _DriverDashboardPageState();
}

class _RouteProgress {
  const _RouteProgress({
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
  AppLatLng? _driverPosition;
  AppLatLng? _lastReportedPosition;
  DateTime? _lastReportedTime;
  int _locationSequence = 0;
  double _driverHeading = 0;
  final List<AppLatLng> _arrivalRoutePoints = [];
  final List<AppLatLng> _tripRoutePoints = [];
  final MapApiService _mapApiService = MapApiService();
  DateTime? _lastArrivalRouteRefreshAt;
  AppLatLng? _lastArrivalRouteRefreshOrigin;
  int? _renderedRouteTripId;
  int? _openingPaymentTripId;
  bool _arrivalRouteRefreshInProgress = false;
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
          content: Text('Không thể lấy vị trí hiện tại: ${e.toString()}'),
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
    WidgetsBinding.instance.addPostFrameCallback((_) {
      final token = context.read<AuthProvider>().token;
      _provider = context.read<DriverDashboardProvider>();
      if (token != null) {
        _provider.initializeRealtime(token);
      }
      _checkActiveCustomerBooking();
      _provider.addListener(_onProviderUpdated);
      _onProviderUpdated();
    });
  }

  void _onProviderUpdated() {
    if (!mounted) return;

    final tripAwaitingPaymentId = _provider.takeTripAwaitingPayment();
    if (tripAwaitingPaymentId != null) {
      _openTripPayment(tripAwaitingPaymentId);
    }

    final activeTrip = _provider.activeTrip;

    if (activeTrip != null && _positionStream == null) {
      _startLocationUpdates();
    }

    if (activeTrip == null) {
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
    _provider.removeListener(_onProviderUpdated);
    _stopLocationUpdates();
    super.dispose();
  }

  void _startLocationUpdates() {
    _stopLocationUpdates();
    if (_provider.isDemoMode) return;
    _positionStream =
        Geolocator.getPositionStream(
          locationSettings: const LocationSettings(
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
          content: Text(
            'Không thể lấy vị trí hiện tại hoặc không thể online: ${e.toString()}',
          ),
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

  void _checkActiveCustomerBooking() {
    final bookingProvider = context.read<BookingProvider>();
    final roleProvider = context.read<RoleProvider>();

    if (bookingProvider.activeBooking != null) {
      debugPrint(
        'DRIVER_DASHBOARD: Active customer booking detected. Forcing switch to customer mode.',
      );
      roleProvider.setRole(AppValues.roleCustomer);
      Navigator.of(context).pushAndRemoveUntil(
        MaterialPageRoute(builder: (_) => const CustomerHomePage()),
        (route) => false,
      );
    }
  }

  void _openTripPayment(int tripId) {
    if (_openingPaymentTripId == tripId) return;
    _openingPaymentTripId = tripId;
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) {
        return;
      }

      Navigator.of(context)
          .push<bool>(
            MaterialPageRoute(
              builder: (_) => DriverTripPaymentPage(tripId: tripId),
            ),
          )
          .then((completed) async {
            if (mounted && completed == true) {
              _provider.markTripPaymentCompleted(tripId);
              await _provider.loadActiveTrip();
            }
          })
          .whenComplete(() {
            if (mounted && _openingPaymentTripId == tripId) {
              _openingPaymentTripId = null;
            }
          });
    });
  }

  @override
  Widget build(BuildContext context) {
    final List<Widget> pages = [
      _buildHomeContent(),
      const HistoryPage(),
      const Center(child: Text('Wallet Page')),
      const ProfilePage(),
    ];

    return Scaffold(
      body: IndexedStack(index: _selectedIndex, children: pages),
      bottomNavigationBar: DriverBottomNavBar(
        currentIndex: _selectedIndex,
        onTap: (index) => setState(() => _selectedIndex = index),
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

        // 2. Top Bar (Income & Drawer/Notification)
        SafeArea(
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
            child: Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                _CircleIconButton(icon: Icons.menu, onPressed: () {}),
                _IncomeHeader(),
                Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    if (const bool.fromEnvironment('dart.vm.product') == false)
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
                                const SnackBar(
                                  content: Text(
                                    'Đã bật mô phỏng GPS (Backend)',
                                  ),
                                ),
                              );
                            } else {
                              _startLocationUpdates();
                              ScaffoldMessenger.of(context).showSnackBar(
                                const SnackBar(
                                  content: Text(
                                    'Đã tắt mô phỏng GPS, dùng GPS thật',
                                  ),
                                ),
                              );
                            }
                          },
                          tooltip: 'Demo GPS Mode',
                        ),
                      ),
                    _CircleIconButton(
                      icon: Icons.notifications_none_rounded,
                      hasBadge: true,
                      onPressed: () {},
                    ),
                  ],
                ),
              ],
            ),
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
                  bool isResponding,
                  bool isUpdatingTrip,
                  bool isWaitingForCustomerConfirmation,
                })
              >(
                selector: (_, provider) => (
                  activeTrip: provider.activeTrip,
                  currentRequest: provider.currentRequest,
                  errorMessage: provider.errorMessage,
                  hasNewRequest: provider.hasNewRequest,
                  isLoadingActiveTrip: provider.isLoadingActiveTrip,
                  isResponding: provider.isResponding,
                  isUpdatingTrip: provider.isUpdatingTrip,
                  isWaitingForCustomerConfirmation:
                      provider.isWaitingForCustomerConfirmation,
                ),
                builder: (context, state, child) {
                  if (state.isLoadingActiveTrip) {
                    return const Padding(
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

                  if (state.activeTrip != null) {
                    return _ActiveTripCard(
                      trip: state.activeTrip!,
                      isUpdating: state.isUpdatingTrip,
                    );
                  }
                  if (state.hasNewRequest && state.currentRequest != null) {
                    return _NewRequestCard(
                      request: state.currentRequest!,
                      isResponding: state.isResponding,
                    );
                  }
                  if (state.isWaitingForCustomerConfirmation) {
                    return const _WaitingCustomerConfirmationCard();
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

              const SizedBox(height: 16),
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

  const _ErrorLoadingActiveTripCard({
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
              offset: const Offset(0, 10),
            ),
          ],
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.cloud_off_rounded, size: 48, color: Colors.grey),
            const SizedBox(height: 12),
            const Text(
              'Lỗi kết nối máy chủ',
              style: TextStyle(
                fontSize: 16,
                fontWeight: FontWeight.bold,
                color: Color(0xFF1A1A1A),
              ),
            ),
            const SizedBox(height: 8),
            Text(
              errorMessage,
              textAlign: TextAlign.center,
              style: const TextStyle(fontSize: 14, color: Colors.black54),
            ),
            const SizedBox(height: 20),
            SizedBox(
              width: double.infinity,
              child: ElevatedButton.icon(
                onPressed: onRetry,
                icon: const Icon(Icons.refresh_rounded),
                label: const Text(
                  'Thử lại',
                  style: TextStyle(fontWeight: FontWeight.bold),
                ),
                style: ElevatedButton.styleFrom(
                  backgroundColor: const Color(0xFF006B70),
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
  const _ActiveTripCard({required this.trip, required this.isUpdating});

  final ActiveDriverTrip trip;
  final bool isUpdating;

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
              offset: const Offset(0, 10),
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
                  decoration: const BoxDecoration(
                    color: Color(0xFFE8F2F2),
                    shape: BoxShape.circle,
                  ),
                  child: const Icon(
                    Icons.route_rounded,
                    color: Color(0xFF006B70),
                    size: 22,
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const Text(
                        'Chuyến đang thực hiện',
                        style: TextStyle(
                          fontSize: 18,
                          fontWeight: FontWeight.bold,
                          color: Colors.black87,
                        ),
                      ),
                      const SizedBox(height: 4),
                      Text(
                        _statusLabel(status),
                        style: const TextStyle(
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
            const SizedBox(height: 20),
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
                  icon: const Icon(Icons.navigation_rounded),
                  label: Text(isUpdating ? 'Đang xử lý...' : 'Bắt đầu đến đón'),
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
                        icon: const Icon(Icons.close_rounded),
                        label: const Text('Hủy chuyến'),
                        style: OutlinedButton.styleFrom(
                          foregroundColor: const Color(0xFFE53935),
                          side: const BorderSide(color: Color(0xFFE53935)),
                          padding: const EdgeInsets.symmetric(vertical: 14),
                          shape: RoundedRectangleBorder(
                            borderRadius: BorderRadius.circular(12),
                          ),
                        ),
                      ),
                    ),
                    const SizedBox(width: 12),
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
                      icon: const Icon(Icons.flag_rounded),
                      label: const Text('Đã tới đón'),
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
                        icon: const Icon(Icons.close_rounded),
                        label: const Text('Hủy chuyến'),
                        style: OutlinedButton.styleFrom(
                          foregroundColor: const Color(0xFFE53935),
                          side: const BorderSide(color: Color(0xFFE53935)),
                          padding: const EdgeInsets.symmetric(vertical: 14),
                          shape: RoundedRectangleBorder(
                            borderRadius: BorderRadius.circular(12),
                          ),
                        ),
                      ),
                    ),
                    const SizedBox(width: 12),
                  ],
                  Expanded(
                    child: ElevatedButton.icon(
                      onPressed: isUpdating
                          ? null
                          : () => _runTripAction(
                              context,
                              () => context
                                  .read<DriverDashboardProvider>()
                                  .startTrip(),
                            ),
                      icon: const Icon(Icons.play_arrow_rounded),
                      label: const Text('Bắt đầu chuyến'),
                      style: _primaryButtonStyle(),
                    ),
                  ),
                ],
              )
            else if (status == 'IN_PROGRESS')
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
                    isUpdating
                        ? 'Đang xử lý...'
                        : DriverReturnEvidenceStrings.endTripButton,
                  ),
                  style: _primaryButtonStyle(),
                ),
              )
            else if (isWaitingReturn)
              _buildWaitingReturnSection(context, trip.tripId, isUpdating)
            else if (isReturnConfirmed)
              _buildReturnConfirmedBanner()
            else if (isWaitingPayment)
              _buildWaitingPaymentSection(context, trip.tripId),
          ],
        ),
      ),
    );
  }

  static ButtonStyle _primaryButtonStyle() {
    return ElevatedButton.styleFrom(
      backgroundColor: const Color(0xFF006B70),
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
            color: const Color(0xFFFFF8E1),
            borderRadius: BorderRadius.circular(14),
            border: Border.all(
              color: const Color(0xFFFFCC02).withValues(alpha: 0.5),
            ),
          ),
          child: const Row(
            children: [
              Icon(
                Icons.hourglass_top_rounded,
                color: Color(0xFFF9A825),
                size: 20,
              ),
              SizedBox(width: 10),
              Expanded(
                child: Text(
                  'Đang chờ khách xác nhận trả xe.\nNếu khách không phản hồi, bạn có thể xác nhận thay.',
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
        const SizedBox(height: 14),

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
            icon: const Icon(Icons.add_photo_alternate_rounded),
            label: const Text('Xác nhận thay bằng ảnh bằng chứng'),
            style: OutlinedButton.styleFrom(
              foregroundColor: const Color(0xFF006B70),
              side: const BorderSide(color: Color(0xFF006B70), width: 1.5),
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

  static Widget _buildReturnConfirmedBanner() {
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: const Color(0xFFE8F7F0),
        borderRadius: BorderRadius.circular(14),
        border: Border.all(
          color: const Color(0xFF0A8F62).withValues(alpha: 0.3),
        ),
      ),
      child: const Row(
        children: [
          Icon(Icons.check_circle_rounded, color: Color(0xFF0A8F62), size: 22),
          SizedBox(width: 12),
          Expanded(
            child: Text(
              'Đã xác nhận trả xe. Đang hoàn tất chuyến đi...',
              style: TextStyle(
                color: Color(0xFF0A5C3E),
                fontSize: 14,
                fontWeight: FontWeight.w600,
              ),
            ),
          ),
        ],
      ),
    );
  }

  static Widget _buildWaitingPaymentSection(BuildContext context, int tripId) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Container(
          padding: const EdgeInsets.all(16),
          decoration: BoxDecoration(
            color: const Color(0xFFE8F2F2),
            borderRadius: BorderRadius.circular(14),
            border: Border.all(
              color: const Color(0xFF006B70).withValues(alpha: 0.3),
            ),
          ),
          child: const Row(
            children: [
              Icon(Icons.payments_rounded, color: Color(0xFF006B70), size: 22),
              SizedBox(width: 12),
              Expanded(
                child: Text(
                  'Đã xác nhận trả xe. Vui lòng xác nhận thanh toán để hoàn tất chuyến đi.',
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
        const SizedBox(height: 14),
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
            icon: const Icon(Icons.receipt_long_rounded),
            label: const Text('Xác nhận thanh toán'),
            style: _primaryButtonStyle(),
          ),
        ),
      ],
    );
  }

  static String _statusLabel(String status) {
    return switch (status) {
      'ACCEPTED' => 'Đã nhận chuyến',
      'DRIVER_ARRIVING' => 'Đang đến điểm đón',
      'ARRIVED' => 'Đã tới điểm đón',
      'IN_PROGRESS' => 'Đang thực hiện chuyến',
      'WAITING_RETURN_CONFIRM' =>
        DriverReturnEvidenceStrings.waitingReturnLabel,
      'RETURN_CONFIRMED' => DriverReturnEvidenceStrings.returnConfirmedLabel,
      'WAITING_PAYMENT' => 'Chờ thanh toán',
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
      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(
          const SnackBar(
            content: Text('Không thể cập nhật trạng thái chuyến.'),
          ),
        );
    }
  }
}

class _CircleIconButton extends StatelessWidget {
  final IconData icon;
  final VoidCallback onPressed;
  final bool hasBadge;

  const _CircleIconButton({
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
            offset: const Offset(0, 2),
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
                decoration: const BoxDecoration(
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
      ({double todayIncome, int todayTrips})
    >(
      selector: (_, provider) =>
          (todayIncome: provider.todayIncome, todayTrips: provider.todayTrips),
      builder: (context, summary, child) {
        return Container(
          padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 8),
          decoration: BoxDecoration(
            color: Colors.white,
            borderRadius: BorderRadius.circular(30),
            boxShadow: [
              BoxShadow(
                color: Colors.black.withValues(alpha: 0.1),
                blurRadius: 10,
                offset: const Offset(0, 4),
              ),
            ],
          ),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Text(
                'THU NHẬP HÔM NAY',
                style: TextStyle(
                  fontSize: 10,
                  fontWeight: FontWeight.bold,
                  color: Colors.grey,
                ),
              ),
              Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text(
                    '${summary.todayIncome.toInt().toString().replaceAllMapped(RegExp(r"(\d{3})(?=\d)"), (m) => "${m[1]},")}đ',
                    style: const TextStyle(
                      fontSize: 18,
                      fontWeight: FontWeight.bold,
                      color: Color(0xFF006B70),
                    ),
                  ),
                  const SizedBox(width: 8),
                  Container(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 6,
                      vertical: 2,
                    ),
                    decoration: BoxDecoration(
                      color: const Color(0xFFE8F2F2),
                      borderRadius: BorderRadius.circular(10),
                    ),
                    child: Text(
                      '${summary.todayTrips} chuyến',
                      style: const TextStyle(
                        fontSize: 10,
                        fontWeight: FontWeight.bold,
                        color: Color(0xFF006B70),
                      ),
                    ),
                  ),
                ],
              ),
            ],
          ),
        );
      },
    );
  }
}

class _StatusToggle extends StatefulWidget {
  final Future<void> Function() onGoOnline;
  final Future<void> Function() onGoOffline;

  const _StatusToggle({required this.onGoOnline, required this.onGoOffline});

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
                  offset: const Offset(0, 4),
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
                                ? const Color(0xFF006B70)
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
                                    decoration: const BoxDecoration(
                                      color: Colors.cyanAccent,
                                      shape: BoxShape.circle,
                                    ),
                                  ),
                                  const SizedBox(width: 8),
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
                  const Center(
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
  const _WaitingCustomerConfirmationCard();

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
              offset: const Offset(0, 8),
            ),
          ],
        ),
        child: const Column(
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
              'Đang đợi xác nhận',
              style: TextStyle(
                fontSize: 20,
                fontWeight: FontWeight.bold,
                color: Color(0xFF1D2939),
              ),
            ),
            SizedBox(height: 8),
            Text(
              'Đang đợi khách hàng xác nhận tài xế. Vui lòng không tắt ứng dụng.',
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

  const _NewRequestCard({required this.request, required this.isResponding});

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
              offset: const Offset(0, 10),
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
                  decoration: const BoxDecoration(
                    color: Color(0xFF006B70),
                    shape: BoxShape.circle,
                  ),
                  child: const Icon(Icons.check, color: Colors.white, size: 20),
                ),
                const SizedBox(width: 12),
                const Text(
                  'Bạn đã có chuyến mới!',
                  style: TextStyle(
                    fontSize: 18,
                    fontWeight: FontWeight.bold,
                    color: Colors.black87,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 20),
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Text(
                      'THU NHẬP DỰ KIẾN',
                      style: TextStyle(
                        fontSize: 10,
                        color: Colors.grey,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                    Text(
                      '${request.expectedIncome.toInt().toString().replaceAllMapped(RegExp(r"(\d{3})(?=\d)"), (m) => "${m[1]},")}đ',
                      style: const TextStyle(
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
                    const Text(
                      'ĐÓN KHÁCH',
                      style: TextStyle(
                        fontSize: 10,
                        color: Colors.grey,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                    Text(
                      '${request.pickupDistance} (${request.pickupTime})',
                      style: const TextStyle(
                        fontSize: 14,
                        fontWeight: FontWeight.bold,
                        color: Colors.black87,
                      ),
                    ),
                  ],
                ),
              ],
            ),
            const SizedBox(height: 20),
            _AddressItem(
              icon: Icons.radio_button_checked,
              iconColor: Colors.teal,
              label: 'Điểm đón (A)',
              address: request.pickupAddress,
            ),
            const Padding(
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
              label: 'Điểm đến (B)',
              address: request.destinationAddress,
            ),
            const SizedBox(height: 24),
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
                    child: const Text('Từ chối'),
                  ),
                ),
                const SizedBox(width: 16),
                Expanded(
                  child: ElevatedButton(
                    onPressed: isResponding
                        ? null
                        : () => context
                              .read<DriverDashboardProvider>()
                              .acceptRequest(),
                    style: ElevatedButton.styleFrom(
                      backgroundColor: const Color(0xFF006B70),
                      foregroundColor: Colors.white,
                      padding: const EdgeInsets.symmetric(vertical: 16),
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(12),
                      ),
                    ),
                    child: Text(isResponding ? 'Đang xử lý...' : 'Chấp nhận'),
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

  const _AddressItem({
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
        const SizedBox(width: 12),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                label,
                style: const TextStyle(
                  fontSize: 10,
                  color: Colors.grey,
                  fontWeight: FontWeight.bold,
                ),
              ),
              Text(
                address,
                style: const TextStyle(fontSize: 14, color: Colors.black87),
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
