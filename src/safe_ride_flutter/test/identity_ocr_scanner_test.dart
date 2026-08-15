import 'package:flutter_test/flutter_test.dart';
import 'package:safe_ride/features/driver/registration/application/services/identity_ocr_scanner.dart';

void main() {
  final scanner = IdentityOcrScanner();

  group('CCCD QR parser', () {
    test('parses the five-field payload and compact birth date', () {
      final result = scanner.parseCccdQrPayload(
        '079200001234|Nguyen Van An|01012000|Nam|Quan 1, TP Ho Chi Minh',
      );

      expect(result, isNotNull);
      expect(result!.documentNumber, '079200001234');
      expect(result.fullName, 'NGUYEN VAN AN');
      expect(result.dateOfBirth, DateTime(2000, 1, 1));
      expect(result.gender, 'Male');
      expect(result.address, 'Quan 1, TP Ho Chi Minh');
    });

    test('parses the seven-field payload with legacy identity number', () {
      final result = scanner.parseCccdQrPayload(
        '079200001234|201234567|NGUYEN THI BINH|25121995|Nu|Da Nang|01072021',
      );

      expect(result, isNotNull);
      expect(result!.fullName, 'NGUYEN THI BINH');
      expect(result.dateOfBirth, DateTime(1995, 12, 25));
      expect(result.gender, 'Female');
      expect(result.issueDate, DateTime(2021, 7, 1));
    });

    test('rejects an invalid CCCD number or calendar date', () {
      expect(
        scanner.parseCccdQrPayload(
          '123|Nguyen Van An|01012000|Nam|Ha Noi',
        ),
        isNull,
      );
      expect(
        scanner.parseCccdQrPayload(
          '079200001234|Nguyen Van An|31022000|Nam|Ha Noi',
        ),
        isNull,
      );
    });
  });

  group('GPLX QR parser', () {
    test('parses all six fields and compact dates', () {
      final result = scanner.parseDrivingLicenseQrPayload(
        '790012345678;Nguyen Van An;01012000;B2;15062020;15062030',
      );

      expect(result, isNotNull);
      expect(result!.documentNumber, '790012345678');
      expect(result.fullName, 'NGUYEN VAN AN');
      expect(result.dateOfBirth, DateTime(2000, 1, 1));
      expect(result.licenseClass, 'B2');
      expect(result.issueDate, DateTime(2020, 6, 15));
      expect(result.expiryDate, DateTime(2030, 6, 15));
    });

    test('accepts slash-separated dates', () {
      final result = scanner.parseDrivingLicenseQrPayload(
        '790012345678;Nguyen Thi Binh;25/12/1995;A1;01/07/2021;01/07/2031',
      );

      expect(result, isNotNull);
      expect(result!.dateOfBirth, DateTime(1995, 12, 25));
      expect(result.licenseClass, 'A1');
    });

    test('accepts a license with no expiry date', () {
      final result = scanner.parseDrivingLicenseQrPayload(
        '790012345678;Nguyen Thi Binh;25121995;A1;01072021;Không thời hạn',
      );

      expect(result, isNotNull);
      expect(result!.hasNoExpiryDate, isTrue);
      expect(result.expiryDate, isNull);
    });

    test('accepts a compact four-field motorcycle license payload', () {
      final result = scanner.parseDrivingLicenseQrPayload(
        '790012345678;Nguyen Thi Binh;25121995;A1',
      );

      expect(result, isNotNull);
      expect(result!.scanMethod, IdentityScanMethod.qr);
      expect(result.licenseClass, 'A1');
      expect(result.hasNoExpiryDate, isTrue);
    });

    test('does not parse the CCCD pipe separator as a GPLX payload', () {
      expect(
        scanner.parseDrivingLicenseQrPayload(
          '790012345678|Nguyen Thi Binh|25121995|A1',
        ),
        isNull,
      );
    });

    test('accepts extra GPLX fields without relying on fixed positions', () {
      final result = scanner.parseDrivingLicenseQrPayload(
        'GPLX;460225004746;Tran La Trinh;01012004;VIET NAM;A1;01012022;Không thời hạn',
      );

      expect(result, isNotNull);
      expect(result!.documentNumber, '460225004746');
      expect(result.fullName, 'TRAN LA TRINH');
      expect(result.dateOfBirth, DateTime(2004, 1, 1));
      expect(result.licenseClass, 'A1');
      expect(result.issueDate, DateTime(2022, 1, 1));
    });

    test('rejects an unsupported class or expiry before issue date', () {
      expect(
        scanner.parseDrivingLicenseQrPayload(
          '790012345678;Nguyen Van An;01012000;C;15062020;15062030',
        ),
        isNull,
      );
      expect(
        scanner.parseDrivingLicenseQrPayload(
          '790012345678;Nguyen Van An;01012000;B2;15062030;15062020',
        ),
        isNull,
      );
    });
  });
}
