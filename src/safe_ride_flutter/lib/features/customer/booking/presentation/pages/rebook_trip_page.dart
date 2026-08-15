import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:intl/intl.dart';

import '../../../../../core/constants/app_colors.dart';
import '../../../../../core/localization/localization_extensions.dart';
import '../../../../../core/localization/locale_provider.dart';
import '../../../../../core/maps/models/map_models.dart';
import '../../../../../core/maps/widgets/map_renderer_widget.dart';
import '../../../../../core/utils/currency_formatter.dart';
import '../../../../../core/widgets/app_loading_screen.dart';
import '../../../../auth/presentation/providers/auth_provider.dart';
import '../../data/models/booking_catalog.dart';
import '../../data/models/booking_location.dart';
import '../../data/models/booking_response.dart';
import '../../data/models/create_booking_request.dart';
import '../../data/models/promo_model.dart';
import '../providers/booking_provider.dart';
import '../widgets/select_promo_sheet.dart';
import 'searching_driver_page.dart';

class RebookTripPage extends StatefulWidget {
  RebookTripPage({super.key, required this.oldBooking});

  final BookingResponse oldBooking;

  @override
  State<RebookTripPage> createState() => _RebookTripPageState();
}

class _RebookTripPageState extends State<RebookTripPage> {
  bool _isScheduled = false;
  BookingLocation? _pickup;
  BookingLocation? _destination;

  @override
  void initState() {
    super.initState();
    _pickup = widget.oldBooking.pickup;
    _destination = widget.oldBooking.destination;
    WidgetsBinding.instance.addPostFrameCallback((_) => _loadData());
  }

  Future<void> _loadData() async {
    final token = context.read<AuthProvider>().token;
    if (token == null) return;

    final provider = context.read<BookingProvider>();
    provider.clearSelectedPromo();

    // Fetch catalog to ensure we have the latest services
    await provider.loadCatalog(token);

    // Estimate fare based on old locations and vehicle
    if (_pickup != null &&
        _destination != null &&
        widget.oldBooking.vehicle != null) {
      // Find the service id, assuming PerTrip = 1 or matching the original mode. We'll find from catalog.
      final service = provider.catalog?.services.firstWhere(
        (s) => s.mode == BookingServiceMode.perTrip,
        orElse: () => provider.catalog!.services.first,
      );

      if (service != null) {
        await provider.estimateFare(
          token,
          vehicleId: widget.oldBooking.vehicle!.id,
          serviceTypeId: service.id,
          pickup: _pickup!,
          destination: _destination!,
        );
      }
    }

    // Load available promotions for choosing a new code only.
    await provider.loadAvailablePromotions(token);
  }

  void _swapLocations() {
    setState(() {
      final temp = _pickup;
      _pickup = _destination;
      _destination = temp;
    });
    _loadData();
  }

  @override
  void dispose() {
    context.read<BookingProvider>().clearSelectedPromo();
    super.dispose();
  }

  void _showPromoSheet() {
    SelectPromoSheet.show(context);
  }

