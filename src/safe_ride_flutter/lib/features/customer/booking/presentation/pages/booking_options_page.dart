import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:intl/intl.dart';

import '../../../../../core/maps/models/map_models.dart';
import '../../../../../core/maps/widgets/map_renderer_widget.dart';
import '../../../../../core/constants/app_colors.dart';
import '../../../../../core/localization/localization_extensions.dart';
import '../../../../../core/localization/locale_provider.dart';
import '../../../../../core/constants/app_strings.dart';
import '../../../../../core/maps/polyline_decoder.dart';
import '../../../../../core/widgets/app_loading_screen.dart';
import '../../../../../core/widgets/server_error_card.dart';
import '../../../../auth/presentation/providers/auth_provider.dart';
import '../../../../shared/profile/data/models/vehicle_model.dart';
import '../../../../shared/profile/presentation/providers/vehicle_provider.dart';
import '../../../../shared/profile/presentation/widgets/vehicle_form_sheet.dart';
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
  BookingOptionsPage({
    super.key,
    this.initialMode = BookingServiceMode.perTrip,
    this.showSchedule = false,
    this.initialPickup,
    this.initialDestination,
  });

  final BookingServiceMode initialMode;
  final bool showSchedule;
  final BookingLocation? initialPickup;
  final BookingLocation? initialDestination;

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
    _pickup = widget.initialPickup;
    _destination = widget.initialDestination;
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
      _showMessage(context.l10n.sessionExpired);
      return;
    }

    final provider = context.read<BookingProvider>();
    provider.clearFareEstimate();

    final currentLocationFuture = provider.getCurrentLocation();
    await provider.loadCatalog(token, forceRefresh: true);

    if (!mounted) return;

    final catalog = provider.catalog;
    if (catalog != null) {
      setState(() {
        _service = _selectInitialService(catalog.services);
        _vehicle = catalog.vehicles.firstOrNull;
        if (widget.showSchedule) {
          _scheduledAt = DateTime.now().add(Duration(minutes: 31));
        }
      });
    } else if (provider.errorMessage != null) {
      _showMessage(provider.errorMessage!);
      return;
    }

    final currentLocation = await currentLocationFuture.catchError((_) => null);
    if (!mounted) return;

    if (_pickup == null && currentLocation != null) {
      setState(() => _pickup = currentLocation);
    }
    if (_pickup != null) {
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
        (_scheduledAt ?? now.add(Duration(minutes: 31))).isBefore(
          now.add(Duration(minutes: 31)),
        )
        ? now.add(Duration(minutes: 31))
        : _scheduledAt ?? now.add(Duration(minutes: 31));

    final date = await showDatePicker(
      context: context,
      firstDate: now,
      lastDate: now.add(Duration(days: 90)),
      initialDate: initial,
      helpText: context.l10n.selectPickupDate,
    );
    if (date == null || !mounted) return;

    final time = await showTimePicker(
      context: context,
      initialTime: TimeOfDay.fromDateTime(initial),
      helpText: context.l10n.selectPickupTimeHelp,
    );
    if (time == null) return;

    final scheduledAt = DateTime(
      date.year,
      date.month,
      date.day,
      time.hour,
      time.minute,
    );
    if (scheduledAt.isBefore(now.add(Duration(minutes: 30)))) {
      _showMessage(context.l10n.invalidSchedule);
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
      _showMessage(context.l10n.sessionExpired);
      return;
    }
    if (pickup == null) {
      _showMessage(context.l10n.selectPickupRequired);
      return;
    }
    if (service == null || vehicle == null) {
      _showMessage(context.l10n.selectServiceAndVehicle);
      return;
    }
    if (!_isHourly && _destination == null) {
      _showMessage(context.l10n.selectDestinationRequired);
      return;
    }
    if (widget.showSchedule && _scheduledAt == null) {
      _showMessage(context.l10n.selectPickupTimeRequired);
      return;
    }
    if (!widget.showSchedule && estimate == null) {
      _showMessage(context.l10n.fareEstimateUnavailable);
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
            context.l10n.bookingFailed,
      );
      return;
    }

    if (widget.showSchedule) {
      _showMessage(context.l10n.bookingSuccess);
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

  Future<BookingVehicleOption?> _showAddVehicleSheet() async {
    final vehicleProvider = context.read<VehicleProvider>();
    final bookingProvider = context.read<BookingProvider>();
    final knownVehicleIds = vehicleProvider.vehicles
        .map((item) => item.id)
        .toSet();
    VehicleModel? savedVehicle;

    await VehicleFormSheet.show(
      context,
      onSave: (newVehicle) async {
        final saved = await vehicleProvider.saveVehicle(newVehicle);
        if (!mounted) return false;
        if (!saved) {
          _showMessage(
            vehicleProvider.errorMessage ?? context.l10n.addVehicleFailed,
          );
          return false;
        }

        savedVehicle =
            vehicleProvider.vehicles
                .where((item) => !knownVehicleIds.contains(item.id))
                .lastOrNull ??
            vehicleProvider.vehicles.lastOrNull;
        return true;
      },
    );

    if (!mounted || savedVehicle == null) return null;

    final token = context.read<AuthProvider>().token;
    if (token != null && token.isNotEmpty) {
      await bookingProvider.loadCatalog(token, forceRefresh: true);
    }
    if (!mounted) return null;

    final catalogVehicle = bookingProvider.catalog?.vehicles
        .where((item) => item.id == savedVehicle!.id)
        .firstOrNull;
    final selectedVehicle =
        catalogVehicle ?? _bookingVehicleFrom(savedVehicle!);

    setState(() => _vehicle = selectedVehicle);
    await _refreshEstimate();
    _showMessage(context.l10n.vehicleAdded);
    return selectedVehicle;
  }

  Future<void> _showVehiclePicker(List<BookingVehicleOption> vehicles) async {
    final selected = await showModalBottomSheet<BookingVehicleOption>(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.white,
      shape: RoundedRectangleBorder(
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
                  color: Color(0xFFD8DCDD),
                  borderRadius: BorderRadius.circular(8),
                ),
              ),
            ),
            SizedBox(height: 20),
            Text(
              context.l10n.selectYourVehicle,
              style: TextStyle(fontSize: 20, fontWeight: FontWeight.w800),
            ),
            SizedBox(height: 16),
            ConstrainedBox(
              constraints: BoxConstraints(
                maxHeight: MediaQuery.of(context).size.height * 0.6,
              ),
              child: SingleChildScrollView(
                child: Column(
                  children: [
                    _AddVehicleTile(
                      onTap: () {
                        Navigator.pop(context);
                        WidgetsBinding.instance.addPostFrameCallback((_) {
                          if (mounted) _showAddVehicleSheet();
                        });
                      },
                    ),
                    SizedBox(height: 10),
                    ...vehicles.map(
                      (vehicle) => _VehicleCard(
                        vehicle: vehicle,
                        selected: vehicle.id == _vehicle?.id,
                        onTap: () => Navigator.pop(context, vehicle),
                      ),
                    ),
                  ],
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

  BookingVehicleOption _bookingVehicleFrom(VehicleModel vehicle) {
    return BookingVehicleOption(
      id: vehicle.id,
      name: vehicle.name,
      plateNumber: vehicle.plateNumber,
      color: vehicle.color,
      isMotorbike: vehicle.type == VehicleType.motorbike,
    );
  }

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<BookingProvider>();
    final catalog = provider.catalog;
    final hasError = provider.errorMessage != null;

    // Show loading only if catalog is null AND there is no error message
    if (catalog == null && !hasError) {
      return AppLoadingScreen(message: context.l10n.loadingServices);
    }

    return Scaffold(
      backgroundColor: Color(0xFFF7FAFA),
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
              decoration: BoxDecoration(
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
                        color: Color(0xFFD8DCDD),
                        borderRadius: BorderRadius.circular(8),
                      ),
                    ),
                  ),
                  SizedBox(height: 18),
                  if (catalog == null || catalog.services.isEmpty) ...[
                    if (hasError)
                      Padding(
                        padding: const EdgeInsets.only(top: 24),
                        child: ServerErrorCard(
                          message: provider.errorMessage!,
                          onRetry: _loadInitialData,
                        ),
                      )
                    else
                      _EmptyCatalogMessage(),
                  ] else ...[
                    _ServiceSelector(
                      services: catalog.services,
                      selected: _selectedServiceOrFirst(catalog.services),
                      onSelected: (service) async {
                        setState(() {
                          _service = service;
                          if (widget.showSchedule) {
                            _scheduledAt ??= DateTime.now().add(
                              Duration(minutes: 31),
                            );
                          }
                        });
                        await _refreshEstimate();
                      },
                    ),
                    SizedBox(height: 18),
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
                      SizedBox(height: 16),
                      _HourInput(
                        value: _estimatedHours,
                        onChanged: (value) async {
                          setState(() => _estimatedHours = value);
                          await _refreshEstimate();
                        },
                      ),
                    ],
                    if (widget.showSchedule) ...[
                      SizedBox(height: 16),
                      _ScheduleCard(
                        scheduledAt: _scheduledAt,
                        onTap: _selectSchedule,
                      ),
                    ],
                    SizedBox(height: 18),
                    Text(
                      context.l10n.selectYourVehicle,
                      style: TextStyle(
                        fontSize: 18,
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                    SizedBox(height: 10),
                    if (_vehicle != null)
                      _VehicleCard(
                        vehicle: _vehicle!,
                        selected: true,
                        isDropdown: true,
                        onTap: () => _showVehiclePicker(catalog.vehicles),
                      )
                    else
                      _AddVehiclePrompt(onTap: _showAddVehicleSheet),
                  ],
                  SizedBox(height: 12),
                  _PromoTile(
                    selectedPromo: provider.selectedPromo,
                    onTap: _showPromoSheet,
                    onClear: provider.clearSelectedPromo,
                  ),
                  SizedBox(height: 12),
                  TextField(
                    controller: _specialRequestController,
                    maxLength: 500,
                    decoration: InputDecoration(
                      hintText: context.l10n.specialRequest,
                      prefixIcon: Icon(Icons.notes),
                      border: OutlineInputBorder(
                        borderRadius: BorderRadius.circular(12),
                      ),
                    ),
                  ),
                  Text(
                    context.l10n.fareCalculationNote,
                    style: TextStyle(color: Color(0xFF667174), fontSize: 13),
                  ),
                  SizedBox(height: 100), // Khoảng trống cho nút bấm phía dưới
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
                      _vehicle == null ||
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
                  ? CircularProgressIndicator(color: Colors.white)
                  : Text(
                      widget.showSchedule
                          ? context.l10n.confirmScheduled
                          : _isHourly
                          ? context.l10n.confirmHourlyHire
                          : context.l10n.confirmNow,
                      style: TextStyle(
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
  _MapPreview({
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

  List<AppLatLng> _cachedPoints = [];
  String? _lastEncodedPolyline;

  List<AppLatLng> get _routePoints {
    final encoded = widget.estimate?.encodedPolyline;
    if (encoded == null || encoded.isEmpty) return [];

    if (encoded == _lastEncodedPolyline) {
      return _cachedPoints;
    }

    try {
      _lastEncodedPolyline = encoded;
      _cachedPoints = decodePolyline(encoded);
      return _cachedPoints;
    } on FormatException {
      return [];
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
      await controller.animateCamera(
        AppCameraPosition(target: _pickup, zoom: 15),
      );
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
                markerType: AppMarkerType.pickup,
              ),
            if (destination != null)
              AppMarker(
                id: 'destination',
                position: destination,
                markerType: AppMarkerType.destination,
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
          top: MediaQuery.of(context).viewPadding.top + 10,
          child: CircleAvatar(
            backgroundColor: Colors.white,
            child: IconButton(
              onPressed: widget.onBack,
              icon: Icon(Icons.arrow_back, color: Color(0xFF263334)),
            ),
          ),
        ),
      ],
    );
  }
}

class _RouteSummary extends StatelessWidget {
  _RouteSummary({
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
        color: Color(0xFFF8F6F6),
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: Color(0xFFE6E1E1)),
      ),
      child: Column(
        children: [
          _RouteRow(
            icon: Icons.person_pin_circle_rounded,
            color: Color(0xFF1565C0),
            label: context.l10n.pickupPoint.toUpperCase(),
            value: pickup?.address ?? context.l10n.selectPickup,
            onTap: onPickupTap,
          ),
          if (onDestinationTap != null) ...[
            Divider(height: 22),
            _RouteRow(
              icon: Icons.flag_rounded,
              color: Color(0xFFC62828),
              label: context.l10n.destinationPoint.toUpperCase(),
              value: destination?.address ?? context.l10n.selectDestination,
              onTap: onDestinationTap!,
            ),
          ],
          Divider(height: 22),
          if (isLoading)
            Row(
              children: [
                SizedBox.square(
                  dimension: 18,
                  child: CircularProgressIndicator(strokeWidth: 2),
                ),
                SizedBox(width: 10),
                Text(context.l10n.calculatingFare),
              ],
            )
          else if (estimate != null) ...[
            Row(
              children: [
                Expanded(
                  child: _EstimateValue(
                    icon: Icons.route,
                    value: estimatedHours == null
                        ? '${estimate!.estimatedDistanceKm.toStringAsFixed(1)} km'
                        : context.l10n.hoursValue(estimatedHours!),
                  ),
                ),
                Expanded(
                  child: _EstimateValue(
                    icon: Icons.schedule,
                    value: estimatedHours == null
                        ? context.l10n.minutesValue(
                            estimate!.estimatedDurationMinutes,
                          )
                        : context.l10n.minutesValue(
                            estimate!.estimatedDurationMinutes,
                          ),
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
            if (estimate!.surgeMultiplier != null &&
                estimate!.surgeMultiplier! > 1.0)
              Padding(
                padding: const EdgeInsets.only(top: 12),
                child: Row(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    Icon(Icons.trending_up, color: Colors.orange, size: 18),
                    SizedBox(width: 6),
                    Text(
                      context.l10n.surgePricing(estimate!.surgeMultiplier!),
                      style: TextStyle(
                        color: Colors.orange,
                        fontWeight: FontWeight.bold,
                        fontSize: 12,
                      ),
                    ),
                  ],
                ),
              ),
          ],
        ],
      ),
    );
  }

  static String _formatEstimateCurrency(double value) {
    return NumberFormat.currency(
      locale: LocaleProvider.currentLocale.toLanguageTag(),
      symbol: 'VND',
      decimalDigits: 0,
    ).format(value);
  }
}

class _RouteRow extends StatelessWidget {
  _RouteRow({
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
          SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  label,
                  style: TextStyle(
                    fontSize: 11,
                    fontWeight: FontWeight.w700,
                    color: Color(0xFF626A6C),
                  ),
                ),
                SizedBox(height: 3),
                Text(value, maxLines: 2, overflow: TextOverflow.ellipsis),
              ],
            ),
          ),
          Icon(Icons.chevron_right, size: 18),
        ],
      ),
    );
  }
}

