import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:safe_ride/features/customer/booking/data/models/booking_response.dart';
import 'package:safe_ride/features/shared/history/presentation/pages/trip_details_page.dart';

void main() {
  test(
    'cancel action is limited to cancellable scheduled customer bookings',
    () {
      final scheduled = _booking(
        bookingType: 'Scheduled',
        bookingStatus: 'PendingSchedule',
      );
      final now = _booking(bookingType: 'Now', bookingStatus: 'Searching');
      final completed = _booking(
        bookingType: 'Scheduled',
        bookingStatus: 'Completed',
      );

      expect(
        shouldShowHistoryScheduledBookingCancel(
          allowedForRole: true,
          booking: scheduled,
        ),
        isTrue,
      );
      expect(
        shouldShowHistoryScheduledBookingCancel(
          allowedForRole: true,
          booking: now,
        ),
        isFalse,
      );
      expect(
        shouldShowHistoryScheduledBookingCancel(
          allowedForRole: true,
          booking: completed,
        ),
        isFalse,
      );
      expect(
        shouldShowHistoryScheduledBookingCancel(
          allowedForRole: false,
          booking: scheduled,
        ),
        isFalse,
      );
    },
  );

  testWidgets('history details renders scheduled booking cancel action', (
    tester,
  ) async {
    var cancelTapped = false;

    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: HistoryScheduledBookingCancelButton(
            onCancel: () async => cancelTapped = true,
          ),
        ),
      ),
    );

    expect(find.text('Hủy chuyến đặt trước'), findsOneWidget);
    await tester.tap(find.byKey(HistoryScheduledBookingCancelButton.buttonKey));
    await tester.pump();

    expect(cancelTapped, isTrue);
  });
}

BookingResponse _booking({
  required String bookingType,
  required String bookingStatus,
}) {
  return BookingResponse(
    bookingId: 42,
    bookingType: bookingType,
    bookingStatus: bookingStatus,
    estimatedDistanceKm: 5,
    estimatedDurationMinutes: 20,
    estimatedFare: 50000,
    encodedPolyline: '',
    message: 'OK',
  );
}
