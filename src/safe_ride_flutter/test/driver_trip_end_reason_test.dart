import 'package:flutter_test/flutter_test.dart';
import 'package:safe_ride/features/driver/dashboard/presentation/providers/driver_dashboard_provider.dart';

void main() {
  test('driver end choices keep trip reason separate from availability', () {
    expect(DriverTripEndReason.values.map((reason) => reason.apiValue), [
      'NORMAL_COMPLETION',
      'CUSTOMER_REQUESTED_STOP',
      'DRIVER_UNABLE_TO_CONTINUE',
      'STARTED_BY_MISTAKE',
    ]);
  });

  test('driver unable reason always forces the driver offline', () {
    expect(
      DriverTripEndReason.driverUnableToContinue.effectiveCanContinueWorking(
        true,
      ),
      isFalse,
    );
    expect(
      DriverTripEndReason.customerRequestedStop.effectiveCanContinueWorking(
        true,
      ),
      isTrue,
    );
  });
}