class _HourInput extends StatelessWidget {
  _HourInput({required this.value, required this.onChanged});

  final int value;
  final ValueChanged<int> onChanged;

  @override
  Widget build(BuildContext context) {
    return Container(
      height: 58,
      decoration: BoxDecoration(
        color: Color(0xFFEAF4F4),
        borderRadius: BorderRadius.circular(12),
      ),
      child: Row(
        children: [
          IconButton(
            onPressed: value > 1 ? () => onChanged(value - 1) : null,
            icon: Icon(Icons.remove_circle_outline),
          ),
          Expanded(
            child: Center(
              child: Text(
                context.l10n.estimatedRentalHours(value),
                style: TextStyle(
                  color: AppColors.primary,
                  fontWeight: FontWeight.w800,
                ),
              ),
            ),
          ),
          IconButton(
            onPressed: value < 24 ? () => onChanged(value + 1) : null,
            icon: Icon(Icons.add_circle_outline),
          ),
        ],
      ),
    );
  }
}

class _PromoTile extends StatelessWidget {
  _PromoTile({required this.onTap, this.selectedPromo, required this.onClear});

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
          color: selectedPromo != null ? Color(0xFFEAF4F4) : Color(0xFFF8F6F6),
          borderRadius: BorderRadius.circular(12),
          border: selectedPromo != null
              ? Border.all(color: AppColors.primary)
              : null,
        ),
        child: Row(
          children: [
            Icon(Icons.local_offer, color: AppColors.primary),
            SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    selectedPromo?.promotionCode ?? context.l10n.addPromoCode,
                    style: TextStyle(
                      fontSize: 16,
                      fontWeight: FontWeight.w700,
                      color: AppColors.primary,
                    ),
                  ),
                  if (selectedPromo != null)
                    Text(
                      selectedPromo!.shortDescription,
                      style: TextStyle(fontSize: 13, color: Color(0xFF626A6C)),
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                    ),
                ],
              ),
            ),
            if (selectedPromo != null)
              IconButton(
                onPressed: onClear,
                icon: Icon(Icons.cancel, color: Colors.grey, size: 20),
                padding: EdgeInsets.zero,
                constraints: BoxConstraints(),
              )
            else
              Icon(Icons.chevron_right),
          ],
        ),
      ),
    );
  }
}

