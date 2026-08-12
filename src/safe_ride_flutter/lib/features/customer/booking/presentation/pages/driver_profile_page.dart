import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../../../../core/constants/app_colors.dart';
import '../../../../../core/localization/localization_extensions.dart';
import '../../../../auth/presentation/providers/auth_provider.dart';
import '../../data/models/booking_catalog.dart';
import '../../data/models/booking_fare_estimate.dart';
import '../../data/models/booking_location.dart';
import '../../data/models/booking_response.dart';
import '../providers/booking_provider.dart';
import 'confirm_booking_page.dart';
import '../../../../shared/feedback/presentation/pages/driver_reviews_page.dart';

class DriverProfilePage extends StatelessWidget {
  const DriverProfilePage({
    super.key,
    required this.driverId,
    this.name = 'SafeRide Driver',
    this.avatarUrl =
        'https://img.freepik.com/free-photo/handsome-young-man-with-new-haircut_176420-19636.jpg',
    this.rating = 4.9,
    this.tripCount = 1200,
    this.experienceYears = 5,
    this.isVerified = true,
    this.booking,
    this.pickup,
    this.destination,
    this.fareEstimate,
    this.vehicle,
  });

  final String driverId;
  final String name;
  final String? avatarUrl;
  final double rating;
  final int tripCount;
  final int experienceYears;
  final bool isVerified;
  final BookingResponse? booking;
  final BookingLocation? pickup;
  final BookingLocation? destination;
  final BookingFareEstimate? fareEstimate;
  final BookingVehicleOption? vehicle;

