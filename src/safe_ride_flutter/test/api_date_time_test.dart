import 'package:flutter_test/flutter_test.dart';
import 'package:safe_ride/core/utils/api_date_time.dart';

void main() {
  group('parseApiUtcDateTimeToUtc7', () {
    test('treats an offset-less API timestamp as UTC', () {
      final result = parseApiUtcDateTimeToUtc7('2026-08-12T10:15:00');

      expect(
        [result?.year, result?.month, result?.day, result?.hour, result?.minute],
        [2026, 8, 12, 17, 15],
      );
    });

    test('normalizes an offset timestamp to UTC+7', () {
      final result = parseApiUtcDateTimeToUtc7(
        '2026-08-12T10:15:00+02:00',
      );

      expect(
        [result?.year, result?.month, result?.day, result?.hour, result?.minute],
        [2026, 8, 12, 15, 15],
      );
    });
  });
}
