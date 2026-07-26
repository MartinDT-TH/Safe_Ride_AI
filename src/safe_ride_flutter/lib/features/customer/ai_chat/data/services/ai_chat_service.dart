import 'package:dio/dio.dart';

import '../../../../../core/constants/app_strings.dart';
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
        if (currentLocation != null)
          'currentLocation': {
            'address': currentLocation.address,
            'latitude': currentLocation.latitude,
            'longitude': currentLocation.longitude,
          },
      },
      options: await _authorizationOptions(),
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

  Future<Options> _authorizationOptions() async {
    final accessToken = await _sessionManager.getValidAccessToken();
    if (accessToken == null) {
      throw StateError('Không tìm thấy phiên đăng nhập hợp lệ.');
    }

    return Options(
      headers: {
        ApiKeys.authorization: AuthHeader.bearer(accessToken),
      },
    );
  }
}
