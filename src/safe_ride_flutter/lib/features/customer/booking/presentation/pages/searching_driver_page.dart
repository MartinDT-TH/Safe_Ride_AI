import 'dart:async';
import 'dart:math' as math;
import 'package:flutter/material.dart';
import 'package:google_maps_flutter/google_maps_flutter.dart';
import 'package:provider/provider.dart';
import '../../../../../core/constants/app_strings.dart';
import '../../../../../core/maps/polyline_decoder.dart';
import '../../../../auth/presentation/providers/auth_provider.dart';
import '../../data/models/booking_catalog.dart';
import '../../data/models/booking_fare_estimate.dart';
import '../../data/models/booking_location.dart';
import '../../data/models/booking_response.dart';
import '../providers/booking_provider.dart';
import '../widgets/booking_cancel_flow.dart';
import 'driver_profile_page.dart';
import 'trip_tracking_page.dart';

class SearchingDriverPage extends StatefulWidget {
  const SearchingDriverPage({
    super.key,
    required this.pickup,
    this.booking,
    this.destination,
    this.fareEstimate,
    this.vehicle,
  });

  final BookingResponse? booking;
  final BookingLocation pickup;
  final BookingLocation? destination;
  final BookingFareEstimate? fareEstimate;
  final BookingVehicleOption? vehicle;

  @override
  State<SearchingDriverPage> createState() => _SearchingDriverPageState();
}

class _SearchingDriverPageState extends State<SearchingDriverPage> {
  GoogleMapController? _controller;
  static const _tealColor = Color(0xFF006B70);
  Offset? _markerScreenOffset;
  StreamSubscription? _nearbyDriversSubscription;
  StreamSubscription? _bookingStatusSubscription;
  bool _didLeaveSearch = false;

  List<LatLng> _cachedPoints = const [];
  String? _lastEncodedPolyline;

