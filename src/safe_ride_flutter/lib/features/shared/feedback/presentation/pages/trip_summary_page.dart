import 'dart:async';
import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:provider/provider.dart';

import '../../../../../core/constants/app_colors.dart';
import '../../../../../core/constants/app_strings.dart';
import '../../../../../core/network/auth_header.dart';
import '../../../../../core/localization/locale_provider.dart';
import '../../../../../core/localization/localization_extensions.dart';
import '../../../../../core/network/dio_client.dart';
import '../../../../../core/session/session_manager.dart';
import '../../../../../dependency_injection/injection.dart';
import '../../../../auth/presentation/providers/auth_provider.dart';
import '../../../../customer/home/presentation/pages/customer_home_page.dart';
import '../../../../customer/home/presentation/providers/home_provider.dart';
import '../../../../customer/booking/data/models/booking_response.dart';
import '../../../../customer/booking/presentation/providers/booking_provider.dart';
import '../../../../driver/dashboard/data/models/payment_models.dart';

class TripSummaryPage extends StatefulWidget {
  TripSummaryPage({
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
  late BookingResponse _booking;
  bool _vehicleReturned = false;
  bool _returnConfirmed = false;
  int _rating = 5;
  bool _isSubmittingRating = false;
  bool _canRateLater = false;
  bool _isWaitingForPayment = false;
  bool _isSubmittingFinalRating = false;
  Timer? _paymentPollingTimer;
  final Dio _dio = DioClient().dio;
  final TextEditingController _commentController = TextEditingController();

  @override
  void initState() {
    super.initState();
    _booking = widget.booking;
    _returnConfirmed = _isReturnConfirmedStatus(_booking.tripStatus);
    WidgetsBinding.instance.addPostFrameCallback((_) {
      unawaited(_refreshBookingSnapshot());
    });
  }

  @override
  void dispose() {
    _paymentPollingTimer?.cancel();
    _commentController.dispose();
    super.dispose();
  }

  Future<void> _finishAndGoHome() async {
    widget.onConfirmedVehicleReturned?.call();
    context.read<BookingProvider>().clearActiveBooking();
    context.read<HomeProvider>().setSelectedIndex(0);

    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(context.l10n.tripCompletedThanks),
        behavior: SnackBarBehavior.floating,
      ),
    );

    final sessionManager = getIt<SessionManager>();
    if (await sessionManager.isTripContinuationSession()) {
      await sessionManager.completeDeferredRelogin();
      return;
    }

    if (!mounted) return;
    Navigator.of(context).pushAndRemoveUntil(
      MaterialPageRoute(builder: (_) => CustomerHomePage()),
      (route) => false,
    );
  }

  Future<void> _submitRatingAndFinish() async {
    if (!_vehicleReturned || _isSubmittingRating) return;

    final token = context.read<AuthProvider>().token;
    final tripId = _booking.tripId;
    if (token == null || token.isEmpty || tripId == null) {
      _showSnack(context.l10n.tripInfoUnavailable);
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
        bookingProvider.errorMessage ?? context.l10n.returnConfirmationFailed,
      );
      return;
    }

