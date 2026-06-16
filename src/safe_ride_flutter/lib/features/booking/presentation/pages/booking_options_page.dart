import 'package:flutter/material.dart';
import 'package:google_maps_flutter/google_maps_flutter.dart';
import 'package:provider/provider.dart';

import '../../../../core/config/api_keys_config.dart';
import '../../../../core/constants/app_colors.dart';
import '../../../../core/constants/app_strings.dart';
import '../../../../core/maps/polyline_decoder.dart';
import '../../../auth/presentation/providers/auth_provider.dart';
import '../../data/models/booking_catalog.dart';
import '../../data/models/booking_fare_estimate.dart';
import '../../data/models/booking_location.dart';
import '../../data/models/booking_response.dart';
import '../../data/models/create_booking_request.dart';
import '../providers/booking_provider.dart';

class BookingOptionsPage extends StatefulWidget {
  const BookingOptionsPage({
    super.key,
    required this.bookingType,
    required this.pickup,
    required this.destination,
  });

  final BookingType bookingType;
  final BookingLocation pickup;
  final BookingLocation destination;

  @override
  State<BookingOptionsPage> createState() => _BookingOptionsPageState();
}

class _BookingOptionsPageState extends State<BookingOptionsPage> {
  BookingServiceOption? _service;
  BookingVehicleOption? _vehicle;
  DateTime? _scheduledAt;
  final _specialRequestController = TextEditingController();

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) async {
      final token = context.read<AuthProvider>().token;
      if (token == null || token.isEmpty) {
        _showMessage(BookingStrings.sessionExpired);
        return;
      }

      final provider = context.read<BookingProvider>();
      provider.clearFareEstimate();
      await provider.loadCatalog(token);
      if (!mounted) return;
      final catalog = provider.catalog;
      setState(() {
        _service = catalog?.services.firstOrNull;
        _vehicle = catalog?.vehicles.firstOrNull;
      });
      await _refreshEstimate();
    });
  }

  @override
  void dispose() {
    _specialRequestController.dispose();
    super.dispose();
  }

  Future<void> _selectSchedule() async {
    final now = DateTime.now();
    // TimeOfDay drops seconds, so use a one-minute buffer for backend validation.
    final initial = now.add(const Duration(minutes: 31));
    final date = await showDatePicker(
      context: context,
      firstDate: now,
      lastDate: now.add(const Duration(days: 90)),
      initialDate: initial,
      helpText: BookingStrings.selectPickupDate,
    );
    if (date == null || !mounted) return;

    final time = await showTimePicker(
      context: context,
      initialTime: TimeOfDay.fromDateTime(initial),
      helpText: BookingStrings.selectPickupTimeHelp,
    );
    if (time == null) return;

    final scheduledAt = DateTime(
      date.year,
      date.month,
      date.day,
      time.hour,
      time.minute,
    );
    if (scheduledAt.isBefore(now.add(const Duration(minutes: 30)))) {
      _showMessage(BookingStrings.invalidSchedule);
      return;
    }

    setState(() => _scheduledAt = scheduledAt);
  }

  Future<void> _refreshEstimate() async {
    final token = context.read<AuthProvider>().token;
    final service = _service;
    final vehicle = _vehicle;
    if (token == null || token.isEmpty || service == null || vehicle == null) {
      return;
    }

    await context.read<BookingProvider>().estimateFare(
      token,
      vehicleId: vehicle.id,
      serviceTypeId: service.id,
      pickup: widget.pickup,
      destination: widget.destination,
    );
  }

  Future<void> _confirmBooking() async {
    final token = context.read<AuthProvider>().token;
    final service = _service;
    final vehicle = _vehicle;
    if (token == null || token.isEmpty) {
      _showMessage(BookingStrings.sessionExpired);
      return;
    }
    if (service == null || vehicle == null) {
      _showMessage(BookingStrings.selectServiceAndVehicle);
      return;
    }
    if (widget.bookingType == BookingType.scheduled && _scheduledAt == null) {
      _showMessage(BookingStrings.selectPickupTimeRequired);
      return;
    }

    final result = await context.read<BookingProvider>().createBooking(
      token,
      CreateBookingRequest(
        vehicleId: vehicle.id,
        serviceTypeId: service.id,
        bookingType: widget.bookingType,
        scheduledAt: _scheduledAt,
        pickup: widget.pickup,
        destination: widget.destination,
        specialRequest: _specialRequestController.text,
      ),
    );
    if (!mounted) return;

    if (result == null) {
      _showMessage(
        context.read<BookingProvider>().errorMessage ??
            BookingStrings.bookingFailed,
      );
      return;
    }

    await _showSuccess(result);
  }

  void _showMessage(String message) {
    ScaffoldMessenger.of(context)
      ..hideCurrentSnackBar()
      ..showSnackBar(SnackBar(content: Text(message)));
  }

  Future<void> _showSuccess(BookingResponse result) async {
    await showDialog<void>(
      context: context,
      barrierDismissible: false,
      builder: (dialogContext) => AlertDialog(
        icon: const Icon(
          Icons.check_circle,
          color: AppColors.primary,
          size: 52,
        ),
        title: const Text(BookingStrings.bookingSuccess),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(result.message, textAlign: TextAlign.center),
            const SizedBox(height: 12),
            Text(
              _formatCurrency(result.estimatedFare),
              style: const TextStyle(
                color: AppColors.primary,
                fontSize: 26,
                fontWeight: FontWeight.w800,
              ),
            ),
          ],
        ),
        actions: [
          FilledButton(
            onPressed: () {
              Navigator.pop(dialogContext);
              Navigator.of(context).popUntil((route) => route.isFirst);
            },
            child: const Text(BookingStrings.backToHome),
          ),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<BookingProvider>();
    final catalog = provider.catalog;

    return Scaffold(
      backgroundColor: const Color(0xFFF7FAFA),
      body: SafeArea(
        child: Column(
          children: [
            Expanded(
              flex: 4,
              child: _MapPreview(
                pickup: widget.pickup,
                destination: widget.destination,
                estimate: provider.fareEstimate,
                onBack: () => Navigator.pop(context),
              ),
            ),
            Expanded(
              flex: 7,
              child: Container(
                width: double.infinity,
                padding: const EdgeInsets.fromLTRB(20, 12, 20, 18),
                decoration: const BoxDecoration(
                  color: Colors.white,
                  borderRadius: BorderRadius.vertical(top: Radius.circular(28)),
                ),
                child: ListView(
                  children: [
                    Center(
                      child: Container(
                        width: 38,
                        height: 5,
                        decoration: BoxDecoration(
                          color: const Color(0xFFD8DCDD),
                          borderRadius: BorderRadius.circular(8),
                        ),
                      ),
                    ),
                    const SizedBox(height: 18),
                    _RouteSummary(
                      pickup: widget.pickup.address,
                      destination: widget.destination.address,
                      estimate: provider.fareEstimate,
                      isLoading: provider.isEstimating,
                    ),
                    if (provider.errorMessage != null) ...[
                      const SizedBox(height: 10),
                      Text(
                        provider.errorMessage!,
                        style: const TextStyle(color: Colors.red),
                      ),
                    ],
                    const SizedBox(height: 18),
                    if (widget.bookingType == BookingType.scheduled)
                      _ScheduleCard(
                        scheduledAt: _scheduledAt,
                        onTap: _selectSchedule,
                      ),
                    if (widget.bookingType == BookingType.scheduled)
                      const SizedBox(height: 16),
                    if (catalog == null)
                      const Center(child: CircularProgressIndicator())
                    else if (catalog.services.isEmpty ||
                        catalog.vehicles.isEmpty)
                      const _EmptyCatalogMessage()
                    else ...[
                      _ServiceSelector(
                        services: catalog.services,
                        selected: _service,
                        onSelected: (service) {
                          setState(() => _service = service);
                          _refreshEstimate();
                        },
                      ),
                      const SizedBox(height: 18),
                      const Text(
                        BookingStrings.selectVehicle,
                        style: TextStyle(
                          fontSize: 18,
                          fontWeight: FontWeight.w800,
                        ),
                      ),
                      const SizedBox(height: 10),
                      ...catalog.vehicles.map(
                        (vehicle) => _VehicleCard(
                          vehicle: vehicle,
                          selected: vehicle.id == _vehicle?.id,
                          onTap: () {
                            setState(() => _vehicle = vehicle);
                            _refreshEstimate();
                          },
                        ),
                      ),
                    ],
                    const SizedBox(height: 12),
                    TextField(
                      controller: _specialRequestController,
                      maxLength: 500,
                      decoration: InputDecoration(
                        hintText: BookingStrings.specialRequest,
                        prefixIcon: const Icon(Icons.notes),
                        border: OutlineInputBorder(
                          borderRadius: BorderRadius.circular(12),
                        ),
                      ),
                    ),
                    const Text(
                      BookingStrings.fareCalculationNote,
                      style: TextStyle(color: Color(0xFF667174), fontSize: 13),
                    ),
                  ],
                ),
              ),
            ),
          ],
        ),
      ),
      bottomNavigationBar: SafeArea(
        child: Padding(
          padding: const EdgeInsets.fromLTRB(20, 10, 20, 14),
          child: SizedBox(
            height: 58,
            child: FilledButton(
              onPressed:
                  provider.isLoading ||
                      provider.isEstimating ||
                      provider.fareEstimate == null
                  ? null
                  : _confirmBooking,
              style: FilledButton.styleFrom(
                backgroundColor: AppColors.primary,
                shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(14),
                ),
              ),
              child: provider.isLoading
                  ? const CircularProgressIndicator(color: Colors.white)
                  : Text(
                      widget.bookingType == BookingType.now
                          ? BookingStrings.confirmNow
                          : BookingStrings.confirmScheduled,
                      style: const TextStyle(
                        fontSize: 18,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
            ),
          ),
        ),
      ),
    );
  }

  String _formatCurrency(double value) {
    final digits = value.round().toString();
    final buffer = StringBuffer();
    for (var index = 0; index < digits.length; index++) {
      if (index > 0 && (digits.length - index) % 3 == 0) buffer.write('.');
      buffer.write(digits[index]);
    }
    return '$bufferđ';
  }
}

class _EmptyCatalogMessage extends StatelessWidget {
  const _EmptyCatalogMessage();

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: const Color(0xFFFFF4E5),
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: const Color(0xFFFFCC80)),
      ),
      child: const Row(
        children: [
          Icon(Icons.directions_car_filled_outlined, color: Color(0xFFB26A00)),
          SizedBox(width: 12),
          Expanded(child: Text(BookingStrings.noBookableVehicles)),
        ],
      ),
    );
  }
}