class _ScheduleCard extends StatelessWidget {
  _ScheduleCard({required this.scheduledAt, required this.onTap});

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
          color: Color(0xFFEAF4F4),
          borderRadius: BorderRadius.circular(12),
          border: Border.all(color: AppColors.primary),
        ),
        child: Row(
          children: [
            Icon(Icons.calendar_month, color: AppColors.primary),
            SizedBox(width: 12),
            Expanded(
              child: Text(
                scheduledAt == null
                    ? context.l10n.selectPickupTimeHelp
                    : '${scheduledAt!.day}/${scheduledAt!.month}/${scheduledAt!.year} '
                          '${scheduledAt!.hour.toString().padLeft(2, '0')}:'
                          '${scheduledAt!.minute.toString().padLeft(2, '0')}',
                style: TextStyle(fontWeight: FontWeight.w700),
              ),
            ),
            Icon(Icons.chevron_right),
          ],
        ),
      ),
    );
  }
}

class _ServiceSelector extends StatelessWidget {
  _ServiceSelector({
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
              label: Text(_translateServiceName(context, service)),
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
              ? Color(0xFFE1F1F2)
              : Color(0xFFF4F1F1),
        ),
      ),
    );
  }

  String _translateServiceName(
    BuildContext context,
    BookingServiceOption service,
  ) {
    if (service.name.toLowerCase() == 'pertrip') {
      return context.l10n.tripService;
    }
    if (service.name.toLowerCase() == 'hourly') {
      return context.l10n.hourlyService;
    }
    // Fallback if the name is already Vietnamese or something else
    return service.name;
  }
}

