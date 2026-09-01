import 'dart:typed_data';

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
    final prepared = await prepareAccidentEvidenceImage(file);
    await _request(
      () async => _dio.post(
        ApiEndpoints.accidentEvidence(accidentId),
        data: FormData.fromMap({
          'file': MultipartFile.fromBytes(
            prepared.bytes,
            filename: prepared.fileName,
            contentType: MediaType.parse(prepared.contentType),
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
}

class PreparedAccidentEvidenceImage {
  const PreparedAccidentEvidenceImage({
    required this.bytes,
    required this.fileName,
    required this.contentType,
  });

  final Uint8List bytes;
  final String fileName;
  final String contentType;
}

Future<PreparedAccidentEvidenceImage> prepareAccidentEvidenceImage(
  XFile file,
) async {
  late final Uint8List bytes;
  try {
    bytes = await file.readAsBytes();
  } catch (_) {
    throw const RiskProtectionException(
      'Không thể đọc ảnh bằng chứng. Vui lòng chụp lại.',
    );
  }

  if (bytes.isEmpty || bytes.length > 10_000_000) {
    throw const RiskProtectionException(
      'Ảnh bằng chứng phải có dung lượng từ 1 byte đến 10 MB.',
    );
  }

  final format = _detectEvidenceImageFormat(bytes);
  if (format == null) {
    throw const RiskProtectionException(
      'Ảnh bằng chứng phải đúng định dạng JPEG, PNG hoặc WebP.',
    );
  }

  return PreparedAccidentEvidenceImage(
    bytes: bytes,
    fileName: 'accident_evidence.${format.extension}',
    contentType: format.contentType,
  );
}

({String extension, String contentType})? _detectEvidenceImageFormat(
  List<int> bytes,
) {
  if (_startsWith(bytes, const [0xFF, 0xD8, 0xFF])) {
    return (extension: 'jpg', contentType: AppValues.jpegMimeType);
  }
  if (_startsWith(bytes, const [
    0x89,
    0x50,
    0x4E,
    0x47,
    0x0D,
    0x0A,
    0x1A,
    0x0A,
  ])) {
    return (extension: 'png', contentType: AppValues.pngMimeType);
  }
  if (bytes.length >= 12 &&
      String.fromCharCodes(bytes.sublist(0, 4)) == 'RIFF' &&
      String.fromCharCodes(bytes.sublist(8, 12)) == 'WEBP') {
    return (extension: 'webp', contentType: AppValues.webpMimeType);
  }
  return null;
}

bool _startsWith(List<int> bytes, List<int> signature) {
  if (bytes.length < signature.length) return false;
  for (var index = 0; index < signature.length; index++) {
    if (bytes[index] != signature[index]) return false;
  }
  return true;
}

class RiskProtectionException implements Exception {
  const RiskProtectionException(this.message);
  final String message;

  @override
  String toString() => message;
}
