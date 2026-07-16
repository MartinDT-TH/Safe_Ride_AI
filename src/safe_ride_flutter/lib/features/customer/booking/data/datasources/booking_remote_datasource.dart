import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';

import '../../../../../core/constants/app_strings.dart';
import '../../../../../core/network/auth_header.dart';
import '../../../../../core/network/dio_client.dart';
import '../models/promo_model.dart';
import '../models/booking_response.dart';
import '../models/booking_fare_estimate.dart';
import '../models/booking_location.dart';
import '../models/create_booking_request.dart';
import '../models/nearby_driver.dart';

class BookingRemoteDatasource {
  BookingRemoteDatasource({Dio? dio}) : _dio = dio ?? DioClient().dio;

  final Dio _dio;

  Future<List<PromoModel>> getAvailablePromotions(String accessToken) async {
    try {
      final response = await _dio.get(
        ApiEndpoints.availablePromotions,
        options: Options(
          headers: {ApiKeys.authorization: AuthHeader.bearer(accessToken)},
        ),
      );

      final List data = (response.data is List)
          ? response.data
          : (response.data is Map && response.data['data'] is List)
          ? response.data['data']
          : [];

      return data
          .map((item) => PromoModel.fromJson(Map<String, dynamic>.from(item)))
          .toList();
    } on FormatException {
      throw const BookingApiException(BookingStrings.sessionExpired);
    } on DioException catch (exception) {
      final data = exception.response?.data;
      if (data is Map) {
        final detail = data[ApiKeys.detail]?.toString();
        final code = data['code']?.toString();
        if (detail != null) {
          throw BookingApiException(detail, code: code);
        }
      }
      throw const BookingApiException(
        'Không thể lấy danh sách khuyến mãi. Vui lòng thử lại.',
      );
    }
  }

  Future<BookingFareEstimate> estimateFare(
    String accessToken, {
    required int vehicleId,
    required int serviceTypeId,
    required BookingLocation pickup,
    BookingLocation? destination,
    int? estimatedHours,
  }) async {
    try {
      final response = await _dio.post(
        ApiEndpoints.bookingEstimate,
        data: {
          ApiKeys.vehicleId: vehicleId,
          ApiKeys.serviceTypeId: serviceTypeId,
          ApiKeys.pickupLatitude: pickup.latitude,
          ApiKeys.pickupLongitude: pickup.longitude,
          ApiKeys.destinationLatitude: destination?.latitude ?? pickup.latitude,
          ApiKeys.destinationLongitude:
              destination?.longitude ?? pickup.longitude,
          ApiKeys.estimatedHours: estimatedHours,
        },
        options: Options(
          headers: {ApiKeys.authorization: AuthHeader.bearer(accessToken)},
        ),
      );

      return BookingFareEstimate.fromJson(
        Map<String, dynamic>.from(response.data as Map),
      );
    } on FormatException {
      throw const BookingApiException(BookingStrings.sessionExpired);
    } on DioException catch (exception) {
      final data = exception.response?.data;
      if (data is Map) {
        final detail = data[ApiKeys.detail]?.toString();
        final code = data['code']?.toString();
        if (detail != null) {
          throw BookingApiException(detail, code: code);
        }
      }
      throw const BookingApiException(
        'Không thể tính tuyến đường. Vui lòng thử lại.',
      );
    }
  }

  Future<BookingResponse> createBooking(
    String accessToken,
    CreateBookingRequest request,
  ) async {
    try {
      final response = await _dio.post(
        ApiEndpoints.bookings,
        data: request.toJson(),
        options: Options(
          headers: {ApiKeys.authorization: AuthHeader.bearer(accessToken)},
        ),
      );

      return BookingResponse.fromJson(
        Map<String, dynamic>.from(response.data as Map),
      );
    } on FormatException {
      throw const BookingApiException(BookingStrings.sessionExpired);
    } on DioException catch (exception) {
      final data = exception.response?.data;
      if (data is Map) {
        final detail = data[ApiKeys.detail]?.toString();
        final code = data['code']?.toString();
        if (detail != null) {
          throw BookingApiException(detail, code: code);
        }
      }
      throw const BookingApiException(BookingStrings.bookingFailed);
    }
  }

  Future<BookingResponse> getBookingDetails(
    String accessToken, {
    required int bookingId,
  }) async {
    try {
      final response = await _dio.get(
        '${ApiEndpoints.bookings}/$bookingId',
        options: Options(
          headers: {ApiKeys.authorization: AuthHeader.bearer(accessToken)},
        ),
      );

      return BookingResponse.fromJson(
        Map<String, dynamic>.from(response.data as Map),
      );
    } on FormatException {
      throw const BookingApiException(BookingStrings.sessionExpired);
    } on DioException catch (exception) {
      final data = exception.response?.data;
      if (data is Map) {
        final detail = data[ApiKeys.detail]?.toString();
        final code = data['code']?.toString();
        if (detail != null) {
          throw BookingApiException(detail, code: code);
        }
      }
      throw const BookingApiException(
        'Không thể lấy thông tin chuyến đi. Vui lòng thử lại.',
      );
    }
  }