class _AddVehiclePrompt extends StatelessWidget {
  _AddVehiclePrompt({required this.onTap});

  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(14),
      child: Container(
        padding: const EdgeInsets.all(14),
        decoration: BoxDecoration(
          color: Color(0xFFFFF8EC),
          borderRadius: BorderRadius.circular(14),
          border: Border.all(color: Color(0xFFFFC56D), width: 1.2),
        ),
        child: Row(
          children: [
            CircleAvatar(
              backgroundColor: Color(0xFFFFE8BD),
              child: Icon(Icons.add_road_rounded, color: Color(0xFF9A5A00)),
            ),
            SizedBox(width: 14),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    context.l10n.addNewVehicle,
                    style: TextStyle(fontSize: 17, fontWeight: FontWeight.w800),
                  ),
                  SizedBox(height: 2),
                  Text(
                    context.l10n.saveVehicleAndContinue,
                    style: TextStyle(color: Color(0xFF626A6C), fontSize: 13),
                  ),
                ],
              ),
            ),
            FilledButton.icon(
              onPressed: onTap,
              icon: Icon(Icons.add, size: 18),
              label: Text(context.l10n.add),
              style: FilledButton.styleFrom(
                backgroundColor: AppColors.primary,
                foregroundColor: Colors.white,
                visualDensity: VisualDensity.compact,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _AddVehicleTile extends StatelessWidget {
  _AddVehicleTile({required this.onTap});

  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(14),
      child: Container(
        padding: const EdgeInsets.all(14),
        decoration: BoxDecoration(
          color: Color(0xFFEAF4F4),
          borderRadius: BorderRadius.circular(14),
          border: Border.all(color: AppColors.primary, width: 1.2),
        ),
        child: Row(
          children: [
            CircleAvatar(
              backgroundColor: Colors.white,
              child: Icon(Icons.add, color: AppColors.primary),
            ),
            SizedBox(width: 14),
            Expanded(
              child: Text(
                context.l10n.addNewVehicle,
                style: TextStyle(fontSize: 17, fontWeight: FontWeight.w800),
              ),
            ),
            Icon(Icons.chevron_right, color: AppColors.primary),
          ],
        ),
      ),
    );
  }
}

