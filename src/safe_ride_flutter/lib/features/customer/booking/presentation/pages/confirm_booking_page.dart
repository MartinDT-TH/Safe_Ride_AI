import 'package:flutter/material.dart';
import '../../../../../core/constants/app_colors.dart';
import '../../../../../core/constants/app_strings.dart';
import '../../../../../core/widgets/custom_button.dart';
import '../../data/models/booking_location.dart';
import '../../data/models/booking_fare_estimate.dart';

class ConfirmBookingPage extends StatelessWidget {
  const ConfirmBookingPage({
    super.key,
    required this.pickup,
    required this.destination,
    required this.estimate,
    this.vehicleName = 'Toyota Vios',
    this.plateNumber = '30F - 987.65',
    this.driverName = 'Nguyễn Văn A',
    this.driverRating = 4.9,
    this.driverExperience = 5,
  });

  final BookingLocation pickup;
  final BookingLocation? destination;
  final BookingFareEstimate estimate;
  final String vehicleName;
  final String plateNumber;
  final String driverName;
  final double driverRating;
  final int driverExperience;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: Colors.white,
      appBar: AppBar(
        leading: const BackButton(color: Colors.black),
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
            _VehicleCard(
              title: 'Xe của bạn: $vehicleName - $plateNumber',
              subtitle:
                  '${estimate.estimatedDurationMinutes} phút • ${estimate.estimatedDistanceKm} km',
              onTapChange: () {},
            ),
            const SizedBox(height: 24),
            const Text(
              'Tài xế đã chọn',
              style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
            ),
            const SizedBox(height: 12),
            _DriverCard(
              name: driverName,
              rating: driverRating,
              experience: driverExperience,
              onTapChange: () {},
            ),
            const SizedBox(height: 32),
            _RouteTimeline(
              pickup: pickup.address,
              destination: destination?.address ?? 'Thuê theo giờ',
            ),
            const SizedBox(height: 32),
            const Text(
              'Chi tiết thanh toán',
              style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
            ),
            const SizedBox(height: 16),
            _PaymentRow(
              label: 'Giá cước',
              value: _formatCurrency(estimate.estimatedFare),
            ),
            _PaymentRow(label: 'Phí nền tảng', value: '2.000đ'),
            _PaymentRow(
              label: 'Khuyến mãi',
              value: '-15.000đ',
              valueColor: const Color(0xFF007A87),
            ),
            const Padding(
              padding: EdgeInsets.symmetric(vertical: 8),
              child: Divider(thickness: 1, color: Color(0xFFEEEEEE)),
            ),
            _PaymentRow(
              label: 'Tổng cộng',
              value: _formatCurrency(estimate.estimatedFare + 2000 - 15000),
              isTotal: true,
            ),
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
          text: 'Xác nhận →',
          onPressed: () {
            // Logic điều hướng tới SearchingDriverPage hoặc gọi API
          },
        ),
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

class _VehicleCard extends StatelessWidget {
  const _VehicleCard({
    required this.title,
    required this.subtitle,
    required this.onTapChange,
  });

  final String title;
  final String subtitle;
  final VoidCallback onTapChange;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: AppColors.border),
      ),
      child: Row(
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  title,
                  style: const TextStyle(
                    fontWeight: FontWeight.bold,
                    fontSize: 15,
                  ),
                ),
                const SizedBox(height: 4),
                Text(
                  subtitle,
                  style: const TextStyle(color: Colors.grey, fontSize: 13),
                ),
                const SizedBox(height: 12),
                _SmallChangeButton(onPressed: onTapChange),
              ],
            ),
          ),
          const SizedBox(width: 12),
          Container(
            padding: const EdgeInsets.all(12),
            decoration: BoxDecoration(
              color: const Color(0xFFF3F3F3),
              borderRadius: BorderRadius.circular(12),
            ),
            child: const Icon(
              Icons.directions_car_filled_outlined,
              size: 40,
              color: Color(0xFF4A4A4A),
            ),
          ),
        ],
      ),
    );
  }
}

