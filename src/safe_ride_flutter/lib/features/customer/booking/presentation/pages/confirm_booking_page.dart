import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:intl/intl.dart';
import '../../../../../core/constants/app_colors.dart';
import '../../../../../core/localization/localization_extensions.dart';
import '../../../../../core/localization/locale_provider.dart';
import '../../../../../core/localization/trip_status_localizer.dart';
import '../../../../../core/widgets/custom_button.dart';
import '../../../../auth/presentation/providers/auth_provider.dart';
import '../../data/models/booking_catalog.dart';
import '../../data/models/booking_fare_estimate.dart';
import '../../data/models/booking_location.dart';
import '../../data/models/booking_response.dart';
import '../../data/models/create_booking_request.dart';
import '../providers/booking_provider.dart';
import '../../../home/presentation/providers/home_provider.dart';
// import 'trip_tracking_page.dart';

class ConfirmBookingPage extends StatelessWidget {
  ConfirmBookingPage({
    super.key,
    required this.pickup,
    this.booking,
    this.destination,
    BookingFareEstimate? fareEstimate,
    BookingFareEstimate? estimate,
    CreateBookingRequest? request,
    BookingServiceOption? service,
    this.vehicle,
    int? estimatedHours,
    this.driverName = 'SafeRide Driver',
    this.driverRating = 4.9,
    this.driverTripCount = 1200,
    this.driverExperienceYears = 5,
  }) : fareEstimate = fareEstimate ?? estimate;

  final BookingResponse? booking;
  final BookingLocation pickup;
  final BookingLocation? destination;
  final BookingFareEstimate? fareEstimate;
  final BookingVehicleOption? vehicle;
  final String driverName;
  final double driverRating;
  final int driverTripCount;
  final int driverExperienceYears;

