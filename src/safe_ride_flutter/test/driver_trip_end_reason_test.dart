import 'package:flutter_test/flutter_test.dart';
import 'package:safe_ride/features/driver/dashboard/presentation/providers/driver_dashboard_provider.dart';

void main() {
  test('driver end reasons serialize to the restricted backend contract', () {
    expect(DriverTripEndReason.values.map((reason) => reason.apiValue), [
      'NORMAL_COMPLETION',
      'CUSTOMER_REQUESTED_STOP',
      'DRIVER_UNABLE_TO_CONTINUE',
      'STARTED_BY_MISTAKE',
    ]);
  });
}
