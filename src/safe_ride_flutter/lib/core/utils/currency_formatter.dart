import 'package:intl/intl.dart';

final NumberFormat _vndNumberFormat = NumberFormat.decimalPattern('vi_VN')
  ..minimumFractionDigits = 0
  ..maximumFractionDigits = 0;

/// Formats a VND amount with Vietnamese thousands separators.
String formatVnd(num amount) => '${_vndNumberFormat.format(amount)} đ';