  @override
  Widget build(BuildContext context) {
    final canConfirmDriver =
        booking?.bookingType == 'Now' &&
        booking?.bookingStatus == 'Searching' &&
        booking?.driverOffer?.offerStatus == 'DriverAccepted';

    return PopScope(
      canPop: true,
      child: Scaffold(
        backgroundColor: Colors.white,
        appBar: AppBar(
          backgroundColor: Colors.white,
          elevation: 0,
          leading: IconButton(
            icon: Icon(Icons.arrow_back, color: AppColors.primary),
            onPressed: () => Navigator.pop(context),
          ),
          title: Text(
            context.l10n.driverProfile,
            style: TextStyle(
              color: AppColors.primary,
              fontSize: 18,
              fontWeight: FontWeight.w700,
            ),
          ),
          centerTitle: true,
          actions: [
            IconButton(
              icon: Icon(Icons.more_vert, color: Color(0xFF6B6B6B)),
              onPressed: () {},
            ),
          ],
        ),
        body: SingleChildScrollView(
          physics: const BouncingScrollPhysics(),
          padding: const EdgeInsets.symmetric(horizontal: 20),
          child: Column(
            children: [
              SizedBox(height: 24),
              _DriverAvatar(avatarUrl: avatarUrl, isVerified: isVerified),
              SizedBox(height: 20),
              Text(
                name,
                style: TextStyle(
                  fontSize: 24,
                  fontWeight: FontWeight.w800,
                  color: Color(0xFF1F1F1F),
                  letterSpacing: -0.5,
                ),
              ),
              SizedBox(height: 8),
              Row(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  Icon(Icons.star, color: Color(0xFFFFB800), size: 18),
                  SizedBox(width: 4),
                  Text(
                    rating.toString(),
                    style: TextStyle(
                      fontSize: 16,
                      fontWeight: FontWeight.w800,
                      color: Color(0xFF1F1F1F),
                    ),
                  ),
                  SizedBox(width: 10),
                  Container(
                    width: 1.5,
                    height: 14,
                    color: Color(0xFFE2E2E2),
                  ),
                  SizedBox(width: 10),
                  Text(
                    context.l10n.tripCountPlus(tripCount.toString()),
                    style: TextStyle(
                      color: Color(0xFF6B6B6B),
                      fontSize: 15,
                      fontWeight: FontWeight.w500,
                    ),
                  ),
                ],
              ),
              SizedBox(height: 32),
              Row(
                children: [
                  Expanded(child: _ExperienceCard(years: experienceYears)),
                  SizedBox(width: 16),
                  Expanded(child: _AttributesCard()),
                ],
              ),
              SizedBox(height: 20),
              _StatusCard(
                icon: Icons.assignment_turned_in_outlined,
                title: context.l10n.kycStatus,
                subtitle: context.l10n.kycApprovedDescription,
                trailing: _VerifiedBadge(),
                iconBgColor: Color(0xFFE8F5E9),
                iconColor: Color(0xFF4CAF50),
              ),
              SizedBox(height: 12),
              _StatusCard(
                icon: Icons.gavel_outlined,
                title: context.l10n.criminalRecord,
                subtitle: context.l10n.cleanCriminalRecord,
                iconBgColor: Color(0xFFF5F5F5),
                iconColor: Color(0xFF757575),
              ),
              SizedBox(height: 32),
            ],
          ),
        ),

        // --- BOTTOM NAVIGATION BAR ---
        bottomNavigationBar: !canConfirmDriver
            ? null
            : Container(
                decoration: BoxDecoration(
                  color: Colors.white,
                  borderRadius:
                      const BorderRadius.vertical(top: Radius.circular(24)),
                  boxShadow: [
                    BoxShadow(
                      color: Colors.black.withValues(alpha: 0.06),
                      blurRadius: 16,
                      offset: const Offset(0, -4),
                    ),
                  ],
                ),
                child: SafeArea(
                  child: Padding(
                    padding: const EdgeInsets.fromLTRB(20, 16, 20, 12),
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Row(
                          children: [
                            Expanded(
                              child: OutlinedButton(
                                onPressed: () {
                                  Navigator.of(context).push(
                                    MaterialPageRoute(
                                      builder: (_) => DriverReviewsPage(
                                        driverId: driverId,
                                        driverName: name,
                                      ),
                                    ),
                                  );
                                },
                                style: OutlinedButton.styleFrom(
                                  padding:
                                      const EdgeInsets.symmetric(vertical: 16),
                                  side: const BorderSide(
                                    color: AppColors.primary,
                                    width: 1.5,
                                  ),
                                  shape: RoundedRectangleBorder(
                                    borderRadius: BorderRadius.circular(12),
                                  ),
                                ),
                                child: Text(
                                  context.l10n.viewReviews,
                                  style: const TextStyle(
                                    color: AppColors.primary,
                                    fontWeight: FontWeight.w700,
                                    fontSize: 16,
                                  ),
                                ),
                              ),
                            ),
                            const SizedBox(width: 12),
                            Expanded(
                              child: ElevatedButton(
                                onPressed: booking == null || pickup == null
                                    ? null
                                    : () {
                                        Navigator.of(context).push(
                                          MaterialPageRoute(
                                            builder: (_) => ConfirmBookingPage(
                                              booking: booking!,
                                              pickup: pickup!,
                                              destination: destination,
                                              fareEstimate: fareEstimate,
                                              vehicle: vehicle,
                                              driverName: name,
                                              driverRating: rating,
                                              driverTripCount: tripCount,
                                              driverExperienceYears:
                                                  experienceYears,
                                            ),
                                          ),
                                        );
                                      },
                                style: ElevatedButton.styleFrom(
                                  backgroundColor: AppColors.primary,
                                  foregroundColor: Colors.white,
                                  padding:
                                      const EdgeInsets.symmetric(vertical: 16),
                                  elevation: 0,
                                  shape: RoundedRectangleBorder(
                                    borderRadius: BorderRadius.circular(12),
                                  ),
                                ),
                                child: Text(
                                  context.l10n.confirmHire,
                                  style: const TextStyle(
                                    fontWeight: FontWeight.w700,
                                    fontSize: 16,
                                  ),
                                  textAlign: TextAlign.center,
                                ),
                              ),
                            ),
                          ],
                        ),
                        const SizedBox(height: 8),
                        SizedBox(
                          width: double.infinity,
                          child: TextButton.icon(
                            onPressed: booking == null
                                ? null
                                : () => _rejectDriver(context),
                            icon: Icon(
                              Icons.close_rounded,
                              size: 18,
                              color: Colors.red.shade600,
                            ),
                            label: Text(
                              context.l10n.rejectAndFindAnotherDriver,
                              style: TextStyle(
                                color: Colors.red.shade600,
                                fontWeight: FontWeight.w600,
                                fontSize: 15,
                              ),
                            ),
                            style: TextButton.styleFrom(
                              padding: const EdgeInsets.symmetric(vertical: 12),
                              shape: RoundedRectangleBorder(
                                borderRadius: BorderRadius.circular(10),
                              ),
                            ),
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
              ),
      ),
    );
  }

  // --- POPUP TỪ CHỐI ---
  Future<void> _rejectDriver(BuildContext context) async {
    if (booking?.bookingType != 'Now' ||
        booking?.bookingStatus != 'Searching' ||
        booking?.driverOffer?.offerStatus != 'DriverAccepted') {
      return;
    }

    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => Dialog(
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
        backgroundColor: Colors.white,
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Container(
                padding: const EdgeInsets.all(16),
                decoration: BoxDecoration(
                  color: Colors.red.shade50,
                  shape: BoxShape.circle,
                ),
                child: Icon(
                  Icons.person_off_outlined,
                  color: Colors.red.shade500,
                  size: 32,
                ),
              ),
              SizedBox(height: 20),
              Text(
                context.l10n.rejectDriverQuestion,
                style: TextStyle(
                  fontSize: 20,
                  fontWeight: FontWeight.w800,
                  color: Color(0xFF1F1F1F),
                ),
              ),
              SizedBox(height: 12),
              Text(
                context.l10n.rejectDriverDescription,
                textAlign: TextAlign.center,
                style: TextStyle(
                  fontSize: 15,
                  color: Color(0xFF6B6B6B),
                  height: 1.4,
                ),
              ),
              SizedBox(height: 28),
              Row(
                children: [
                  Expanded(
                    child: OutlinedButton(
                      onPressed: () => Navigator.pop(context, false),
                      style: OutlinedButton.styleFrom(
                        padding: const EdgeInsets.symmetric(vertical: 14),
                        side: BorderSide(color: Color(0xFFE2E2E2)),
                        shape: RoundedRectangleBorder(
                          borderRadius: BorderRadius.circular(12),
                        ),
                      ),
                      child: Text(
                        context.l10n.goBack,
                        style: TextStyle(
                          color: Color(0xFF6B6B6B),
                          fontWeight: FontWeight.w600,
                          fontSize: 15,
                        ),
                      ),
                    ),
                  ),
                  SizedBox(width: 12),
                  Expanded(
                    child: ElevatedButton(
                      onPressed: () => Navigator.pop(context, true),
                      style: ElevatedButton.styleFrom(
                        backgroundColor: Colors.red.shade600,
                        foregroundColor: Colors.white,
                        elevation: 0,
                        padding: const EdgeInsets.symmetric(vertical: 14),
                        shape: RoundedRectangleBorder(
                          borderRadius: BorderRadius.circular(12),
                        ),
                      ),
                      child: Text(
                        context.l10n.confirm,
                        style: TextStyle(
                          fontWeight: FontWeight.w700,
                          fontSize: 15,
                        ),
                      ),
                    ),
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );

    if (confirmed != true || !context.mounted) return;

    final token = context.read<AuthProvider>().token;
    if (token == null) return;

    final result = await context.read<BookingProvider>().rejectDriver(
      token,
      bookingId: booking!.bookingId,
    );

    if (!context.mounted) return;

    if (result != null) {
      Navigator.pop(context); // Go back to searching screen
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(context.l10n.findingAnotherDriver)),
      );
    } else {
      final error = context.read<BookingProvider>().errorMessage;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(error ?? context.l10n.rejectDriverFailed)),
      );
    }
  }
}

class _DriverAvatar extends StatelessWidget {
  const _DriverAvatar({this.avatarUrl, required this.isVerified});
  final String? avatarUrl;
  final bool isVerified;

  @override
  Widget build(BuildContext context) {
    return Stack(
      children: [
        Container(
          padding: const EdgeInsets.all(4),
          decoration: BoxDecoration(
            shape: BoxShape.circle,
            border: Border.all(color: Color(0xFFF5F5F5), width: 1.5),
          ),
          child: CircleAvatar(
            radius: 65,
            backgroundColor: Color(0xFFF5F5F5),
            backgroundImage: avatarUrl != null
                ? NetworkImage(avatarUrl!)
                : null,
            child: avatarUrl == null
                ? Icon(Icons.person, size: 80, color: Color(0xFFBDBDBD))
                : null,
          ),
        ),
        if (isVerified)
          Positioned(
            right: 4,
            bottom: 4,
            child: Container(
              padding: const EdgeInsets.all(3),
              decoration: BoxDecoration(
                color: Colors.white,
                shape: BoxShape.circle,
              ),
              child: Icon(
                Icons.verified,
                color: Color(0xFF007A87),
                size: 28,
              ),
            ),
          ),
      ],
    );
  }
}

class _ExperienceCard extends StatelessWidget {
  const _ExperienceCard({required this.years});
  final int years;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(16),
      height: 120,
      decoration: BoxDecoration(
        color: Color(0xFFE0F2F1),
        borderRadius: BorderRadius.circular(16),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(
            Icons.work_history_outlined,
            color: AppColors.primary,
            size: 28,
          ),
          Spacer(),
          Text(
            context.l10n.experienceUpper,
            style: TextStyle(
              fontSize: 11,
              fontWeight: FontWeight.w800,
              color: Color(0xFF5AB1B1),
              letterSpacing: 0.5,
            ),
          ),
          Text(
            context.l10n.yearsValueCapitalized(years),
            style: TextStyle(
              fontSize: 20,
              fontWeight: FontWeight.w900,
              color: AppColors.primary,
            ),
          ),
        ],
      ),
    );
  }
}