class _MapPreview extends StatefulWidget {
  const _MapPreview({
    required this.pickup,
    required this.destination,
    required this.estimate,
    required this.onBack,
  });

  final BookingLocation pickup;
  final BookingLocation destination;
  final BookingFareEstimate? estimate;
  final VoidCallback onBack;

  @override
  State<_MapPreview> createState() => _MapPreviewState();
}

class _MapPreviewState extends State<_MapPreview> {
  GoogleMapController? _controller;

  List<LatLng> get _routePoints {
    final encoded = widget.estimate?.encodedPolyline;
    if (encoded == null || encoded.isEmpty) return const [];

    try {
      return decodePolyline(encoded);
    } on FormatException {
      return const [];
    }
  }

  LatLng get _pickup => LatLng(widget.pickup.latitude, widget.pickup.longitude);

  LatLng get _destination =>
      LatLng(widget.destination.latitude, widget.destination.longitude);

  @override
  void dispose() {
    _controller?.dispose();
    super.dispose();
  }

  @override
  void didUpdateWidget(covariant _MapPreview oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.estimate?.encodedPolyline !=
        widget.estimate?.encodedPolyline) {
      WidgetsBinding.instance.addPostFrameCallback((_) => _fitRoute());
    }
  }

  Future<void> _fitRoute() async {
    final controller = _controller;
    if (controller == null) return;

    final routePoints = _routePoints;
    final boundsPoints = routePoints.isEmpty
        ? [_pickup, _destination]
        : routePoints;
    var minLatitude = boundsPoints.first.latitude;
    var maxLatitude = boundsPoints.first.latitude;
    var minLongitude = boundsPoints.first.longitude;
    var maxLongitude = boundsPoints.first.longitude;

    for (final point in boundsPoints.skip(1)) {
      minLatitude = point.latitude < minLatitude ? point.latitude : minLatitude;
      maxLatitude = point.latitude > maxLatitude ? point.latitude : maxLatitude;
      minLongitude = point.longitude < minLongitude
          ? point.longitude
          : minLongitude;
      maxLongitude = point.longitude > maxLongitude
          ? point.longitude
          : maxLongitude;
    }

    final southWest = LatLng(minLatitude, minLongitude);
    final northEast = LatLng(maxLatitude, maxLongitude);

    if (southWest == northEast) {
      await controller.animateCamera(CameraUpdate.newLatLngZoom(_pickup, 16));
      return;
    }

    await controller.animateCamera(
      CameraUpdate.newLatLngBounds(
        LatLngBounds(southwest: southWest, northeast: northEast),
        54,
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    if (!ApiKeysConfig.hasGoogleMapsKey) {
      return _MapConfigurationError(onBack: widget.onBack);
    }

    final routePoints = _routePoints;

    return Stack(
      fit: StackFit.expand,
      children: [
        GoogleMap(
          initialCameraPosition: CameraPosition(target: _pickup, zoom: 14),
          markers: {
            Marker(
              markerId: const MarkerId('pickup'),
              position: _pickup,
              infoWindow: InfoWindow(
                title: BookingStrings.pickupLabel,
                snippet: widget.pickup.address,
              ),
            ),
            Marker(
              markerId: const MarkerId('destination'),
              position: _destination,
              icon: BitmapDescriptor.defaultMarkerWithHue(
                BitmapDescriptor.hueRed,
              ),
              infoWindow: InfoWindow(
                title: BookingStrings.destinationLabel,
                snippet: widget.destination.address,
              ),
            ),
          },
          polylines: routePoints.isEmpty
              ? {}
              : {
                  Polyline(
                    polylineId: const PolylineId('route'),
                    points: routePoints,
                    color: AppColors.primary,
                    width: 5,
                  ),
                },
          zoomControlsEnabled: false,
          mapToolbarEnabled: false,
          myLocationButtonEnabled: false,
          compassEnabled: false,
          onMapCreated: (controller) {
            _controller = controller;
            WidgetsBinding.instance.addPostFrameCallback((_) => _fitRoute());
          },
        ),
        Positioned(
          left: 20,
          top: 18,
          child: CircleAvatar(
            backgroundColor: Colors.white,
            child: IconButton(
              onPressed: widget.onBack,
              icon: const Icon(Icons.arrow_back, color: Color(0xFF263334)),
            ),
          ),
        ),
      ],
    );
  }
}

class _MapConfigurationError extends StatelessWidget {
  const _MapConfigurationError({required this.onBack});

  final VoidCallback onBack;

  @override
  Widget build(BuildContext context) {
    return ColoredBox(
      color: const Color(0xFFE7EEEE),
      child: Stack(
        children: [
          const Center(
            child: Padding(
              padding: EdgeInsets.all(32),
              child: Text(
                'Thiếu GOOGLE_MAPS_API_KEY. Hãy chạy app bằng cấu hình local.',
                textAlign: TextAlign.center,
              ),
            ),
          ),
          Positioned(
            left: 20,
            top: 18,
            child: CircleAvatar(
              backgroundColor: Colors.white,
              child: IconButton(
                onPressed: onBack,
                icon: const Icon(Icons.arrow_back),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _RouteSummary extends StatelessWidget {
  const _RouteSummary({
    required this.pickup,
    required this.destination,
    required this.estimate,
    required this.isLoading,
  });

  final String pickup;
  final String destination;
  final BookingFareEstimate? estimate;
  final bool isLoading;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: const Color(0xFFF8F6F6),
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: const Color(0xFFE6E1E1)),
      ),
      child: Column(
        children: [
          _RouteRow(
            icon: Icons.my_location,
            color: AppColors.primary,
            label: BookingStrings.pickupLabel,
            value: pickup,
          ),
          const Divider(height: 22),
          _RouteRow(
            icon: Icons.location_on,
            color: Color(0xFFC61E27),
            label: BookingStrings.destinationLabel,
            value: destination,
          ),
          const Divider(height: 22),
          if (isLoading)
            const Row(
              children: [
                SizedBox.square(
                  dimension: 18,
                  child: CircularProgressIndicator(strokeWidth: 2),
                ),
                SizedBox(width: 10),
                Text('Đang tính tuyến đường và giá dự kiến...'),
              ],
            )
          else if (estimate != null)
            Row(
              children: [
                Expanded(
                  child: _EstimateValue(
                    icon: Icons.route,
                    value:
                        '${estimate!.estimatedDistanceKm.toStringAsFixed(1)} km',
                  ),
                ),
                Expanded(
                  child: _EstimateValue(
                    icon: Icons.schedule,
                    value: '${estimate!.estimatedDurationMinutes} phút',
                  ),
                ),
                Expanded(
                  child: _EstimateValue(
                    icon: Icons.payments_outlined,
                    value: _formatEstimateCurrency(estimate!.estimatedFare),
                  ),
                ),
              ],
            ),
        ],
      ),
    );
  }

  static String _formatEstimateCurrency(double value) {
    final digits = value.round().toString();
    return '${digits.replaceAllMapped(RegExp(r'(?=(\d{3})+(?!\d))'), (_) => '.')}đ';
  }
}

class _EstimateValue extends StatelessWidget {
  const _EstimateValue({required this.icon, required this.value});

  final IconData icon;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Icon(icon, color: AppColors.primary, size: 20),
        const SizedBox(height: 4),
        Text(
          value,
          textAlign: TextAlign.center,
          style: const TextStyle(fontWeight: FontWeight.w700),
        ),
      ],
    );
  }
}

class _RouteRow extends StatelessWidget {
  const _RouteRow({
    required this.icon,
    required this.color,
    required this.label,
    required this.value,
  });

  final IconData icon;
  final Color color;
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Icon(icon, color: color),
        const SizedBox(width: 12),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                label,
                style: const TextStyle(
                  fontSize: 11,
                  fontWeight: FontWeight.w700,
                  color: Color(0xFF626A6C),
                ),
              ),
              const SizedBox(height: 3),
              Text(value, maxLines: 2, overflow: TextOverflow.ellipsis),
            ],
          ),
        ),
      ],
    );
  }
}