class _VehicleCard extends StatelessWidget {
  _VehicleCard({
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
          color: selected ? Color(0xFFE2F0F1) : Colors.white,
          borderRadius: BorderRadius.circular(14),
          border: Border.all(
            color: selected ? AppColors.primary : Color(0xFFD2DCDE),
            width: selected ? 2 : 1,
          ),
        ),
        child: Row(
          children: [
            CircleAvatar(
              backgroundColor: Color(0xFFF3F1F1),
              child: Icon(
                vehicle.isMotorbike ? Icons.two_wheeler : Icons.directions_car,
                color: AppColors.primary,
              ),
            ),
            SizedBox(width: 14),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    vehicle.name,
                    style: TextStyle(fontSize: 17, fontWeight: FontWeight.w800),
                  ),
                  Text(context.l10n.plateNumberLabel(vehicle.plateNumber)),
                  Text(context.l10n.vehicleColorLabel(vehicle.color)),
                ],
              ),
            ),
            if (isDropdown)
              Icon(Icons.keyboard_arrow_down, color: Color(0xFF626A6C))
            else if (selected)
              Icon(Icons.check_circle, color: AppColors.primary),
          ],
        ),
      ),
    );
  }
}

class _EstimateValue extends StatelessWidget {
  _EstimateValue({required this.icon, required this.value});

  final IconData icon;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Icon(icon, color: AppColors.primary, size: 20),
        SizedBox(height: 4),
        Text(
          value,
          textAlign: TextAlign.center,
          style: TextStyle(fontWeight: FontWeight.w700),
        ),
      ],
    );
  }
}

class _EmptyCatalogMessage extends StatelessWidget {
  _EmptyCatalogMessage();

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: Color(0xFFFFF4E5),
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: Color(0xFFFFCC80)),
      ),
      child: Row(
        children: [
          Icon(Icons.directions_car_filled_outlined, color: Color(0xFFB26A00)),
          SizedBox(width: 12),
          Expanded(child: Text(context.l10n.noBookableVehicles)),
        ],
      ),
    );
  }
}

class _MapConfigurationError extends StatelessWidget {
  _MapConfigurationError({required this.onBack});

  final VoidCallback onBack;

  @override
  Widget build(BuildContext context) {
    return ColoredBox(
      color: Color(0xFFE7EEEE),
      child: Stack(
        children: [
          Center(
            child: Padding(
              padding: EdgeInsets.all(32),
              child: Text(
                context.l10n.mapsConfigMissing,
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
                icon: Icon(Icons.arrow_back),
              ),
            ),
          ),
        ],
      ),
    );
  }
}
