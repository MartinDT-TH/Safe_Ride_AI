import 'dart:async';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:image_picker/image_picker.dart';
import 'package:safe_ride/core/session/session_manager.dart';
import 'package:safe_ride/core/storage/secure_storage_service.dart';
import 'package:safe_ride/features/shared/risk_protection/data/datasources/risk_protection_remote_datasource.dart';
import 'package:safe_ride/features/shared/risk_protection/data/models/risk_protection_models.dart';
import 'package:safe_ride/features/shared/risk_protection/presentation/providers/risk_protection_provider.dart';

void main() {
  test('normalizes camera filename and MIME from the JPEG signature', () async {
    final file = XFile.fromData(
      Uint8List.fromList([0xFF, 0xD8, 0xFF, 0x00]),
      name: 'camera-image.tmp',
      mimeType: 'application/octet-stream',
    );

    final prepared = await prepareAccidentEvidenceImage(file);

    expect(prepared.fileName, 'accident_evidence.jpg');
    expect(prepared.contentType, 'image/jpeg');
    expect(prepared.bytes, await file.readAsBytes());
  });

  test('normalizes PNG evidence independently of picker metadata', () async {
    final file = XFile.fromData(
      Uint8List.fromList([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
      name: 'photo.jpg',
      mimeType: 'image/jpeg',
    );

    final prepared = await prepareAccidentEvidenceImage(file);

    expect(prepared.fileName, 'accident_evidence.png');
    expect(prepared.contentType, 'image/png');
  });

  test('rejects bytes that are not a supported image', () async {
    final file = XFile.fromData(
      Uint8List.fromList([0x00, 0x01, 0x02, 0x03]),
      name: 'fake.jpg',
      mimeType: 'image/jpeg',
    );

    await expectLater(
      prepareAccidentEvidenceImage(file),
      throwsA(
        isA<RiskProtectionException>().having(
          (error) => error.message,
          'message',
          contains('JPEG, PNG hoặc WebP'),
        ),
      ),
    );
  });

  test(
    'provider blocks duplicate evidence uploads and refreshes on success',
    () async {
      final datasource = _FakeRiskProtectionRemoteDatasource(
        uploadGate: Completer<void>(),
      );
      final provider = RiskProtectionProvider(
        datasource,
        _FakeSessionManager(),
      );
      final file = XFile.fromData(
        Uint8List.fromList([0xFF, 0xD8, 0xFF, 0x00]),
        name: 'evidence.jpg',
      );

      final firstUpload = provider.uploadEvidence(accidentId: 42, file: file);
      await Future<void>.delayed(Duration.zero);
      final duplicateUpload = await provider.uploadEvidence(
        accidentId: 42,
        file: file,
      );

      expect(duplicateUpload, isFalse);
      expect(datasource.uploadCount, 1);
      expect(provider.isMutating, isTrue);

      datasource.uploadGate!.complete();
      expect(await firstUpload, isTrue);
      expect(provider.isMutating, isFalse);
      expect(datasource.loadCount, 1);
      expect(provider.accident?.id, 42);
    },
  );

  test('provider surfaces upload failure without reporting success', () async {
    final datasource = _FakeRiskProtectionRemoteDatasource(
      uploadError: const RiskProtectionException('Upload failed'),
    );
    final provider = RiskProtectionProvider(datasource, _FakeSessionManager());

    final success = await provider.uploadEvidence(
      accidentId: 42,
      file: XFile.fromData(Uint8List.fromList([0xFF, 0xD8, 0xFF, 0x00])),
    );

    expect(success, isFalse);
    expect(provider.isMutating, isFalse);
    expect(provider.errorMessage, 'Upload failed');
    expect(datasource.loadCount, 0);
  });
}

class _FakeSessionManager extends SessionManager {
  _FakeSessionManager() : super(storage: SecureStorageService());

  @override
  Future<String?> getValidAccessToken({bool forceRefresh = false}) async =>
      'test-token';
}

class _FakeRiskProtectionRemoteDatasource
    extends RiskProtectionRemoteDatasource {
  _FakeRiskProtectionRemoteDatasource({this.uploadGate, this.uploadError})
    : super(dio: Dio());

  final Completer<void>? uploadGate;
  final Object? uploadError;
  int uploadCount = 0;
  int loadCount = 0;

  @override
  Future<void> uploadEvidence(
    String accessToken,
    int accidentId, {
    required XFile file,
    required String evidenceType,
    String? description,
  }) async {
    uploadCount++;
    if (uploadError case final error?) throw error;
    await uploadGate?.future;
  }

  @override
  Future<RiskProtectionAccident> getAccident(
    String accessToken,
    int accidentId,
  ) async {
    loadCount++;
    return RiskProtectionAccident(
      id: accidentId,
      tripId: 7,
      category: 'MULTIPLE',
      status: 'EVIDENCE_COLLECTION',
      occurredAt: DateTime.utc(2026, 8, 22),
      description: 'Test accident',
      evidence: const [],
    );
  }
}