class _ScheduleCard extends StatelessWidget {
  const _ScheduleCard({required this.scheduledAt, required this.onTap});

  final DateTime? scheduledAt;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(12),
      child: Container(
        padding: const EdgeInsets.all(16),
        decoration: BoxDecoration(
          color: const Color(0xFFEAF4F4),
          borderRadius: BorderRadius.circular(12),
          border: Border.all(color: AppColors.primary),
        ),
        child: Row(
          children: [
            const Icon(Icons.calendar_month, color: AppColors.primary),
            const SizedBox(width: 12),
            Expanded(
              child: Text(
                scheduledAt == null
                    ? BookingStrings.selectPickupTime
                    : '${scheduledAt!.day}/${scheduledAt!.month}/${scheduledAt!.year} '
                          '${scheduledAt!.hour.toString().padLeft(2, '0')}:'
                          '${scheduledAt!.minute.toString().padLeft(2, '0')}',
                style: const TextStyle(fontWeight: FontWeight.w700),
              ),
            ),
            const Icon(Icons.chevron_right),
          ],
        ),
      ),
    );
  }
}

class _ServiceSelector extends StatelessWidget {
  const _ServiceSelector({
    required this.services,
    required this.selected,
    required this.onSelected,
  });

  final List<BookingServiceOption> services;
  final BookingServiceOption? selected;
  final ValueChanged<BookingServiceOption> onSelected;

