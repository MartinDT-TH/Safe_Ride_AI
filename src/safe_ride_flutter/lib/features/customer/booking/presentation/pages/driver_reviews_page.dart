import 'package:flutter/material.dart';
import '../../../../../core/constants/app_colors.dart';

class DriverReviewsPage extends StatelessWidget {
  const DriverReviewsPage({super.key});

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
          // Rating Summary Card
          const _RatingSummaryCard(),
          const SizedBox(height: 24),
          // Filters
          const _ReviewFilters(),
          const SizedBox(height: 16),
          // Review List
          const _ReviewList(),
          const SizedBox(height: 32),
        ],
      ),
    );
  }
}

class _RatingSummaryCard extends StatelessWidget {
  const _RatingSummaryCard();

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
                const Text(
                  '4.9',
                  style: TextStyle(
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
                      index < 4 ? Icons.star : Icons.star_half,
                      color: const Color(0xFF9E5425), // Bronze color from image
                      size: 20,
                    );
                  }),
                ),
                const SizedBox(height: 6),
                const Text(
                  '1,248 đánh giá',
                  style: TextStyle(
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
              children: [
                _buildStarLine(5, 0.85),
                _buildStarLine(4, 0.10),
                _buildStarLine(3, 0.03),
                _buildStarLine(2, 0.01),
                _buildStarLine(1, 0.01),
              ],
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
                valueColor: const AlwaysStoppedAnimation<Color>(AppColors.primary),
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
                padding: const EdgeInsets.symmetric(horizontal: 22, vertical: 10),
                decoration: BoxDecoration(
                  color: isSelected ? AppColors.primary : const Color(0xFFF5F5F5),
                  borderRadius: BorderRadius.circular(100),
                  border: Border.all(
                    color: isSelected ? AppColors.primary : const Color(0xFFE2E2E2),
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
    final reviews = [
      const _ReviewItem(
        initial: 'L',
        name: 'Lê T***',
        date: '20/10/2023',
        comment: 'Tài xế lái xe rất cẩn thận và lịch sự. Tôi cảm thấy rất an tâm trong suốt chuyến đi.',
      ),
      const _ReviewItem(
        initial: 'N',
        name: 'Nguyễn V***',
        date: '18/10/2023',
        comment: 'Xe sạch sẽ, thơm. Tài xế nói chuyện rất nhã nhặn. Sẽ tiếp tục đặt xe!',
      ),
      const _ReviewItem(
        initial: 'H',
        name: 'Hoàng A***',
        date: '15/10/2023',
        comment: 'Tài xế đến rất đúng giờ, xe mới và vận hành êm ái.',
      ),
      const _ReviewItem(
        initial: 'P',
        name: 'Phạm M***',
        date: '12/10/2023',
        comment: 'Dịch vụ 5 sao, không có gì để phàn nàn.',
      ),
    ];

    return Column(
      children: reviews.map((review) => _ReviewCard(review: review)).toList(),
    );
  }
}

class _ReviewItem {
  final String initial;
  final String name;
  final String date;
  final String comment;

  const _ReviewItem({
    required this.initial,
    required this.name,
    required this.date,
    required this.comment,
  });
}

class _ReviewCard extends StatelessWidget {
  final _ReviewItem review;
  const _ReviewCard({required this.review});

  @override
  Widget build(BuildContext context) {
    return Container(
      margin: const EdgeInsets.only(bottom: 16),
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: const Color(0xFFF9F9F9), // Lightest grey from image
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
                backgroundColor: const Color(0xFFE0EAEB), // Light teal background for avatar
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
