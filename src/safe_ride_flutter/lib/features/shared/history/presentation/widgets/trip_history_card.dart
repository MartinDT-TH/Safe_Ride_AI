import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import '../../../../core/constants/app_colors.dart';
import '../../../../core/constants/app_strings.dart';
import '../../data/models/history_trip.dart';
import './interactive_button.dart';

class TripHistoryCard extends StatelessWidget {
  const TripHistoryCard({super.key, required this.trip, required this.onRebook});

  final HistoryTrip trip;
  final VoidCallback onRebook;

  @override
  Widget build(BuildContext context) {
    final isCancelled = trip.status == HistoryTripStatus.cancelled;
    final dateStr = DateFormat('HH:mm, d ThMM', 'vi').format(trip.time);
    final fareStr = trip.fare > 0 
        ? NumberFormat.currency(locale: 'vi_VN', symbol: 'đ', decimalDigits: 0).format(trip.fare)
        : '0đ';

    return Container(
      margin: const EdgeInsets.only(bottom: 16),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(24),
        border: Border.all(color: const Color(0xFFEEEEEE)),
        boxShadow: [
          BoxShadow(
            color: Colors.black.withOpacity(0.02),
            blurRadius: 10,
            offset: const Offset(0, 4),
          ),
        ],
      ),
      child: Padding(
        padding: const EdgeInsets.all(16.0),
        child: Column(
          children: [
            // Header: Icon, Time, Price
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Container(
                  padding: const EdgeInsets.all(10),
                  decoration: BoxDecoration(
                    color: const Color(0xFFF5F5F5),
                    shape: BoxShape.circle,
                  ),
                  child: Icon(
                    isCancelled ? Icons.cancel_outlined : (trip.isMotorbike ? Icons.two_wheeler : Icons.directions_car),
                    color: isCancelled ? Colors.grey : AppColors.textSecondary,
                    size: 20,
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        dateStr,
                        style: const TextStyle(
                          fontSize: 16,
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                      Text(
                        '${trip.vehicleName} • ${trip.distanceKm} km',
                        style: const TextStyle(
                          color: Colors.grey,
                          fontSize: 13,
                        ),
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
            const SizedBox(height: 16),

            // Route
            _buildRouteLine(isCancelled),
            
            const SizedBox(height: 16),
            const Divider(height: 1, color: Color(0xFFF0F0F0)),
            const SizedBox(height: 16),

            // Driver & Rebook Button
            Row(
              children: [
                if (isCancelled) ...[
                  Expanded(
                    child: Text(
                      HistoryStrings.cancelledByCustomer,
                      style: const TextStyle(color: Colors.red, fontSize: 14),
                    ),
                  ),
                ] else if (trip.driverName != null) ...[
                  CircleAvatar(
                    radius: 20,
                    backgroundImage: trip.driverAvatar != null ? NetworkImage(trip.driverAvatar!) : null,
                    backgroundColor: const Color(0xFFE0E0E0),
                    child: trip.driverAvatar == null ? const Icon(Icons.person, color: Colors.white) : null,
                  ),
                  const SizedBox(width: 10),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          trip.driverName!,
                          style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 14),
                        ),
                        Row(
                          children: [
                            const Icon(Icons.star, color: Colors.orange, size: 14),
                            const SizedBox(width: 2),
                            Text(
                              trip.driverRating?.toString() ?? '5.0',
                              style: const TextStyle(color: Colors.grey, fontSize: 12),
                            ),
                          ],
                        ),
                      ],
                    ),
                  ),
                ],
                
                InteractiveButton(
                  onTap: onRebook,
                  borderRadius: BorderRadius.circular(12),
                  child: Container(
                    padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
                    decoration: BoxDecoration(
                      color: const Color(0xFFE8ECEF),
                      borderRadius: BorderRadius.circular(12),
                    ),
                    child: const Text(
                      HistoryStrings.rebook,
                      style: TextStyle(
                        fontWeight: FontWeight.bold,
                        color: Color(0xFF626A6C),
                      ),
                    ),
                  ),
                ),
              ],
            ),
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
            const SizedBox(width: 12),
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
              decoration: BoxDecoration(
                color: Colors.grey.withOpacity(0.3),
              ),
            ),
          ),
        ),
        Row(
          children: [
            _buildDot(Colors.red, isCancelled),
            const SizedBox(width: 12),
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