  List<LatLng> get _routePoints {
    final encoded = widget.fareEstimate?.encodedPolyline;
    if (encoded == null || encoded.isEmpty) return const [];

    if (encoded == _lastEncodedPolyline) {
      return _cachedPoints;
    }

    try {
      _lastEncodedPolyline = encoded;
      _cachedPoints = decodePolyline(encoded);
      return _cachedPoints;
    } on FormatException {
      return const [];
    }
  }

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      context.read<BookingProvider>().setSearchingBooking(widget.booking);
    });
    _startPolling();
  }

  @override
  void dispose() {
    _nearbyDriversSubscription?.cancel();
    _bookingStatusSubscription?.cancel();
    _controller?.dispose();
    super.dispose();
  }

  void _onMapCreated(GoogleMapController controller) {
    _controller = controller;
    _fitRoute();
    // Delay slightly to ensure map is fully rendered before getting coordinates
    Future.delayed(const Duration(milliseconds: 300), _updateMarkerOffset);
    _fetchNearbyDrivers();
  }

  void _startPolling() {
    // Refresh nearby drivers every 5 seconds while on this page for better real-time feel
    _nearbyDriversSubscription = Stream.periodic(const Duration(seconds: 5))
        .listen((_) {
          if (mounted) _fetchNearbyDrivers();
        });

    // Refresh booking status every 3 seconds to check for driver offers
    _bookingStatusSubscription = Stream.periodic(const Duration(seconds: 3))
        .listen((_) {
          if (mounted) _refreshBookingStatus();
        });
  }

  Future<void> _refreshBookingStatus() async {
    final auth = context.read<AuthProvider>();
    final bookingProvider = context.read<BookingProvider>();
    final token = auth.token;
    final bookingId =
        bookingProvider.searchingBooking?.bookingId ??
        widget.booking?.bookingId;

    if (token != null && bookingId != null) {
      try {
        final booking = await bookingProvider.refreshSearchingBooking(
          token,
          bookingId: bookingId,
        );

        if (!mounted || booking == null || _didLeaveSearch) {
          return;
        }

        if (booking.bookingStatus == 'DriverAssigned' &&
            booking.tripStatus != null &&
            booking.driverOffer != null) {
          _didLeaveSearch = true;
          _nearbyDriversSubscription?.cancel();
          _bookingStatusSubscription?.cancel();
          bookingProvider.setActiveBooking(
            booking: booking,
            pickup: booking.pickup ?? widget.pickup,
            destination: booking.destination ?? widget.destination,
            vehicle: booking.vehicle ?? widget.vehicle,
          );
          await Navigator.of(context).pushReplacement(
            MaterialPageRoute(
              builder: (_) => TripTrackingPage(
                state: booking.tripStatus == 'IN_PROGRESS'
                    ? TripTrackingState.inProgress
                    : TripTrackingState.arriving,
                booking: booking,
                pickup: booking.pickup ?? widget.pickup,
                destination: booking.destination ?? widget.destination,
                vehicle: booking.vehicle ?? widget.vehicle,
              ),
            ),
          );
          return;
        }

        if (booking.bookingStatus == 'Cancelled' ||
            booking.bookingStatus == 'Expired' ||
            booking.bookingStatus == 'Completed') {
          _didLeaveSearch = true;
          _nearbyDriversSubscription?.cancel();
          _bookingStatusSubscription?.cancel();
          bookingProvider.setSearchingBooking(null);
          bookingProvider.clearActiveBooking();
          if (mounted) {
            ScaffoldMessenger.of(context).showSnackBar(
              SnackBar(
                content: Text('Chuyến #${booking.bookingId} đã kết thúc.'),
              ),
            );
            // Safety check: ensure we land on Home, not Login
            Navigator.of(context).popUntil((route) => route.isFirst);
          }
        }
      } catch (e) {
        debugPrint('ERROR in _refreshBookingStatus: $e');
        // Don't pop to login on polling error
      }
    }
  }

  void _fetchNearbyDrivers() {
    final auth = context.read<AuthProvider>();
    final booking = context.read<BookingProvider>();
    final token = auth.token;
    if (token != null) {
      debugPrint(
        'Fetching nearby drivers for: ${widget.pickup.latitude}, ${widget.pickup.longitude}',
      );
      booking.fetchNearbyDrivers(
        token,
        latitude: widget.pickup.latitude,
        longitude: widget.pickup.longitude,
      );
    } else {
      debugPrint('Nearby drivers fetch skipped: Token is null');
    }
  }

  Future<void> _updateMarkerOffset() async {
    if (_controller == null) return;
    try {
      final pos = LatLng(widget.pickup.latitude, widget.pickup.longitude);
      final screenPos = await _controller!.getScreenCoordinate(pos);
      if (mounted) {
        setState(() {
          _markerScreenOffset = Offset(
            screenPos.x.toDouble(),
            screenPos.y.toDouble(),
          );
        });
      }
    } catch (e) {
      debugPrint('Error updating marker offset: $e');
    }
  }

  Future<void> _fitRoute() async {
    final controller = _controller;
    if (controller == null) return;

    final pickup = LatLng(widget.pickup.latitude, widget.pickup.longitude);
    final points = <LatLng>[pickup];

    if (widget.destination != null) {
      points.add(
        LatLng(widget.destination!.latitude, widget.destination!.longitude),
      );
    }

    if (_routePoints.isNotEmpty) {
      points.addAll(_routePoints);
    }

    if (points.length == 1) {
      await controller.animateCamera(CameraUpdate.newLatLngZoom(pickup, 15));
      return;
    }

    var minLat = points.first.latitude;
    var maxLat = points.first.latitude;
    var minLng = points.first.longitude;
    var maxLng = points.first.longitude;

    for (final p in points) {
      minLat = math.min(minLat, p.latitude);
      maxLat = math.max(maxLat, p.latitude);
      minLng = math.min(minLng, p.longitude);
      maxLng = math.max(maxLng, p.longitude);
    }

    await controller.animateCamera(
      CameraUpdate.newLatLngBounds(
        LatLngBounds(
          southwest: LatLng(minLat, minLng),
          northeast: LatLng(maxLat, maxLng),
        ),
        100,
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final pickupPos = LatLng(widget.pickup.latitude, widget.pickup.longitude);
    final destPos = widget.destination != null
        ? LatLng(widget.destination!.latitude, widget.destination!.longitude)
        : null;

    final bookingProvider = context.watch<BookingProvider>();
    final nearbyDrivers = bookingProvider.nearbyDrivers;
    final currentBooking = bookingProvider.searchingBooking ?? widget.booking;

    return PopScope(
      canPop: false,
      onPopInvokedWithResult: (didPop, _) async {
        if (didPop) return;
        await handleBookingBack(context, booking: currentBooking);
      },
      child: Scaffold(
        body: Stack(
          children: [
            GoogleMap(
              initialCameraPosition: CameraPosition(
                target: pickupPos,
                zoom: 15,
              ),
              onMapCreated: _onMapCreated,
              // Update on every camera move to keep radar attached
              onCameraMove: (_) => _updateMarkerOffset(),
              onCameraIdle: _updateMarkerOffset,
              markers: {
                Marker(
                  markerId: const MarkerId('pickup'),
                  position: pickupPos,
                  // Using a more visible anchor for the center of the radar
                  anchor: const Offset(0.5, 0.5),
                  icon: BitmapDescriptor.defaultMarkerWithHue(
                    BitmapDescriptor.hueAzure,
                  ),
                ),
                if (destPos != null)
                  Marker(
                    markerId: const MarkerId('destination'),
                    position: destPos,
                    icon: BitmapDescriptor.defaultMarkerWithHue(
                      BitmapDescriptor.hueRed,
                    ),
                  ),
                ...nearbyDrivers.map(
                  (driver) => Marker(
                    markerId: MarkerId('driver_${driver.driverId}'),
                    position: LatLng(driver.latitude, driver.longitude),
                    icon: BitmapDescriptor.defaultMarkerWithHue(
                      BitmapDescriptor.hueOrange,
                    ),
                    anchor: const Offset(0.5, 0.5),
                  ),
                ),
              },
              polylines: {
                if (_routePoints.isNotEmpty)
                  Polyline(
                    polylineId: const PolylineId('route'),
                    points: _routePoints,
                    color: _tealColor,
                    width: 5,
                    jointType: JointType.round,
                    startCap: Cap.roundCap,
                    endCap: Cap.roundCap,
                    zIndex: 1,
                  ),
                if (destPos != null && _routePoints.isEmpty)
                  Polyline(
                    polylineId: const PolylineId('direct_route'),
                    points: [pickupPos, destPos],
                    color: _tealColor.withOpacity(0.5),
                    width: 4,
                    patterns: [PatternItem.dash(20), PatternItem.gap(10)],
                  ),
              },
              zoomControlsEnabled: false,
              myLocationButtonEnabled: false,
              compassEnabled: false,
            ),

            // Radar Scanner Overlay positioned over the pickup marker center
            if (_markerScreenOffset != null)
              Positioned(
                // Offset calculation: screen coordinate - (radar size / 2)
                left: _markerScreenOffset!.dx - 100,
                top: _markerScreenOffset!.dy - 100,
                child: const IgnorePointer(child: _RadarScanner(size: 200)),
              ),

            // Navigation and Info Panel
            // Note: Keep the panel at the top level of the stack to ensure clicks work
            Positioned.fill(
              child: SafeArea(
                child: Column(
                  children: [
                    Padding(
                      padding: const EdgeInsets.all(16),
                      child: Row(
                        children: [
                          CircleAvatar(
                            backgroundColor: Colors.white,
                            child: IconButton(
                              icon: const Icon(
                                Icons.arrow_back,
                                color: Colors.black,
                              ),
                              onPressed: () => handleBookingBack(
                                context,
                                booking: currentBooking,
                              ),
                            ),
                          ),
                        ],
                      ),
                    ),
                    const Spacer(),
                    _SearchingPanel(
                      booking: currentBooking,
                      vehicle: widget.vehicle ?? currentBooking?.vehicle,
                      fareEstimate: widget.fareEstimate,
                      pickupAddress:
                          currentBooking?.pickup?.address ??
                          widget.pickup.address,
                      onDriverPreviewTap: _shouldShowDriverCard(currentBooking)
                          ? () {
                              final offer = currentBooking!.driverOffer!;
                              Navigator.of(context).push(
                                MaterialPageRoute(
                                  builder: (_) => DriverProfilePage(
                                    name: offer.driverName,
                                    avatarUrl: offer.driverAvatarUrl,
                                    rating: offer.rating,
                                    tripCount: offer.tripCount,
                                    experienceYears: offer.experienceYears,
                                    booking: currentBooking,
                                    pickup:
                                        currentBooking.pickup ?? widget.pickup,
                                    destination:
                                        currentBooking.destination ??
                                        widget.destination,
                                    fareEstimate: widget.fareEstimate,
                                    vehicle:
                                        currentBooking.vehicle ?? widget.vehicle,
                                  ),
                                ),
                              );
                            }
                          : null,
                      onCancelTap: () =>
                          handleBookingBack(context, booking: currentBooking),
                      destinationAddress:
                          widget.destination?.address ?? 'Thuê theo giờ',
                    ),
                  ],
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }

  bool _shouldShowDriverCard(BookingResponse? booking) {
    return booking?.driverOffer != null;
  }
}

class _SearchingPanel extends StatelessWidget {
  const _SearchingPanel({
    required this.pickupAddress,
    required this.destinationAddress,
    this.booking,
    this.vehicle,
    this.fareEstimate,
    this.onDriverPreviewTap,
    this.onCancelTap,
  });

  final BookingResponse? booking;
  final BookingVehicleOption? vehicle;
  final BookingFareEstimate? fareEstimate;
  final String pickupAddress;
  final String destinationAddress;
  final VoidCallback? onDriverPreviewTap;
  final VoidCallback? onCancelTap;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 24),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(24),
        boxShadow: [
          BoxShadow(
            color: Colors.black.withValues(alpha: 0.1),
            blurRadius: 20,
            offset: const Offset(0, 4),
          ),
        ],
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          const SizedBox(height: 12),
          Container(
            width: 40,
            height: 4,
            decoration: BoxDecoration(
              color: Colors.grey[300],
              borderRadius: BorderRadius.circular(2),
            ),
          ),
          const SizedBox(height: 22),
          const Text(
            BookingStrings.searchingDriver,
            style: TextStyle(
              fontSize: 18,
              fontWeight: FontWeight.bold,
              color: Color(0xFF1A1A1A),
            ),
          ),
          const SizedBox(height: 4),
          Text(
            _statusText,
            style: const TextStyle(fontSize: 14, color: Color(0xFF666666)),
          ),

          const SizedBox(height: 18),
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 24),
            child: _BookingSummary(
              booking: booking,
              vehicle: vehicle,
              fareEstimate: fareEstimate,
            ),
          ),
          const SizedBox(height: 14),
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 24),
            child: _CompactRouteInfo(
              pickup: pickupAddress,
              destination: destinationAddress,
            ),
          ),
          if (onDriverPreviewTap != null) ...[
            const SizedBox(height: 14),
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 24),
              child: _DriverFoundCard(
                booking: booking,
                onTap: onDriverPreviewTap!,
              ),
            ),
          ],
          const SizedBox(height: 24),
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 24),
            child: SizedBox(
              width: double.infinity,
              height: 52,
              child: ElevatedButton.icon(
                onPressed: (booking == null ||
                          context.watch<BookingProvider>().isLoading)
                          ? null : onCancelTap,
                style: ElevatedButton.styleFrom(
                  backgroundColor: const Color(0xFFF2F2F2),
                  foregroundColor: const Color(0xFFC62828),
                  elevation: 0,
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(12),
                  ),
                ),
                icon: context.watch<BookingProvider>().isLoading
                    ? const SizedBox(
                        width: 20,
                        height: 20,
                        child: CircularProgressIndicator(
                          strokeWidth: 2,
                          color: Color(0xFFC62828),
                        ),
                      )
                    : const Icon(Icons.close, size: 20),
                label: Text(
                  context.watch<BookingProvider>().isLoading
                      ? 'Đang hủy...'
                      : BookingStrings.cancelBooking,
                  style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 16),
                ),
              ),
            ),
          ),
          const SizedBox(height: 24),
        ],
      ),
    );
  }

  String get _statusText {
    final bookingId = booking?.bookingId;
    if (bookingId == null) return BookingStrings.estimatedWaitTime;
    return 'Mã chuyến #$bookingId • ${booking?.bookingStatus ?? 'Searching'}';
  }
}

