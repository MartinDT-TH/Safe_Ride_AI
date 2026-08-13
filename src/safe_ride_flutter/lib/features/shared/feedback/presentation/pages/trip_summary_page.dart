import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:provider/provider.dart';

import '../../../../../core/constants/app_colors.dart';
import '../../../../../core/localization/locale_provider.dart';
import '../../../../../core/localization/localization_extensions.dart';
import '../../../../../core/session/session_manager.dart';
import '../../../../../dependency_injection/injection.dart';
import '../../../../auth/presentation/providers/auth_provider.dart';
import '../../../../customer/home/presentation/pages/customer_home_page.dart';
import '../../../../customer/home/presentation/providers/home_provider.dart';
import '../../../../customer/booking/data/models/booking_response.dart';
import '../../../../customer/booking/presentation/providers/booking_provider.dart';

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
  int _rating = 5;
  bool _isSubmittingRating = false;
  final TextEditingController _commentController = TextEditingController();

  @override
  void initState() {
    super.initState();
    _booking = widget.booking;
    WidgetsBinding.instance.addPostFrameCallback((_) {
      _refreshBookingSnapshot();
    });
  }

  @override
  void dispose() {
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

  Future<void> _submitRatingAndConfirmReturn() async {
    if (!_vehicleReturned || _isSubmittingRating) return;

    final token = context.read<AuthProvider>().token;
    final tripId = _booking.tripId;
    if (token == null || token.isEmpty || tripId == null) {
      _showSnack(context.l10n.tripInfoUnavailable);
      return;
    }

    setState(() {
      _isSubmittingRating = true;
    });

    final bookingProvider = context.read<BookingProvider>();
    final comment = _commentController.text.trim();
    final confirmedAndRated = await bookingProvider.respondToDriverEndTrip(
      token,
      tripId: tripId,
      accepted: true,
      ratingScore: _rating,
      comment: comment.isEmpty ? null : comment,
    );

    if (!mounted) return;

    if (!confirmedAndRated) {
      if (bookingProvider.errorStatusCode == 409) {
        await _refreshBookingSnapshot(
          bookingProvider: bookingProvider,
          token: token,
        );
        if (mounted && _booking.tripStatus == 'COMPLETED') {
          await _finishAndGoHome();
          return;
        }
      }

      if (!mounted) return;
      setState(() {
        _isSubmittingRating = false;
      });
      _showSnack(
        bookingProvider.errorMessage ?? context.l10n.returnConfirmationFailed,
      );
      return;
    }

    await _finishAndGoHome();
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

  @override
  Widget build(BuildContext context) {
    final originalFare = _booking.originalFare ?? _booking.estimatedFare;
    final discount = _booking.discountAmount ?? 0;
    final finalFare = _booking.finalFare ?? (originalFare - discount);

    return PopScope(
      canPop: false,
      onPopInvokedWithResult: (didPop, result) {
        if (didPop) return;
        _showSnack(context.l10n.completeRequirementsBeforeLeaving);
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
                        child: Icon(Icons.check, color: Colors.white, size: 32),
                      ),
                      SizedBox(height: 20),
                      Text(
                        context.l10n.confirmVehicleReturned,
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
                              ? _submitRatingAndConfirmReturn
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
                                      context.l10n.confirmVehicleReturned,
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
                    ],
                  ),
                ),
              ],
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
                style: TextStyle(fontSize: 18, fontWeight: FontWeight.w700),
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
          SizedBox(height: 18),
          Align(
            alignment: Alignment.centerLeft,
            child: Text(
              context.l10n.driverCommentHint,
              style: TextStyle(
                fontSize: 14,
                fontWeight: FontWeight.w700,
                color: Color(0xFF344054),
              ),
            ),
          ),
          SizedBox(height: 8),
          TextField(
            key: ValueKey('tripRatingCommentField'),
            controller: commentController,
            enabled: enabled,
            decoration: InputDecoration(
              hintText: context.l10n.driverCommentHint,
              hintStyle: TextStyle(fontSize: 14, color: Color(0xFF98A2B3)),
              prefixIcon: Padding(
                padding: const EdgeInsets.only(bottom: 54),
                child: Icon(
                  Icons.chat_bubble_outline_rounded,
                  color: AppColors.primary,
                ),
              ),
              filled: true,
              fillColor: Colors.white,
              border: OutlineInputBorder(
                borderRadius: BorderRadius.circular(12),
                borderSide: BorderSide(color: Color(0xFFD0D5DD)),
              ),
              enabledBorder: OutlineInputBorder(
                borderRadius: BorderRadius.circular(12),
                borderSide: BorderSide(color: Color(0xFFD0D5DD)),
              ),
              focusedBorder: OutlineInputBorder(
                borderRadius: BorderRadius.circular(12),
                borderSide: BorderSide(color: AppColors.primary, width: 1.5),
              ),
              alignLabelWithHint: true,
            ),
            minLines: 3,
            maxLines: 3,
            maxLength: 1000,
            textCapitalization: TextCapitalization.sentences,
          ),
        ],
      ),
    );
  }
}

class _StatCard extends StatelessWidget {
  _StatCard({required this.icon, required this.label, required this.value});

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
