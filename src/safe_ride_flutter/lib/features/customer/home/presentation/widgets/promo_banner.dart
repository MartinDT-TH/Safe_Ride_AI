import 'package:flutter/material.dart';
import '../../../../../core/localization/localization_extensions.dart';
import 'package:intl/intl.dart';
import '../../../booking/data/models/promo_model.dart';

class PromoBanner extends StatelessWidget {
  final PromoModel promo;

  PromoBanner({super.key, required this.promo});

  @override
  Widget build(BuildContext context) {
    final currencyFormatter = NumberFormat.currency(
      locale: Localizations.localeOf(context).toLanguageTag(),
      symbol: '₫',
      decimalDigits: 0,
    );

    String discountText = '';
    if (promo.discountType.toLowerCase() == 'percentage') {
      discountText = context.l10n.percentDiscount(promo.discountValue.round());
      if (promo.maximumDiscountValue > 0) {
        discountText += context.l10n.maximumDiscount(
          currencyFormatter.format(promo.maximumDiscountValue),
        );
      }
    } else {
      discountText = context.l10n.fixedDiscount(
        currencyFormatter.format(promo.discountValue),
      );
    }

    String expiryText = '';
    if (promo.endDate != null) {
      expiryText = context.l10n.expiresOn(
        DateFormat.yMd(
          Localizations.localeOf(context).toLanguageTag(),
        ).format(promo.endDate!),
      );
    }

    String minOrderText = '';
    if (promo.minimumOrderValue > 0) {
      minOrderText = context.l10n.minimumOrderShort(
        currencyFormatter.format(promo.minimumOrderValue),
      );
    }

    final cardWidth = (MediaQuery.sizeOf(context).width - 40)
        .clamp(240.0, 384.0)
        .toDouble();

    return Opacity(
      opacity: promo.isUnlocked ? 1 : 0.72,
      child: Container(
        width: cardWidth,
        height: 144,
        decoration: BoxDecoration(
          borderRadius: BorderRadius.circular(16),
          boxShadow: [
            BoxShadow(
              color: Colors.black.withValues(alpha: 0.16),
              blurRadius: 12,
              offset: Offset(0, 5),
            ),
          ],
          image: DecorationImage(
            image: NetworkImage(
              'https://images.unsplash.com/photo-1503376780353-7e6692767b70?auto=format&fit=crop&q=80&w=800',
            ),
            fit: BoxFit.cover,
          ),
        ),
        child: Container(
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(16),
            gradient: LinearGradient(
              begin: Alignment.bottomCenter,
              end: Alignment.topCenter,
              colors: [
                Color(0xFF042F2E).withValues(alpha: 0.92),
                Color(0xFF134E4A).withValues(alpha: 0.65),
                Color(0xFF134E4A).withValues(alpha: 0.40),
              ],
            ),
          ),
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Flexible(
                    child: Container(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 8,
                        vertical: 4,
                      ),
                      decoration: BoxDecoration(
                        color: Colors.white,
                        borderRadius: BorderRadius.circular(8),
                      ),
                      child: Text(
                        promo.promotionCode,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: TextStyle(
                          color: Color(0xFF006B70),
                          fontSize: 12,
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                    ),
                  ),
                  if (!promo.isUnlocked) ...[
                    Spacer(),
                    Container(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 8,
                        vertical: 4,
                      ),
                      decoration: BoxDecoration(
                        color: Colors.black.withValues(alpha: 0.58),
                        borderRadius: BorderRadius.circular(8),
                      ),
                      child: Row(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Icon(Icons.lock, color: Colors.white, size: 13),
                          SizedBox(width: 4),
                          Text(
                            'Đang khóa',
                            style: TextStyle(
                              color: Colors.white,
                              fontSize: 11,
                              fontWeight: FontWeight.w700,
                            ),
                          ),
                        ],
                      ),
                    ),
                  ],
                ],
              ),
              SizedBox(height: 10),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      discountText,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(
                        color: Colors.white,
                        fontSize: 18,
                        fontWeight: FontWeight.bold,
                        height: 1.1,
                      ),
                    ),
                    const Spacer(),
                    if (!promo.isUnlocked)
                      Text(
                        'Còn ${promo.remainingTripsToUnlock} chuyến nữa để mở khóa',
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: TextStyle(
                          color: Colors.white,
                          fontSize: 11,
                          fontWeight: FontWeight.w700,
                        ),
                      )
                    else if (minOrderText.isNotEmpty || expiryText.isNotEmpty)
                      Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          if (minOrderText.isNotEmpty)
                            Text(
                              minOrderText,
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                              style: const TextStyle(
                                color: Colors.white70,
                                fontSize: 11,
                                height: 1.2,
                              ),
                            ),
                          if (minOrderText.isNotEmpty && expiryText.isNotEmpty)
                            const SizedBox(height: 2),
                          if (expiryText.isNotEmpty)
                            Text(
                              expiryText,
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                              style: const TextStyle(
                                color: Colors.white70,
                                fontSize: 11,
                                height: 1.2,
                              ),
                            ),
                        ],
                      ),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
