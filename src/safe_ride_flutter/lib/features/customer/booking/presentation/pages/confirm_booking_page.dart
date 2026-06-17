import 'package:flutter/material.dart';

import '../../../../../core/constants/app_colors.dart';
import '../../../../../core/widgets/custom_button.dart';
import '../../data/models/booking_catalog.dart';
import '../../data/models/booking_fare_estimate.dart';
import '../../data/models/booking_location.dart';
import '../../data/models/booking_response.dart';
import '../../data/models/create_booking_request.dart';
import '../widgets/booking_cancel_flow.dart';

class ConfirmBookingPage extends StatelessWidget {
  const ConfirmBookingPage({
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
    this.driverName = 'Nguyễn Văn An',
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
    final fare = booking?.estimatedFare ?? fareEstimate?.estimatedFare;
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
            icon: const Icon(Icons.arrow_back, color: Colors.black),
            onPressed: () => Navigator.pop(context),
          ),
          title: const Text(
            'Xác nhận thuê tài xế',
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
          physics: const BouncingScrollPhysics(),
          padding: const EdgeInsets.symmetric(horizontal: 20),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const SizedBox(height: 16),
              _DriverCard(
                name: driverName,
                rating: driverRating,
                tripCount: driverTripCount,
                experienceYears: driverExperienceYears,
              ),
              const SizedBox(height: 18),
              if (vehicle != null) ...[
                _VehicleCard(vehicle: vehicle!),
                const SizedBox(height: 18),
              ],
              _RouteTimeline(
                pickup: pickup.address,
                destination: destination?.address ?? 'Thuê theo giờ',
              ),
              const SizedBox(height: 24),
              const Text(
                'Chi tiết chuyến đi',
                style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
              ),
              const SizedBox(height: 12),
              _InfoRow(
                label: 'Mã chuyến',
                value: booking == null ? 'Chưa tạo' : '#${booking!.bookingId}',
              ),
              _InfoRow(
                label: 'Trạng thái',
                value: booking?.bookingStatus ?? 'Chờ xác nhận',
              ),
              if (distance != null)
                _InfoRow(
                  label: 'Quãng đường',
                  value: '${distance.toStringAsFixed(1)} km',
                ),
              if (duration != null)
                _InfoRow(label: 'Thời gian dự kiến', value: '$duration phút'),
              const Padding(
                padding: EdgeInsets.symmetric(vertical: 8),
                child: Divider(thickness: 1, color: Color(0xFFEEEEEE)),
              ),
              _InfoRow(
                label: 'Tổng dự kiến',
                value: fare == null ? 'Đang cập nhật' : _formatCurrency(fare),
                isTotal: true,
              ),
              const SizedBox(height: 24),
              const _NoticeCard(),
              const SizedBox(height: 40),
            ],
          ),
        ),
        bottomNavigationBar: Container(
          padding: const EdgeInsets.fromLTRB(20, 10, 20, 24),
          decoration: const BoxDecoration(
            color: Colors.white,
            border: Border(top: BorderSide(color: Color(0xFFF5F5F5))),
          ),
          child: CustomButton(
            text: 'Xác nhận thuê tài xế',
            onPressed: () => _confirmDriver(context),
          ),
        ),
      ),
    );
  }

  Future<void> _confirmDriver(BuildContext context) async {
    await showDialog<void>(
      context: context,
      barrierDismissible: false,
      builder: (dialogContext) => AlertDialog(
        icon: const Icon(
          Icons.check_circle,
          color: AppColors.primary,
          size: 52,
        ),
        title: const Text('Đã xác nhận tài xế'),
        content: Text(
          booking == null
              ? '$driverName đã được chọn cho chuyến đi.'
              : '$driverName sẽ nhận chuyến #${booking!.bookingId}.',
          textAlign: TextAlign.center,
        ),
        actions: [
          FilledButton(
            onPressed: () {
              Navigator.pop(dialogContext);
              Navigator.of(context).popUntil((route) => route.isFirst);
            },
            child: const Text('Về trang chủ'),
          ),
        ],
      ),
    );
  }

  String _formatCurrency(double value) {
    final formatter = value.round().toString().replaceAllMapped(
      RegExp(r'(\d{1,3})(?=(\d{3})+(?!\d))'),
      (Match m) => '${m[1]}.',
    );
    return '${formatter}đ';
  }
}

class _DriverCard extends StatelessWidget {
  const _DriverCard({
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
        color: const Color(0xFFEAF4F4),
        borderRadius: BorderRadius.circular(16),
      ),
      child: Row(
        children: [
          const CircleAvatar(
            radius: 28,
            backgroundColor: AppColors.primary,
            child: Icon(Icons.person, color: Colors.white, size: 30),
          ),
          const SizedBox(width: 14),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  name,
                  style: const TextStyle(
                    fontWeight: FontWeight.w800,
                    fontSize: 16,
                  ),
                ),
                const SizedBox(height: 4),
                Text(
                  '${rating.toStringAsFixed(1)} sao • $tripCount chuyến • $experienceYears năm',
                  style: const TextStyle(color: Color(0xFF667174)),
                ),
              ],
            ),
          ),
          const Icon(Icons.verified, color: AppColors.primary),
        ],
      ),
    );
  }
}

class _VehicleCard extends StatelessWidget {
  const _VehicleCard({required this.vehicle});

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
          const SizedBox(width: 12),
          Expanded(
            child: Text(
              '${vehicle.name} • ${vehicle.plateNumber}',
              style: const TextStyle(fontWeight: FontWeight.w700),
            ),
          ),
        ],
      ),
    );
  }
}

class _RouteTimeline extends StatelessWidget {
  const _RouteTimeline({required this.pickup, required this.destination});

  final String pickup;
  final String destination;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        _RouteItem(
          icon: Icons.location_searching,
          label: 'Điểm đón',
          address: pickup,
        ),
        const SizedBox(height: 12),
        _RouteItem(
          icon: Icons.near_me,
          label: 'Điểm đến',
          address: destination,
          filled: true,
        ),
      ],
    );
  }
}

class _RouteItem extends StatelessWidget {
  const _RouteItem({
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
          backgroundColor: filled ? AppColors.primary : const Color(0xFFF0F0F0),
          child: Icon(
            icon,
            size: 18,
            color: filled ? Colors.white : Colors.black,
          ),
        ),
        const SizedBox(width: 12),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                label,
                style: const TextStyle(color: Color(0xFF667174), fontSize: 12),
              ),
              Text(
                address,
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
                style: const TextStyle(fontWeight: FontWeight.w700),
              ),
            ],
          ),
        ),
      ],
    );
  }
}

class _InfoRow extends StatelessWidget {
  const _InfoRow({
    required this.label,
    required this.value,
    this.isTotal = false,
  });

  final String label;
  final String value;
  final bool isTotal;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Text(label, style: const TextStyle(color: Color(0xFF667174))),
          Text(
            value,
            style: TextStyle(
              color: isTotal ? AppColors.primary : Colors.black,
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
  const _NoticeCard();

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: const Color(0xFFFFF8E1),
        borderRadius: BorderRadius.circular(12),
      ),
      child: const Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(Icons.info_outline, color: Color(0xFFFFA000), size: 20),
          SizedBox(width: 10),
          Expanded(
            child: Text(
              'Bước này xác nhận lựa chọn tài xế. Khi backend có API nhận chuyến/assign driver, nút này sẽ gọi API đó.',
              style: TextStyle(color: Color(0xFF6B5B00), height: 1.35),
            ),
          ),
        ],
      ),
    );
  }
}
