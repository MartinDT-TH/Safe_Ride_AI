import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../../../../core/constants/app_colors.dart';
import '../../../../../core/localization/localization_extensions.dart';
import '../../data/models/history_trip.dart';
import './interactive_button.dart';

class TripHistoryCard extends StatelessWidget {
  TripHistoryCard({
    super.key,
    required this.trip,
    this.onRebook,
    this.onReport,
    this.onChat,
    this.onViewFeedback,
    this.unreadChatCount = 0,
  });

  final HistoryTrip trip;
  final VoidCallback? onRebook;
  final VoidCallback? onReport;
  final VoidCallback? onChat;
  final VoidCallback? onViewFeedback;
  final int unreadChatCount;

  @override
  Widget build(BuildContext context) {
    final isCancelled = trip.status == HistoryTripStatus.cancelled;
    final locale = Localizations.localeOf(context).toLanguageTag();
    final dateStr = DateFormat.yMd(locale).add_Hm().format(trip.time);
    final fareStr = trip.fare > 0
        ? NumberFormat.currency(
            locale: locale,
            symbol: '₫',
            decimalDigits: 0,
          ).format(trip.fare)
        : '0 ₫';
    final showFooter =
        isCancelled ||
        trip.driverName != null ||
        onRebook != null ||
        onReport != null ||
        onChat != null ||
        onViewFeedback != null;

    return Container(
      margin: const EdgeInsets.only(bottom: 16),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(24),
        border: Border.all(color: Color(0xFFEEEEEE)),
        boxShadow: [
          BoxShadow(
            color: Colors.black.withOpacity(0.02),
            blurRadius: 10,
            offset: Offset(0, 4),
          ),
        ],
      ),
      child: Padding(
        padding: const EdgeInsets.all(16.0),
        child: Column(
          children: [
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Container(
                  padding: const EdgeInsets.all(10),
                  decoration: BoxDecoration(
                    color: Color(0xFFF5F5F5),
                    shape: BoxShape.circle,
                  ),
                  child: Icon(
                    isCancelled
                        ? Icons.cancel_outlined
                        : (trip.isMotorbike
                              ? Icons.two_wheeler
                              : Icons.directions_car),
                    color: isCancelled ? Colors.grey : AppColors.textSecondary,
                    size: 20,
                  ),
                ),
                SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        dateStr,
                        style: TextStyle(
                          fontSize: 16,
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                      if (unreadChatCount > 0) ...[
                        SizedBox(height: 6),
                        Container(
                          padding: const EdgeInsets.symmetric(
                            horizontal: 8,
                            vertical: 4,
                          ),
                          decoration: BoxDecoration(
                            color: Color(0xFFFFE4E6),
                            borderRadius: BorderRadius.circular(8),
                          ),
                          child: Text(
                            'Tin nhắn mới',
                            style: TextStyle(
                              color: Color(0xFFBE123C),
                              fontSize: 11,
                              fontWeight: FontWeight.w700,
                            ),
                          ),
                        ),
                      ],
                      Text(
                        '${trip.vehicleName} \u2022 ${trip.distanceKm} km',
                        style: TextStyle(color: Colors.grey, fontSize: 13),
                      ),
                    ],
                  ),
                ),
                Text(
                  fareStr,
                  style: TextStyle(
                    fontSize: 18,
                    fontWeight: FontWeight.bold,
                    color: isCancelled ? Colors.grey : AppColors.primary,
                  ),
                ),
              ],
            ),
            SizedBox(height: 16),
            _buildRouteLine(isCancelled),
            if (showFooter) ...[
              SizedBox(height: 16),
              Divider(height: 1, color: Color(0xFFF0F0F0)),
              SizedBox(height: 16),
              Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  if (isCancelled)
                    Text(
                      context.l10n.cancelledByCustomer,
                      style: TextStyle(
                        color: Colors.red,
                        fontSize: 14,
                        fontWeight: FontWeight.w500,
                      ),
                    )
                  else if (trip.driverName != null)
                    Row(
                      children: [
                        CircleAvatar(
                          radius: 20,
                          backgroundImage: trip.driverAvatar != null
                              ? NetworkImage(trip.driverAvatar!)
                              : null,
                          backgroundColor: Color(0xFFE0E0E0),
                          child: trip.driverAvatar == null
                              ? Icon(Icons.person, color: Colors.white)
                              : null,
                        ),
                        SizedBox(width: 10),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(
                                trip.driverName!,
                                style: TextStyle(
                                  fontWeight: FontWeight.bold,
                                  fontSize: 14,
                                ),
                                maxLines: 2,
                                overflow: TextOverflow.ellipsis,
                              ),
                              if (trip.driverRating != null)
                                Row(
                                  children: [
                                    Icon(
                                      Icons.star,
                                      color: Colors.orange,
                                      size: 14,
                                    ),
                                    SizedBox(width: 2),
                                    Text(
                                      trip.driverRating!.toString(),
                                      style: TextStyle(
                                        color: Colors.grey,
                                        fontSize: 12,
                                      ),
                                    ),
                                  ],
                                ),
                            ],
                          ),
                        ),
                      ],
                    ),
                  if (onReport != null ||
                      onRebook != null ||
                      onChat != null ||
                      onViewFeedback != null) ...[
                    SizedBox(height: 12),
                    SizedBox(
                      width: double.infinity,
                      child: Wrap(
                        spacing: 8,
                        runSpacing: 8,
                        alignment: WrapAlignment.end,
                        children: [
                          if (onChat != null)
                            InteractiveButton(
                              onTap: onChat!,
                              borderRadius: BorderRadius.circular(10),
                              child: Container(
                                height: 38,
                                padding: const EdgeInsets.symmetric(
                                  horizontal: 12,
                                ),
                                decoration: BoxDecoration(
                                  color: Colors.white,
                                  borderRadius: BorderRadius.circular(10),
                                  border: Border.all(color: Color(0xFFE0E0E0)),
                                ),
                                child: Row(
                                  mainAxisSize: MainAxisSize.min,
                                  children: [
                                    Stack(
                                      clipBehavior: Clip.none,
                                      children: [
                                        Icon(
                                          Icons.chat_bubble_outline_rounded,
                                          size: 16,
                                          color: Color(0xFF626A6C),
                                        ),
                                        if (unreadChatCount > 0)
                                          Positioned(
                                            top: -4,
                                            right: -4,
                                            child: Container(
                                              width: 7,
                                              height: 7,
                                              decoration: BoxDecoration(
                                                color: Color(0xFFE11D48),
                                                shape: BoxShape.circle,
                                              ),
                                            ),
                                          ),
                                      ],
                                    ),
                                    SizedBox(width: 6),
                                    Text(
                                      context.l10n.chat,
                                      style: TextStyle(
                                        fontSize: 13,
                                        fontWeight: FontWeight.bold,
                                        color: Color(0xFF626A6C),
                                      ),
                                    ),
                                  ],
                                ),
                              ),
                            ),
                          if (onViewFeedback != null)
                            InteractiveButton(
                              onTap: onViewFeedback!,
                              borderRadius: BorderRadius.circular(10),
                              child: Container(
                                height: 38,
                                padding: const EdgeInsets.symmetric(
                                  horizontal: 12,
                                ),
                                decoration: BoxDecoration(
                                  color: Colors.white,
                                  borderRadius: BorderRadius.circular(10),
                                  border: Border.all(color: Color(0xFFE0E0E0)),
                                ),
                                child: Row(
                                  mainAxisSize: MainAxisSize.min,
                                  children: [
                                    Icon(
                                      Icons.star_outline_rounded,
                                      size: 16,
                                      color: Color(0xFF626A6C),
                                    ),
                                    SizedBox(width: 6),
                                    Text(
                                      context.l10n.viewReviews,
                                      style: TextStyle(
                                        fontSize: 13,
                                        fontWeight: FontWeight.bold,
                                        color: Color(0xFF626A6C),
                                      ),
                                    ),
                                  ],
                                ),
                              ),
                            ),
                          if (onReport != null)
                            InteractiveButton(
                              onTap: trip.hasReported ? () {} : onReport!,
                              borderRadius: BorderRadius.circular(10),
                              child: Container(
                                height: 38,
                                padding: const EdgeInsets.symmetric(
                                  horizontal: 12,
                                ),
                                decoration: BoxDecoration(
                                  color: trip.hasReported
                                      ? Color(0xFFF2F4F7)
                                      : Colors.white,
                                  borderRadius: BorderRadius.circular(10),
                                  border: Border.all(
                                    color: trip.hasReported
                                        ? Colors.transparent
                                        : Color(0xFFE0E0E0),
                                  ),
                                ),
                                child: Row(
                                  mainAxisSize: MainAxisSize.min,
                                  children: [
                                    Icon(
                                      trip.hasReported
                                          ? Icons.check_circle_outline
                                          : Icons.report_outlined,
                                      size: 16,
                                      color: trip.hasReported
                                          ? Color(0xFF98A2B3)
                                          : Color(0xFF626A6C),
                                    ),
                                    SizedBox(width: 6),
                                    Text(
                                      trip.hasReported
                                          ? context.l10n.reported
                                          : context.l10n.report,
                                      style: TextStyle(
                                        fontSize: 13,
                                        fontWeight: FontWeight.bold,
                                        color: trip.hasReported
                                            ? Color(0xFF98A2B3)
                                            : Color(0xFF626A6C),
                                      ),
                                    ),
                                  ],
                                ),
                              ),
                            ),
                          if (onRebook != null)
                            InteractiveButton(
                              onTap: onRebook!,
                              borderRadius: BorderRadius.circular(10),
                              child: Container(
                                height: 38,
                                padding: const EdgeInsets.symmetric(
                                  horizontal: 16,
                                ),
                                decoration: BoxDecoration(
                                  color: Color(0xFFE8ECEF),
                                  borderRadius: BorderRadius.circular(10),
                                ),
                                child: Center(
                                  child: Text(
                                    context.l10n.rebook,
                                    style: TextStyle(
                                      fontSize: 13,
                                      fontWeight: FontWeight.bold,
                                      color: Color(0xFF626A6C),
                                    ),
                                  ),
                                ),
                              ),
                            ),
                        ],
                      ),
                    ),
                  ],
                ],
              ),
            ],
          ],
        ),
      ),
    );
  }

  Widget _buildRouteLine(bool isCancelled) {
    return Column(
      children: [
        Row(
          children: [
            _buildDot(AppColors.primary, isCancelled),
            SizedBox(width: 12),
            Expanded(
              child: Text(
                trip.pickup,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(
                  color: isCancelled ? Colors.grey : AppColors.textPrimary,
                  fontSize: 15,
                ),
              ),
            ),
          ],
        ),
        Padding(
          padding: const EdgeInsets.only(left: 5),
          child: Align(
            alignment: Alignment.centerLeft,
            child: Container(
              width: 1,
              height: 20,
              decoration: BoxDecoration(color: Colors.grey.withOpacity(0.3)),
            ),
          ),
        ),
        Row(
          children: [
            _buildDot(Colors.red, isCancelled),
            SizedBox(width: 12),
            Expanded(
              child: Text(
                trip.destination,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(
                  color: isCancelled ? Colors.grey : AppColors.textPrimary,
                  fontSize: 15,
                ),
              ),
            ),
          ],
        ),
      ],
    );
  }

  Widget _buildDot(Color color, bool isCancelled) {
    return Container(
      width: 10,
      height: 10,
      decoration: BoxDecoration(
        color: isCancelled ? Colors.grey.shade400 : color,
        shape: BoxShape.circle,
        border: Border.all(color: Colors.white, width: 2),
      ),
    );
  }
}
