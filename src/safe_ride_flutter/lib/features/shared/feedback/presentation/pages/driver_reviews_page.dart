import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:intl/intl.dart';
import '../../../../../core/constants/app_colors.dart';
import '../../../../auth/presentation/providers/auth_provider.dart';
import '../providers/feedback_provider.dart';
import '../../data/models/driver_rating_item.dart';
import '../../data/models/driver_rating_summary.dart';

class DriverReviewsPage extends StatefulWidget {
  const DriverReviewsPage({
    super.key,
    required this.driverId,
    this.driverName,
  });

  final String driverId;
  final String? driverName;

  @override
  State<DriverReviewsPage> createState() => _DriverReviewsPageState();
}

class _DriverReviewsPageState extends State<DriverReviewsPage> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      final token = context.read<AuthProvider>().token;
      context.read<FeedbackProvider>().loadDriverRatings(token, widget.driverId);
    });
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: Colors.white,
      appBar: AppBar(
        backgroundColor: Colors.white,
        elevation: 0,
        leading: IconButton(
          icon: const Icon(Icons.arrow_back, color: AppColors.primary),
          onPressed: () => Navigator.pop(context),
        ),
        title: const Text(
          'Đánh giá tài xế',
          style: TextStyle(
            color: AppColors.primary,
            fontSize: 18,
            fontWeight: FontWeight.w700,
          ),
        ),
        centerTitle: true,
        bottom: PreferredSize(
          preferredSize: const Size.fromHeight(1),
          child: Container(color: const Color(0xFFF0F0F0), height: 1),
        ),
      ),
      body: Consumer<FeedbackProvider>(
        builder: (context, provider, child) {
          if (provider.isLoading) {
            return const Center(child: CircularProgressIndicator());
          }

          if (provider.errorMessage != null) {
            return Center(
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  const Icon(Icons.error_outline, size: 48, color: Colors.red),
                  const SizedBox(height: 16),
                  Text(
                    provider.errorMessage!,
                    style: const TextStyle(color: Color(0xFF6B6B6B)),
                  ),
                  const SizedBox(height: 16),
                  ElevatedButton(
                    onPressed: () {
                      final token = context.read<AuthProvider>().token;
                      provider.loadDriverRatings(token, widget.driverId);
                    },
                    child: const Text('Thử lại'),
                  ),
                ],
              ),
            );
          }

          final summary = provider.driverRatingSummary;
          if (summary == null || summary.totalRatings == 0) {
            return const Center(
              child: Text(
                'Tài xế chưa có đánh giá nào.',
                style: TextStyle(color: Color(0xFF6B6B6B)),
              ),
            );
          }

          return ListView(
            physics: const BouncingScrollPhysics(),
            padding: const EdgeInsets.symmetric(horizontal: 20),
            children: [
              const SizedBox(height: 24),
              _RatingSummaryCard(summary: summary),
              const SizedBox(height: 24),
              const Text(
                'Tất cả nhận xét',
                style: TextStyle(
                  fontSize: 16,
                  fontWeight: FontWeight.w700,
                  color: Color(0xFF1F1F1F),
                ),
              ),
              const SizedBox(height: 16),
              _ReviewList(ratings: summary.ratings),
              const SizedBox(height: 32),
            ],
          );
        },
      ),
    );
  }
}

class _RatingSummaryCard extends StatelessWidget {
  const _RatingSummaryCard({required this.summary});

  final DriverRatingSummary summary;

