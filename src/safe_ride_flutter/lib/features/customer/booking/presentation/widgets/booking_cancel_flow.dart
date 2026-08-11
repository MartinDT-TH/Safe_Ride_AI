import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../../auth/presentation/providers/auth_provider.dart';
import '../../data/models/booking_response.dart';
import '../providers/booking_provider.dart';
import 'cancel_booking_sheet.dart';

bool isBookingCancellable(BookingResponse? booking) {
  final status = booking?.bookingStatus;
  final tripStatus = booking?.tripStatus;
  if (status == 'PendingSchedule' || status == 'Searching') {
    return true;
  }

  return status == 'DriverAssigned' &&
      (tripStatus == 'ACCEPTED' ||
          tripStatus == 'DRIVER_ARRIVING' ||
          tripStatus == 'ARRIVED');
}

Future<void> handleBookingBack(
  BuildContext context, {
  required BookingResponse? booking,
}) async {
  debugPrint(
    'CANCEL_FLOW: handleBookingBack for booking: ${booking?.bookingId}',
  );

  if (booking == null) {
    debugPrint('CANCEL_FLOW: booking is null, just popping');
    Navigator.of(context).pop();
    return;
  }

  if (!isBookingCancellable(booking)) {
    debugPrint(
      'CANCEL_FLOW: booking ${booking.bookingId} is not cancellable (Status: ${booking.bookingStatus}), going to root',
    );
    Navigator.of(context).popUntil((route) => route.isFirst);
    return;
  }

  final cancelled = await requestBookingCancellation(context, booking: booking);
  if (!cancelled || !context.mounted) {
    return;
  }

  // Navigate back to home (root)
  Navigator.of(context).popUntil((route) => route.isFirst);
}

Future<bool> requestBookingCancellation(
  BuildContext context, {
  required BookingResponse booking,
}) async {
  if (!isBookingCancellable(booking)) {
    _showMessage(context, 'Chuyến đi này không thể hủy ở trạng thái hiện tại.');
    return false;
  }

  final reason = await CancelBookingSheet.show(
    context,
    bookingId: booking.bookingId,
  );

  if (reason == null || !context.mounted) {
    debugPrint(
      'CANCEL_FLOW: Cancellation cancelled by user or context unmounted',
    );
    return false;
  }

  final token = context.read<AuthProvider>().token;
  if (token == null || token.isEmpty) {
    debugPrint('CANCEL_FLOW: No token found for cancellation');
    _showMessage(context, 'Phiên đăng nhập đã hết hạn.');
    return false;
  }

  debugPrint(
    'CANCEL_FLOW: Calling provider.cancelBooking for ${booking.bookingId}',
  );
  final result = await context.read<BookingProvider>().cancelBooking(
    token,
    bookingId: booking.bookingId,
    reason: reason,
  );

  if (!context.mounted) return false;

  if (result == null) {
    final error = context.read<BookingProvider>().errorMessage;
    debugPrint('CANCEL_FLOW: Cancellation failed: $error');
    _showMessage(context, error ?? 'Không thể hủy chuyến. Vui lòng thử lại.');
    return false;
  }

  debugPrint(
    'CANCEL_FLOW: Cancellation success, result status: ${result.bookingStatus}',
  );

  // Ensure we clear searching state
  final provider = context.read<BookingProvider>();
  provider.setSearchingBooking(null);
  provider.clearActiveBooking();

  _showMessage(
    context,
    result.bookingStatus == 'Expired'
        ? 'Chuyến đã hết thời gian chờ và được kết thúc.'
        : booking.bookingType == 'Scheduled'
        ? 'Đã hủy chuyến đặt trước thành công.'
        : 'Đã hủy chuyến thành công.',
  );

  return true;
}

void _showMessage(BuildContext context, String message) {
  ScaffoldMessenger.of(context)
    ..hideCurrentSnackBar()
    ..showSnackBar(SnackBar(content: Text(message)));
}