  @override
  Widget build(BuildContext context) {
    final fare =
        booking?.finalFare ??
        booking?.estimatedFare ??
        fareEstimate?.estimatedFare;
    final originalFare =
        booking?.originalFare ??
        booking?.estimatedFare ??
        fareEstimate?.estimatedFare;
    final discount = booking?.discountAmount ?? 0;
    final promoCode = booking?.promotionCode;

    final distance =
        booking?.estimatedDistanceKm ?? fareEstimate?.estimatedDistanceKm;
    final duration =
        booking?.estimatedDurationMinutes ??
        fareEstimate?.estimatedDurationMinutes;

    return PopScope(
      canPop: true,
      child: Scaffold(
        backgroundColor: Colors.white,
        appBar: AppBar(
          leading: IconButton(
            icon: Icon(Icons.arrow_back, color: Colors.black),
            onPressed: () => Navigator.pop(context),
          ),
          title: Text(
            context.l10n.confirmHireDriver,
            style: TextStyle(
              color: Colors.black,
              fontWeight: FontWeight.bold,
              fontSize: 18,
            ),
          ),
          centerTitle: true,
          elevation: 0,
          backgroundColor: Colors.white,
        ),
        body: SingleChildScrollView(
          physics: BouncingScrollPhysics(),
          padding: const EdgeInsets.symmetric(horizontal: 20),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              SizedBox(height: 16),
              _DriverCard(
                name: driverName,
                rating: driverRating,
                tripCount: driverTripCount,
                experienceYears: driverExperienceYears,
              ),
              SizedBox(height: 18),
              if (vehicle != null) ...[
                _VehicleCard(vehicle: vehicle!),
                SizedBox(height: 18),
              ],
              _RouteTimeline(
                pickup: pickup.address,
                destination: destination?.address ?? context.l10n.hourlyHire,
              ),
              SizedBox(height: 24),
              Text(
                context.l10n.tripDetailsHeading,
                style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
              ),
              SizedBox(height: 12),
              _InfoRow(
                label: context.l10n.tripCode,
                value: booking == null
                    ? context.l10n.notCreated
                    : '#${booking!.bookingId}',
              ),
              _InfoRow(
                    label: context.l10n.statusLabel,
                value: booking == null
                    ? context.l10n.awaitingConfirmation
                    : TripStatusLocalizer.translate(
                        context.l10n,
                        booking!.bookingStatus,
                      ),
              ),
              if (distance != null)
                _InfoRow(
                  label: context.l10n.distance,
                  value: '${distance.toStringAsFixed(1)} km',
                ),
              if (duration != null)
                _InfoRow(
                  label: context.l10n.estimatedDuration,
                  value: context.l10n.minutesValue(duration),
                ),
              Padding(
                padding: EdgeInsets.symmetric(vertical: 8),
                child: Divider(thickness: 1, color: Color(0xFFEEEEEE)),
              ),
              if (discount > 0 || promoCode != null) ...[
                _InfoRow(
                  label: context.l10n.baseFare,
                  value: originalFare == null
                      ? context.l10n.updating
                      : _formatCurrency(originalFare),
                ),
                _InfoRow(
                  label:
                      '${context.l10n.promotion} ${promoCode != null ? '($promoCode)' : ''}',
                  value: '-${_formatCurrency(discount)}',
                  valueColor: Colors.red,
                ),
              ],
              _InfoRow(
                label: context.l10n.estimatedTotalPayment,
                value: fare == null
                    ? context.l10n.updating
                    : _formatCurrency(fare),
                isTotal: true,
              ),
              SizedBox(height: 24),
              _NoticeCard(),
              SizedBox(height: 40),
            ],
          ),
        ),
        bottomNavigationBar: Container(
          padding: const EdgeInsets.fromLTRB(20, 10, 20, 24),
          decoration: BoxDecoration(
            color: Colors.white,
            border: Border(top: BorderSide(color: Color(0xFFF5F5F5))),
          ),
          child: CustomButton(
            text: context.l10n.confirmHireDriver,
            onPressed: () => _confirmDriver(context),
          ),
        ),
      ),
    );
  }

  Future<void> _confirmDriver(BuildContext context) async {
    if (booking == null) {
      _showMessage(context, context.l10n.missingTripToConfirmDriver);
      return;
    }

    final token = context.read<AuthProvider>().token;
    if (token == null || token.isEmpty) {
      _showMessage(context, context.l10n.sessionExpired);
      return;
    }

    final offerId = booking!.driverOffer?.offerId;
    if (offerId == null) {
      _showMessage(context, context.l10n.driverOfferNotFound);
      return;
    }

    final result = await context.read<BookingProvider>().confirmDriverOffer(
      token,
      bookingId: booking!.bookingId,
      offerId: offerId,
    );
    if (!context.mounted) {
      return;
    }

    if (result == null) {
      _showMessage(
        context,
        context.read<BookingProvider>().errorMessage ??
            context.l10n.confirmDriverFailed,
      );
      return;
    }

    // Set active booking in provider
    context.read<BookingProvider>().setActiveBooking(
      booking: result,
      pickup: pickup,
      destination: destination,
      vehicle: vehicle,
    );

    await showDialog<void>(
      context: context,
      barrierDismissible: false,
      builder: (dialogContext) => AlertDialog(
        icon: Icon(
          Icons.check_circle,
          color: AppColors.primary,
          size: 52,
        ),
        title: Text(context.l10n.driverConfirmed),
        content: Text(
          context.l10n.driverConfirmedMessage(driverName, booking!.bookingId),
          textAlign: TextAlign.center,
        ),
        actions: [
          FilledButton(
            onPressed: () {
              final homeProvider = context.read<HomeProvider>();
              Navigator.pop(dialogContext);
              // Switch to tracking tab and go back to home
              homeProvider.setSelectedIndex(1);
              Navigator.of(context).popUntil((route) => route.isFirst);
            },
            child: Text(context.l10n.agree),
          ),
        ],
      ),
    );
  }

  void _showMessage(BuildContext context, String message) {
    ScaffoldMessenger.of(context)
      ..hideCurrentSnackBar()
      ..showSnackBar(SnackBar(content: Text(message)));
  }

  String _formatCurrency(double value) {
    return NumberFormat.currency(
      locale: LocaleProvider.currentLocale.toLanguageTag(),
      symbol: 'VND',
      decimalDigits: 0,
    ).format(value);
  }
}

