import 'dart:io';
import 'package:dio/dio.dart';
import 'package:http_parser/http_parser.dart';
import 'package:safe_ride/core/constants/app_strings.dart';
import 'package:safe_ride/core/network/auth_header.dart';
import 'package:safe_ride/core/network/dio_client.dart';
import '../models/trip_chat_message_model.dart';

class TripChatRemoteDatasource {
  TripChatRemoteDatasource({Dio? dio}) : _dio = dio ?? DioClient().dio;

  final Dio _dio;

  Future<List<TripChatMessageModel>> getTripChatMessages({
    required String token,
    required int tripId,
    required String currentUserId,
  }) async {
    try {
      final response = await _dio.get(
        '/trips/$tripId/chat/messages',
        options: Options(
          headers: {ApiKeys.authorization: AuthHeader.bearer(token)},
        ),
      );

      final List data = response.data as List;
      return data
          .map((item) => TripChatMessageModel.fromJson(
                Map<String, dynamic>.from(item),
                currentUserId,
              ))
          .toList();
    } catch (e) {
      return [];
    }
  }

  Future<TripChatMessageModel> sendTripChatImage({
    required String token,
    required int tripId,
    required File imageFile,
    required String currentUserId,
  }) async {
    final fileName = imageFile.path.split(Platform.pathSeparator).last;
    final extension = fileName.split('.').last.toLowerCase();
    final contentType = switch (extension) {
      'png' => 'image/png',
      'webp' => 'image/webp',
      _ => 'image/jpeg',
    };

    final formData = FormData.fromMap({
      'image': await MultipartFile.fromFile(
        imageFile.path,
        filename: fileName,
        contentType: MediaType.parse(contentType),
      ),
    });

    final response = await _dio.post(
      '/trips/$tripId/chat/images',
      data: formData,
      options: Options(
        headers: {ApiKeys.authorization: AuthHeader.bearer(token)},
      ),
    );

    return TripChatMessageModel.fromJson(
      Map<String, dynamic>.from(response.data as Map),
      currentUserId,
    );
  }
}