  @override
  Widget build(BuildContext context) {
    return SegmentedButton<int>(
      segments: services
          .map(
            (service) =>
                ButtonSegment(value: service.id, label: Text(service.name)),
          )
          .toList(),
      selected: selected == null ? <int>{} : {selected!.id},
      onSelectionChanged: (selection) {
        onSelected(services.firstWhere((item) => item.id == selection.first));
      },
      style: ButtonStyle(
        backgroundColor: WidgetStateProperty.resolveWith(
          (states) => states.contains(WidgetState.selected)
              ? const Color(0xFFE1F1F2)
              : const Color(0xFFF4F1F1),
        ),
      ),
    );
  }
}

class _VehicleCard extends StatelessWidget {
  const _VehicleCard({
    required this.vehicle,
    required this.selected,
    required this.onTap,
  });

  final BookingVehicleOption vehicle;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(14),
      child: Container(
        margin: const EdgeInsets.only(bottom: 10),
        padding: const EdgeInsets.all(14),
        decoration: BoxDecoration(
          color: selected ? const Color(0xFFE2F0F1) : Colors.white,
          borderRadius: BorderRadius.circular(14),
          border: Border.all(
            color: selected ? AppColors.primary : const Color(0xFFD2DCDE),
            width: selected ? 2 : 1,
          ),
        ),
        child: Row(
          children: [
            CircleAvatar(
              backgroundColor: const Color(0xFFF3F1F1),
              child: Icon(
                vehicle.isMotorbike ? Icons.two_wheeler : Icons.directions_car,
                color: AppColors.primary,
              ),
            ),
            const SizedBox(width: 14),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    vehicle.name,
                    style: const TextStyle(
                      fontSize: 17,
                      fontWeight: FontWeight.w800,
                    ),
                  ),
                  Text(BookingStrings.plateNumber(vehicle.plateNumber)),
                  Text(BookingStrings.vehicleColor(vehicle.color)),
                ],
              ),
            ),
            if (selected)
              const Icon(Icons.check_circle, color: AppColors.primary),
          ],
        ),
      ),
    );
  }
}
