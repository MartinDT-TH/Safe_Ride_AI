import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import '../../../booking/data/models/promo_model.dart';

class PromoBanner extends StatelessWidget {
  final PromoModel promo;

  const PromoBanner({super.key, required this.promo});

  @override
  Widget build(BuildContext context) {
    final currencyFormatter = NumberFormat.currency(
      locale: 'vi_VN',
      symbol: 'đ',
      decimalDigits: 0,
    );

    String discountText = '';
    if (promo.discountType.toLowerCase() == 'percentage') {
      discountText = 'Giảm ${promo.discountValue.round()}%';
      if (promo.maximumDiscountValue > 0) {
        discountText += ' (Tối đa ${currencyFormatter.format(promo.maximumDiscountValue)})';
      }
    } else {
      discountText = 'Giảm ${currencyFormatter.format(promo.discountValue)}';
    }

    String expiryText = '';
    if (promo.endDate != null) {
      expiryText = 'Hết hạn: ${DateFormat('dd/MM/yyyy').format(promo.endDate!)}';
    }

    String minOrderText = '';
    if (promo.minimumOrderValue > 0) {
      minOrderText = 'Đơn tối thiểu ${currencyFormatter.format(promo.minimumOrderValue)}';
    }

    return Container(
      width: 280,
      height: 160,
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(20),
        image: const DecorationImage(
          image: NetworkImage(
            'https://images.unsplash.com/photo-1503376780353-7e6692767b70?auto=format&fit=crop&q=80&w=800',
          ),
          fit: BoxFit.cover,
        ),
      ),
      child: Container(
        decoration: BoxDecoration(
          borderRadius: BorderRadius.circular(20),
          gradient: LinearGradient(
            begin: Alignment.centerLeft,
            end: Alignment.centerRight,
            colors: [
              const Color(0xFF006B70).withValues(alpha: 0.85),
              const Color(0xFF006B70).withValues(alpha: 0.4),
            ],
          ),
        ),
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
              decoration: BoxDecoration(
                color: Colors.white,
                borderRadius: BorderRadius.circular(6),
              ),
              child: Text(
                promo.promotionCode,
                style: const TextStyle(
                  color: Color(0xFF006B70),
                  fontSize: 12,
                  fontWeight: FontWeight.bold,
                ),
              ),
            ),
            const SizedBox(height: 8),
            Text(
              promo.shortDescription,
              maxLines: 2,
              overflow: TextOverflow.ellipsis,
              style: const TextStyle(
                color: Colors.white,
                fontSize: 18,
                fontWeight: FontWeight.bold,
              ),
            ),
            const SizedBox(height: 4),
            Text(
              discountText,
              style: const TextStyle(
                color: Colors.white,
                fontSize: 13,
                fontWeight: FontWeight.w600,
              ),
            ),
            if (minOrderText.isNotEmpty || expiryText.isNotEmpty) ...[
              const SizedBox(height: 4),
              Text(
                '${minOrderText}${minOrderText.isNotEmpty && expiryText.isNotEmpty ? ' • ' : ''}${expiryText}',
                style: const TextStyle(color: Colors.white70, fontSize: 11),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

