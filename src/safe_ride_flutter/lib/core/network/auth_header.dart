import '../constants/app_strings.dart';

class AuthHeader {
  const AuthHeader._();

  static String bearer(String accessToken) {
    final token = normalizeAccessToken(accessToken);
    if (!isCompactJwt(token)) {
      throw const FormatException('Access token is not a valid JWT.');
    }

    return '${ApiKeys.bearer} $token';
  }

  static String normalizeAccessToken(String value) {
    final trimmed = value.trim();
    const prefix = '${ApiKeys.bearer} ';
    if (trimmed.toLowerCase().startsWith(prefix.toLowerCase())) {
      return trimmed.substring(prefix.length).trim();
    }

    return trimmed;
  }

  static bool isCompactJwt(String value) {
    final token = normalizeAccessToken(value);
    final parts = token.split('.');
    return parts.length == 3 && parts.every((part) => part.isNotEmpty);
  }
}

