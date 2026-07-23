import 'package:dio/dio.dart';

import '../../../../../core/constants/app_strings.dart';
import '../../../../../core/network/auth_header.dart';
import '../../../../../core/network/dio_client.dart';
import '../models/system_notification_item.dart';
import '../models/system_notifications_page.dart';

class NotificationRemoteDatasource {
  NotificationRemoteDatasource({Dio? dio}) : _dio = dio ?? DioClient().dio;

  static const _loadErrorMessage =
      'Không thể tải danh sách thông báo. Vui lòng thử lại.';
  static const _markReadErrorMessage =
      'Không thể cập nhật trạng thái đã đọc.';

  final Dio _dio;

  Future<SystemNotificationsPage> getNotifications(
    String accessToken, {
    int page = 1,
    int pageSize = 20,
  }) async {
    try {
      final response = await _dio.get(
        ApiEndpoints.notifications,
        queryParameters: {
          'page': page,
          'pageSize': pageSize,
        },
        options: Options(
          headers: {
            ApiKeys.authorization: AuthHeader.bearer(accessToken),
          },
        ),
      );

      return SystemNotificationsPage.fromJson(
        Map<String, dynamic>.from(response.data as Map),
      );
    } on DioException catch (exception) {
      throw NotificationApiException(_readErrorMessage(
        exception,
        _loadErrorMessage,
      ));
    }
  }

  Future<SystemNotificationItem> markAsRead(
    String accessToken,
    int notificationId,
  ) async {
    try {
      final response = await _dio.patch(
        ApiEndpoints.notificationRead(notificationId),
        options: Options(
          headers: {
            ApiKeys.authorization: AuthHeader.bearer(accessToken),
          },
        ),
      );

      return SystemNotificationItem.fromJson(
        Map<String, dynamic>.from(response.data as Map),
      );
    } on DioException catch (exception) {
      throw NotificationApiException(_readErrorMessage(
        exception,
        _markReadErrorMessage,
      ));
    }
  }

  String _readErrorMessage(DioException exception, String fallbackMessage) {
    final data = exception.response?.data;
    if (data is Map) {
      if (data[ApiKeys.detail] != null) {
        return data[ApiKeys.detail].toString();
      }
      if (data[ApiKeys.message] != null) {
        return data[ApiKeys.message].toString();
      }
      if (data['title'] != null) {
        return data['title'].toString();
      }
    }

    return fallbackMessage;
  }
}

class NotificationApiException implements Exception {
  const NotificationApiException(this.message);

  final String message;

  @override
  String toString() => message;
}
