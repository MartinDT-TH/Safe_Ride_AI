import 'package:flutter/material.dart';
import '../../../../../core/constants/app_colors.dart';
import '../../data/models/booking_catalog.dart';
import '../../data/models/booking_fare_estimate.dart';
import '../../data/models/booking_location.dart';
import '../../data/models/booking_response.dart';
import '../widgets/booking_cancel_flow.dart';
import 'confirm_booking_page.dart';
import 'driver_reviews_page.dart';

class DriverProfilePage extends StatelessWidget {
  const DriverProfilePage({
    super.key,
    this.name = 'Nguyễn Văn An',
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
    return PopScope(
      canPop: true,
      child: Scaffold(
        backgroundColor: Colors.white,
        appBar: AppBar(
          backgroundColor: Colors.white,
          elevation: 0,
          leading: IconButton(
            icon: const Icon(Icons.arrow_back, color: AppColors.primary),
            onPressed: () => Navigator.pop(context),
          ),
          title: const Text(
            'Hồ sơ tài xế',
            style: TextStyle(
              color: AppColors.primary,
              fontSize: 18,
              fontWeight: FontWeight.w700,
            ),
          ),
          centerTitle: true,
          actions: [
            IconButton(
              icon: const Icon(Icons.more_vert, color: Color(0xFF6B6B6B)),
              onPressed: () {},
            ),
          ],
        ),
        body: SingleChildScrollView(
          physics: const BouncingScrollPhysics(),
          padding: const EdgeInsets.symmetric(horizontal: 20),
          child: Column(
            children: [
              const SizedBox(height: 24),
              // Avatar Section
              _DriverAvatar(avatarUrl: avatarUrl, isVerified: isVerified),
              const SizedBox(height: 20),
              Text(
                name,
                style: const TextStyle(
                  fontSize: 24,
                  fontWeight: FontWeight.w800,
                  color: Color(0xFF1F1F1F),
                  letterSpacing: -0.5,
                ),
              ),
              const SizedBox(height: 8),
              Row(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  const Icon(Icons.star, color: Color(0xFFFFB800), size: 18),
                  const SizedBox(width: 4),
                  Text(
                    rating.toString(),
                    style: const TextStyle(
                      fontSize: 16,
                      fontWeight: FontWeight.w800,
                      color: Color(0xFF1F1F1F),
                    ),
                  ),
                  const SizedBox(width: 10),
                  Container(
                    width: 1,
                    height: 14,
                    color: const Color(0xFFE2E2E2),
                  ),
                  const SizedBox(width: 10),
                  Text(
                    '${tripCount.toString().replaceAllMapped(RegExp(r'(\d{1,3})(?=(\d{3})+(?!\d))'), (Match m) => '${m[1]},')}+ chuyến đi',
                    style: const TextStyle(
                      color: Color(0xFF6B6B6B),
                      fontSize: 15,
                      fontWeight: FontWeight.w500,
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 32),

              // Highlight Cards Row
              Row(
                children: [
                  Expanded(child: _ExperienceCard(years: experienceYears)),
                  const SizedBox(width: 16),
                  const Expanded(child: _AttributesCard()),
                ],
              ),
              const SizedBox(height: 20),

              // KYC Status Card
              _StatusCard(
                icon: Icons.assignment_turned_in_outlined,
                title: 'Trạng thái KYC',
                subtitle: 'Hồ sơ đã được duyệt bởi hệ thống',
                trailing: _VerifiedBadge(),
                iconBgColor: const Color(0xFFE8F5E9),
                iconColor: const Color(0xFF4CAF50),
              ),
              const SizedBox(height: 12),
              // Legal History Card
              const _StatusCard(
                icon: Icons.gavel_outlined,
                title: 'Lý lịch tư pháp',
                subtitle: 'Hoàn toàn trong sạch & minh bạch',
                iconBgColor: Color(0xFFF5F5F5),
                iconColor: Color(0xFF757575),
              ),
              const SizedBox(height: 32),
            ],
          ),
        ),
        bottomNavigationBar: Container(
          decoration: BoxDecoration(
            color: Colors.white,
            boxShadow: [
              BoxShadow(
                color: Colors.black.withOpacity(0.05),
                blurRadius: 10,
                offset: const Offset(0, -5),
              ),
            ],
          ),
          padding: const EdgeInsets.fromLTRB(20, 12, 20, 32),
          child: Row(
            children: [
              Expanded(
                child: OutlinedButton(
                  onPressed: () {
                    Navigator.of(context).push(
                      MaterialPageRoute(
                        builder: (_) => DriverReviewsPage(
                          driverName: name,
                          rating: rating,
                          reviewCount: tripCount,
                        ),
                      ),
                    );
                  },
                  style: OutlinedButton.styleFrom(
                    padding: const EdgeInsets.symmetric(vertical: 16),
                    side: const BorderSide(
                      color: AppColors.primary,
                      width: 1.5,
                    ),
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(12),
                    ),
                  ),
                  child: const Text(
                    'Xem đánh giá',
                    style: TextStyle(
                      color: AppColors.primary,
                      fontWeight: FontWeight.w700,
                      fontSize: 16,
                    ),
                  ),
                ),
              ),
              const SizedBox(width: 16),
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
                                driverExperienceYears: experienceYears,
                              ),
                            ),
                          );
                        },
                  style: ElevatedButton.styleFrom(
                    backgroundColor: AppColors.primary,
                    foregroundColor: Colors.white,
                    padding: const EdgeInsets.symmetric(vertical: 16),
                    elevation: 0,
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(12),
                    ),
                  ),
                  child: const Row(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      Icon(Icons.check_circle_outline, size: 20),
                      SizedBox(width: 8),
                      Text(
                        'Xác nhận thuê',
                        style: TextStyle(
                          fontWeight: FontWeight.w700,
                          fontSize: 16,
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
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
            border: Border.all(color: const Color(0xFFF5F5F5), width: 1.5),
          ),
          child: CircleAvatar(
            radius: 65,
            backgroundColor: const Color(0xFFF5F5F5),
            backgroundImage: avatarUrl != null
                ? NetworkImage(avatarUrl!)
                : null,
            child: avatarUrl == null
                ? const Icon(Icons.person, size: 80, color: Color(0xFFBDBDBD))
                : null,
          ),
        ),
        if (isVerified)
          Positioned(
            right: 4,
            bottom: 4,
            child: Container(
              padding: const EdgeInsets.all(3),
              decoration: const BoxDecoration(
                color: Colors.white,
                shape: BoxShape.circle,
              ),
              child: const Icon(
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
        color: const Color(0xFFE0F2F1),
        borderRadius: BorderRadius.circular(16),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Icon(
            Icons.work_history_outlined,
            color: AppColors.primary,
            size: 28,
          ),
          const Spacer(),
          const Text(
            'KINH NGHIỆM',
            style: TextStyle(
              fontSize: 11,
              fontWeight: FontWeight.w800,
              color: Color(0xFF5AB1B1),
              letterSpacing: 0.5,
            ),
          ),
          Text(
            '$years Năm',
            style: const TextStyle(
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
        color: const Color(0xFFF5F7F8),
        borderRadius: BorderRadius.circular(16),
      ),
      child: const Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Row(
            children: [
              Icon(Icons.shield_outlined, size: 20, color: AppColors.primary),
              SizedBox(width: 10),
              Expanded(
                child: Text(
                  'Lái xe an toàn',
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
                  'Thân thiện',
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
            color: Colors.black.withOpacity(0.02),
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
  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 8),
      decoration: BoxDecoration(
        color: const Color(0xFFE8F5E9),
        borderRadius: BorderRadius.circular(20),
      ),
      child: const Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(Icons.check_circle, color: Color(0xFF4CAF50), size: 14),
          SizedBox(width: 6),
          Text(
            'Đã xác minh',
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