class _DriverFoundCard extends StatelessWidget {
  const _DriverFoundCard({required this.booking, required this.onTap});

  final BookingResponse? booking;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(14),
      child: Container(
        padding: const EdgeInsets.all(14),
        decoration: BoxDecoration(
          color: const Color(0xFFFFF8E1),
          borderRadius: BorderRadius.circular(14),
          border: Border.all(color: const Color(0xFFFFECB3)),
        ),
        child: Row(
          children: [
            const CircleAvatar(
              backgroundColor: Color(0xFFFFB300),
              child: Icon(Icons.person_search_rounded, color: Colors.white),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'Tài xế phù hợp đã sẵn sàng',
                    style: TextStyle(fontWeight: FontWeight.w800),
                  ),
                  SizedBox(height: 2),
                  Text(
                    'Xem hồ sơ trước khi xác nhận thuê.',
                    style: TextStyle(fontSize: 12, color: Color(0xFF666666)),
                  ),
                ],
              ),
            ),
            const Icon(Icons.chevron_right_rounded, color: Color(0xFF006B70)),
          ],
        ),
      ),
    );
  }
}

class _RadarScanner extends StatefulWidget {
  final double size;
  const _RadarScanner({this.size = 120});

  @override
  State<_RadarScanner> createState() => _RadarScannerState();
}

