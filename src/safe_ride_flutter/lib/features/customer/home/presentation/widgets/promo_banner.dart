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

    return Container(
      width: 280,
      height: 176,
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(20),
        image: DecorationImage(
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
              Color(0xFF006B70).withValues(alpha: 0.85),
              Color(0xFF006B70).withValues(alpha: 0.4),
            ],
          ),
        ),
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Container(
              constraints: BoxConstraints(maxWidth: double.infinity),
              padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
              decoration: BoxDecoration(
                color: Colors.white,
                borderRadius: BorderRadius.circular(6),
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
            SizedBox(height: 8),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    promo.shortDescription,
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                    style: TextStyle(
                      color: Colors.white,
                      fontSize: 18,
                      fontWeight: FontWeight.bold,
                      height: 1.15,
                    ),
                  ),
                  SizedBox(height: 4),
                  Text(
                    discountText,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: TextStyle(
                      color: Colors.white,
                      fontSize: 13,
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                  Spacer(),
                  if (minOrderText.isNotEmpty || expiryText.isNotEmpty)
                    Row(
                      children: [
                        if (minOrderText.isNotEmpty)
                          Expanded(
                            child: Text(
                              minOrderText,
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                              style: TextStyle(
                                color: Colors.white70,
                                fontSize: 11,
                              ),
                            ),
                          ),
                        if (minOrderText.isNotEmpty &&
                            expiryText.isNotEmpty) ...[
                          SizedBox(width: 8),
                          Text(
                            '•',
                            style: TextStyle(
                              color: Colors.white70,
                              fontSize: 11,
                            ),
                          ),
                          SizedBox(width: 8),
                        ],
                        if (expiryText.isNotEmpty)
                          Text(
                            expiryText,
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                            style: TextStyle(
                              color: Colors.white70,
                              fontSize: 11,
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
    );
  }
}
