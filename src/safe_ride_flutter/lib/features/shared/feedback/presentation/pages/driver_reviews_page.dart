import 'package:flutter/material.dart';
import 'package:safe_ride/core/constants/app_colors.dart';
import 'package:safe_ride/features/customer/booking/data/models/driver_rating_summary_model.dart';
import 'package:safe_ride/features/customer/booking/data/models/driver_review_model.dart';

class DriverReviewsPage extends StatelessWidget {
  const DriverReviewsPage({
    super.key,
    this.driverName = 'Nguyễn Văn An',
    this.rating = 4.9,
    this.reviewCount = 1248,
  });

  final String driverName;
  final double rating;
  final int reviewCount;

  @override
  Widget build(BuildContext context) {
    final summary = DriverRatingSummaryModel(
      averageRating: rating,
      totalReviews: reviewCount,
      ratingPercentages: const {5: 0.85, 4: 0.10, 3: 0.03, 2: 0.01, 1: 0.01},
    );

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
          'Hồ sơ tài xế',
          style: TextStyle(
            color: AppColors.primary,
            fontSize: 18,
            fontWeight: FontWeight.w700,
          ),
        ),
        centerTitle: true,
        actions: [
          IconButton(
            icon: const Icon(Icons.more_vert, color: Color(0xFF6B6B6B)),
            onPressed: () {},
          ),
        ],
        bottom: PreferredSize(
          preferredSize: const Size.fromHeight(1),
          child: Container(color: const Color(0xFFF0F0F0), height: 1),
        ),
      ),
      body: ListView(
        physics: const BouncingScrollPhysics(),
        padding: const EdgeInsets.symmetric(horizontal: 20),
        children: [
          const SizedBox(height: 24),
          _RatingSummaryCard(summary: summary),
          const SizedBox(height: 24),
          const _ReviewFilters(),
          const SizedBox(height: 16),
          const _ReviewList(),
          const SizedBox(height: 32),
        ],
      ),
    );
  }
}

class _RatingSummaryCard extends StatelessWidget {
  const _RatingSummaryCard({required this.summary});

  final DriverRatingSummaryModel summary;