  Future<BookingResponse?> getActiveBooking(String accessToken) async {
    try {
      final response = await _dio.get(
        ApiEndpoints.activeBooking,
        options: Options(
          headers: {ApiKeys.authorization: AuthHeader.bearer(accessToken)},
        ),
      );

      if (response.statusCode == 204 || response.data == null) {
        return null;
      }

      return BookingResponse.fromJson(
        Map<String, dynamic>.from(response.data as Map),
      );
    } on FormatException {
      throw const BookingApiException(BookingStrings.sessionExpired);
    } on DioException catch (exception) {
      final data = exception.response?.data;
      if (data is Map) {
        final detail = data[ApiKeys.detail]?.toString();
        final code = data['code']?.toString();
        if (detail != null) {
          throw BookingApiException(detail, code: code);
        }
      }
      throw const BookingApiException(
        'KhÃ´ng thá»ƒ láº¥y chuyáº¿n Ä‘ang hoáº¡t Ä‘á»™ng. Vui lÃ²ng thá»­ láº¡i.',
      );
    }
  }

  Future<BookingResponse> cancelBooking(
    String accessToken, {
    required int bookingId,
    required String reason,
  }) async {
    final url = '${ApiEndpoints.bookings}/$bookingId/cancel';
    debugPrint('CANCEL_BOOKING: Requesting $url');
    debugPrint('CANCEL_BOOKING: BookingID: $bookingId, Reason: $reason');
    debugPrint(
      'CANCEL_BOOKING: Token exists: ${accessToken.isNotEmpty}, Length: ${accessToken.length}',
    );

    try {
      final response = await _dio.post(
        url,
        data: {'reason': reason},
        options: Options(
          headers: {ApiKeys.authorization: AuthHeader.bearer(accessToken)},
        ),
      );

      debugPrint('CANCEL_BOOKING: Response Status: ${response.statusCode}');
      debugPrint('CANCEL_BOOKING: Response Data: ${response.data}');

      return BookingResponse.fromJson(
        Map<String, dynamic>.from(response.data as Map),
      );
    } on FormatException catch (e) {
      debugPrint('CANCEL_BOOKING: FormatException: $e');
      throw const BookingApiException(BookingStrings.sessionExpired);
    } on DioException catch (exception) {
      final status = exception.response?.statusCode;
      final data = exception.response?.data;
      debugPrint('CANCEL_BOOKING: DioException Status: $status');
      debugPrint('CANCEL_BOOKING: DioException Data: $data');

      if (data is Map && data[ApiKeys.detail] != null) {
        throw BookingApiException(data[ApiKeys.detail].toString());
      }

      if (status == 401) {
        throw const BookingApiException(
          'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.',
        );
      } else if (status == 403) {
        throw const BookingApiException(
          'Bạn không có quyền hủy chuyến đi này.',
        );
      }

      throw const BookingApiException(
        'Không thể hủy chuyến. Vui lòng thử lại.',
      );
    } catch (e) {
      debugPrint('CANCEL_BOOKING: Unknown Error: $e');
      throw const BookingApiException(
        'Đã xảy ra lỗi không xác định khi hủy chuyến.',
      );
    }
  }

  Future<BookingResponse> confirmDriver(
    String accessToken, {
    required int bookingId,
  }) async {
    try {
      final response = await _dio.post(
        '${ApiEndpoints.bookings}/$bookingId/confirm-driver',
        options: Options(
          headers: {ApiKeys.authorization: AuthHeader.bearer(accessToken)},
        ),
      );

      return BookingResponse.fromJson(
        Map<String, dynamic>.from(response.data as Map),
      );
    } on FormatException {
      throw const BookingApiException(BookingStrings.sessionExpired);
    } on DioException catch (exception) {
      final data = exception.response?.data;
      if (data is Map) {
        final detail = data[ApiKeys.detail]?.toString();
        final code = data['code']?.toString();
        if (detail != null) {
          throw BookingApiException(detail, code: code);
        }
      }
      throw const BookingApiException(
        'Không thể xác nhận tài xế. Vui lòng thử lại.',
      );
    }
  }

  Future<BookingResponse> confirmDriverOffer(
    String accessToken, {
    required int bookingId,
    required int offerId,
  }) async {
    try {
      final response = await _dio.post(
        ApiEndpoints.confirmDriverOffer(bookingId, offerId),
        options: Options(
          headers: {ApiKeys.authorization: AuthHeader.bearer(accessToken)},
        ),
      );

      return BookingResponse.fromJson(
        Map<String, dynamic>.from(response.data as Map),
      );
    } on FormatException {
      throw const BookingApiException(BookingStrings.sessionExpired);
    } on DioException catch (exception) {
      final data = exception.response?.data;
      if (data is Map) {
        final detail = data[ApiKeys.detail]?.toString();
        final code = data['code']?.toString();
        if (detail != null) {
          throw BookingApiException(detail, code: code);
        }
      }
      throw const BookingApiException(
        'Không thể xác nhận thuê tài xế. Vui lòng thử lại.',
      );
    }
  }