    setState(() {
      _isSubmittingRating = false;
    });
    _startWaitingForPayment();
  }

  Future<bool> _confirmReturnIfNeeded(
    BookingProvider bookingProvider,
    String token,
    int tripId,
  ) async {
    if (_returnConfirmed) {
      await _refreshBookingSnapshot(
        bookingProvider: bookingProvider,
        token: token,
      );
      return true;
    }

    final ok = await bookingProvider.confirmCustomerReturn(
      token,
      tripId: tripId,
    );
    if (ok) {
      _returnConfirmed = true;
      await _refreshBookingSnapshot(
        bookingProvider: bookingProvider,
        token: token,
      );
      return true;
    }

    if (bookingProvider.errorStatusCode == 409) {
      final latest = await bookingProvider.refreshActiveBookingDetails(
        token,
        bookingId: _booking.bookingId,
      );
      if (_isReturnConfirmedStatus(latest?.tripStatus)) {
        _returnConfirmed = true;
        if (mounted && latest != null) {
          setState(() {
            _booking = latest;
          });
        }
        return true;
      }
    }

    return false;
  }

  Future<void> _refreshBookingSnapshot({
    BookingProvider? bookingProvider,
    String? token,
  }) async {
    if (!mounted) return;

    final accessToken = token ?? context.read<AuthProvider>().token;
    if (accessToken == null || accessToken.isEmpty) return;

    final provider = bookingProvider ?? context.read<BookingProvider>();
    final latest = await provider.refreshActiveBookingDetails(
      accessToken,
      bookingId: _booking.bookingId,
    );
    if (!mounted || latest == null) return;

    setState(() {
      _booking = latest;
    });
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
    _paymentPollingTimer = Timer.periodic(Duration(seconds: 3), (
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
    final tripId = _booking.tripId;
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
      if (status.isSuccess || status.tripStatus.toUpperCase() == 'COMPLETED') {
        return await _submitRatingAfterPayment(token, tripId);
      }
    } catch (e) {
      debugPrint('Polling payment status failed: $e');
    }
    return false;
  }

  Future<bool> _submitRatingAfterPayment(String token, int tripId) async {
    if (_isSubmittingFinalRating) {
      return true;
    }

    _isSubmittingFinalRating = true;
    final bookingProvider = context.read<BookingProvider>();
    final comment = _commentController.text.trim();
    final ok = await bookingProvider.submitTripRating(
      token,
      tripId: tripId,
      ratingScore: _rating,
      comment: comment.isEmpty ? null : comment,
    );

    if (!mounted) return true;

    _isSubmittingFinalRating = false;
    if (ok || _isAlreadyRated(bookingProvider)) {
      await _finishAndGoHome();
      return true;
    }

    setState(() {
      _isWaitingForPayment = false;
      _isSubmittingRating = false;
      _canRateLater = (bookingProvider.errorStatusCode ?? 0) >= 500;
    });
    _showSnack(bookingProvider.errorMessage ?? context.l10n.ratingSubmitFailed);
    return true;
  }

  static bool _isAlreadyRated(BookingProvider bookingProvider) {
    return bookingProvider.errorStatusCode == 409;
  }

  @override
  Widget build(BuildContext context) {
    final originalFare = _booking.originalFare ?? _booking.estimatedFare;
    final discount = _booking.discountAmount ?? 0;
    final finalFare = _booking.finalFare ?? (originalFare - discount);

    return PopScope(
      canPop: false,
      onPopInvokedWithResult: (didPop, result) {
        if (didPop) return;
        if (_isWaitingForPayment) {
          _showSnack(context.l10n.waitForPayment);
        } else {
          _showSnack(context.l10n.completeRequirementsBeforeLeaving);
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
                        decoration: BoxDecoration(
                          color: AppColors.primary,
                          shape: BoxShape.circle,
                        ),
                        child: Icon(
                          Icons.check,
                          color: Colors.white,
                          size: 32,
                        ),
                      ),
                      SizedBox(height: 20),
                      Text(
                        context.l10n.tripComplete,
                        style: TextStyle(
                          fontSize: 28,
                          fontWeight: FontWeight.w900,
                          color: Color(0xFF1D2939),
                        ),
                      ),
                      SizedBox(height: 4),
                      Text(
                        context.l10n.thanksForUsingService,
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
                    physics: BouncingScrollPhysics(),
                    padding: const EdgeInsets.symmetric(horizontal: 24),
                    child: Column(
                      children: [
                        Row(
                          children: [
                            Expanded(
                              child: _StatCard(
                                icon: Icons.route_outlined,
                                label: context.l10n.distanceUpper,
                                value:
                                    '${(_booking.actualDistanceKm ?? _booking.estimatedDistanceKm).toStringAsFixed(1)} km',
                              ),
                            ),
                            SizedBox(width: 16),
                            Expanded(
                              child: _StatCard(
                                icon: Icons.access_time,
                                label: context.l10n.durationUpper,
                                value: context.l10n.minutesValue(
                                  _booking.actualDurationMinutes ??
                                      _booking.estimatedDurationMinutes,
                                ),
                              ),
                            ),
                          ],
                        ),
                        SizedBox(height: 24),
                        _PaymentDetails(
                          originalFare: originalFare,
                          discount: discount,
                          finalFare: finalFare,
                          formatCurrency: _formatCurrency,
                        ),
                        SizedBox(height: 24),
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
                        SizedBox(height: 24),
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
                                    : Color(0xFFD0D5DD),
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
                                      : Color(0xFF667085),
                                ),
                                SizedBox(width: 12),
                                Expanded(
                                  child: Text(
                                    context.l10n.confirmVehicleReturned,
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
                        SizedBox(height: 30),
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
                            disabledBackgroundColor: Color(0xFFEAECF0),
                            disabledForegroundColor: Color(0xFF98A2B3),
                            shape: RoundedRectangleBorder(
                              borderRadius: BorderRadius.circular(16),
                            ),
                            elevation: 0,
                          ),
                          child: _isSubmittingRating
                              ? SizedBox(
                                  width: 22,
                                  height: 22,
                                  child: CircularProgressIndicator(
                                    strokeWidth: 2.5,
                                    valueColor: AlwaysStoppedAnimation<Color>(
                                      Colors.white,
                                    ),
                                  ),
                                )
                              : Row(
                                  mainAxisAlignment: MainAxisAlignment.center,
                                  children: [
                                    Text(
                                      context.l10n.sendRatingAndWaitPayment,
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
                        SizedBox(height: 10),
                        SizedBox(
                          width: double.infinity,
                          height: 48,
                          child: OutlinedButton(
                            onPressed: _isSubmittingRating
                                ? null
                                : _finishAndGoHome,
                            style: OutlinedButton.styleFrom(
                              foregroundColor: AppColors.primary,
                              side: BorderSide(color: AppColors.primary),
                              shape: RoundedRectangleBorder(
                                borderRadius: BorderRadius.circular(14),
                              ),
                            ),
                            child: Text(
                              context.l10n.confirmTripRateLater,
                              style: TextStyle(
                                fontWeight: FontWeight.w800,
                              ),
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
                child: _WaitingPaymentPopup(),
              ),
            ),
        ],
      ),
    );
  }

  String _formatCurrency(double value) {
    return NumberFormat.currency(
      locale: LocaleProvider.currentLocale.toLanguageTag(),
      symbol: '₫',
      decimalDigits: 0,
    ).format(value);
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
    throw FormatException('Invalid payment status response.');
  }
}

class _PaymentDetails extends StatelessWidget {
  _PaymentDetails({
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
        color: Color(0xFFF9FAFB),
        borderRadius: BorderRadius.circular(20),
        border: Border.all(color: Color(0xFFEAECF0)),
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
                child: Icon(Icons.receipt_long_outlined, size: 20),
              ),
              SizedBox(width: 12),
              Text(
                context.l10n.paymentDetails,
                style: TextStyle(
                  fontSize: 18,
                  fontWeight: FontWeight.w800,
                  color: Color(0xFF1D2939),
                ),
              ),
            ],
          ),
          Padding(
            padding: EdgeInsets.symmetric(vertical: 12),
            child: Divider(height: 1, color: Color(0xFFEAECF0)),
          ),
          _PriceRow(
            label: context.l10n.baseFare,
            value: formatCurrency(originalFare),
          ),
          SizedBox(height: 12),
          _PriceRow(
            label: context.l10n.promotion,
            value: '-${formatCurrency(discount)}',
            valueColor: AppColors.primary,
          ),
          Padding(
            padding: EdgeInsets.symmetric(vertical: 12),
            child: Divider(height: 1, color: Color(0xFFEAECF0)),
          ),
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text(
                context.l10n.total,
                style: TextStyle(
                  fontSize: 18,
                  fontWeight: FontWeight.w700,
                ),
              ),
              Text(
                formatCurrency(finalFare),
                style: TextStyle(
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
  _RatingCard({
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
        color: Color(0xFFF9FAFB),
        borderRadius: BorderRadius.circular(20),
        border: Border.all(color: Color(0xFFEAECF0)),
      ),
      child: Column(
        children: [
          Text(
            context.l10n.driverRatingQuestion,
            style: TextStyle(
              fontSize: 16,
              fontWeight: FontWeight.w800,
              color: Color(0xFF1D2939),
            ),
          ),
          SizedBox(height: 12),
          Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: List.generate(5, (index) {
              final selected = index < rating;
              return IconButton(
                tooltip: context.l10n.ratingStars(index + 1),
                onPressed: enabled ? () => onRatingChanged(index + 1) : null,
                icon: Icon(
                  selected ? Icons.star_rounded : Icons.star_outline_rounded,
                  color: selected ? Colors.amber : Color(0xFFD0D5DD),
                  size: 38,
                ),
              );
            }),
          ),
          SizedBox(height: 12),
          TextField(
            controller: commentController,
            enabled: enabled,
            decoration: InputDecoration(
              hintText: context.l10n.driverCommentHint,
              hintStyle: TextStyle(
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
  _StatCard({
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
        color: Color(0xFFF9FAFB),
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: Color(0xFFEAECF0)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Icon(icon, size: 16, color: Color(0xFF667085)),
              SizedBox(width: 6),
              Flexible(
                child: Text(
                  label,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(
                    fontSize: 11,
                    fontWeight: FontWeight.w700,
                    color: Color(0xFF667085),
                  ),
                ),
              ),
            ],
          ),
          SizedBox(height: 8),
          RichText(
            text: TextSpan(
              children: [
                TextSpan(
                  text: number,
                  style: TextStyle(
                    fontSize: 22,
                    fontWeight: FontWeight.w900,
                    color: Color(0xFF1D2939),
                  ),
                ),
                TextSpan(
                  text: unit.isEmpty ? '' : ' $unit',
                  style: TextStyle(
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
  _PriceRow({required this.label, required this.value, this.valueColor});

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
                    color: Color(0xFF475467),
                  ),
                ),
              ),
            ],
          ),
        ),
        SizedBox(width: 12),
        Text(
          value,
          style: TextStyle(
            fontSize: 15,
            fontWeight: FontWeight.w700,
            color: valueColor ?? Color(0xFF1D2939),
          ),
        ),
      ],
    );
  }
}

class _WaitingPaymentPopup extends StatefulWidget {
  _WaitingPaymentPopup();

  @override
  State<_WaitingPaymentPopup> createState() => _WaitingPaymentPopupState();
}

class _WaitingPaymentPopupState extends State<_WaitingPaymentPopup>
    with SingleTickerProviderStateMixin {
  late AnimationController _controller;

  @override
  void initState() {
    super.initState();
    _controller = AnimationController(
      vsync: this,
      duration: Duration(seconds: 2),
    )..repeat(reverse: true);
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 32, vertical: 40),
        margin: const EdgeInsets.symmetric(horizontal: 32),
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(32),
          boxShadow: [
            BoxShadow(
              color: Colors.black.withValues(alpha: 0.15),
              blurRadius: 40,
              offset: Offset(0, 10),
            ),
          ],
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            AnimatedBuilder(
              animation: _controller,
              builder: (context, child) {
                return Container(
                  width: 100,
                  height: 100,
                  decoration: BoxDecoration(
                    shape: BoxShape.circle,
                    color: AppColors.primary.withValues(alpha: 0.08),
                    boxShadow: [
                      BoxShadow(
                        color: AppColors.primary.withValues(
                          alpha: 0.25 * _controller.value,
                        ),
                        blurRadius: 30 * _controller.value,
                        spreadRadius: 15 * _controller.value,
                      ),
                    ],
                  ),
                  child: Center(
                    child: Icon(
                      Icons.qr_code_scanner_rounded,
                      color: AppColors.primary,
                      size: 48,
                    ),
                  ),
                );
              },
            ),
            SizedBox(height: 32),
            Text(
              context.l10n.waitingForPayment,
              style: TextStyle(
                fontSize: 22,
                fontWeight: FontWeight.w900,
                color: Color(0xFF1D2939),
              ),
            ),
            SizedBox(height: 12),
            Text(
              context.l10n.paymentWaitingInstructions,
              textAlign: TextAlign.center,
              style: TextStyle(
                fontSize: 15,
                height: 1.5,
                color: Color(0xFF667085),
              ),
            ),
            SizedBox(height: 32),
            SizedBox(
              width: 40,
              height: 40,
              child: CircularProgressIndicator(
                color: AppColors.primary,
                strokeWidth: 3,
              ),
            ),
          ],
        ),
      ),
    );
  }
}
