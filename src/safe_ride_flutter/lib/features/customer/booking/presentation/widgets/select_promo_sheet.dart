import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../../../../core/constants/app_colors.dart';
import '../../../../../core/localization/localization_extensions.dart';
import '../../../../../core/localization/locale_provider.dart';
import 'package:intl/intl.dart';
import '../../../../auth/presentation/providers/auth_provider.dart';
import '../providers/booking_provider.dart';
import '../../data/models/promo_model.dart';

class SelectPromoSheet extends StatefulWidget {
  SelectPromoSheet({super.key});

  static Future<PromoModel?> show(BuildContext context) {
    return showModalBottomSheet<PromoModel>(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.transparent,
      builder: (context) => SelectPromoSheet(),
    );
  }

  @override
  State<SelectPromoSheet> createState() => _SelectPromoSheetState();
}

class _SelectPromoSheetState extends State<SelectPromoSheet> {
  final _promoController = TextEditingController();

  @override
  void initState() {
    super.initState();
    _promoController.addListener(() => setState(() {}));
    WidgetsBinding.instance.addPostFrameCallback((_) {
      final token = context.read<AuthProvider>().token;
      if (token != null) {
        context.read<BookingProvider>().loadAvailablePromotions(token);
      }
    });
  }

  @override
  void dispose() {
    _promoController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<BookingProvider>();
    final promos = provider.availablePromotions;
    final isLoading = provider.isLoadingPromotions;
    final manualCode = _promoController.text.trim();

    return SafeArea(
      top: false,
      child: Container(
        height: MediaQuery.of(context).size.height * 0.85,
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.vertical(top: Radius.circular(32)),
        ),
        child: Column(
          children: [
            SizedBox(height: 12),
            // Handle bar
            Container(
              width: 48,
              height: 5,
              decoration: BoxDecoration(
                color: Color(0xFFE2E2E2),
                borderRadius: BorderRadius.circular(10),
              ),
            ),
            SizedBox(height: 16),
            // Header
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 20),
              child: Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Text(
                    context.l10n.selectPromotion,
                    style: TextStyle(
                      fontSize: 20,
                      fontWeight: FontWeight.w800,
                      color: AppColors.textPrimary,
                    ),
                  ),
                  IconButton(
                    onPressed: () => Navigator.pop(context),
                    icon: Container(
                      padding: const EdgeInsets.all(4),
                      decoration: BoxDecoration(
                        color: Color(0xFFF5F5F5),
                        shape: BoxShape.circle,
                      ),
                      child: Icon(
                        Icons.close,
                        size: 20,
                        color: Color(0xFF6B6B6B),
                      ),
                    ),
                  ),
                ],
              ),
            ),
            Divider(height: 1),
            SizedBox(height: 20),
            // Input Section
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 20),
              child: Row(
                children: [
                  Expanded(
                    child: SizedBox(
                      height: 54,
                      child: TextField(
                        controller: _promoController,
                        decoration: InputDecoration(
                          hintText: context.l10n.enterPromoCode,
                          hintStyle: TextStyle(
                            color: Color(0xFFAAAAAA),
                            fontSize: 15,
                          ),
                          filled: true,
                          fillColor: Color(0xFFF7F7F7),
                          contentPadding: const EdgeInsets.symmetric(
                            horizontal: 16,
                          ),
                          border: OutlineInputBorder(
                            borderRadius: BorderRadius.circular(12),
                            borderSide: BorderSide.none,
                          ),
                        ),
                      ),
                    ),
                  ),
                  SizedBox(width: 12),
                  SizedBox(
                    height: 54,
                    child: ElevatedButton(
                      onPressed: manualCode.isEmpty
                          ? null
                          : () {
                              final normalizedCode = manualCode.toUpperCase();
                              final matchingPromo = promos
                                  .where(
                                    (promo) =>
                                        promo.promotionCode.toUpperCase() ==
                                        normalizedCode,
                                  )
                                  .firstOrNull;
                              if (matchingPromo != null &&
                                  !matchingPromo.isUnlocked) {
                                ScaffoldMessenger.of(context).showSnackBar(
                                  SnackBar(
                                    content: Text(
                                      matchingPromo.resolvedUnlockMessage,
                                    ),
                                  ),
                                );
                                return;
                              }
                              if (matchingPromo != null) {
                                provider.selectPromo(matchingPromo);
                                Navigator.pop(context, matchingPromo);
                                return;
                              }
                              final promo = PromoModel(
                                promotionId: -manualCode.hashCode.abs(),
                                promotionCode: normalizedCode,
                                discountType: '',
                                discountValue: 0,
                                remainingUsageCount: 1,
                                shortDescription:
                                    context.l10n.promoValidatedOnBooking,
                              );
                              provider.selectPromo(promo);
                              Navigator.pop(context, promo);
                            },
                      style: ElevatedButton.styleFrom(
                        backgroundColor: AppColors.primary,
                        foregroundColor: Colors.white,
                        elevation: 0,
                        shape: RoundedRectangleBorder(
                          borderRadius: BorderRadius.circular(12),
                        ),
                        padding: const EdgeInsets.symmetric(horizontal: 24),
                      ),
                      child: Text(
                        context.l10n.apply,
                        style: TextStyle(
                          fontWeight: FontWeight.w700,
                          fontSize: 16,
                        ),
                      ),
                    ),
                  ),
                ],
              ),
            ),
            SizedBox(height: 24),
            // List Section
            Expanded(
              child: isLoading
                  ? Center(child: CircularProgressIndicator())
                  : promos.isEmpty
                  ? Center(child: Text(context.l10n.noAvailablePromoCodes))
                  : ListView.separated(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 20,
                        vertical: 8,
                      ),
                      itemCount: promos.length,
                      physics: BouncingScrollPhysics(),
                      separatorBuilder: (context, index) =>
                          SizedBox(height: 16),
                      itemBuilder: (context, index) {
                        return _PromoCard(
                          promo: promos[index],
                          isSelected:
                              provider.selectedPromo?.promotionId ==
                              promos[index].promotionId,
                          onUse: () {
                            if (!promos[index].isUnlocked) {
                              ScaffoldMessenger.of(context).showSnackBar(
                                SnackBar(
                                  content: Text(
                                    promos[index].resolvedUnlockMessage,
                                  ),
                                ),
                              );
                              return;
                            }
                            provider.selectPromo(promos[index]);
                            Navigator.pop(context, promos[index]);
                          },
                        );
                      },
                    ),
            ),
            if (provider.selectedPromo != null)
              Padding(
                padding: const EdgeInsets.all(20),
                child: SizedBox(
                  width: double.infinity,
                  height: 54,
                  child: OutlinedButton(
                    onPressed: () {
                      provider.clearSelectedPromo();
                      Navigator.pop(context);
                    },
                    style: OutlinedButton.styleFrom(
                      foregroundColor: Colors.red,
                      side: BorderSide(color: Colors.red),
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(12),
                      ),
                    ),
                    child: Text(context.l10n.deselectPromo),
                  ),
                ),
              ),
          ],
        ),
      ),
    );
  }
}

