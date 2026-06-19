import 'dart:async';
import 'dart:math' as Math;

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:google_maps_flutter/google_maps_flutter.dart';
import 'package:provider/provider.dart';
import '../../../../../core/maps/polyline_decoder.dart';
import '../../../../../core/services/socket_service.dart';
import '../../../../../dependency_injection/injection.dart';
import '../../../../auth/presentation/providers/auth_provider.dart';
import '../../data/models/booking_catalog.dart';
import '../../data/models/booking_location.dart';
import '../../data/models/booking_response.dart';
import '../widgets/booking_cancel_flow.dart';

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
  GoogleMapController? _mapController;
  late AnimationController _pulseController;
  final SocketService _socketService = getIt<SocketService>();
  final List<LatLng> _arrivalRoutePoints = [];
  final List<LatLng> _tripRoutePoints = [];
  LatLng? _driverPosition;
  double _driverHeading = 0;
  int? _joinedTripId;
  static const _tealColor = Color(0xFF006B70);

  @override
  void initState() {
    super.initState();
    _pulseController = AnimationController(
      vsync: this,
      duration: const Duration(seconds: 2),
    )..repeat(reverse: true);
    _initializeRoutes();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      unawaited(_connectTripSocket());
    });
  }

  @override
  void dispose() {
    final joinedTripId = _joinedTripId;
    if (joinedTripId != null) {
      unawaited(_socketService.leaveTrip(joinedTripId));
    }
    _pulseController.dispose();
    super.dispose();
  }

  void _initializeRoutes() {
    final arrivalPolyline = widget.booking.arrivalPolyline;
    if (arrivalPolyline != null && arrivalPolyline.isNotEmpty) {
      try {
        _arrivalRoutePoints.addAll(decodePolyline(arrivalPolyline));
      } on FormatException {
        _arrivalRoutePoints.clear();
      }
    }

    if (widget.booking.encodedPolyline.isNotEmpty) {
      try {
        _tripRoutePoints.addAll(decodePolyline(widget.booking.encodedPolyline));
      } on FormatException {
        _tripRoutePoints.clear();
      }
    }

    if (_arrivalRoutePoints.isNotEmpty) {
      _driverPosition = _arrivalRoutePoints.first;
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
      debugPrint('Tracking: Connected to Socket for Trip $tripId');
      
      _socketService.onDriverLocationUpdated((update) {
        debugPrint('Tracking: Received Driver Update for Trip ${update.tripId} (Current: $tripId)');
        if (!mounted || update.tripId != tripId) {
          return;
        }

        setState(() {
          if (_driverPosition != null) {
            _driverHeading = _calculateHeading(_driverPosition!, LatLng(update.latitude, update.longitude));
          }
          _driverPosition = LatLng(update.latitude, update.longitude);
          debugPrint('Tracking: Updated Driver Position to ${_driverPosition!.latitude}, ${_driverPosition!.longitude}');
        });
      });
      await _socketService.joinTrip(tripId);
      debugPrint('Tracking: Joined Trip Group $tripId');
      _joinedTripId = tripId;
    } catch (e) {
      debugPrint('Tracking Error: $e');
      if (mounted) {
        _showMessage('Khong the ket noi theo doi vi tri tai xe.');
      }
    }
  }

  double _calculateHeading(LatLng start, LatLng end) {
    final lat1 = start.latitude * (3.1415926535897932 / 180);
    final lon1 = start.longitude * (3.1415926535897932 / 180);
    final lat2 = end.latitude * (3.1415926535897932 / 180);
    final lon2 = end.longitude * (3.1415926535897932 / 180);

    final dLon = lon2 - lon1;
    final y = Math.sin(dLon) * Math.cos(lat2);
    final x = Math.cos(lat1) * Math.sin(lat2) -
        Math.sin(lat1) * Math.cos(lat2) * Math.cos(dLon);

    final radians = Math.atan2(y, x);
    return (radians * 180 / 3.1415926535897932 + 360) % 360;
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
        children: [
          // 1. Map Background
          _buildMap(),

          // 2. Top Header
          _buildTopHeader(),

          // 3. Bottom Panel
          _buildBottomPanel(),
        ],
      ),
    );
  }

  // _buildBottomNavigationBar removed to avoid duplicate navbar when hosted in CustomerHomePage IndexedStack

  // BottomNavigationBarItem _buildNavItem helper logic removed, now using CustomerBottomNavBar

  Widget _buildMap() {
    return GoogleMap(
      initialCameraPosition: CameraPosition(
        target: LatLng(widget.pickup.latitude, widget.pickup.longitude),
        zoom: 15,
      ),
      onMapCreated: (controller) {
        _mapController = controller;
        _fitMapToVisibleRoute();
      },
      zoomControlsEnabled: false,
      myLocationButtonEnabled: false,
      mapToolbarEnabled: false,
      myLocationEnabled: true,
      markers: _buildMarkers(),
      polylines: _buildPolylines(),
    );
  }

  Set<Marker> _buildMarkers() {
    final markers = <Marker>{
      Marker(
        markerId: const MarkerId('pickup'),
        position: LatLng(widget.pickup.latitude, widget.pickup.longitude),
        anchor: const Offset(0.5, 0.5),
        icon: BitmapDescriptor.defaultMarkerWithHue(BitmapDescriptor.hueAzure),
        infoWindow: const InfoWindow(title: 'Pickup'),
      ),
    };

    final driverPosition = _driverPosition;
    if (driverPosition != null) {
      markers.add(
        Marker(
          markerId: const MarkerId('driver'),
          position: driverPosition,
          rotation: _driverHeading,
          anchor: const Offset(0.5, 0.5),
          icon: BitmapDescriptor.defaultMarkerWithHue(
            BitmapDescriptor.hueOrange,
          ),
          infoWindow: const InfoWindow(title: 'Driver'),
        ),
      );
    }

    final destination = widget.destination;
    if (destination != null) {
      markers.add(
        Marker(
          markerId: const MarkerId('destination'),
          position: LatLng(destination.latitude, destination.longitude),
          icon: BitmapDescriptor.defaultMarkerWithHue(BitmapDescriptor.hueRed),
          infoWindow: const InfoWindow(title: 'Destination'),
        ),
      );
    }

    return markers;
  }

  Set<Polyline> _buildPolylines() {
    final polylines = <Polyline>{};
    final showArrival = widget.state == TripTrackingState.arriving;

    if (_tripRoutePoints.length >= 2) {
      polylines.add(
        Polyline(
          polylineId: const PolylineId('trip-route'),
          points: _tripRoutePoints,
          color: showArrival ? const Color(0x88006B70) : _tealColor,
          width: showArrival ? 4 : 6,
          zIndex: showArrival ? 1 : 3,
          jointType: JointType.round,
          startCap: Cap.roundCap,
          endCap: Cap.roundCap,
        ),
      );
    }

    if (showArrival) {
      final arrivalPoints = _getDynamicArrivalPoints();
      if (arrivalPoints.length >= 2) {
        polylines.add(
          Polyline(
            polylineId: const PolylineId('arrival-route'),
            points: arrivalPoints,
            color: const Color(0xFF2F80ED),
            width: 5,
            patterns: [PatternItem.dash(12), PatternItem.gap(8)],
            zIndex: 4,
            jointType: JointType.round,
            startCap: Cap.roundCap,
            endCap: Cap.roundCap,
          ),
        );
      }
    }

    return polylines;
  }

  List<LatLng> _getDynamicArrivalPoints() {
    final driverPos = _driverPosition;
    if (driverPos == null) return const [];

    if (_arrivalRoutePoints.isEmpty) {
      return [driverPos, LatLng(widget.pickup.latitude, widget.pickup.longitude)];
    }

    // Find the closest point index in the static route to the current driver position
    int closestIdx = 0;
    double minDistSq = double.maxFinite;
    for (int i = 0; i < _arrivalRoutePoints.length; i++) {
      final dx = driverPos.latitude - _arrivalRoutePoints[i].latitude;
      final dy = driverPos.longitude - _arrivalRoutePoints[i].longitude;
      final distSq = dx * dx + dy * dy;
      if (distSq < minDistSq) {
        minDistSq = distSq;
        closestIdx = i;
      }
    }

    // Return the route starting from the car's real position,
    // followed by all remaining points in the static route.
    return [driverPos, ..._arrivalRoutePoints.skip(closestIdx + 1)];
  }

  List<LatLng> _fallbackArrivalPoints() {
    final driverPosition = _driverPosition;
    if (driverPosition == null) {
      return const [];
    }

    return [
      driverPosition,
      LatLng(widget.pickup.latitude, widget.pickup.longitude),
    ];
  }

  void _fitMapToVisibleRoute() {
    final driverPosition = _driverPosition;
    final points = <LatLng>[
      LatLng(widget.pickup.latitude, widget.pickup.longitude),
    ];
    final destination = widget.destination;
    if (destination != null) {
      points.add(LatLng(destination.latitude, destination.longitude));
    }
    if (driverPosition != null) {
      points.add(driverPosition);
    }
    if (widget.state == TripTrackingState.arriving) {
      points.addAll(_arrivalRoutePoints);
    }
    points.addAll(_tripRoutePoints);

    if (points.length < 2) {
      return;
    }

    final bounds = _boundsFor(points);
    unawaited(
      _mapController?.animateCamera(CameraUpdate.newLatLngBounds(bounds, 80)),
    );
  }

  LatLngBounds _boundsFor(List<LatLng> points) {
    var minLat = points.first.latitude;
    var maxLat = points.first.latitude;
    var minLng = points.first.longitude;
    var maxLng = points.first.longitude;

    for (final point in points.skip(1)) {
      minLat = point.latitude < minLat ? point.latitude : minLat;
      maxLat = point.latitude > maxLat ? point.latitude : maxLat;
      minLng = point.longitude < minLng ? point.longitude : minLng;
      maxLng = point.longitude > maxLng ? point.longitude : maxLng;
    }

    return LatLngBounds(
      southwest: LatLng(minLat, minLng),
      northeast: LatLng(maxLat, maxLng),
    );
  }

  Widget _buildTopHeader() {
    final bool isArriving = widget.state == TripTrackingState.arriving;

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
                    } else {
                      widget.onSwitchTab?.call(0);
                    }
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
                          color: Colors.black.withOpacity(0.12),
                          blurRadius: 16,
                          offset: const Offset(0, 4),
                        ),
                      ],
                    ),
                    child: Row(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        if (isArriving) ...[
                          _buildLiveIndicator(),
                          const SizedBox(width: 8),
                          const Flexible(
                            child: Text(
                              'Tài xế đang đến • 3 phút',
                              style: TextStyle(
                                fontWeight: FontWeight.w800,
                                fontSize: 13,
                                color: _tealColor,
                              ),
                              overflow: TextOverflow.ellipsis,
                            ),
                          ),
                        ] else ...[
                          _buildLiveIndicator(),
                          const SizedBox(width: 8),
                          const Text(
                            'Đang di chuyển • 12 phút',
                            style: TextStyle(
                              fontWeight: FontWeight.w800,
                              fontSize: 14,
                              color: _tealColor,
                            ),
                          ),
                        ],
                      ],
                    ),
                  ),
                ),
                const SizedBox(width: 12),
                // Hidden balance item to keep center container centered
                const Opacity(
                  opacity: 0,
                  child: _CircleIconButton(
                    icon: Icons.arrow_back,
                    onPressed: null,
                  ),
                ),
              ],
            ),
            if (!isArriving) ...[
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
                      color: Colors.black.withOpacity(0.08),
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
                        widget.destination?.address ?? 'Sân bay Tân Sơn Nhất',
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
    final bool isArriving = widget.state == TripTrackingState.arriving;
    final offer = widget.booking.driverOffer;
    final vehicle = widget.vehicle;
    final plateParts = vehicle?.plateNumber.split('-') ?? const <String>[];

    return Positioned(
      bottom: 0,
      left: 0,
      right: 0,
      child: Container(
        padding: const EdgeInsets.fromLTRB(24, 12, 24, 32),
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: const BorderRadius.vertical(top: Radius.circular(32)),
          boxShadow: [
            BoxShadow(
              color: Colors.black.withOpacity(0.1),
              blurRadius: 24,
              offset: const Offset(0, -10),
            ),
          ],
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            // Handle bar
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
              // Route Status
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Row(
                    children: [
                      const Icon(
                        Icons.check_circle_rounded,
                        color: _tealColor,
                        size: 22,
                      ),
                      const SizedBox(width: 10),
                      const Text(
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

            // Driver Info Row
            Row(
              children: [
                Stack(
                  children: [
                    CircleAvatar(
                      radius: 30,
                      backgroundColor: Colors.grey[200],
                      backgroundImage:
                          widget.booking.driverOffer?.driverAvatarUrl != null
                          ? NetworkImage(
                              widget.booking.driverOffer!.driverAvatarUrl!,
                            )
                          : null,
                      child: widget.booking.driverOffer?.driverAvatarUrl == null
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
                          boxShadow: [
                            BoxShadow(
                              color: Colors.black.withOpacity(0.1),
                              blurRadius: 4,
                            ),
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

            // Action Buttons
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
                    style: TextButton.styleFrom(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 12,
                        vertical: 4,
                      ),
                    ),
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
            color: Colors.black.withOpacity(0.08),
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
    const tealColor = Color(0xFF006B70);
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
            Icon(icon, color: tealColor, size: 22),
            if (label != null) ...[
              const SizedBox(width: 8),
              Text(
                label!,
                style: const TextStyle(
                  color: tealColor,
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
    const redColor = Color(0xFFE53935);
    return GestureDetector(
      onTap: onTap,
      child: Container(
        padding: isCircle
            ? const EdgeInsets.all(16)
            : const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
        decoration: BoxDecoration(
          color: redColor,
          shape: isCircle ? BoxShape.circle : BoxShape.rectangle,
          borderRadius: isCircle ? null : BorderRadius.circular(20),
          boxShadow: [
            BoxShadow(
              color: redColor.withOpacity(0.3),
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
    const String trackLink = 'saferide.vn/track/SR94210';

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
              color: Colors.black.withOpacity(0.15),
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
                      trackLink,
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
                      Clipboard.setData(const ClipboardData(text: trackLink));
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
