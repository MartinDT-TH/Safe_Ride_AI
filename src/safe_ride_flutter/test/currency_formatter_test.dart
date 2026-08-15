import 'package:flutter_test/flutter_test.dart';
import 'package:safe_ride/core/utils/currency_formatter.dart';

void main() {
  test('formatVnd uses Vietnamese thousands separators', () {
    expect(formatVnd(120000), '120.000 đ');
    expect(formatVnd(1200000), '1.200.000 đ');
  });

  test('formatVnd rounds fractional VND amounts', () {
    expect(formatVnd(12500.4), '12.500 đ');
    expect(formatVnd(12500.5), '12.501 đ');
  });
}