class _RadarScannerState extends State<_RadarScanner>
    with TickerProviderStateMixin {
  late AnimationController _pulseController;
  late AnimationController _driverController;

  @override
  void initState() {
    super.initState();
    _pulseController = AnimationController(
      vsync: this,
      duration: const Duration(seconds: 2),
    )..repeat();

    _driverController = AnimationController(
      vsync: this,
      duration: const Duration(seconds: 4),
    )..repeat();
  }

  @override
  void dispose() {
    _pulseController.dispose();
    _driverController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    const primaryColor = Color(0xFF006B70);

    return SizedBox(
      width: widget.size,
      height: widget.size,
      child: Stack(
        alignment: Alignment.center,
        children: [
          // Concentric pulsing circles
          for (int i = 0; i < 3; i++)
            AnimatedBuilder(
              animation: _pulseController,
              builder: (context, child) {
                double progress = (_pulseController.value + (i / 3)) % 1.0;
                return Container(
                  width: widget.size * progress,
                  height: widget.size * progress,
                  decoration: BoxDecoration(
                    shape: BoxShape.circle,
                    border: Border.all(
                      color: primaryColor.withValues(
                        alpha: 0.6 * (1.0 - progress),
                      ),
                      width: 2,
                    ),
                  ),
                );
              },
            ),

          // Scanning Overlay (Faded circle)
          Container(
            width: widget.size * 0.8,
            height: widget.size * 0.8,
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              color: primaryColor.withValues(alpha: 0.08),
            ),
          ),

          // Simulated Drivers (Dots appearing randomly)
          _buildSimulatedDriver(
            top: widget.size * 0.2,
            left: widget.size * 0.25,
            delay: 0.0,
          ),
          _buildSimulatedDriver(
            top: widget.size * 0.35,
            right: widget.size * 0.15,
            delay: 0.4,
          ),
          _buildSimulatedDriver(
            bottom: widget.size * 0.2,
            left: widget.size * 0.4,
            delay: 0.8,
          ),
        ],
      ),
    );
  }

  Widget _buildSimulatedDriver({
    double? top,
    double? left,
    double? right,
    double? bottom,
    required double delay,
  }) {
    return AnimatedBuilder(
      animation: _driverController,
      builder: (context, child) {
        double val = (_driverController.value + delay) % 1.0;
        double opacity = val < 0.2
            ? val * 5
            : (val < 0.8 ? 1.0 : (1.0 - val) * 5);

        return Positioned(
          top: top,
          left: left,
          right: right,
          bottom: bottom,
          child: Opacity(
            opacity: opacity.clamp(0.0, 1.0),
            child: Container(
              padding: const EdgeInsets.all(4),
              decoration: const BoxDecoration(
                color: Colors.white,
                shape: BoxShape.circle,
                boxShadow: [BoxShadow(color: Colors.black12, blurRadius: 4)],
              ),
              child: const Icon(
                Icons.directions_car_rounded,
                size: 14,
                color: Color(0xFF006B70),
              ),
            ),
          ),
        );
      },
    );
  }
}

