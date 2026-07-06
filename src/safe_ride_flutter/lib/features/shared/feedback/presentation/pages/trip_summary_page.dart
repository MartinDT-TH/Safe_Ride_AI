import 'dart:async';
import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../../../core/constants/app_colors.dart';
import '../../../../../core/constants/app_strings.dart';
import '../../../../../core/network/auth_header.dart';
import '../../../../../core/network/dio_client.dart';
import '../../../../auth/presentation/providers/auth_provider.dart';
import '../../../../customer/home/presentation/pages/customer_home_page.dart';
import '../../../../customer/home/presentation/providers/home_provider.dart';
import '../../../../customer/booking/data/models/booking_response.dart';
import '../../../../customer/booking/presentation/providers/booking_provider.dart';
import '../../../../driver/dashboard/data/models/payment_models.dart';

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
  bool _returnConfirmed = false;
  int _rating = 5;
  bool _isSubmittingRating = false;
  bool _canRateLater = false;
  bool _isWaitingForPayment = false;
  Timer? _paymentPollingTimer;
  final Dio _dio = DioClient().dio;
  final TextEditingController _commentController = TextEditingController();

  @override
  void initState() {
    super.initState();
    _returnConfirmed = _isReturnConfirmedStatus(widget.booking.tripStatus);
  }

  @override
  void dispose() {
    _paymentPollingTimer?.cancel();
    _commentController.dispose();
    super.dispose();
  }

  void _finishAndGoHome() {
    widget.onConfirmedVehicleReturned?.call();
    context.read<BookingProvider>().clearActiveBooking();
    context.read<HomeProvider>().setSelectedIndex(0);

    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(
        content: Text('Chuyến đi đã hoàn thành. Cảm ơn bạn!'),
        behavior: SnackBarBehavior.floating,
      ),
    );

    Navigator.of(context).pushAndRemoveUntil(
      MaterialPageRoute(builder: (_) => const CustomerHomePage()),
      (route) => false,
    );
  }

  Future<void> _submitRatingAndFinish() async {
    if (!_vehicleReturned || _isSubmittingRating) return;

    final token = context.read<AuthProvider>().token;
    final tripId = widget.booking.tripId;
    if (token == null || token.isEmpty || tripId == null) {
      _showSnack('Không thể xác định thông tin chuyến đi. Vui lòng thử lại.');
      return;
    }

    setState(() {
      _isSubmittingRating = true;
      _canRateLater = false;
    });

    final bookingProvider = context.read<BookingProvider>();
    final returnConfirmed = await _confirmReturnIfNeeded(
      bookingProvider,
      token,
      tripId,
    );

    if (!mounted) return;

    if (!returnConfirmed) {
      setState(() {
        _isSubmittingRating = false;
      });
      _showSnack(
        bookingProvider.errorMessage ??
            'Không thể xác nhận trả xe. Vui lòng thử lại.',
      );
      return;
    }

    final comment = _commentController.text.trim();
    final ok = await bookingProvider.submitTripRating(
      token,
      tripId: tripId,
      ratingScore: _rating,
      comment: comment.isEmpty ? null : comment,
    );

    if (!mounted) return;

    setState(() {
      _isSubmittingRating = false;
      _canRateLater = (bookingProvider.errorStatusCode ?? 0) >= 500;
    });

    if (ok) {
      _startWaitingForPayment();
      return;
    }

    final isAlreadyRated =
        bookingProvider.errorStatusCode == 409 ||
        (bookingProvider.errorMessage != null &&
            (bookingProvider.errorMessage!.toLowerCase().contains('already') ||
                bookingProvider.errorMessage!.toLowerCase().contains('rated') ||
                bookingProvider.errorMessage!.toLowerCase().contains(
                  'đã đánh giá',
                )));

    if (isAlreadyRated) {
      _startWaitingForPayment();
      return;
    }

    _showSnack(
      bookingProvider.errorMessage ??
          'Không thể gửi đánh giá. Vui lòng thử lại.',
    );
  }

  Future<bool> _confirmReturnIfNeeded(
    BookingProvider bookingProvider,
    String token,
    int tripId,
  ) async {
    if (_returnConfirmed) return true;

    final ok = await bookingProvider.confirmCustomerReturn(
      token,
      tripId: tripId,
    );
    if (ok) {
      _returnConfirmed = true;
      return true;
    }

    if (bookingProvider.errorStatusCode == 409) {
      final latest = await bookingProvider.refreshActiveBookingDetails(
        token,
        bookingId: widget.booking.bookingId,
      );
      if (_isReturnConfirmedStatus(latest?.tripStatus)) {
        _returnConfirmed = true;
        return true;
      }
    }

    return false;
  }

  void _showSnack(String message) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(message), behavior: SnackBarBehavior.floating),
    );
  }

  void _startWaitingForPayment() {
    if (!mounted) return;
    setState(() {
      _isWaitingForPayment = true;
    });

    _paymentPollingTimer?.cancel();
    unawaited(_pollPaymentStatusOnce());
    _paymentPollingTimer = Timer.periodic(const Duration(seconds: 3), (
      timer,
    ) async {
      final completed = await _pollPaymentStatusOnce();
      if (completed || !mounted) {
        timer.cancel();
      }
    });
  }

  Future<bool> _pollPaymentStatusOnce() async {
    final token = context.read<AuthProvider>().token;
    final tripId = widget.booking.tripId;
    if (token == null || tripId == null || !mounted) {
      return true;
    }

    try {
      final response = await _dio.get(
        ApiEndpoints.customerTripPaymentStatus(tripId),
        options: Options(
          headers: {ApiKeys.authorization: AuthHeader.bearer(token)},
        ),
      );
      final payload = _responsePayload(response.data);
      final status = PaymentStatusResult.fromJson(payload);
      if (status.isSuccess) {
        if (mounted) {
          _finishAndGoHome();
        }
        return true;
      }
    } catch (e) {
      debugPrint('Polling payment status failed: $e');
    }
    return false;
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
        if (_isWaitingForPayment) {
          _showSnack('Vui lòng đợi thanh toán hoàn tất.');
        } else {
          _showSnack(
            'Vui lòng xác nhận trả xe và gửi đánh giá trước khi rời màn hình.',
          );
        }
      },
      child: Stack(
        children: [
          Scaffold(
            backgroundColor: Colors.white,
            body: Column(
              children: [
                Container(
                  width: double.infinity,
                  padding: const EdgeInsets.fromLTRB(24, 60, 24, 30),
                  decoration: BoxDecoration(
                    gradient: LinearGradient(
                      begin: Alignment.topCenter,
                      end: Alignment.bottomCenter,
                      colors: [
                        AppColors.primary.withValues(alpha: 0.15),
                        AppColors.primary.withValues(alpha: 0.02),
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
                        child: const Icon(
                          Icons.check,
                          color: Colors.white,
                          size: 32,
                        ),
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
                        _PaymentDetails(
                          originalFare: originalFare,
                          discount: discount,
                          finalFare: finalFare,
                          formatCurrency: _formatCurrency,
                        ),
                        const SizedBox(height: 24),
                        _RatingCard(
                          rating: _rating,
                          enabled: !_isSubmittingRating,
                          commentController: _commentController,
                          onRatingChanged: (value) {
                            setState(() {
                              _rating = value;
                              _canRateLater = false;
                            });
                          },
                        ),
                        const SizedBox(height: 24),
                        InkWell(
                          onTap: _isSubmittingRating
                              ? null
                              : () {
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
                                  ? AppColors.primary.withValues(alpha: 0.05)
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
                Padding(
                  padding: const EdgeInsets.fromLTRB(24, 16, 24, 24),
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      SizedBox(
                        width: double.infinity,
                        height: 56,
                        child: ElevatedButton(
                          onPressed: _vehicleReturned && !_isSubmittingRating
                              ? _submitRatingAndFinish
                              : null,
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
                          child: _isSubmittingRating
                              ? const SizedBox(
                                  width: 22,
                                  height: 22,
                                  child: CircularProgressIndicator(
                                    strokeWidth: 2.5,
                                    valueColor: AlwaysStoppedAnimation<Color>(
                                      Colors.white,
                                    ),
                                  ),
                                )
                              : const Row(
                                  mainAxisAlignment: MainAxisAlignment.center,
                                  children: [
                                    Text(
                                      'Gửi đánh giá & chờ thanh toán',
                                      style: TextStyle(
                                        fontSize: 16,
                                        fontWeight: FontWeight.w800,
                                      ),
                                    ),
                                    SizedBox(width: 10),
                                    Icon(Icons.arrow_forward, size: 20),
                                  ],
                                ),
                        ),
                      ),
                      if (_canRateLater) ...[
                        const SizedBox(height: 10),
                        SizedBox(
                          width: double.infinity,
                          height: 48,
                          child: OutlinedButton(
                            onPressed: _isSubmittingRating
                                ? null
                                : _finishAndGoHome,
                            style: OutlinedButton.styleFrom(
                              foregroundColor: AppColors.primary,
                              side: const BorderSide(color: AppColors.primary),
                              shape: RoundedRectangleBorder(
                                borderRadius: BorderRadius.circular(14),
                              ),
                            ),
                            child: const Text(
                              'Xác nhận chuyến & đánh giá sau',
                              style: TextStyle(fontWeight: FontWeight.w800),
                            ),
                          ),
                        ),
                      ],
                    ],
                  ),
                ),
              ],
            ),
          ),
          if (_isWaitingForPayment)
            Positioned.fill(
              child: Container(
                color: Colors.black.withValues(alpha: 0.6),
                child: Center(
                  child: Container(
                    padding: const EdgeInsets.all(24),
                    margin: const EdgeInsets.symmetric(horizontal: 40),
                    decoration: BoxDecoration(
                      color: Colors.white,
                      borderRadius: BorderRadius.circular(16),
                    ),
                    child: const Column(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        CircularProgressIndicator(color: AppColors.primary),
                        SizedBox(height: 16),
                        Text(
                          'Đang chờ thanh toán...',
                          style: TextStyle(
                            fontSize: 16,
                            fontWeight: FontWeight.bold,
                            color: Color(0xFF1D2939),
                          ),
                        ),
                        SizedBox(height: 8),
                        Text(
                          'Vui lòng chờ tài xế xác nhận thanh toán hoặc quét mã QR.',
                          textAlign: TextAlign.center,
                          style: TextStyle(
                            fontSize: 14,
                            color: Color(0xFF667085),
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
              ),
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
    return '$formatterđ';
  }

  static bool _isReturnConfirmedStatus(String? status) {
    if (status == null) return false;
    final s = status.toUpperCase();
    return s == 'RETURN_CONFIRMED' ||
        s == 'WAITING_PAYMENT' ||
        s == 'COMPLETED' ||
        s == '5' ||
        s == '6' ||
        s == '7';
  }

  static Map<String, dynamic> _responsePayload(Object? data) {
    if (data is Map) {
      final wrapped = data['data'];
      if (wrapped is Map) {
        return Map<String, dynamic>.from(wrapped);
      }
      return Map<String, dynamic>.from(data);
    }
    throw const FormatException('Invalid payment status response.');
  }
}

class _PaymentDetails extends StatelessWidget {
  const _PaymentDetails({
    required this.originalFare,
    required this.discount,
    required this.finalFare,
    required this.formatCurrency,
  });

  final double originalFare;
  final double discount;
  final double finalFare;
  final String Function(double value) formatCurrency;

  @override
  Widget build(BuildContext context) {
    return Container(
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
                      color: Colors.black.withValues(alpha: 0.05),
                      blurRadius: 4,
                    ),
                  ],
                ),
                child: const Icon(Icons.receipt_long_outlined, size: 20),
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
          ),
          const Padding(
            padding: EdgeInsets.symmetric(vertical: 12),
            child: Divider(height: 1, color: Color(0xFFEAECF0)),
          ),
          _PriceRow(
            label: 'Cước phí cơ bản',
            value: formatCurrency(originalFare),
          ),
          const SizedBox(height: 12),
          _PriceRow(
            label: 'Khuyến mãi',
            value: '-${formatCurrency(discount)}',
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
                style: TextStyle(fontSize: 18, fontWeight: FontWeight.w700),
              ),
              Text(
                formatCurrency(finalFare),
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
    );
  }
}

class _RatingCard extends StatelessWidget {
  const _RatingCard({
    required this.rating,
    required this.enabled,
    required this.commentController,
    required this.onRatingChanged,
  });

  final int rating;
  final bool enabled;
  final TextEditingController commentController;
  final ValueChanged<int> onRatingChanged;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        color: const Color(0xFFF9FAFB),
        borderRadius: BorderRadius.circular(20),
        border: Border.all(color: const Color(0xFFEAECF0)),
      ),
      child: Column(
        children: [
          const Text(
            'Bạn thấy tài xế thế nào?',
            style: TextStyle(
              fontSize: 16,
              fontWeight: FontWeight.w800,
              color: Color(0xFF1D2939),
            ),
          ),
          const SizedBox(height: 12),
          Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: List.generate(5, (index) {
              final selected = index < rating;
              return IconButton(
                tooltip: '${index + 1} sao',
                onPressed: enabled ? () => onRatingChanged(index + 1) : null,
                icon: Icon(
                  selected ? Icons.star_rounded : Icons.star_outline_rounded,
                  color: selected ? Colors.amber : const Color(0xFFD0D5DD),
                  size: 38,
                ),
              );
            }),
          ),
          const SizedBox(height: 12),
          TextField(
            controller: commentController,
            enabled: enabled,
            decoration: InputDecoration(
              hintText: 'Nhận xét về tài xế (không bắt buộc)',
              hintStyle: const TextStyle(
                fontSize: 14,
                color: Color(0xFF98A2B3),
              ),
              filled: true,
              fillColor: Colors.white,
              border: OutlineInputBorder(
                borderRadius: BorderRadius.circular(12),
                borderSide: BorderSide.none,
              ),
            ),
            maxLines: 3,
            maxLength: 1000,
          ),
        ],
      ),
    );
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
    final parts = value.split(' ');
    final number = parts.isEmpty ? value : parts.first;
    final unit = parts.length < 2 ? '' : parts.last;

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
              Flexible(
                child: Text(
                  label,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(
                    fontSize: 11,
                    fontWeight: FontWeight.w700,
                    color: Color(0xFF667085),
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: 8),
          RichText(
            text: TextSpan(
              children: [
                TextSpan(
                  text: number,
                  style: const TextStyle(
                    fontSize: 22,
                    fontWeight: FontWeight.w900,
                    color: Color(0xFF1D2939),
                  ),
                ),
                TextSpan(
                  text: unit.isEmpty ? '' : ' $unit',
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
  const _PriceRow({required this.label, required this.value, this.valueColor});

  final String label;
  final String value;
  final Color? valueColor;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: [
        Flexible(
          child: Row(
            children: [
              Flexible(
                child: Text(
                  label,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(
                    fontSize: 15,
                    fontWeight: FontWeight.w500,
                    color: const Color(0xFF475467),
                  ),
                ),
              ),
            ],
          ),
        ),
        const SizedBox(width: 12),
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
