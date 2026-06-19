import 'dart:io';

import 'package:dio/dio.dart';
import 'package:http_parser/http_parser.dart';

import '../../../../../core/constants/app_strings.dart';
import '../../../../../core/network/auth_header.dart';
import '../../../../../core/network/dio_client.dart';
import '../models/identity_verification_submission.dart';

class IdentityVerificationRemoteDatasource {
  IdentityVerificationRemoteDatasource({Dio? dio}) : _dio = dio ?? DioClient().dio;

  final Dio _dio;

  Future<void> submitAll(
    String accessToken,
    IdentityVerificationSubmission submission,
  ) async {
    if (submission.cccdFrontImage != null && submission.cccdBackImage != null) {
      await _uploadDocument(
        accessToken,
        'ID_CARD',
        {
          ApiKeys.frontImage: submission.cccdFrontImage,
          ApiKeys.backImage: submission.cccdBackImage,
          if (_hasText(submission.cccdNumber))
            ApiKeys.documentNumber: submission.cccdNumber!.trim(),
        },
      );
    }

    if (submission.licenseFrontImage != null &&
        submission.licenseBackImage != null) {
      await _uploadDocument(
        accessToken,
        'DRIVING_LICENSE',
        {
          ApiKeys.frontImage: submission.licenseFrontImage,
          ApiKeys.backImage: submission.licenseBackImage,
          if (_hasText(submission.licenseNumber))
            ApiKeys.documentNumber: submission.licenseNumber!.trim(),
          if (_hasText(submission.licenseClass))
            ApiKeys.licenseClass: _toBackendLicenseClass(
              submission.licenseClass!,
            ),
          if (submission.licenseIssueDate != null)
            ApiKeys.issueDate: _formatDate(submission.licenseIssueDate!),
          if (submission.licenseExpiryDate != null)
            ApiKeys.expiryDate: _formatDate(submission.licenseExpiryDate!),
        },
      );
    }

    if (submission.criminalRecordFile != null) {
      await _uploadDocument(
        accessToken,
        'CRIMINAL_RECORD',
        {
          ApiKeys.file: submission.criminalRecordFile,
        },
      );
    }
  }

  Future<void> _uploadDocument(
    String accessToken,
    String documentType,
    Map<String, Object?> fields,
  ) async {
    final formFields = <String, Object?>{};
    for (final entry in fields.entries) {
      final value = entry.value;
      if (value is File) {
        formFields[entry.key] = await _toMultipartFile(value);
      } else if (value != null) {
        formFields[entry.key] = value;
      }
    }

    await _dio.post(
      '${ApiEndpoints.identityVerificationDocuments}/$documentType',
      data: FormData.fromMap(formFields),
      options: Options(
        headers: {ApiKeys.authorization: AuthHeader.bearer(accessToken)},
        contentType: AppValues.multipartFormData,
      ),
    );
  }

  Future<MultipartFile> _toMultipartFile(File file) {
    final fileName = file.path.split(RegExp(r'[/\\]')).last;
    return MultipartFile.fromFile(
      file.path,
      filename: fileName,
      contentType: MediaType.parse(_contentTypeFor(fileName)),
    );
  }

  String _contentTypeFor(String fileName) {
    final extension = fileName.split('.').last.toLowerCase();
    return switch (extension) {
      AppValues.pngExtension => AppValues.pngMimeType,
      'pdf' => 'application/pdf',
      _ => AppValues.jpegMimeType,
    };
  }

  String _toBackendLicenseClass(String value) {
    return switch (value) {
      'A2' => 'Old_A2',
      'B1' => 'Old_B1',
      'B2' => 'Old_B2',
      _ => value,
    };
  }

  String _formatDate(DateTime value) {
    final month = value.month.toString().padLeft(2, '0');
    final day = value.day.toString().padLeft(2, '0');
    return '${value.year}-$month-$day';
  }

  bool _hasText(String? value) => value != null && value.trim().isNotEmpty;
}
