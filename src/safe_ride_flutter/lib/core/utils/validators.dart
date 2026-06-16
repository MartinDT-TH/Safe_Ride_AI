import '../constants/app_strings.dart';

abstract final class PhoneNumberValidator {
  static const supportedCountryCodes = [
    '+84',
    '+1',
    '+44',
    '+61',
    '+81',
    '+82',
  ];

  static String normalizePhone(
    String value, {
    String countryCode = AppValues.vietnamCountryCode,
  }) {
    final trimmed = value.trim();
    if (RegExp(r'[^0-9+ ().-]').hasMatch(trimmed) ||
        '+'.allMatches(trimmed).length > 1 ||
        (trimmed.contains('+') && !trimmed.startsWith('+'))) {
      return '';
    }

    final digits = trimmed.replaceAll(RegExp(r'\D'), '');
    if (digits.isEmpty) {
      return '';
    }

    if (trimmed.startsWith('+')) {
      return _normalizeCompleteInternationalNumber(digits);
    }

    if (countryCode == AppValues.vietnamCountryCode) {
      return normalizeVietnamPhone(value);
    }

    final countryDigits = countryCode.replaceAll(RegExp(r'\D'), '');
    if (digits.startsWith(countryDigits)) {
      return _normalizeCompleteInternationalNumber(digits);
    }

    final localDigits = digits.startsWith('0') ? digits.substring(1) : digits;
    return _normalizeCompleteInternationalNumber('$countryDigits$localDigits');
  }

  static bool isValidPhone(
    String value, {
    String countryCode = AppValues.vietnamCountryCode,
  }) {
    return normalizePhone(value, countryCode: countryCode).isNotEmpty;
  }

  static String normalizeVietnamPhone(String value) {
    final digits = value.replaceAll(RegExp(r'\D'), '');

    if (digits.length == 9) {
      return '${AppValues.vietnamCountryCode}$digits';
    }

    if (digits.length == 10 && digits.startsWith('0')) {
      return '${AppValues.vietnamCountryCode}${digits.substring(1)}';
    }

    if (digits.length == 11 && digits.startsWith('84')) {
      return '+$digits';
    }

    return '';
  }

  static bool isValidVietnamPhone(String value) {
    final normalized = normalizeVietnamPhone(value);
    return normalized.length == 12 &&
        normalized.startsWith(AppValues.vietnamCountryCode);
  }

  static String _normalizeCompleteInternationalNumber(String digits) {
    if (digits.length < 9 || digits.length > 15 || digits.startsWith('0')) {
      return '';
    }

    if (digits.startsWith('84') &&
        (digits.length != 11 || digits.startsWith('840'))) {
      return '';
    }

    return '+$digits';
  }
}
