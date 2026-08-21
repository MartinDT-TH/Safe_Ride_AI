import 'package:dio/dio.dart';
import 'package:http_parser/http_parser.dart';
import 'package:image_picker/image_picker.dart';

import '../../../../../core/constants/app_strings.dart';
import '../../../../../core/network/dio_client.dart';
import '../../../../../core/network/auth_header.dart';
import '../models/risk_protection_models.dart';

class RiskProtectionRemoteDatasource {
  RiskProtectionRemoteDatasource({Dio? dio}) : _dio = dio ?? DioClient().dio;

  final Dio _dio;

  Future<RiskProtectionAccident> getAccident(
    String accessToken,
    int accidentId,
  ) async {
    final response = await _request(
      () => _dio.get(
        ApiEndpoints.accidentDetails(accidentId),
        options: _options(accessToken),
      ),
    );
    return RiskProtectionAccident.fromJson(
      (response.data as Map).cast<String, dynamic>(),
    );
  }

  Future<void> uploadEvidence(
    String accessToken,
    int accidentId, {
    required XFile file,
    required String evidenceType,
    String? description,
  }) async {
    final contentType = _contentType(file);
    await _request(
      () async => _dio.post(
        ApiEndpoints.accidentEvidence(accidentId),
        data: FormData.fromMap({
          'file': await MultipartFile.fromFile(
            file.path,
            filename: file.name,
            contentType: MediaType.parse(contentType),
          ),
          'evidenceType': evidenceType,
          if (description?.trim().isNotEmpty == true)
            'description': description!.trim(),
        }),
        options: Options(
          headers: {ApiKeys.authorization: AuthHeader.bearer(accessToken)},
          contentType: AppValues.multipartFormData,
        ),
      ),
    );
  }

  Future<void> disputeLiability(
    String accessToken,
    int accidentId,
    String reason,
    List<int> evidenceIds,
  ) => _request(
    () => _dio.post(
      ApiEndpoints.accidentDisputes(accidentId),
      data: {'reason': reason.trim(), 'evidenceIds': evidenceIds},
      options: _options(accessToken),
    ),
  ).then((_) {});

  Future<List<DriverLiabilityItem>> getDriverLiabilities(
    String accessToken,
  ) async {
    final response = await _request(
      () => _dio.get(
        ApiEndpoints.driverLiabilities,
        options: _options(accessToken),
      ),
    );
    return (response.data as List? ?? const [])
        .whereType<Map>()
        .map(
          (item) => DriverLiabilityItem.fromJson(item.cast<String, dynamic>()),
        )
        .toList(growable: false);
  }

  Options _options(String accessToken) =>
      Options(headers: {ApiKeys.authorization: AuthHeader.bearer(accessToken)});

  Future<Response<dynamic>> _request(
    Future<Response<dynamic>> Function() action,
  ) async {
    try {
      return await action();
    } on DioException catch (exception) {
      final data = exception.response?.data;
      if (data is Map && data[ApiKeys.detail] != null) {
        throw RiskProtectionException(data[ApiKeys.detail].toString());
      }
      throw RiskProtectionException(
        'Không thể tải dữ liệu bảo vệ chuyến đi. Vui lòng thử lại.',
      );
    }
  }

  String _contentType(XFile file) {
    final provided = file.mimeType?.toLowerCase();
    if (provided == AppValues.pngMimeType ||
        provided == AppValues.webpMimeType ||
        provided == AppValues.jpegMimeType) {
      return provided!;
    }
    final extension = file.name.split('.').last.toLowerCase();
    return switch (extension) {
      'png' => AppValues.pngMimeType,
      'webp' => AppValues.webpMimeType,
      _ => AppValues.jpegMimeType,
    };
  }
}

class RiskProtectionException implements Exception {
  const RiskProtectionException(this.message);
  final String message;

  @override
  String toString() => message;
}