class _DriverCard extends StatelessWidget {
  const _DriverCard({
    required this.name,
    required this.rating,
    required this.experience,
    required this.onTapChange,
  });

  final String name;
  final double rating;
  final int experience;
  final VoidCallback onTapChange;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: AppColors.border),
      ),
      child: Row(
        children: [
          const CircleAvatar(
            radius: 28,
            backgroundColor: Color(0xFFF0F0F0),
            child: Icon(Icons.person_outline, color: Colors.grey, size: 30),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  name,
                  style: const TextStyle(
                    fontWeight: FontWeight.bold,
                    fontSize: 16,
                  ),
                ),
                const SizedBox(height: 4),
                Row(
                  children: [
                    Text(
                      rating.toString(),
                      style: const TextStyle(
                        fontWeight: FontWeight.bold,
                        color: AppColors.primary,
                        fontSize: 13,
                      ),
                    ),
                    const SizedBox(width: 4),
                    const Icon(Icons.star, color: AppColors.primary, size: 14),
                    Text(
                      ' • $experience năm kinh nghiệm',
                      style: const TextStyle(color: Colors.grey, fontSize: 13),
                    ),
                  ],
                ),
              ],
            ),
          ),
          _SmallChangeButton(onPressed: onTapChange),
        ],
      ),
    );
  }
}

class _SmallChangeButton extends StatelessWidget {
  const _SmallChangeButton({required this.onPressed});
  final VoidCallback onPressed;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onPressed,
      borderRadius: BorderRadius.circular(8),
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
        decoration: BoxDecoration(
          color: const Color(0xFFE0ECEE),
          borderRadius: BorderRadius.circular(8),
        ),
        child: const Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(
              'Thay đổi',
              style: TextStyle(
                fontWeight: FontWeight.bold,
                fontSize: 12,
                color: Color(0xFF2D2D2D),
              ),
            ),
            SizedBox(width: 4),
            Icon(Icons.change_history, size: 10),
          ],
        ),
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
          isFirst: true,
        ),
        _RouteItem(
          icon: Icons.near_me,
          label: 'Điểm đến',
          address: destination,
          isLast: true,
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
    this.isFirst = false,
    this.isLast = false,
  });

  final IconData icon;
  final String label;
  final String address;
  final bool isFirst;
  final bool isLast;

  @override
  Widget build(BuildContext context) {
    return IntrinsicHeight(
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Column(
            children: [
              Container(
                padding: const EdgeInsets.all(8),
                decoration: BoxDecoration(
                  color: isFirst ? const Color(0xFFF0F0F0) : AppColors.primary,
                  shape: BoxShape.circle,
                ),
                child: Icon(
                  icon,
                  size: 18,
                  color: isFirst ? Colors.black : Colors.white,
                ),
              ),
              if (!isLast)
                Expanded(
                  child: Container(
                    width: 1,
                    margin: const EdgeInsets.symmetric(vertical: 4),
                    decoration: BoxDecoration(
                      color: Colors.grey[300],
                      borderRadius: BorderRadius.circular(1),
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
                  label,
                  style: TextStyle(color: Colors.grey[600], fontSize: 12),
                ),
                Text(
                  address,
                  style: const TextStyle(
                    fontWeight: FontWeight.w600,
                    fontSize: 15,
                  ),
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                ),
                if (!isLast) const SizedBox(height: 24),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _PaymentRow extends StatelessWidget {
  const _PaymentRow({
    required this.label,
    required this.value,
    this.valueColor,
    this.isTotal = false,
  });

  final String label;
  final String value;
  final Color? valueColor;
  final bool isTotal;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Text(
            label,
            style: TextStyle(
              fontSize: isTotal ? 17 : 15,
              fontWeight: isTotal ? FontWeight.bold : FontWeight.normal,
              color: isTotal ? Colors.black : Colors.grey[700],
            ),
          ),
          Text(
            value,
            style: TextStyle(
              fontSize: isTotal ? 20 : 15,
              fontWeight: FontWeight.bold,
              color: isTotal ? AppColors.primary : (valueColor ?? Colors.black),
            ),
          ),
        ],
      ),
    );
  }
}