class _PromoCard extends StatelessWidget {
  final PromoModel promo;
  final VoidCallback onUse;
  final bool isSelected;

  _PromoCard({
    required this.promo,
    required this.onUse,
    this.isSelected = false,
  });

  @override
  Widget build(BuildContext context) {
    return Opacity(
      opacity: promo.isUnlocked ? 1 : 0.62,
      child: Container(
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(16),
          border: Border.all(
            color: isSelected ? AppColors.primary : Color(0xFFEEEEEE),
            width: 1.5,
          ),
        ),
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              // Voucher Icon
              Container(
                width: 48,
                height: 48,
                decoration: BoxDecoration(
                  color: Color(0xFFE0F2F2),
                  borderRadius: BorderRadius.circular(12),
                ),
                child: Icon(
                  promo.isUnlocked
                      ? Icons.confirmation_num_outlined
                      : Icons.lock_rounded,
                  color: AppColors.primary,
                  size: 24,
                ),
              ),
              SizedBox(width: 16),
              // Info
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Container(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 8,
                        vertical: 4,
                      ),
                      decoration: BoxDecoration(
                        color: Color(0xFFE0F2F2),
                        borderRadius: BorderRadius.circular(6),
                      ),
                      child: Text(
                        promo.promotionCode,
                        style: TextStyle(
                          color: AppColors.primary,
                          fontWeight: FontWeight.w800,
                          fontSize: 12,
                          letterSpacing: 0.5,
                        ),
                      ),
                    ),
                    SizedBox(height: 10),
                    Text(
                      promo.shortDescription,
                      style: TextStyle(
                        fontSize: 15,
                        fontWeight: FontWeight.w700,
                        color: Color(0xFF2D2D2D),
                        height: 1.3,
                      ),
                    ),
                    if (promo.minimumOrderValue > 0) ...[
                      SizedBox(height: 4),
                      Text(
                        context.l10n.minimumOrder(
                          _formatCurrency(promo.minimumOrderValue),
                        ),
                        style: TextStyle(
                          fontSize: 12,
                          color: Color(0xFF888888),
                        ),
                      ),
                    ],
                    if (!promo.isUnlocked) ...[
                      SizedBox(height: 8),
                      Text(
                        'Còn ${promo.remainingTripsToUnlock} chuyến nữa để mở khóa',
                        style: TextStyle(
                          fontSize: 13,
                          fontWeight: FontWeight.w700,
                          color: Color(0xFFB54708),
                        ),
                      ),
                    ],
                    SizedBox(height: 10),
                    Row(
                      children: [
                        Icon(
                          Icons.access_time,
                          size: 14,
                          color: Color(0xFF888888),
                        ),
                        SizedBox(width: 4),
                        Text(
                          promo.remainingUsageCount > 0
                              ? context.l10n.remainingUseCount(
                                  promo.remainingUsageCount,
                                )
                              : context.l10n.usageExhausted,
                          style: TextStyle(
                            fontSize: 13,
                            fontWeight: FontWeight.w600,
                            color: Color(0xFF888888),
                          ),
                        ),
                      ],
                    ),
                  ],
                ),
              ),
              SizedBox(width: 12),
              // Action
              TextButton(
                onPressed: promo.remainingUsageCount > 0 ? onUse : null,
                style: TextButton.styleFrom(
                  foregroundColor: AppColors.primary,
                  padding: const EdgeInsets.symmetric(horizontal: 8),
                  tapTargetSize: MaterialTapTargetSize.shrinkWrap,
                ),
                child: Text(
                  !promo.isUnlocked
                      ? 'Đang khóa'
                      : isSelected
                      ? context.l10n.inUse
                      : context.l10n.useNow,
                  textAlign: TextAlign.center,
                  style: TextStyle(
                    fontWeight: FontWeight.w800,
                    fontSize: 14,
                    height: 1.2,
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  String _formatCurrency(double value) {
    return NumberFormat.currency(
      locale: LocaleProvider.currentLocale.toLanguageTag(),
      symbol: 'VND',
      decimalDigits: 0,
    ).format(value);
  }
}
