import 'package:flutter_test/flutter_test.dart';
import 'package:safe_ride/core/maps/polyline_decoder.dart';

void main() {
  test('decodes Google encoded polyline', () {
    final points = decodePolyline('_p~iF~ps|U_ulLnnqC_mqNvxq`@');

    expect(points, hasLength(3));
    expect(points[0].latitude, closeTo(38.5, 0.00001));
    expect(points[0].longitude, closeTo(-120.2, 0.00001));
    expect(points[2].latitude, closeTo(43.252, 0.00001));
    expect(points[2].longitude, closeTo(-126.453, 0.00001));
  });

  test('rejects incomplete encoded polyline', () {
    expect(() => decodePolyline('_p~iF'), throwsFormatException);
  });
}
