import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:safe_ride/features/shared/risk_protection/presentation/risk_protection_labels.dart';
import 'package:safe_ride/l10n/generated/app_localizations.dart';

void main() {
  test('main accident labels never expose raw enum values', () {
    for (final locale in const [
      Locale('vi'),
      Locale('en'),
      Locale('ja'),
      Locale('ko'),
      Locale('zh'),
    ]) {
      final l10n = lookupAppLocalizations(locale);
      expect(
        accidentStatusLabel(l10n, 'LIABILITY_PENDING'),
        isNot('LIABILITY_PENDING'),
      );
      expect(
        accidentCategoryLabel(l10n, 'CUSTOMER_VEHICLE_DAMAGE'),
        isNot('CUSTOMER_VEHICLE_DAMAGE'),
      );
      expect(
        driverFaultLevelLabel(l10n, 'GROSS_NEGLIGENCE'),
        isNot('GROSS_NEGLIGENCE'),
      );
      expect(
        claimStatusLabel(l10n, 'PENDING_FUNDING'),
        isNot('PENDING_FUNDING'),
      );
      expect(
        driverLiabilityStatusLabel(l10n, 'PARTIALLY_PAID'),
        isNot('PARTIALLY_PAID'),
      );
      expect(
        safetyReasonLabel(l10n, 'INTERFERING_WITH_VEHICLE'),
        isNot('INTERFERING_WITH_VEHICLE'),
      );
    }
  });

  test('customer intoxication is not a supported presentation reason', () {
    final l10n = lookupAppLocalizations(const Locale('vi'));
    expect(
      safetyReasonLabel(l10n, 'CUSTOMER_INTOXICATION'),
      l10n.riskReasonOther,
    );
  });
}