class _AttributesCard extends StatelessWidget {
  const _AttributesCard();

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
      height: 120,
      decoration: BoxDecoration(
        color: Color(0xFFF5F7F8),
        borderRadius: BorderRadius.circular(16),
      ),
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Row(
            children: [
              Icon(Icons.shield_outlined, size: 20, color: AppColors.primary),
              SizedBox(width: 10),
              Expanded(
                child: Text(
                  context.l10n.safeDriving,
                  style: TextStyle(
                    fontSize: 14,
                    fontWeight: FontWeight.w600,
                    color: Color(0xFF455A64),
                  ),
                ),
              ),
            ],
          ),
          SizedBox(height: 12),
          Row(
            children: [
              Icon(
                Icons.sentiment_satisfied_alt,
                size: 20,
                color: AppColors.primary,
              ),
              SizedBox(width: 10),
              Expanded(
                child: Text(
                  context.l10n.friendly,
                  style: TextStyle(
                    fontSize: 14,
                    fontWeight: FontWeight.w600,
                    color: Color(0xFF455A64),
                  ),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _StatusCard extends StatelessWidget {
  const _StatusCard({
    required this.icon,
    required this.title,
    required this.subtitle,
    this.trailing,
    required this.iconBgColor,
    required this.iconColor,
  });

  final IconData icon;
  final String title;
  final String subtitle;
  final Widget? trailing;
  final Color iconBgColor;
  final Color iconColor;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: const Color(0xFFF0F0F0)),
        boxShadow: [
          BoxShadow(
            color: Colors.black.withValues(alpha: 0.02),
            blurRadius: 10,
            offset: const Offset(0, 4),
          ),
        ],
      ),
      child: Row(
        children: [
          Container(
            padding: const EdgeInsets.all(12),
            decoration: BoxDecoration(
              color: iconBgColor,
              borderRadius: BorderRadius.circular(14),
            ),
            child: Icon(icon, color: iconColor, size: 24),
          ),
          const SizedBox(width: 16),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  title,
                  style: const TextStyle(
                    fontSize: 16,
                    fontWeight: FontWeight.w800,
                    color: Color(0xFF263238),
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  subtitle,
                  style: const TextStyle(
                    fontSize: 13,
                    color: Color(0xFF78909C),
                    height: 1.3,
                  ),
                ),
              ],
            ),
          ),
          if (trailing != null) trailing!,
        ],
      ),
    );
  }
}

class _VerifiedBadge extends StatelessWidget {
  const _VerifiedBadge();
  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 8),
      decoration: BoxDecoration(
        color: Color(0xFFE8F5E9),
        borderRadius: BorderRadius.circular(20),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(Icons.check_circle, size: 14, color: Color(0xFF4CAF50)),
          SizedBox(width: 6),
          Text(
            context.l10n.verified,
            style: TextStyle(
              color: Color(0xFF2E7D32),
              fontSize: 12,
              fontWeight: FontWeight.w800,
            ),
          ),
        ],
      ),
    );
  }
}
