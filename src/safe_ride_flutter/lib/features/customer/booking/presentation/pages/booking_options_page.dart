import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../../../core/maps/models/map_models.dart';
import '../../../../../core/maps/widgets/map_renderer_widget.dart';
import '../../../../../core/config/api_keys_config.dart';
import '../../../../../core/constants/app_colors.dart';
import '../../../../../core/constants/app_strings.dart';
import '../../../../../core/maps/polyline_decoder.dart';
import '../../../../../core/widgets/app_loading_screen.dart';
import '../../../../../core/widgets/server_error_card.dart';
import '../../../../auth/presentation/providers/auth_provider.dart';
import '../../data/models/booking_catalog.dart';
import '../../data/models/booking_fare_estimate.dart';
import '../../data/models/booking_location.dart';
import '../../data/models/create_booking_request.dart';
import '../../data/models/promo_model.dart';
import '../providers/booking_provider.dart';
import 'location_picker_page.dart';
import '../widgets/select_promo_sheet.dart';
import 'searching_driver_page.dart';

class BookingOptionsPage extends StatefulWidget {
  const BookingOptionsPage({
    super.key,
    this.initialMode = BookingServiceMode.perTrip,
    this.showSchedule = false,
  });

  final BookingServiceMode initialMode;
  final bool showSchedule;

  @override
  State<BookingOptionsPage> createState() => _BookingOptionsPageState();
}

class _BookingOptionsPageState extends State<BookingOptionsPage> {
  BookingLocation? _pickup;
  BookingLocation? _destination;
  BookingServiceOption? _service;
  BookingVehicleOption? _vehicle;
  DateTime? _scheduledAt;
  int _estimatedHours = 2;
  final _specialRequestController = TextEditingController();