  @override
  Widget build(BuildContext context) {
    // Calculating percentages for UI
    final Map<int, int> starCounts = {5: 0, 4: 0, 3: 0, 2: 0, 1: 0};
    for (var r in summary.ratings) {
      if (starCounts.containsKey(r.score)) {
        starCounts[r.score] = starCounts[r.score]! + 1;
      }
    }

    return Container(
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: const Color(0xFFF0F0F0)),
        boxShadow: [
          BoxShadow(
            color: Colors.black.withOpacity(0.02),
            blurRadius: 15,
            offset: const Offset(0, 8),
          ),
        ],
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Expanded(
            flex: 4,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                FittedBox(
                  fit: BoxFit.scaleDown,
                  child: Text(
                    summary.averageRating.toStringAsFixed(1),
                    style: const TextStyle(
                      fontSize: 48,
                      fontWeight: FontWeight.w800,
                      color: Color(0xFF1F1F1F),
                      height: 1,
                    ),
                  ),
                ),
                const SizedBox(height: 12),
                FittedBox(
                  fit: BoxFit.scaleDown,
                  child: Row(
                    children: List.generate(5, (index) {
                      final double score = summary.averageRating;
                      if (index < score.floor()) {
                        return const Icon(Icons.star,
                            color: Color(0xFFFFB800), size: 20);
                      } else if (index < score) {
                        return const Icon(Icons.star_half,
                            color: Color(0xFFFFB800), size: 20);
                      } else {
                        return const Icon(Icons.star_outline,
                            color: Color(0xFFFFB800), size: 20);
                      }
                    }),
                  ),
                ),
                const SizedBox(height: 6),
                Text(
                  '${summary.totalRatings} đánh giá',
                  style: const TextStyle(
                    fontSize: 13,
                    color: Color(0xFF6B6B6B),
                    fontWeight: FontWeight.w600,
                  ),
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                ),
              ],
            ),
          ),
          const SizedBox(width: 16),
          Expanded(
            flex: 6,
            child: Column(
              children: [5, 4, 3, 2, 1].map((star) {
                final count = starCounts[star] ?? 0;
                final percent = summary.totalRatings > 0
                    ? count / summary.totalRatings
                    : 0.0;
                return _buildStarLine(star, percent);
              }).toList(),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildStarLine(int star, double percent) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 2.5),
      child: Row(
        children: [
          Text(
            '$star',
            style: const TextStyle(
              fontSize: 12,
              fontWeight: FontWeight.w700,
              color: Color(0xFF1F1F1F),
            ),
          ),
          const SizedBox(width: 8),
          Expanded(
            child: ClipRRect(
              borderRadius: BorderRadius.circular(4),
              child: LinearProgressIndicator(
                value: percent,
                minHeight: 6,
                backgroundColor: const Color(0xFFF0F0F0),
                valueColor:
                    const AlwaysStoppedAnimation<Color>(AppColors.primary),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _ReviewList extends StatelessWidget {
  const _ReviewList({required this.ratings});

  final List<DriverRatingItem> ratings;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: ratings.map((rating) => _ReviewCard(rating: rating)).toList(),
    );
  }
}

class _ReviewCard extends StatelessWidget {
  final DriverRatingItem rating;
  const _ReviewCard({required this.rating});

  @override
  Widget build(BuildContext context) {
    final dateStr = DateFormat('dd/MM/yyyy').format(rating.createdAt);
    final initial = rating.customerName.isNotEmpty
        ? rating.customerName[0].toUpperCase()
        : '?';

    return Container(
      margin: const EdgeInsets.only(bottom: 16),
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: const Color(0xFFF9F9F9),
        borderRadius: BorderRadius.circular(16),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              CircleAvatar(
                radius: 22,
                backgroundColor: const Color(0xFFE0EAEB),
                backgroundImage: rating.customerAvatarUrl != null
                    ? NetworkImage(rating.customerAvatarUrl!)
                    : null,
                child: rating.customerAvatarUrl == null
                    ? Text(
                        initial,
                        style: const TextStyle(
                          color: AppColors.primary,
                          fontWeight: FontWeight.w800,
                          fontSize: 16,
                        ),
                      )
                    : null,
              ),
              const SizedBox(width: 14),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      rating.customerName,
                      style: const TextStyle(
                        fontSize: 15,
                        fontWeight: FontWeight.w800,
                        color: Color(0xFF1F1F1F),
                      ),
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                    ),
                    const SizedBox(height: 2),
                    Text(
                      dateStr,
                      style: const TextStyle(
                        fontSize: 11,
                        color: Color(0xFF6B6B6B),
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                    const SizedBox(height: 4),
                    Row(
                      children: List.generate(5, (index) {
                        return Icon(
                          index < rating.score ? Icons.star : Icons.star_border,
                          color: const Color(0xFFFFB800),
                          size: 16,
                        );
                      }),
                    ),
                  ],
                ),
              ),
            ],
          ),
          if (rating.comment != null && rating.comment!.isNotEmpty) ...[
            const SizedBox(height: 14),
            Text(
              rating.comment!,
              style: const TextStyle(
                fontSize: 14,
                color: Color(0xFF4A4A4A),
                height: 1.5,
                fontWeight: FontWeight.w500,
              ),
            ),
          ],
        ],
      ),
    );
  }
}
