import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../../../../core/constants/app_colors.dart';
import '../../../home/presentation/pages/customer_home_page.dart';
import '../../../home/presentation/providers/home_provider.dart';
import '../../data/models/booking_response.dart';
import '../providers/booking_provider.dart';

class TripSummaryPage extends StatefulWidget {
  const TripSummaryPage({
    super.key,
    required this.booking,
    this.onConfirmedVehicleReturned,
  });

  final BookingResponse booking;
  final VoidCallback? onConfirmedVehicleReturned;

  @override
  State<TripSummaryPage> createState() => _TripSummaryPageState();
}

class _TripSummaryPageState extends State<TripSummaryPage> {
  bool _vehicleReturned = false;
  int _rating = 5;
  final TextEditingController _commentController = TextEditingController();

  void _finishAndGoHome() {
    // 1. Dọn dẹp trạng thái booking
    context.read<BookingProvider>().clearActiveBooking();
    
    // 2. Chuyển tab về Trang chủ
    context.read<HomeProvider>().setSelectedIndex(0);

    // 3. Hiển thị thông báo
    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(
        content: Text('Chuyến đi đã hoàn thành. Cảm ơn bạn!'),
        behavior: SnackBarBehavior.floating,
      ),
    );

    // 4. Quay về màn hình gốc (CustomerHomePage) và xóa stack
    Navigator.of(context).pushAndRemoveUntil(
      MaterialPageRoute(builder: (_) => const CustomerHomePage()),
      (route) => false,
    );
  }

  @override
  void dispose() {
    _commentController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final originalFare =
        widget.booking.originalFare ?? widget.booking.estimatedFare;
    final discount = widget.booking.discountAmount ?? 0;
    final finalFare = widget.booking.finalFare ?? (originalFare - discount);

    return PopScope(
      canPop: false,
      onPopInvokedWithResult: (didPop, result) {
        if (didPop) return;
        _finishAndGoHome();
      },
      child: Scaffold(
        backgroundColor: Colors.white,
        body: Column(
          children: [
            // Header section
            Container(
              width: double.infinity,
              padding: const EdgeInsets.fromLTRB(24, 60, 24, 30),
              decoration: BoxDecoration(
                gradient: LinearGradient(
                  begin: Alignment.topCenter,
                  end: Alignment.bottomCenter,
                  colors: [
                    AppColors.primary.withOpacity(0.15),
                    AppColors.primary.withOpacity(0.02),
                    Colors.white,
                  ],
                ),
              ),
              child: Column(
                children: [
                  Container(
                    padding: const EdgeInsets.all(16),
                    decoration: const BoxDecoration(
                      color: AppColors.primary,
                      shape: BoxShape.circle,
                    ),
                    child: const Icon(Icons.check, color: Colors.white, size: 32),
                  ),
                  const SizedBox(height: 20),
                  const Text(
                    'Chuyến đi hoàn tất',
                    style: TextStyle(
                      fontSize: 28,
                      fontWeight: FontWeight.w900,
                      color: Color(0xFF1D2939),
                    ),
                  ),
                  const SizedBox(height: 4),
                  const Text(
                    'Cảm ơn bạn đã sử dụng dịch vụ',
                    style: TextStyle(
                      fontSize: 15,
                      color: Color(0xFF667085),
                    ),
                  ),
                ],
              ),
            ),
            Expanded(
              child: SingleChildScrollView(
                physics: const BouncingScrollPhysics(),
                padding: const EdgeInsets.symmetric(horizontal: 24),
                child: Column(
                  children: [
                    // Stats row
                    Row(
                      children: [
                        Expanded(
                          child: _StatCard(
                            icon: Icons.route_outlined,
                            label: 'QUÃNG ĐƯỜNG',
                            value:
                                '${widget.booking.estimatedDistanceKm.toStringAsFixed(1)} km',
                          ),
                        ),
                        const SizedBox(width: 16),
                        Expanded(
                          child: _StatCard(
                            icon: Icons.access_time,
                            label: 'THỜI GIAN',
                            value:
                                '${widget.booking.estimatedDurationMinutes} phút',
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 24),

                    // Payment details
                    Container(
                      padding: const EdgeInsets.all(20),
                      decoration: BoxDecoration(
                        color: const Color(0xFFF9FAFB),
                        borderRadius: BorderRadius.circular(20),
                        border: Border.all(color: const Color(0xFFEAECF0)),
                      ),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Row(
                            children: [
                            Container(
                              padding: const EdgeInsets.all(8),
                              decoration: BoxDecoration(
                                color: Colors.white,
                                borderRadius: BorderRadius.circular(8),
                                boxShadow: [
                                  BoxShadow(
                                    color: Colors.black.withOpacity(0.05),
                                    blurRadius: 4,
                                  ),
                                ],
                              ),
                              child: const Icon(
                                Icons.receipt_long_outlined,
                                size: 20,
                              ),
                            ),
                            const SizedBox(width: 12),
                            const Text(
                              'Chi tiết thanh toán',
                              style: TextStyle(
                                fontSize: 18,
                                fontWeight: FontWeight.w800,
                                color: Color(0xFF1D2939),
                              ),
                            ),
                          ],
                        ), // có thể xóa ếu cần
                          const Padding(
                            padding: EdgeInsets.symmetric(vertical: 12),
                            child: Divider(height: 1, color: Color(0xFFEAECF0)),
                          ),
                          _PriceRow(
                            label: 'Cước phí cơ bản',
                            value: _formatCurrency(originalFare),
                          ),
                          const SizedBox(height: 12),
                          _PriceRow(
                            label: 'Khuyến mãi',
                            value: '-${_formatCurrency(discount)}',
                            valueColor: AppColors.primary,
                          ),
                          const Padding(
                            padding: EdgeInsets.symmetric(vertical: 12),
                            child: Divider(height: 1, color: Color(0xFFEAECF0)),
                          ),
                          Row(
                            mainAxisAlignment: MainAxisAlignment.spaceBetween,
                            children: [
                              const Text(
                                'Tổng cộng',
                                style: TextStyle(
                                  fontSize: 18,
                                  fontWeight: FontWeight.w700,
                                ),
                              ),
                              Text(
                                _formatCurrency(finalFare),
                                style: const TextStyle(
                                  fontSize: 24,
                                  fontWeight: FontWeight.w900,
                                  color: AppColors.primary,
                                ),
                              ),
                            ],
                          ),
                        ],
                      ),
                    ),
                    const SizedBox(height: 24),

                    /* 
                    // Rating section (Commented out as API is not ready)
                    const Text(
                      'Bạn thấy tài xế thế nào?',
                      style: TextStyle(fontSize: 16, fontWeight: FontWeight.w700),
                    ),
                    const SizedBox(height: 12),
                    Row(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: List.generate(5, (index) {
                        return IconButton(
                          onPressed: () => setState(() => _rating = index + 1),
                          icon: Icon(
                            index < _rating ? Icons.star_rounded : Icons.star_outline_rounded,
                            color: index < _rating ? Colors.amber : Colors.grey,
                            size: 36,
                          ),
                        );
                      }),
                    ),
                    const SizedBox(height: 12),
                    TextField(
                      controller: _commentController,
                      decoration: InputDecoration(
                        hintText: 'Nhận xét về tài xế (không bắt buộc)',
                        hintStyle: const TextStyle(fontSize: 14, color: Colors.grey),
                        filled: true,
                        fillColor: const Color(0xFFF9FAFB),
                        border: OutlineInputBorder(
                          borderRadius: BorderRadius.circular(12),
                          borderSide: BorderSide.none,
                        ),
                      ),
                      maxLines: 3,
                    ),
                    const SizedBox(height: 24),
                    */

                    // Vehicle return confirmation
                    InkWell(
                      onTap: () {
                        setState(() {
                          _vehicleReturned = !_vehicleReturned;
                        });
                      },
                      borderRadius: BorderRadius.circular(12),
                      child: Container(
                        padding: const EdgeInsets.symmetric(
                          vertical: 12,
                          horizontal: 16,
                        ),
                        decoration: BoxDecoration(
                          color: _vehicleReturned
                              ? AppColors.primary.withOpacity(0.05)
                              : Colors.transparent,
                          borderRadius: BorderRadius.circular(12),
                          border: Border.all(
                            color: _vehicleReturned
                                ? AppColors.primary
                                : const Color(0xFFD0D5DD),
                          ),
                        ),
                        child: Row(
                          children: [
                            Icon(
                              _vehicleReturned
                                  ? Icons.check_box
                                  : Icons.check_box_outline_blank,
                              color: _vehicleReturned
                                  ? AppColors.primary
                                  : const Color(0xFF667085),
                            ),
                            const SizedBox(width: 12),
                            const Expanded(
                              child: Text(
                                'Xác nhận tài xế đã trả lại phương tiện',
                                style: TextStyle(
                                  fontWeight: FontWeight.w600,
                                  fontSize: 14,
                                  color: Color(0xFF344054),
                                ),
                              ),
                            ),
                          ],
                        ),
                      ),
                    ),
                    const SizedBox(height: 30),
                  ],
                ),
              ),
            ),

            // Action button
            Padding(
              padding: const EdgeInsets.fromLTRB(24, 16, 24, 24),
              child: SizedBox(
                width: double.infinity,
                height: 56,
                child: ElevatedButton(
                onPressed: _vehicleReturned ? _finishAndGoHome : null,
                  style: ElevatedButton.styleFrom(
                    backgroundColor: AppColors.primary,
                    foregroundColor: Colors.white,
                    disabledBackgroundColor: const Color(0xFFEAECF0),
                    disabledForegroundColor: const Color(0xFF98A2B3),
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(16),
                    ),
                    elevation: 0,
                  ),
                  child: const Row(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      Text(
                        'Hoàn tất & Về trang chủ',
                        style: TextStyle(
                          fontSize: 16,
                          fontWeight: FontWeight.w800,
                        ),
                      ),
                      const SizedBox(width: 10),
                      const Icon(Icons.arrow_forward, size: 20),
                    ],
                  ),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }

  String _formatCurrency(double value) {
    final formatter = value.round().toString().replaceAllMapped(
      RegExp(r'(\d{1,3})(?=(\d{3})+(?!\d))'),
      (Match m) => '${m[1]}.',
    );
    return '$formatterđ';
  }
}

class _StatCard extends StatelessWidget {
  const _StatCard({
    required this.icon,
    required this.label,
    required this.value,
  });

  final IconData icon;
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: const Color(0xFFF9FAFB),
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: const Color(0xFFEAECF0)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Icon(icon, size: 16, color: const Color(0xFF667085)),
              const SizedBox(width: 6),
              Text(
                label,
                style: const TextStyle(
                  fontSize: 11,
                  fontWeight: FontWeight.w700,
                  color: Color(0xFF667085),
                  letterSpacing: 0.5,
                ),
              ),
            ],
          ),
          const SizedBox(height: 8),
          RichText(
            text: TextSpan(
              children: [
                TextSpan(
                  text: value.split(' ').first,
                  style: const TextStyle(
                    fontSize: 22,
                    fontWeight: FontWeight.w900,
                    color: Color(0xFF1D2939),
                  ),
                ),
                TextSpan(
                  text: ' ${value.split(' ').last}',
                  style: const TextStyle(
                    fontSize: 14,
                    fontWeight: FontWeight.w600,
                    color: Color(0xFF667085),
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _PriceRow extends StatelessWidget {
  const _PriceRow({
    required this.label,
    required this.value,
    this.valueColor,
    this.labelIcon,
  });

  final String label;
  final String value;
  final Color? valueColor;
  final IconData? labelIcon;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: [
        Row(
          children: [
            if (labelIcon != null) ...[
              Icon(
                labelIcon,
                size: 16,
                color: valueColor ?? const Color(0xFF475467),
              ),
              const SizedBox(width: 8),
            ],
            Text(
              label,
              style: TextStyle(
                fontSize: 15,
                fontWeight: FontWeight.w500,
                color: labelIcon != null
                    ? (valueColor ?? const Color(0xFF475467))
                    : const Color(0xFF475467),
              ),
            ),
          ],
        ),
        Text(
          value,
          style: TextStyle(
            fontSize: 15,
            fontWeight: FontWeight.w700,
            color: valueColor ?? const Color(0xFF1D2939),
          ),
        ),
      ],
    );
  }
}
