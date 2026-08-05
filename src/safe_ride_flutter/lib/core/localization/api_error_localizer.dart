import '../../l10n/generated/app_localizations.dart';

abstract final class ApiErrorLocalizer {
  static String translate(
    AppLocalizations l10n, {
    String? code,
    String? fallback,
  }) {
    switch (code) {
      case 'trip.not_found':
        return l10n.tripNotFound;
      case 'auth.session_expired':
      case 'authentication.session_expired':
        return l10n.sessionExpired;
      default:
        return l10n.localeName == 'vi' && fallback?.trim().isNotEmpty == true
            ? fallback!
            : l10n.genericError;
    }
  }
}
