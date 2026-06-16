import 'package:flutter_test/flutter_test.dart';
import 'package:safe_ride/core/network/auth_header.dart';

void main() {
  const jwt = 'header.payload.signature';

  test('bearer builds authorization header from raw JWT', () {
    expect(AuthHeader.bearer(jwt), 'Bearer $jwt');
  });

  test('bearer accepts an already prefixed token', () {
    expect(AuthHeader.bearer('Bearer $jwt'), 'Bearer $jwt');
  });

  test('bearer rejects malformed token values', () {
    expect(() => AuthHeader.bearer('Bearer'), throwsFormatException);
    expect(() => AuthHeader.bearer('not-a-jwt'), throwsFormatException);
  });
}