  void _submitRebook() async {
    final token = context.read<AuthProvider>().token;
    if (token == null) return;

    final provider = context.read<BookingProvider>();
    final pickup = _pickup;
    final destination = _destination;
    final vehicle = widget.oldBooking.vehicle;
    final estimate = provider.fareEstimate;

    if (pickup == null || destination == null || vehicle == null) {
      ScaffoldMessenger.of(
        context,
      ).showSnackBar(SnackBar(content: Text(context.l10n.oldTripDataInvalid)));
      return;
    }

    if (!_isScheduled && estimate == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(context.l10n.calculatingFarePleaseWait)),
      );
      return;
    }

    final service = provider.catalog?.services.firstWhere(
      (s) => s.mode == BookingServiceMode.perTrip,
      orElse: () => provider.catalog!.services.first,
    );

    if (service == null) return;

    AppLoadingScreen.show(context);
    final result = await provider.createBooking(
      token,
      CreateBookingRequest(
        vehicleId: vehicle.id,
        serviceTypeId: service.id,
        bookingType: _isScheduled ? BookingType.scheduled : BookingType.now,
        scheduledAt: _isScheduled
            ? DateTime.now().add(Duration(minutes: 35))
            : null,
        pickup: pickup,
        destination: destination,
      ),
    );
    if (!mounted) return;
    AppLoadingScreen.hide(context);

    if (result == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(provider.errorMessage ?? context.l10n.genericError),
        ),
      );
    } else {
      if (result.bookingType == 'Now') {
        provider.setSearchingBooking(result);
        Navigator.pushReplacement(
          context,
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
      } else {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(context.l10n.bookingSuccessful)),
        );
        Navigator.pop(context);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<BookingProvider>();
    final pickup = _pickup;
    final destination = _destination;
    final vehicle = widget.oldBooking.vehicle;

    final markers = <AppMarker>{};
    if (pickup != null) {
      markers.add(
        AppMarker(
          id: 'pickup',
          markerType: AppMarkerType.pickup,
          position: AppLatLng(pickup.latitude, pickup.longitude),
        ),
      );
    }
    if (destination != null) {
      markers.add(
        AppMarker(
          id: 'destination',
          markerType: AppMarkerType.destination,
          position: AppLatLng(destination.latitude, destination.longitude),
        ),
      );
    }

    // Determine center of map
    AppCameraPosition cameraPos = AppCameraPosition(
      target: AppLatLng(10.762622, 106.660172),
      zoom: 14,
    );
    if (pickup != null && destination != null) {
      final centerLat = (pickup.latitude + destination.latitude) / 2;
      final centerLng = (pickup.longitude + destination.longitude) / 2;
      cameraPos = AppCameraPosition(
        target: AppLatLng(centerLat, centerLng),
        zoom: 13,
      );
    } else if (pickup != null) {
      cameraPos = AppCameraPosition(
        target: AppLatLng(pickup.latitude, pickup.longitude),
        zoom: 15,
      );
    }

    final fare = provider.fareEstimate?.estimatedFare ?? 0;
    final discount = _calculateDiscount(provider.selectedPromo, fare);
    final finalFare = (fare - discount).clamp(0, double.infinity);

    return Scaffold(
      backgroundColor: Color(0xFFF9FAFB),
      appBar: AppBar(
        backgroundColor: Colors.white,
        elevation: 0,
        centerTitle: true,
        title: Text(
          context.l10n.rebookTrip,
          style: TextStyle(
            color: AppColors.textPrimary,
            fontSize: 18,
            fontWeight: FontWeight.bold,
          ),
        ),
        leading: IconButton(
          icon: Icon(Icons.arrow_back, color: AppColors.textPrimary),
          onPressed: () => Navigator.pop(context),
        ),
      ),
      body: SingleChildScrollView(
        child: Padding(
          padding: const EdgeInsets.all(16.0),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                context.l10n.confirmPreviousInformation,
                style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
              ),
              SizedBox(height: 4),
              Text(
                context.l10n.reviewRouteAndVehicle,
                style: TextStyle(color: Colors.grey, fontSize: 14),
              ),
              SizedBox(height: 16),

              // Map & Route Card
              Container(
                decoration: BoxDecoration(
                  color: Colors.white,
                  borderRadius: BorderRadius.circular(16),
                  border: Border.all(color: Colors.grey.shade200),
                ),
                child: Column(
                  children: [
                    // Map snippet
                    ClipRRect(
                      borderRadius: const BorderRadius.vertical(
                        top: Radius.circular(16),
                      ),
                      child: SizedBox(
                        height: 120,
                        child: AbsorbPointer(
                          child: MapRendererWidget(
                            initialCameraPosition: cameraPos,
                            markers: markers,
                            myLocationButtonEnabled: false,
                          ),
                        ),
                      ),
                    ),
                    Padding(
                      padding: const EdgeInsets.all(16.0),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Row(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Column(
                                children: [
                                  Icon(
                                    Icons.circle,
                                    size: 12,
                                    color: AppColors.primary,
                                  ),
                                  Container(
                                    height: 20,
                                    width: 2,
                                    color: Colors.grey.shade300,
                                  ),
                                  Icon(
                                    Icons.location_on,
                                    size: 14,
                                    color: Colors.red,
                                  ),
                                ],
                              ),
                              SizedBox(width: 12),
                              Expanded(
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Text(
                                      context.l10n.pickupPoint.toUpperCase(),
                                      style: TextStyle(
                                        color: Colors.grey,
                                        fontSize: 12,
                                      ),
                                    ),
                                    Text(
                                      pickup?.address ?? 'N/A',
                                      style: TextStyle(
                                        fontWeight: FontWeight.bold,
                                      ),
                                      maxLines: 2,
                                    ),
                                    SizedBox(height: 8),
                                    Text(
                                      context.l10n.destinationPoint
                                          .toUpperCase(),
                                      style: TextStyle(
                                        color: Colors.grey,
                                        fontSize: 12,
                                      ),
                                    ),
                                    Text(
                                      destination?.address ?? 'N/A',
                                      style: TextStyle(
                                        fontWeight: FontWeight.bold,
                                      ),
                                      maxLines: 2,
                                    ),
                                  ],
                                ),
                              ),
                              IconButton(
                                icon: Icon(
                                  Icons.swap_vert,
                                  color: AppColors.primary,
                                ),
                                onPressed: _swapLocations,
                              ),
                            ],
                          ),
                          Padding(
                            padding: EdgeInsets.symmetric(vertical: 12),
                            child: Divider(),
                          ),
                          Row(
                            children: [
                              Container(
                                padding: const EdgeInsets.all(8),
                                decoration: BoxDecoration(
                                  color: Colors.grey.shade100,
                                  borderRadius: BorderRadius.circular(8),
                                ),
                                child: Icon(
                                  Icons.directions_car,
                                  color: AppColors.primary,
                                ),
                              ),
                              SizedBox(width: 12),
                              Expanded(
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Text(
                                      vehicle?.name ?? 'N/A',
                                      style: TextStyle(
                                        fontWeight: FontWeight.bold,
                                      ),
                                    ),
                                    Text(
                                      _vehicleSubtitle(vehicle),
                                      style: TextStyle(
                                        color: Colors.grey,
                                        fontSize: 13,
                                      ),
                                    ),
                                  ],
                                ),
                              ),
                            ],
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
              ),
              SizedBox(height: 24),

              // Time Selection
              Text(
                context.l10n.departureTime,
                style: TextStyle(fontWeight: FontWeight.bold, fontSize: 16),
              ),
              SizedBox(height: 12),
              Row(
                children: [
                  Expanded(
                    child: GestureDetector(
                      onTap: () => setState(() => _isScheduled = false),
                      child: Container(
                        padding: const EdgeInsets.symmetric(vertical: 16),
                        decoration: BoxDecoration(
                          color: !_isScheduled
                              ? AppColors.primary.withValues(alpha: 0.05)
                              : Colors.white,
                          border: Border.all(
                            color: !_isScheduled
                                ? AppColors.primary
                                : Colors.grey.shade300,
                          ),
                          borderRadius: BorderRadius.circular(12),
                        ),
                        child: Row(
                          mainAxisAlignment: MainAxisAlignment.center,
                          children: [
                            Icon(
                              Icons.flash_on,
                              color: !_isScheduled
                                  ? AppColors.primary
                                  : Colors.grey,
                              size: 20,
                            ),
                            SizedBox(width: 8),
                            Text(
                              context.l10n.leaveNow,
                              style: TextStyle(
                                color: !_isScheduled
                                    ? AppColors.primary
                                    : Colors.grey,
                                fontWeight: FontWeight.bold,
                              ),
                            ),
                          ],
                        ),
                      ),
                    ),
                  ),
                  SizedBox(width: 12),
                  Expanded(
                    child: GestureDetector(
                      onTap: () => setState(() => _isScheduled = true),
                      child: Container(
                        padding: const EdgeInsets.symmetric(vertical: 16),
                        decoration: BoxDecoration(
                          color: _isScheduled
                              ? AppColors.primary.withValues(alpha: 0.05)
                              : Colors.white,
                          border: Border.all(
                            color: _isScheduled
                                ? AppColors.primary
                                : Colors.grey.shade300,
                          ),
                          borderRadius: BorderRadius.circular(12),
                        ),
                        child: Row(
                          mainAxisAlignment: MainAxisAlignment.center,
                          children: [
                            Icon(
                              Icons.calendar_today,
                              color: _isScheduled
                                  ? AppColors.primary
                                  : Colors.grey,
                              size: 18,
                            ),
                            SizedBox(width: 8),
                            Text(
                              context.l10n.scheduleAhead,
                              style: TextStyle(
                                color: _isScheduled
                                    ? AppColors.primary
                                    : Colors.grey,
                                fontWeight: FontWeight.bold,
                              ),
                            ),
                          ],
                        ),
                      ),
                    ),
                  ),
                ],
              ),
              SizedBox(height: 24),
              Text(
                context.l10n.promotionCode,
                style: TextStyle(fontWeight: FontWeight.bold, fontSize: 16),
              ),
              SizedBox(height: 12),
              _RebookPromoTile(
                selectedPromo: provider.selectedPromo,
                onTap: _showPromoSheet,
                onClear: provider.clearSelectedPromo,
              ),
              SizedBox(height: 8),
              Text(
                context.l10n.oldPromoCannotBeReused,
                style: TextStyle(color: Colors.grey, fontSize: 13),
              ),
            ],
          ),
        ),
      ),
      bottomNavigationBar: Container(
        padding: const EdgeInsets.all(20),
        decoration: BoxDecoration(
          color: Colors.white,
          boxShadow: [
            BoxShadow(
              color: Colors.black.withValues(alpha: 0.05),
              blurRadius: 10,
              offset: Offset(0, -5),
            ),
          ],
        ),
        child: SafeArea(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              if (provider.isEstimating)
                LinearProgressIndicator()
              else ...[
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  crossAxisAlignment: CrossAxisAlignment.end,
                  children: [
                    Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          context.l10n.grandTotal,
                          style: TextStyle(color: Colors.grey, fontSize: 14),
                        ),
                        SizedBox(height: 4),
                        Text(
                          _formatCurrency(finalFare.toDouble()),
                          style: TextStyle(
                            color: AppColors.primary,
                            fontSize: 20,
                            fontWeight: FontWeight.bold,
                          ),
                        ),
                      ],
                    ),
                    Column(
                      crossAxisAlignment: CrossAxisAlignment.end,
                      children: [
                        if (discount > 0)
                          Text(
                            context.l10n.discountApplied(
                              _formatCurrency(discount),
                            ),
                            style: TextStyle(
                              color: Colors.red,
                              fontSize: 13,
                              fontWeight: FontWeight.bold,
                            ),
                          ),
                        SizedBox(height: 2),
                        Text(
                          context.l10n.taxesIncluded,
                          style: TextStyle(color: Colors.grey, fontSize: 12),
                        ),
                      ],
                    ),
                  ],
                ),
                SizedBox(height: 16),
                SizedBox(
                  width: double.infinity,
                  height: 50,
                  child: ElevatedButton(
                    onPressed: provider.isLoading ? null : _submitRebook,
                    style: ElevatedButton.styleFrom(
                      backgroundColor: AppColors.primary,
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(12),
                      ),
                    ),
                    child: provider.isLoading
                        ? CircularProgressIndicator(color: Colors.white)
                        : Row(
                            mainAxisAlignment: MainAxisAlignment.center,
                            children: [
                              Text(
                                context.l10n.confirmAndFindDriver,
                                style: TextStyle(
                                  fontSize: 16,
                                  fontWeight: FontWeight.bold,
                                  color: Colors.white,
                                ),
                              ),
                              SizedBox(width: 8),
                              Icon(
                                Icons.arrow_forward,
                                size: 20,
                                color: Colors.white,
                              ),
                            ],
                          ),
                  ),
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }

  double _calculateDiscount(PromoModel? promo, double fare) {
    if (promo == null || fare <= 0 || promo.remainingUsageCount == 0) {
      return 0;
    }

    if (promo.minimumOrderValue > 0 && fare < promo.minimumOrderValue) {
      return 0;
    }

    final type = promo.discountType.toLowerCase();
    var discount = type.contains('percent')
        ? fare * promo.discountValue / 100
        : promo.discountValue;

    if (promo.maximumDiscountValue > 0 &&
        discount > promo.maximumDiscountValue) {
      discount = promo.maximumDiscountValue;
    }

    return discount.clamp(0, fare).toDouble();
  }

  String _formatCurrency(double value) => NumberFormat.currency(
    locale: LocaleProvider.currentLocale.toLanguageTag(),
    symbol: 'VND',
    decimalDigits: 0,
  ).format(value);

  String _vehicleSubtitle(BookingVehicleOption? vehicle) {
    if (vehicle == null) return 'N/A';

    final parts = [
      if (vehicle.plateNumber.trim().isNotEmpty) vehicle.plateNumber.trim(),
      if (vehicle.color.trim().isNotEmpty) vehicle.color.trim(),
      vehicle.isMotorbike ? context.l10n.motorbike : context.l10n.car,
    ];

    return parts.join(' • ');
  }
}