class _BookingSummary extends StatelessWidget {
  const _BookingSummary({
    required this.booking,
    required this.vehicle,
    required this.fareEstimate,
  });

  final BookingResponse? booking;
  final BookingVehicleOption? vehicle;
  final BookingFareEstimate? fareEstimate;

  @override
  Widget build(BuildContext context) {
    final originalFare = booking?.originalFare ??
        booking?.estimatedFare ??
        fareEstimate?.estimatedFare;

    // Fallback logic: prefer finalFare from booking, then estimatedFare, then fareEstimate
    final finalFare = booking?.finalFare ??
                     booking?.estimatedFare ??
                     fareEstimate?.estimatedFare;

    final discount = booking?.discountAmount ?? 0;
    final promoCode = booking?.promotionCode;

    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: const Color(0xFFEAF4F4),
        borderRadius: BorderRadius.circular(14),
      ),
      child: Column(
        children: [
          Row(
            children: [
              Icon(
                vehicle?.isMotorbike == true
                    ? Icons.directions_bike_rounded
                    : Icons.directions_car_rounded,
                color: const Color(0xFF006B70),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: Text(
                  vehicle == null
                      ? 'Đang chờ tài xế nhận chuyến'
                      : '${vehicle!.name} • ${vehicle!.plateNumber}',
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(fontWeight: FontWeight.w700),
                ),
              ),
              if (finalFare != null)
                Text(
                  _formatCurrency(finalFare),
                  style: const TextStyle(
                    color: Color(0xFF006B70),
                    fontWeight: FontWeight.w800,
                  ),
                ),
            ],
          ),
          if (discount > 0 || promoCode != null) ...[
            const Divider(height: 20),
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                const Text(
                  'Giá gốc:',
                  style: TextStyle(fontSize: 13, color: Color(0xFF666666)),
                ),
                if (originalFare != null)
                  Text(
                    _formatCurrency(originalFare),
                    style: const TextStyle(
                      fontSize: 13,
                      decoration: TextDecoration.lineThrough,
                    ),
                  ),
              ],
            ),
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Text(
                  'Khuyến mãi (${promoCode ?? 'Mã đã áp dụng'}):',
                  style: const TextStyle(fontSize: 13, color: Color(0xFF666666)),
                ),
                Text(
                  '-${_formatCurrency(discount)}',
                  style: const TextStyle(
                    fontSize: 13,
                    color: Color(0xFFC62828),
                    fontWeight: FontWeight.bold,
                  ),
                ),
              ],
            ),
          ],
        ],
      ),
    );
  }

  String _formatCurrency(double value) {
    final formatter = value.round().toString().replaceAllMapped(
      RegExp(r'(\d{1,3})(?=(\d{3})+(?!\d))'),
      (Match m) => '${m[1]}.',
    );
    return '$formatterđ';
  }
}