  Future<void> completeTrip(String accessToken, {required int tripId}) async {
    try {
      await _dio.post(
        ApiEndpoints.completeTrip(tripId),
        options: Options(
          headers: {ApiKeys.authorization: AuthHeader.bearer(accessToken)},
        ),
      );
    } on DioException catch (exception) {
      final data = exception.response?.data;
      if (data is Map) {
        final detail = data[ApiKeys.detail]?.toString();
        final code = data['code']?.toString();
        if (detail != null) {
          throw BookingApiException(detail, code: code);
        }
      }
      throw const BookingApiException(
        'KhÃ´ng thá»ƒ káº¿t thÃºc chuyáº¿n. Vui lÃ²ng thá»­ láº¡i.',
      );
    }
  }

  Future<void> submitTripRating(
    String accessToken, {
    required int tripId,
    required int ratingScore,
    String? comment,
  }) async {
    try {
      await _dio.post(
        ApiEndpoints.submitTripRating(tripId),
        data: {
          ApiKeys.ratingScore: ratingScore,
          if (comment != null && comment.trim().isNotEmpty)
            ApiKeys.comment: comment.trim(),
        },
        options: Options(
          headers: {ApiKeys.authorization: AuthHeader.bearer(accessToken)},
        ),
      );
    } on DioException catch (exception) {
      final statusCode = exception.response?.statusCode;
      final data = exception.response?.data;

      if (data is Map) {
        final detail = data[ApiKeys.detail]?.toString();
        final code = data[ApiKeys.code]?.toString();
        if (detail != null) {
          throw BookingApiException(detail, code: code, statusCode: statusCode);
        }
      }

      if (statusCode != null && statusCode >= 500) {
        throw BookingApiException(
          'Máy chủ đang gặp sự cố. Bạn có thể thử lại hoặc đánh giá sau.',
          statusCode: statusCode,
        );
      }

      throw BookingApiException(
        'Không thể gửi đánh giá. Vui lòng thử lại.',
        statusCode: statusCode,
      );
    }
  }

  Future<void> submitTripReport(
    String accessToken, {
    required int bookingId,
    required String subject,
    required String description,
  }) async {
    try {
      await _dio.post(
        ApiEndpoints.submitTripReport(bookingId),
        data: {
          ApiKeys.subject: subject.trim(),
          ApiKeys.description: description.trim(),
        },
        options: Options(
          headers: {ApiKeys.authorization: AuthHeader.bearer(accessToken)},
        ),
      );
    } on DioException catch (exception) {
      final statusCode = exception.response?.statusCode;
      final data = exception.response?.data;

      if (data is Map) {
        final detail = data[ApiKeys.detail]?.toString();
        final code = data[ApiKeys.code]?.toString();
        if (detail != null) {
          throw BookingApiException(detail, code: code, statusCode: statusCode);
        }
      }

      if (statusCode != null && statusCode >= 500) {
        throw BookingApiException(
          'Máy chủ đang gặp sự cố. Vui lòng thử lại sau.',
          statusCode: statusCode,
        );
      }

      throw BookingApiException(
        'Không thể gửi báo cáo. Vui lòng thử lại.',
        statusCode: statusCode,
      );
    }
  }

  Future<BookingResponse> rejectDriver(
    String accessToken, {
    required int bookingId,
  }) async {
    try {
      final response = await _dio.post(
        '${ApiEndpoints.bookings}/$bookingId/reject-driver',
        options: Options(
          headers: {ApiKeys.authorization: AuthHeader.bearer(accessToken)},
        ),
      );

      return BookingResponse.fromJson(
        Map<String, dynamic>.from(response.data as Map),
      );
    } on FormatException {
      throw const BookingApiException(BookingStrings.sessionExpired);
    } on DioException catch (exception) {
      final data = exception.response?.data;
      if (data is Map) {
        final detail = data[ApiKeys.detail]?.toString();
        final code = data['code']?.toString();
        if (detail != null) {
          throw BookingApiException(detail, code: code);
        }
      }
      throw const BookingApiException(
        'Không thể từ chối tài xế. Vui lòng thử lại.',
      );
    }
  }

  Future<List<NearbyDriver>> getNearbyDrivers(
    String accessToken, {
    required double latitude,
    required double longitude,
  }) async {
    try {
      final response = await _dio.get(
        ApiEndpoints.nearbyDrivers,
        queryParameters: {
          'latitude': latitude,
          'longitude': longitude,
          'radiusKm':
              5, // Tăng lên 1000km để chắc chắn thấy dữ liệu test ở Đà Nẵng
        },
        options: Options(
          headers: {ApiKeys.authorization: AuthHeader.bearer(accessToken)},
        ),
      );

      final List data = response.data as List;
      return data
          .map((item) => NearbyDriver.fromJson(Map<String, dynamic>.from(item)))
          .toList();
    } on DioException {
      return const [];
    }
  }
}

class BookingApiException implements Exception {
  const BookingApiException(this.message, {this.code, this.statusCode});

  final String message;
  final String? code;
  final int? statusCode;

  @override
  String toString() => message;
}