class _DriverCard extends StatelessWidget {
  _DriverCard({
    required this.name,
    required this.rating,
    required this.tripCount,
    required this.experienceYears,
  });

  final String name;
  final double rating;
  final int tripCount;
  final int experienceYears;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Color(0xFFEAF4F4),
        borderRadius: BorderRadius.circular(16),
      ),
      child: Row(
        children: [
          CircleAvatar(
            radius: 28,
            backgroundColor: AppColors.primary,
            child: Icon(Icons.person, color: Colors.white, size: 30),
          ),
          SizedBox(width: 14),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  name,
                  style: TextStyle(
                    fontWeight: FontWeight.w800,
                    fontSize: 16,
                  ),
                ),
                SizedBox(height: 4),
                Text(
                  context.l10n.driverRatingSummary(
                    rating.toStringAsFixed(1),
                    tripCount,
                    experienceYears,
                  ),
                  style: TextStyle(color: Color(0xFF667174)),
                ),
              ],
            ),
          ),
          Icon(Icons.verified, color: AppColors.primary),
        ],
      ),
    );
  }
}

class _VehicleCard extends StatelessWidget {
  _VehicleCard({required this.vehicle});
  final BookingVehicleOption vehicle;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        border: Border.all(color: AppColors.border),
        borderRadius: BorderRadius.circular(16),
      ),
      child: Row(
        children: [
          Icon(
            vehicle.isMotorbike
                ? Icons.directions_bike_rounded
                : Icons.directions_car_rounded,
            color: AppColors.primary,
          ),
          SizedBox(width: 12),
          Expanded(
            child: Text(
              '${vehicle.name} • ${vehicle.plateNumber}',
              style: TextStyle(fontWeight: FontWeight.w700),
            ),
          ),
        ],
      ),
    );
  }
}

class _RouteTimeline extends StatelessWidget {
  _RouteTimeline({required this.pickup, required this.destination});

  final String pickup;
  final String destination;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        _RouteItem(
          icon: Icons.location_searching,
          label: context.l10n.pickupPoint,
          address: pickup,
        ),
        SizedBox(height: 12),
        _RouteItem(
          icon: Icons.near_me,
          label: context.l10n.destinationPoint,
          address: destination,
          filled: true,
        ),
      ],
    );
  }
}

class _RouteItem extends StatelessWidget {
  _RouteItem({
    required this.icon,
    required this.label,
    required this.address,
    this.filled = false,
  });

  final IconData icon;
  final String label;
  final String address;
  final bool filled;

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        CircleAvatar(
          radius: 18,
          backgroundColor: filled ? AppColors.primary : Color(0xFFF0F0F0),
          child: Icon(
            icon,
            size: 18,
            color: filled ? Colors.white : Colors.black,
          ),
        ),
        SizedBox(width: 12),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                label,
                style: TextStyle(color: Color(0xFF667174), fontSize: 12),
              ),
              Text(
                address,
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(fontWeight: FontWeight.w700),
              ),
            ],
          ),
        ),
      ],
    );
  }
}

class _InfoRow extends StatelessWidget {
  _InfoRow({
    required this.label,
    required this.value,
    this.isTotal = false,
    this.valueColor,
  });

  final String label;
  final String value;
  final bool isTotal;
  final Color? valueColor;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Text(label, style: TextStyle(color: Color(0xFF667174))),
          Text(
            value,
            style: TextStyle(
              color: valueColor ?? (isTotal ? AppColors.primary : Colors.black),
              fontSize: isTotal ? 18 : 15,
              fontWeight: isTotal ? FontWeight.w800 : FontWeight.w600,
            ),
          ),
        ],
      ),
    );
  }
}

class _NoticeCard extends StatelessWidget {
  _NoticeCard();

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: Color(0xFFFFF8E1),
        borderRadius: BorderRadius.circular(12),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(Icons.info_outline, color: Color(0xFFFFA000), size: 20),
          SizedBox(width: 10),
          Expanded(
            child: Text(
              context.l10n.confirmDriverNotice,
              style: TextStyle(color: Color(0xFF6B5B00), height: 1.35),
            ),
          ),
        ],
      ),
    );
  }
}
