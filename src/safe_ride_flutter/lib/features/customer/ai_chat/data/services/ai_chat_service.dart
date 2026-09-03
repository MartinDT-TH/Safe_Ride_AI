import 'package:dio/dio.dart';

import '../../../../../core/constants/app_strings.dart';
import '../../../../../core/localization/locale_provider.dart';
import '../../../../../core/network/auth_header.dart';
import '../../../../../core/network/dio_client.dart';
import '../../../../../core/session/session_manager.dart';
import '../../../../../dependency_injection/injection.dart';
import '../../../booking/data/models/booking_location.dart';
import '../models/ai_chat_models.dart';

class AiChatService {
  AiChatService({Dio? dio, SessionManager? sessionManager})
    : _dio = dio ?? DioClient().dio,
      _sessionManager = sessionManager ?? getIt<SessionManager>();

  final Dio _dio;
  final SessionManager _sessionManager;

  static const _messageReceiveTimeout = Duration(seconds: 90);
  static const _audioReceiveTimeout = Duration(minutes: 3);

  Future<List<AiConversation>> getConversations() async {
    final response = await _dio.get<List<dynamic>>(
      'ai-chat/conversations',
      options: await _authorizationOptions(),
    );
    return (response.data ?? [])
        .whereType<Map<String, dynamic>>()
        .map(AiConversation.fromJson)
        .toList();
  }

  Future<AiChatReply> sendMessage({
    required String message,
    String? conversationId,
    BookingLocation? currentLocation,
  }) async {
    final response = await _dio.post<Map<String, dynamic>>(
      'ai-chat/messages',
      data: {
        'message': message,
        'conversationId': conversationId,
        'languageCode': LocaleProvider.currentLocale.languageCode,
        if (currentLocation != null)
          'currentLocation': {
            'address': currentLocation.address,
            'latitude': currentLocation.latitude,
            'longitude': currentLocation.longitude,
          },
      },
      options: await _authorizationOptions(
        receiveTimeout: _messageReceiveTimeout,
      ),
    );
    return AiChatReply.fromJson(response.data!);
  }

  Future<AiChatReply> sendAudio({
    required String filePath,
    String? conversationId,
    BookingLocation? currentLocation,
  }) async {
    final response = await _dio.post<Map<String, dynamic>>(
      'ai-chat/audio',
      data: FormData.fromMap({
        'audio': await MultipartFile.fromFile(
          filePath,
          filename: 'voice-${DateTime.now().millisecondsSinceEpoch}.m4a',
          contentType: DioMediaType.parse('audio/mp4'),
        ),
        if (conversationId != null) 'conversationId': conversationId,
        'languageCode': LocaleProvider.currentLocale.languageCode,
        if (currentLocation != null) ...{
          'currentAddress': currentLocation.address,
          'currentLatitude': currentLocation.latitude.toString(),
          'currentLongitude': currentLocation.longitude.toString(),
        },
      }),
      options: await _authorizationOptions(
        receiveTimeout: _audioReceiveTimeout,
      ),
    );
    return AiChatReply.fromJson(response.data!);
  }

  Future<List<AiChatMessage>> getMessages(String conversationId) async {
    final response = await _dio.get<List<dynamic>>(
      'ai-chat/conversations/$conversationId/messages',
      options: await _authorizationOptions(),
    );
    return (response.data ?? [])
        .whereType<Map<String, dynamic>>()
        .map(AiChatMessage.fromJson)
        .toList();
  }

  Future<void> deleteConversation(String conversationId) async {
    await _dio.delete<void>(
      'ai-chat/conversations/$conversationId',
      options: await _authorizationOptions(),
    );
  }

  Future<Options> _authorizationOptions({Duration? receiveTimeout}) async {
    final accessToken = await _sessionManager.getValidAccessToken();
    if (accessToken == null) {
      throw StateError(LocaleProvider.currentLocalizations.sessionExpired);
    }

    return Options(
      headers: {ApiKeys.authorization: AuthHeader.bearer(accessToken)},
      receiveTimeout: receiveTimeout,
      // AI chat is an optional feature and presents request failures inside its
      // own sheet. A MongoDB/Gemini outage must not mark the whole SafeRide API
      // as unavailable or show the global server-error snackbar on the home page.
      extra: {DioRequestExtras.suppressGlobalErrorSnackBar: true},
    );
  }
}
