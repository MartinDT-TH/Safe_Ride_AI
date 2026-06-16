import 'package:flutter/material.dart';
import 'package:google_maps_flutter/google_maps_flutter.dart';
import '../../../../../core/constants/app_strings.dart';
import '../../../../../core/maps/polyline_decoder.dart';
import '../../data/models/booking_location.dart';
import '../../data/models/booking_fare_estimate.dart';
import '../widgets/cancel_booking_sheet.dart';

class SearchingDriverPage extends StatefulWidget {
  const SearchingDriverPage({
    super.key,
    required this.pickup,
    this.destination,
    this.fareEstimate,
  });

  final BookingLocation pickup;
  final BookingLocation? destination;
  final BookingFareEstimate? fareEstimate;

  @override
  State<SearchingDriverPage> createState() => _SearchingDriverPageState();
}

class _SearchingDriverPageState extends State<SearchingDriverPage> {
  GoogleMapController? _controller;
  static const _tealColor = Color(0xFF006B70);

  List<LatLng> get _routePoints {
    final encoded = widget.fareEstimate?.encodedPolyline;
    if (encoded == null || encoded.isEmpty) return const [];
    try {
      return decodePolyline(encoded);
    } on FormatException {
      return const [];
    }
  }

  @override
  void dispose() {
    _controller?.dispose();
    super.dispose();
  }

  void _onMapCreated(GoogleMapController controller) {
    _controller = controller;
    _fitRoute();
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
      if (p.latitude < minLat) minLat = p.latitude;
      if (p.latitude > maxLat) maxLat = p.latitude;
      if (p.longitude < minLng) minLng = p.longitude;
      if (p.longitude > maxLng) maxLng = p.longitude;
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

    return Scaffold(
      body: Stack(
        children: [
          // Background Map
          GoogleMap(
            initialCameraPosition: CameraPosition(target: pickupPos, zoom: 15),
            onMapCreated: _onMapCreated,
            markers: {
              Marker(
                markerId: const MarkerId('pickup'),
                position: pickupPos,
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
            },
            polylines: _routePoints.isEmpty
                ? {}
                : {
                    Polyline(
                      polylineId: const PolylineId('route'),
                      points: _routePoints,
                      color: _tealColor,
                      width: 5,
                    ),
                  },
            zoomControlsEnabled: false,
            myLocationButtonEnabled: false,
            compassEnabled: false,
          ),

          // Overlay Content
          SafeArea(
            child: Column(
              children: [
                Padding(
                  padding: const EdgeInsets.all(16.0),
                  child: Row(
                    children: [
                      CircleAvatar(
                        backgroundColor: Colors.white,
                        child: IconButton(
                          icon: const Icon(
                            Icons.arrow_back,
                            color: Colors.black,
                          ),
                          onPressed: () => Navigator.pop(context),
                        ),
                      ),
                    ],
                  ),
                ),
                const Spacer(),
                _SearchingPanel(
                  pickupAddress: widget.pickup.address,
                  destinationAddress:
                      widget.destination?.address ?? 'Thuê theo giờ',
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _SearchingPanel extends StatelessWidget {
  const _SearchingPanel({
    required this.pickupAddress,
    required this.destinationAddress,
  });

  final String pickupAddress;
  final String destinationAddress;

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
            color: Colors.black.withOpacity(0.1),
            blurRadius: 20,
            offset: const Offset(0, 4),
          ),
        ],
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          const SizedBox(height: 12),
          // Drag handle
          Container(
            width: 40,
            height: 4,
            decoration: BoxDecoration(
              color: Colors.grey[300],
              borderRadius: BorderRadius.circular(2),
            ),
          ),
          const SizedBox(height: 24),

          // Animated Loading Circle with Car
          const _AnimatedLoadingVehicle(),

          const SizedBox(height: 16),
          const Text(
            BookingStrings.searchingDriver,
            style: TextStyle(
              fontSize: 18,
              fontWeight: FontWeight.bold,
              color: Color(0xFF1A1A1A),
            ),
          ),
          const SizedBox(height: 4),
          const Text(
            BookingStrings.estimatedWaitTime,
            style: TextStyle(fontSize: 14, color: Color(0xFF666666)),
          ),

          const SizedBox(height: 24),
          // Route info
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 24),
            child: _CompactRouteInfo(
              pickup: pickupAddress,
              destination: destinationAddress,
            ),
          ),

          const SizedBox(height: 24),
          // Cancel Button
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 24),
            child: SizedBox(
              width: double.infinity,
              height: 52,
              child: ElevatedButton.icon(
                onPressed: () async {
                  final reason = await CancelBookingSheet.show(context);
                  if (reason != null && context.mounted) {
                    // TODO: Call API to cancel booking with reason
                    Navigator.pop(context);
                  }
                },
                style: ElevatedButton.styleFrom(
                  backgroundColor: const Color(0xFFF2F2F2),
                  foregroundColor: const Color(0xFFC62828),
                  elevation: 0,
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(12),
                  ),
                ),
                icon: const Icon(Icons.close, size: 20),
                label: const Text(
                  BookingStrings.cancelBooking,
                  style: TextStyle(fontWeight: FontWeight.bold, fontSize: 16),
                ),
              ),
            ),
          ),
          const SizedBox(height: 24),
        ],
      ),
    );
  }
}

class _AnimatedLoadingVehicle extends StatefulWidget {
  const _AnimatedLoadingVehicle();

  @override
  State<_AnimatedLoadingVehicle> createState() =>
      _AnimatedLoadingVehicleState();
}

class _AnimatedLoadingVehicleState extends State<_AnimatedLoadingVehicle>
    with SingleTickerProviderStateMixin {
  late AnimationController _controller;

  @override
  void initState() {
    super.initState();
    _controller = AnimationController(
      vsync: this,
      duration: const Duration(seconds: 2),
    )..repeat();
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Stack(
      alignment: Alignment.center,
      children: [
        SizedBox(
          width: 70,
          height: 70,
          child: CircularProgressIndicator(
            valueColor: const AlwaysStoppedAnimation<Color>(Color(0xFF006B70)),
            backgroundColor: const Color(0xFF006B70).withOpacity(0.1),
            strokeWidth: 6,
          ),
        ),
        Container(
          width: 50,
          height: 50,
          decoration: BoxDecoration(
            color: const Color(0xFFE0F2F1),
            shape: BoxShape.circle,
          ),
          child: const Icon(
            Icons.directions_car_rounded,
            color: Color(0xFF006B70),
            size: 30,
          ),
        ),
      ],
    );
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
