const vietnamTimeOffset = Duration(hours: 7);

/// Converts an instant to Vietnam/Thailand wall-clock time (UTC+7).
DateTime toVietnamTime(DateTime value) => value.toUtc().add(vietnamTimeOffset);

/// Parses a UTC timestamp returned by the SafeRide API and converts it to
/// Vietnam/Thailand wall-clock time for presentation.
///
/// SQL Server `datetime2` values created before the API preserved DateTimeKind
/// can arrive without an offset. Those legacy values are UTC by contract, so
/// they must not be interpreted as local wall-clock time.
DateTime? parseApiUtcDateTimeToLocal(Object? value) {
  if (value == null) return null;

  final raw = value.toString().trim();
  if (raw.isEmpty) return null;

  final hasTimeZone = RegExp(r'(?:[zZ]|[+-]\d{2}:\d{2})$').hasMatch(raw);
  final parsed = DateTime.tryParse(hasTimeZone ? raw : '${raw}Z');

  return parsed == null ? null : toVietnamTime(parsed);
}

/// Parses an API UTC timestamp for display in Vietnam/Thailand time (UTC+7).
/// This is intentionally independent of the device timezone.
DateTime? parseApiUtcDateTimeToUtc7(Object? value) {
  if (value == null) return null;

  final raw = value.toString().trim();
  if (raw.isEmpty) return null;

  final hasTimeZone = RegExp(r'(?:[zZ]|[+-]\d{2}:\d{2})$').hasMatch(raw);
  final parsed = DateTime.tryParse(hasTimeZone ? raw : '${raw}Z');

  return parsed == null ? null : toVietnamTime(parsed);
}
