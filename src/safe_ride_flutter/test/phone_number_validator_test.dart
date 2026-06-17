import 'package:flutter_test/flutter_test.dart';
import 'package:safe_ride/core/utils/validators.dart';

void main() {
  group('PhoneNumberValidator', () {
    test('normalizes accepted Vietnam phone formats', () {
      const expected = '+84901234567';

      expect(PhoneNumberValidator.normalizePhone('0901234567'), expected);
      expect(PhoneNumberValidator.normalizePhone('901234567'), expected);
      expect(PhoneNumberValidator.normalizePhone('84901234567'), expected);
      expect(PhoneNumberValidator.normalizePhone('+84901234567'), expected);
    });

    test('normalizes selected international country code formats', () {
      const expected = '+14155552671';

      expect(
        PhoneNumberValidator.normalizePhone('4155552671', countryCode: '+1'),
        expected,
      );
      expect(
        PhoneNumberValidator.normalizePhone('+14155552671', countryCode: '+84'),
        expected,
      );
    });

    test('rejects invalid Vietnam phone formats', () {
      expect(PhoneNumberValidator.normalizePhone('12345'), isEmpty);
      expect(PhoneNumberValidator.normalizePhone('01234567890'), isEmpty);
      expect(PhoneNumberValidator.normalizePhone('+840901234567'), isEmpty);
      expect(PhoneNumberValidator.normalizePhone('abc0901234567'), isEmpty);
      expect(PhoneNumberValidator.normalizePhone('090+1234567'), isEmpty);
    });
  });
}
