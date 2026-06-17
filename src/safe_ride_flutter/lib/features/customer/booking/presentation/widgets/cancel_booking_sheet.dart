import 'package:flutter/material.dart';

import '../../../../../core/constants/app_colors.dart';

class CancelBookingSheet extends StatefulWidget {
  const CancelBookingSheet({super.key, this.bookingId});

  final int? bookingId;

  static Future<String?> show(BuildContext context, {int? bookingId}) {
    return showModalBottomSheet<String>(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.transparent,
      builder: (_) => CancelBookingSheet(bookingId: bookingId),
    );
  }

  @override
  State<CancelBookingSheet> createState() => _CancelBookingSheetState();
}

class _CancelBookingSheetState extends State<CancelBookingSheet> {
  late String _selectedReason = _reasons.first;

  static const List<String> _reasons = [
    'Thay đổi kế hoạch',
    'Thời gian chờ quá lâu',
    'Đặt nhầm địa điểm',
    'Không còn cần tài xế',
    'Lý do khác',
  ];

  @override
  Widget build(BuildContext context) {
    return SafeArea(
      top: false,
      child: Container(
        decoration: const BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.vertical(top: Radius.circular(32)),
        ),
        padding: const EdgeInsets.fromLTRB(24, 12, 24, 32),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Center(
              child: Container(
                width: 40,
                height: 4,
                decoration: BoxDecoration(
                  color: Colors.grey[300],
                  borderRadius: BorderRadius.circular(2),
                ),
              ),
            ),
            const SizedBox(height: 24),
            const Text(
              'Hủy chuyến đi?',
              style: TextStyle(
                fontSize: 22,
                fontWeight: FontWeight.bold,
                color: Color(0xFF1A1A1A),
              ),
            ),
            const SizedBox(height: 8),
            Text(
              widget.bookingId == null
                  ? 'Bạn có chắc chắn muốn hủy yêu cầu tìm tài xế này không?'
                  : 'Bạn có chắc chắn muốn hủy chuyến #${widget.bookingId} không?',
              style: TextStyle(
                fontSize: 15,
                color: Colors.grey[700],
                height: 1.5,
              ),
            ),
            const SizedBox(height: 24),
            const Text(
              'Lý do hủy chuyến',
              style: TextStyle(
                fontSize: 16,
                fontWeight: FontWeight.bold,
                color: Color(0xFF1A1A1A),
              ),
            ),
            const SizedBox(height: 16),
            ..._reasons.map(_buildReasonOption),
            const SizedBox(height: 28),
            SizedBox(
              width: double.infinity,
              height: 56,
              child: ElevatedButton(
                onPressed: () => Navigator.pop(context, _selectedReason),
                style: ElevatedButton.styleFrom(
                  backgroundColor: const Color(0xFFC62828),
                  foregroundColor: Colors.white,
                  elevation: 0,
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(16),
                  ),
                ),
                child: const Text(
                  'Xác nhận hủy',
                  style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold),
                ),
              ),
            ),
            const SizedBox(height: 12),
            SizedBox(
              width: double.infinity,
              height: 56,
              child: TextButton(
                onPressed: () => Navigator.pop(context),
                style: TextButton.styleFrom(
                  backgroundColor: const Color(0xFFE8F2F2),
                  foregroundColor: AppColors.primary,
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(16),
                  ),
                ),
                child: const Text(
                  'Không, quay lại',
                  style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildReasonOption(String reason) {
    final isSelected = _selectedReason == reason;
    return InkWell(
      onTap: () => setState(() => _selectedReason = reason),
      borderRadius: BorderRadius.circular(12),
      child: Container(
        margin: const EdgeInsets.only(bottom: 12),
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 16),
        decoration: BoxDecoration(
          color: isSelected ? const Color(0xFFE8F2F2) : const Color(0xFFF8F9FA),
          borderRadius: BorderRadius.circular(12),
          border: Border.all(
            color: isSelected ? AppColors.primary : Colors.transparent,
            width: 1,
          ),
        ),
        child: Row(
          children: [
            Expanded(
              child: Text(
                reason,
                style: TextStyle(
                  fontSize: 15,
                  fontWeight: isSelected ? FontWeight.bold : FontWeight.normal,
                  color: isSelected
                      ? AppColors.primary
                      : const Color(0xFF1A1A1A),
                ),
              ),
            ),
            Container(
              width: 20,
              height: 20,
              decoration: BoxDecoration(
                shape: BoxShape.circle,
                border: Border.all(
                  color: isSelected ? AppColors.primary : Colors.grey[400]!,
                  width: 2,
                ),
                color: isSelected ? AppColors.primary : Colors.transparent,
              ),
              child: isSelected
                  ? const Center(
                      child: Icon(Icons.circle, size: 8, color: Colors.white),
                    )
                  : null,
            ),
          ],
        ),
      ),
    );
  }
}