class _CompactRouteInfo extends StatelessWidget {
  const _CompactRouteInfo({required this.pickup, required this.destination});

  final String pickup;
  final String destination;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: const Color(0xFFF8F9FA),
        borderRadius: BorderRadius.circular(16),
      ),
      child: Column(
        children: [
          _LocationRow(
            icon: Icons.circle,
            iconColor: const Color(0xFF006B70),
            label: 'ĐIỂM ĐÓN',
            address: pickup,
          ),
          Padding(
            padding: const EdgeInsets.only(left: 11),
            child: Align(
              alignment: Alignment.centerLeft,
              child: Container(width: 1, height: 20, color: Colors.grey[300]),
            ),
          ),
          _LocationRow(
            icon: Icons.location_on,
            iconColor: const Color(0xFFC62828),
            label: 'ĐIỂM ĐẾN',
            address: destination,
          ),
        ],
      ),
    );
  }
}

class _LocationRow extends StatelessWidget {
  const _LocationRow({
    required this.icon,
    required this.iconColor,
    required this.label,
    required this.address,
  });

  final IconData icon;
  final Color iconColor;
  final String label;
  final String address;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Icon(icon, color: iconColor, size: 22),
        const SizedBox(width: 12),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                label,
                style: TextStyle(
                  fontSize: 10,
                  fontWeight: FontWeight.bold,
                  color: Colors.grey[600],
                ),
              ),
              Text(
                address,
                style: const TextStyle(
                  fontSize: 14,
                  fontWeight: FontWeight.w500,
                  color: Color(0xFF1A1A1A),
                ),
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