  bool get _isHourly => _service?.mode == BookingServiceMode.hourly;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _loadInitialData());
  }

  @override
  void dispose() {
    _specialRequestController.dispose();
    super.dispose();
  }

  Future<void> _loadInitialData() async {
    final token = context.read<AuthProvider>().token;
    if (token == null || token.isEmpty) {
      _showMessage(BookingStrings.sessionExpired);
      return;
    }

    final provider = context.read<BookingProvider>();
    provider.clearFareEstimate();

    final currentLocationFuture = provider.getCurrentLocation().timeout(
      const Duration(seconds: 15),
      onTimeout: () => null,
    );
    await provider.loadCatalog(token, forceRefresh: true);

    if (!mounted) return;

    final catalog = provider.catalog;
    if (catalog != null) {
      setState(() {
        _service = _selectInitialService(catalog.services);
        _vehicle = catalog.vehicles.firstOrNull;
        if (widget.showSchedule) {
          _scheduledAt = DateTime.now().add(const Duration(minutes: 31));
        }
      });
    } else if (provider.errorMessage != null) {
      _showMessage(provider.errorMessage!);
      return;
    }

    final currentLocation = await currentLocationFuture.catchError((_) => null);
    if (mounted && currentLocation != null) {
      setState(() => _pickup = currentLocation);
      await _refreshEstimate();
    }
  }

  BookingServiceOption? _selectInitialService(
    List<BookingServiceOption> services,
  ) {
    final matching = services
        .where((service) => service.mode == widget.initialMode)
        .firstOrNull;
    return matching ?? services.firstOrNull;
  }

  Future<void> _pickLocation(LocationPickerType type) async {
    final selected = type == LocationPickerType.pickup ? _pickup : _destination;
    final location = await Navigator.of(context).push<BookingLocation>(
      MaterialPageRoute(
        builder: (_) => LocationPickerPage(
          type: type,
          initialLocation: selected,
          initialCameraLocation: selected ?? _pickup,
        ),
      ),
    );
    if (!mounted || location == null) return;

    setState(() {
      if (type == LocationPickerType.pickup) {
        _pickup = location;
      } else {
        _destination = location;
      }
    });
    await _refreshEstimate();
  }

  Future<void> _selectSchedule() async {
    final now = DateTime.now();
    final initial =
        (_scheduledAt ?? now.add(const Duration(minutes: 31))).isBefore(
          now.add(const Duration(minutes: 31)),
        )
        ? now.add(const Duration(minutes: 31))
        : _scheduledAt ?? now.add(const Duration(minutes: 31));

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
    final pickup = _pickup;
    final service = _service;
    final vehicle = _vehicle;
    if (token == null ||
        token.isEmpty ||
        pickup == null ||
        service == null ||
        vehicle == null) {
      return;
    }
    if (service.mode == BookingServiceMode.perTrip && _destination == null) {
      context.read<BookingProvider>().clearFareEstimate();
      return;
    }

    await context.read<BookingProvider>().estimateFare(
      token,
      vehicleId: vehicle.id,
      serviceTypeId: service.id,
      pickup: pickup,
      destination: _isHourly ? null : _destination,
      estimatedHours: _isHourly ? _estimatedHours : null,
    );
  }

  Future<void> _startDriverSearch() async {
    final token = context.read<AuthProvider>().token;
    final pickup = _pickup;
    final service = _service;
    final vehicle = _vehicle;
    final estimate = context.read<BookingProvider>().fareEstimate;

    if (token == null || token.isEmpty) {
      _showMessage(BookingStrings.sessionExpired);
      return;
    }
    if (pickup == null) {
      _showMessage('Vui lòng chọn điểm đón.');
      return;
    }
    if (service == null || vehicle == null) {
      _showMessage(BookingStrings.selectServiceAndVehicle);
      return;
    }
    if (!_isHourly && _destination == null) {
      _showMessage('Vui lòng chọn điểm đến.');
      return;
    }
    if (widget.showSchedule && _scheduledAt == null) {
      _showMessage(BookingStrings.selectPickupTimeRequired);
      return;
    }
    if (!widget.showSchedule && estimate == null) {
      _showMessage('Chưa có giá dự kiến. Vui lòng kiểm tra lại tuyến đường.');
      return;
    }

    final destination = _isHourly ? null : _destination;

    final result = await context.read<BookingProvider>().createBooking(
      token,
      CreateBookingRequest(
        vehicleId: vehicle.id,
        serviceTypeId: service.id,
        bookingType: widget.showSchedule
            ? BookingType.scheduled
            : BookingType.now,
        scheduledAt: widget.showSchedule ? _scheduledAt : null,
        pickup: pickup,
        destination: destination,
        estimatedHours: _isHourly ? _estimatedHours : null,
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

    if (widget.showSchedule) {
      _showMessage(BookingStrings.bookingSuccess);
      Navigator.of(context).popUntil((route) => route.isFirst);
      return;
    }

    await Navigator.of(context).pushReplacement(
      MaterialPageRoute(
        builder: (_) => SearchingDriverPage(
          booking: result,
          pickup: pickup,
          destination: destination,
          fareEstimate: estimate,
          vehicle: vehicle,
        ),
      ),
    );
  }

  void _showPromoSheet() {
    SelectPromoSheet.show(context);
  }

  void _showMessage(String message) {
    ScaffoldMessenger.of(context)
      ..hideCurrentSnackBar()
      ..showSnackBar(SnackBar(content: Text(message)));
  }

  Future<void> _showVehiclePicker(List<BookingVehicleOption> vehicles) async {
    final selected = await showModalBottomSheet<BookingVehicleOption>(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.white,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(24)),
      ),
      builder: (context) => Container(
        padding: const EdgeInsets.fromLTRB(20, 12, 20, 20),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
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
            const SizedBox(height: 20),
            const Text(
              BookingStrings.selectVehicle,
              style: TextStyle(fontSize: 20, fontWeight: FontWeight.w800),
            ),
            const SizedBox(height: 16),
            ConstrainedBox(
              constraints: BoxConstraints(
                maxHeight: MediaQuery.of(context).size.height * 0.6,
              ),
              child: SingleChildScrollView(
                child: Column(
                  children: vehicles
                      .map(
                        (vehicle) => _VehicleCard(
                          vehicle: vehicle,
                          selected: vehicle.id == _vehicle?.id,
                          onTap: () => Navigator.pop(context, vehicle),
                        ),
                      )
                      .toList(),
                ),
              ),
            ),
          ],
        ),
      ),
    );

    if (selected != null && mounted) {
      setState(() => _vehicle = selected);
      await _refreshEstimate();
    }
  }

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<BookingProvider>();
    final catalog = provider.catalog;
    final hasError = provider.errorMessage != null;

    // Show loading only if catalog is null AND there is no error message
    if (catalog == null && !hasError) {
      return const AppLoadingScreen(message: 'Đang tải thông tin dịch vụ...');
    }

    return Scaffold(
      backgroundColor: const Color(0xFFF7FAFA),
      body: Stack(
        children: [
          // Lớp dưới cùng: Bản đồ
          Positioned.fill(
            bottom:
                MediaQuery.of(context).size.height *
                0.55, // Để bản đồ không bị che hết bởi panel
            child: _MapPreview(
              pickup: _pickup,
              destination: _isHourly ? null : _destination,
              estimate: provider.fareEstimate,
              onBack: () => Navigator.pop(context),
            ),
          ),

          // Lớp trên: Panel trắng bo tròn
          Positioned(
            top:
                MediaQuery.of(context).size.height *
                0.36, // Vị trí bắt đầu của panel trắng
            left: 0,
            right: 0,
            bottom: 0,
            child: Container(
              width: double.infinity,
              padding: const EdgeInsets.fromLTRB(20, 12, 20, 0),
              decoration: const BoxDecoration(
                color: Colors.white,
                borderRadius: BorderRadius.vertical(
                  top: Radius.circular(32),
                ), // Tăng độ bo tròn
                boxShadow: [
                  BoxShadow(
                    color: Colors.black12,
                    blurRadius: 15,
                    offset: Offset(0, -5),
                  ),
                ],
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
                  if (catalog == null ||
                      catalog.services.isEmpty ||
                      catalog.vehicles.isEmpty) ...[
                    if (hasError)
                      Padding(
                        padding: const EdgeInsets.only(top: 24),
                        child: ServerErrorCard(
                          message: provider.errorMessage!,
                          onRetry: _loadInitialData,
                        ),
                      )
                    else
                      const _EmptyCatalogMessage(),
                  ] else ...[
                    _ServiceSelector(
                      services: catalog.services,
                      selected: _selectedServiceOrFirst(catalog.services),
                      onSelected: (service) async {
                        setState(() {
                          _service = service;
                          if (widget.showSchedule) {
                            _scheduledAt ??= DateTime.now().add(
                              const Duration(minutes: 31),
                            );
                          }
                        });
                        await _refreshEstimate();
                      },
                    ),
                    const SizedBox(height: 18),
                    _RouteSummary(
                      pickup: _pickup,
                      destination: _isHourly ? null : _destination,
                      estimate: provider.fareEstimate,
                      isLoading: provider.isEstimating,
                      onPickupTap: () =>
                          _pickLocation(LocationPickerType.pickup),
                      onDestinationTap: _isHourly
                          ? null
                          : () => _pickLocation(LocationPickerType.destination),
                      estimatedHours: _isHourly ? _estimatedHours : null,
                    ),
                    if (hasError)
                      Padding(
                        padding: const EdgeInsets.only(top: 16),
                        child: ServerErrorCard(
                          message: provider.errorMessage!,
                          onRetry: _refreshEstimate,
                        ),
                      ),
                    if (_isHourly) ...[
                      const SizedBox(height: 16),
                      _HourInput(
                        value: _estimatedHours,
                        onChanged: (value) async {
                          setState(() => _estimatedHours = value);
                          await _refreshEstimate();
                        },
                      ),
                    ],
                    if (widget.showSchedule) ...[
                      const SizedBox(height: 16),
                      _ScheduleCard(
                        scheduledAt: _scheduledAt,
                        onTap: _selectSchedule,
                      ),
                    ],
                    const SizedBox(height: 18),
                    const Text(
                      BookingStrings.selectVehicle,
                      style: TextStyle(
                        fontSize: 18,
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                    const SizedBox(height: 10),
                    if (_vehicle != null)
                      _VehicleCard(
                        vehicle: _vehicle!,
                        selected: true,
                        isDropdown: true,
                        onTap: () => _showVehiclePicker(catalog.vehicles),
                      ),
                  ],
                  const SizedBox(height: 12),
                  _PromoTile(
                    selectedPromo: provider.selectedPromo,
                    onTap: _showPromoSheet,
                    onClear: provider.clearSelectedPromo,
                  ),
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
                  const SizedBox(
                    height: 100,
                  ), // Khoảng trống cho nút bấm phía dưới
                ],
              ),
            ),
          ),
        ],
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
                  : _startDriverSearch,
              style: FilledButton.styleFrom(
                backgroundColor: AppColors.primary,
                shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(14),
                ),
              ),
              child: provider.isLoading
                  ? const CircularProgressIndicator(color: Colors.white)
                  : Text(
                      widget.showSchedule
                          ? BookingStrings.confirmScheduled
                          : _isHourly
                          ? 'Xác nhận thuê theo giờ'
                          : BookingStrings.confirmNow,
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

  BookingServiceOption? _selectedServiceOrFirst(
    List<BookingServiceOption> services,
  ) {
    if (services.isEmpty) return null;
    return services.firstWhere(
      (service) => service.id == _service?.id,
      orElse: () => services.first,
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

  final BookingLocation? pickup;
  final BookingLocation? destination;
  final BookingFareEstimate? estimate;
  final VoidCallback onBack;

  @override
  State<_MapPreview> createState() => _MapPreviewState();
}

class _MapPreviewState extends State<_MapPreview> {
  static const _fallback = AppLatLng(10.7769, 106.7009);
  AppMapController? _controller;

  List<AppLatLng> _cachedPoints = const [];
  String? _lastEncodedPolyline;

  List<AppLatLng> get _routePoints {
    final encoded = widget.estimate?.encodedPolyline;
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

  AppLatLng get _pickup => widget.pickup == null
      ? _fallback
      : AppLatLng(widget.pickup!.latitude, widget.pickup!.longitude);

  AppLatLng? get _destination => widget.destination == null
      ? null
      : AppLatLng(widget.destination!.latitude, widget.destination!.longitude);

  @override
  void dispose() {
    _controller?.dispose();
    super.dispose();
  }

  @override
  void didUpdateWidget(covariant _MapPreview oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.estimate?.encodedPolyline !=
            widget.estimate?.encodedPolyline ||
        oldWidget.pickup != widget.pickup ||
        oldWidget.destination != widget.destination) {
      WidgetsBinding.instance.addPostFrameCallback((_) => _fitRoute());
    }
  }

  Future<void> _fitRoute() async {
    final controller = _controller;
    if (controller == null) return;

    final destination = _destination;
    final routePoints = _routePoints;
    final boundsPoints = routePoints.isNotEmpty
        ? routePoints
        : destination == null
        ? [_pickup]
        : [_pickup, destination];

    if (boundsPoints.length == 1) {
      await controller.animateCamera(AppCameraPosition(target: _pickup, zoom: 15));
      return;
    }

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

    await controller.animateCameraToBounds(
      AppLatLng(minLatitude, minLongitude),
      AppLatLng(maxLatitude, maxLongitude),
      80, // Increased padding for better visibility
    );
  }

  @override
  Widget build(BuildContext context) {
    final destination = _destination;
    final routePoints = _routePoints;
    return Stack(
      fit: StackFit.expand,
      children: [
        MapRendererWidget(
          initialCameraPosition: AppCameraPosition(target: _pickup, zoom: 14),
          markers: {
            if (widget.pickup != null)
              AppMarker(
                id: 'pickup',
                position: _pickup,
                hue: 210.0, // Azure
              ),
            if (destination != null)
              AppMarker(
                id: 'destination',
                position: destination,
                hue: 0.0, // Red
              ),
          },
          polylines: {
            if (routePoints.isNotEmpty)
              AppPolyline(
                id: 'route',
                points: routePoints,
                color: AppColors.primary,
                width: 5,
              )
            else if (widget.pickup != null && destination != null)
              AppPolyline(
                id: 'direct_route',
                points: [_pickup, destination],
                color: AppColors.primary.withOpacity(0.5),
                width: 4,
              ),
          },
          onMapCreated: (controller) {
            _controller = controller;
            WidgetsBinding.instance.addPostFrameCallback((_) => _fitRoute());
          },
          myLocationButtonEnabled: false,
        ),
        // Nút quay lại được bọc trong SafeArea để tránh bị lấp bởi Status Bar
        Positioned(
          left: 20,
          top: 0,
          child: SafeArea(
            child: Padding(
              padding: const EdgeInsets.only(top: 10),
              child: CircleAvatar(
                backgroundColor: Colors.white,
                child: IconButton(
                  onPressed: widget.onBack,
                  icon: const Icon(Icons.arrow_back, color: Color(0xFF263334)),
                ),
              ),
            ),
          ),
        ),
      ],
    );
  }
}

class _RouteSummary extends StatelessWidget {
  const _RouteSummary({
    required this.pickup,
    required this.destination,
    required this.estimate,
    required this.isLoading,
    required this.onPickupTap,
    required this.onDestinationTap,
    required this.estimatedHours,
  });

  final BookingLocation? pickup;
  final BookingLocation? destination;
  final BookingFareEstimate? estimate;
  final bool isLoading;
  final VoidCallback onPickupTap;
  final VoidCallback? onDestinationTap;
  final int? estimatedHours;

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
            value: pickup?.address ?? 'Chọn điểm đón',
            onTap: onPickupTap,
          ),
          if (onDestinationTap != null) ...[
            const Divider(height: 22),
            _RouteRow(
              icon: Icons.location_on,
              color: const Color(0xFFC61E27),
              label: BookingStrings.destinationLabel,
              value: destination?.address ?? 'Chọn điểm đến',
              onTap: onDestinationTap!,
            ),
          ],
          const Divider(height: 22),
          if (isLoading)
            const Row(
              children: [
                SizedBox.square(
                  dimension: 18,
                  child: CircularProgressIndicator(strokeWidth: 2),
                ),
                SizedBox(width: 10),
                Text('Đang tính giá dự kiến...'),
              ],
            )
          else if (estimate != null)
            Row(
              children: [
                Expanded(
                  child: _EstimateValue(
                    icon: Icons.route,
                    value: estimatedHours == null
                        ? '${estimate!.estimatedDistanceKm.toStringAsFixed(1)} km'
                        : '$estimatedHours giờ',
                  ),
                ),
                Expanded(
                  child: _EstimateValue(
                    icon: Icons.schedule,
                    value: estimatedHours == null
                        ? '${estimate!.estimatedDurationMinutes} phút'
                        : '${estimate!.estimatedDurationMinutes} phút',
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
    final buffer = StringBuffer();
    for (var index = 0; index < digits.length; index++) {
      if (index > 0 && (digits.length - index) % 3 == 0) buffer.write('.');
      buffer.write(digits[index]);
    }
    return '$bufferđ';
  }
}

class _RouteRow extends StatelessWidget {
  const _RouteRow({
    required this.icon,
    required this.color,
    required this.label,
    required this.value,
    required this.onTap,
  });

  final IconData icon;
  final Color color;
  final String label;
  final String value;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      child: Row(
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
          const Icon(Icons.chevron_right, size: 18),
        ],
      ),
    );
  }
}

class _HourInput extends StatelessWidget {
  const _HourInput({required this.value, required this.onChanged});

  final int value;
  final ValueChanged<int> onChanged;

  @override
  Widget build(BuildContext context) {
    return Container(
      height: 58,
      decoration: BoxDecoration(
        color: const Color(0xFFEAF4F4),
        borderRadius: BorderRadius.circular(12),
      ),
      child: Row(
        children: [
          IconButton(
            onPressed: value > 1 ? () => onChanged(value - 1) : null,
            icon: const Icon(Icons.remove_circle_outline),
          ),
          Expanded(
            child: Center(
              child: Text(
                '$value giờ thuê dự kiến',
                style: const TextStyle(
                  color: AppColors.primary,
                  fontWeight: FontWeight.w800,
                ),
              ),
            ),
          ),
          IconButton(
            onPressed: value < 24 ? () => onChanged(value + 1) : null,
            icon: const Icon(Icons.add_circle_outline),
          ),
        ],
      ),
    );
  }
}

class _PromoTile extends StatelessWidget {
  const _PromoTile({
    required this.onTap,
    this.selectedPromo,
    required this.onClear,
  });

  final VoidCallback onTap;
  final PromoModel? selectedPromo;
  final VoidCallback onClear;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(12),
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
        decoration: BoxDecoration(
          color: selectedPromo != null
              ? const Color(0xFFEAF4F4)
              : const Color(0xFFF8F6F6),
          borderRadius: BorderRadius.circular(12),
          border: selectedPromo != null
              ? Border.all(color: AppColors.primary)
              : null,
        ),
        child: Row(
          children: [
            const Icon(Icons.local_offer, color: AppColors.primary),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    selectedPromo?.promotionCode ?? 'Thêm mã khuyến mãi',
                    style: const TextStyle(
                      fontSize: 16,
                      fontWeight: FontWeight.w700,
                      color: AppColors.primary,
                    ),
                  ),
                  if (selectedPromo != null)
                    Text(
                      selectedPromo!.shortDescription,
                      style: const TextStyle(
                        fontSize: 13,
                        color: Color(0xFF626A6C),
                      ),
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                    ),
                ],
              ),
            ),
            if (selectedPromo != null)
              IconButton(
                onPressed: onClear,
                icon: const Icon(Icons.cancel, color: Colors.grey, size: 20),
                padding: EdgeInsets.zero,
                constraints: const BoxConstraints(),
              )
            else
              const Icon(Icons.chevron_right),
          ],
        ),
      ),
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
    final selectedId = services.any((service) => service.id == selected?.id)
        ? selected!.id
        : services.first.id;

    return SegmentedButton<int>(
      segments: services
          .map(
            (service) => ButtonSegment(
              value: service.id,
              label: Text(_translateServiceName(service)),
            ),
          )
          .toList(),
      selected: {selectedId},
      onSelectionChanged: (selection) {
        final selectedService = services.firstWhere(
          (item) => item.id == selection.first,
          orElse: () => services.first,
        );
        onSelected(selectedService);
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

  String _translateServiceName(BookingServiceOption service) {
    if (service.name.toLowerCase() == 'pertrip') {
      return BookingStrings.tripService;
    }
    if (service.name.toLowerCase() == 'hourly') {
      return BookingStrings.hourlyService;
    }
    // Fallback if the name is already Vietnamese or something else
    return service.name;
  }
}

class _VehicleCard extends StatelessWidget {
  const _VehicleCard({
    required this.vehicle,
    required this.selected,
    required this.onTap,
    this.isDropdown = false,
  });

  final BookingVehicleOption vehicle;
  final bool selected;
  final VoidCallback onTap;
  final bool isDropdown;

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
            if (isDropdown)
              const Icon(Icons.keyboard_arrow_down, color: Color(0xFF626A6C))
            else if (selected)
              const Icon(Icons.check_circle, color: AppColors.primary),
          ],
        ),
      ),
    );
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