  @override
  Widget build(BuildContext context) {
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
                Text(
                  summary.averageRating.toStringAsFixed(1),
                  style: const TextStyle(
                    fontSize: 48,
                    fontWeight: FontWeight.w800,
                    color: Color(0xFF1F1F1F),
                    height: 1,
                  ),
                ),
                const SizedBox(height: 12),
                Row(
                  children: List.generate(5, (index) {
                    return Icon(
                      index < summary.averageRating.floor()
                          ? Icons.star
                          : Icons.star_half,
                      color: const Color(0xFF9E5425),
                      size: 20,
                    );
                  }),
                ),
                const SizedBox(height: 6),
                Text(
                  '${summary.totalReviews.toString().replaceAllMapped(RegExp(r"(\d{1,3})(?=(\d{3})+(?!\d))"), (Match m) => "${m[1]},")} đánh giá',
                  style: const TextStyle(
                    fontSize: 13,
                    color: Color(0xFF6B6B6B),
                    fontWeight: FontWeight.w600,
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(width: 20),
          Expanded(
            flex: 6,
            child: Column(
              children: [5, 4, 3, 2, 1].map((star) {
                return _buildStarLine(
                  star,
                  summary.ratingPercentages[star] ?? 0,
                );
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

class _ReviewFilters extends StatefulWidget {
  const _ReviewFilters();

  @override
  State<_ReviewFilters> createState() => _ReviewFiltersState();
}

class _ReviewFiltersState extends State<_ReviewFilters> {
  int _selectedIndex = 0;
  final List<String> _options = ['Tất cả', '5 sao', '4 sao', 'Có bình luận'];

  @override
  Widget build(BuildContext context) {
    return SingleChildScrollView(
      scrollDirection: Axis.horizontal,
      physics: const BouncingScrollPhysics(),
      child: Row(
        children: List.generate(_options.length, (index) {
          final isSelected = _selectedIndex == index;
          return Padding(
            padding: const EdgeInsets.only(right: 10),
            child: InkWell(
              onTap: () => setState(() => _selectedIndex = index),
              borderRadius: BorderRadius.circular(100),
              child: Container(
                padding: const EdgeInsets.symmetric(
                  horizontal: 22,
                  vertical: 10,
                ),
                decoration: BoxDecoration(
                  color:
                      isSelected ? AppColors.primary : const Color(0xFFF5F5F5),
                  borderRadius: BorderRadius.circular(100),
                  border: Border.all(
                    color: isSelected
                        ? AppColors.primary
                        : const Color(0xFFE2E2E2),
                    width: 1,
                  ),
                ),
                child: Text(
                  _options[index],
                  style: TextStyle(
                    fontSize: 14,
                    fontWeight: isSelected ? FontWeight.w700 : FontWeight.w600,
                    color: isSelected ? Colors.white : const Color(0xFF6B6B6B),
                  ),
                ),
              ),
            ),
          );
        }),
      ),
    );
  }
}

class _ReviewList extends StatelessWidget {
  const _ReviewList();

  @override
  Widget build(BuildContext context) {
    const reviews = [
      DriverReviewModel(
        initial: 'L',
        name: 'Lê T***',
        date: '20/10/2023',
        rating: 5,
        comment:
            'Tài xế lái xe rất cẩn thận và lịch sự. Tôi cảm thấy rất an tâm trong suốt chuyến đi.',
      ),
      DriverReviewModel(
        initial: 'N',
        name: 'Nguyễn V***',
        date: '18/10/2023',
        rating: 5,
        comment:
            'Xe sạch sẽ, thơm. Tài xế nói chuyện rất nhã nhặn. Sẽ tiếp tục đặt xe!',
      ),
      DriverReviewModel(
        initial: 'H',
        name: 'Hoàng A***',
        date: '15/10/2023',
        rating: 5,
        comment: 'Tài xế đến rất đúng giờ, xe mới và vận hành êm ái.',
      ),
      DriverReviewModel(
        initial: 'P',
        name: 'Phạm M***',
        date: '12/10/2023',
        rating: 5,
        comment: 'Dịch vụ 5 sao, không có gì để phàn nàn.',
      ),
    ];

    return Column(
      children: reviews.map((review) => _ReviewCard(review: review)).toList(),
    );
  }
}

class _ReviewCard extends StatelessWidget {
  final DriverReviewModel review;
  const _ReviewCard({required this.review});

  @override
  Widget build(BuildContext context) {
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
                child: Text(
                  review.initial,
                  style: const TextStyle(
                    color: AppColors.primary,
                    fontWeight: FontWeight.w800,
                    fontSize: 16,
                  ),
                ),
              ),
              const SizedBox(width: 14),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        Text(
                          review.name,
                          style: const TextStyle(
                            fontSize: 15,
                            fontWeight: FontWeight.w800,
                            color: Color(0xFF1F1F1F),
                          ),
                        ),
                        Text(
                          review.date,
                          style: const TextStyle(
                            fontSize: 11,
                            color: Color(0xFF6B6B6B),
                            fontWeight: FontWeight.w600,
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 3),
                    Row(
                      children: List.generate(5, (index) {
                        return const Icon(
                          Icons.star,
                          color: Color(0xFF9E5425),
                          size: 16,
                        );
                      }),
                    ),
                  ],
                ),
              ),
            ],
          ),
          const SizedBox(height: 14),
          Text(
            review.comment,
            style: const TextStyle(
              fontSize: 14,
              color: Color(0xFF4A4A4A),
              height: 1.5,
              fontWeight: FontWeight.w500,
            ),
          ),
        ],
      ),
    );
  }
}
