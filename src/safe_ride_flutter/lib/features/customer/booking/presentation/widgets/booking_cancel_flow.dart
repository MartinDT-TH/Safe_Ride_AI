import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../../auth/presentation/providers/auth_provider.dart';
import '../../data/models/booking_response.dart';
import '../providers/booking_provider.dart';
import 'cancel_booking_sheet.dart';

bool isBookingCancellable(BookingResponse? booking) {
  final status = booking?.bookingStatus;
  final tripStatus = booking?.tripStatus;
  if (status == null || status == 'Searching' || status == 'PendingSchedule') {
    return true;
  }

  return status == 'DriverAssigned' &&
      tripStatus != 'IN_PROGRESS' &&
      tripStatus != 'COMPLETED' &&
      tripStatus != 'CANCELLED';
}

Future<void> handleBookingBack(
  BuildContext context, {
  required BookingResponse? booking,
}) async {
  if (!isBookingCancellable(booking)) {
    Navigator.of(context).popUntil((route) => route.isFirst);
    return;
  }

  final reason = await CancelBookingSheet.show(
    context,
    bookingId: booking?.bookingId,
  );
  if (reason == null || !context.mounted) {
    return;
  }

  if (booking == null) {
    Navigator.of(context).popUntil((route) => route.isFirst);
    return;
  }

  final token = context.read<AuthProvider>().token;
  if (token == null || token.isEmpty) {
    _showMessage(context, 'Phiên đăng nhập đã hết hạn.');
    return;
  }

  final result = await context.read<BookingProvider>().cancelBooking(
    token,
    bookingId: booking.bookingId,
    reason: reason,
  );
  if (!context.mounted) {
    return;
  }

  if (result == null) {
    _showMessage(
      context,
      context.read<BookingProvider>().errorMessage ??
          'Không thể hủy chuyến. Vui lòng thử lại.',
    );
    return;
  }

  context.read<BookingProvider>().setSearchingBooking(null);
  context.read<BookingProvider>().clearActiveBooking();
  _showMessage(
    context,
    result.bookingStatus == 'Expired'
        ? 'Chuyến đã hết thời gian chờ và được kết thúc.'
        : 'Đã hủy chuyến thành công.',
  );
  Navigator.of(context).popUntil((route) => route.isFirst);
}

void _showMessage(BuildContext context, String message) {
  ScaffoldMessenger.of(context)
    ..hideCurrentSnackBar()
    ..showSnackBar(SnackBar(content: Text(message)));
}