class _RebookPromoTile extends StatelessWidget {
  _RebookPromoTile({
    required this.onTap,
    required this.onClear,
    this.selectedPromo,
  });

  final VoidCallback onTap;
  final VoidCallback onClear;
  final PromoModel? selectedPromo;

  @override
  Widget build(BuildContext context) {
    final hasPromo = selectedPromo != null;

    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(12),
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
        decoration: BoxDecoration(
          color: hasPromo ? Color(0xFFEAF4F4) : Colors.white,
          borderRadius: BorderRadius.circular(12),
          border: Border.all(
            color: hasPromo ? AppColors.primary : Colors.grey.shade300,
          ),
        ),
        child: Row(
          children: [
            Icon(Icons.local_offer_outlined, color: AppColors.primary),
            SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    selectedPromo?.promotionCode ??
                        context.l10n.addNewPromoCode,
                    style: TextStyle(
                      color: AppColors.primary,
                      fontWeight: FontWeight.w700,
                      fontSize: 15,
                    ),
                  ),
                  if (hasPromo && selectedPromo!.shortDescription.isNotEmpty)
                    Text(
                      selectedPromo!.shortDescription,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: TextStyle(
                        color: Color(0xFF626A6C),
                        fontSize: 13,
                      ),
                    ),
                ],
              ),
            ),
            if (hasPromo)
              IconButton(
                onPressed: onClear,
                icon: Icon(Icons.cancel, color: Colors.grey, size: 20),
                padding: EdgeInsets.zero,
                constraints: BoxConstraints(),
              )
            else
              Icon(Icons.chevron_right, color: Colors.grey),
          ],
        ),
      ),
    );
  }
}
