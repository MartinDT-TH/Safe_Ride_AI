import 'dart:async';
import 'dart:io';
import 'dart:typed_data';
import 'dart:ui';

import 'package:google_mlkit_barcode_scanning/google_mlkit_barcode_scanning.dart';
import 'package:google_mlkit_text_recognition/google_mlkit_text_recognition.dart';

enum IdentityOcrDocumentType { idCard, drivingLicense, criminalRecord }
enum IdentityScanMethod { qr, ocr }

class QrNotDetectedException implements Exception {
  const QrNotDetectedException();
}

class QrPayloadInvalidException implements Exception {
  const QrPayloadInvalidException();
}

class _QrScanOutcome {
  const _QrScanOutcome({required this.detected, this.result});

  final bool detected;
  final IdentityOcrResult? result;
}

class IdentityOcrResult {
  final IdentityOcrDocumentType documentType;
  final IdentityScanMethod scanMethod;
  final String rawText;
  final double confidence;
  final String? documentNumber;
  final String? fullName;
  final String? licenseClass;
  final DateTime? issueDate;
  final DateTime? expiryDate;
  final bool hasNoExpiryDate;
  final DateTime? dateOfBirth;
  final String? gender;
  final String? address;

  const IdentityOcrResult({
    required this.documentType,
    this.scanMethod = IdentityScanMethod.ocr,
    required this.rawText,
    required this.confidence,
    this.documentNumber,
    this.fullName,
    this.licenseClass,
    this.issueDate,
    this.expiryDate,
    this.hasNoExpiryDate = false,
    this.dateOfBirth,
    this.gender,
    this.address,
  });
}

class IdentityOcrScanner {
  static final RegExp _idCardNumberRegex = RegExp(r'\b\d{12}\b');
  static final RegExp _licenseNumberRegex = RegExp(r'\b\d{9,12}\b');
  static final RegExp _nameLabelRegex = RegExp(
    r'(?:h[oọ]\s*(?:v[aà]\s*)?t[eê]n|full\s*name|fullname)',
    caseSensitive: false,
  );
  static final RegExp _criminalRecordNumberRegex = RegExp(
    r'\b(?:LLTP|LYLICH|PHIEU)[-\s]?[A-Z0-9-]{4,}\b',
    caseSensitive: false,
  );
  static final RegExp _dateRegex = RegExp(
    r'\b(?<day>\d{1,2})[\/\-.](?<month>\d{1,2})[\/\-.](?<year>\d{4})\b',
  );
  static final RegExp _isoDateRegex = RegExp(
    r'\b(?<year>\d{4})[\/\-.](?<month>\d{1,2})[\/\-.](?<day>\d{1,2})\b',
  );

  Future<IdentityOcrResult> scanImage({
    required File image,
    File? qrFallbackImage,
    String? detectedQrPayload,
    required IdentityOcrDocumentType documentType,
    bool allowOcrFallback = true,
    Future<void> Function()? onQrNotDetected,
    Future<void> Function()? onQrDetectedButInvalid,
  }) async {
    if (documentType == IdentityOcrDocumentType.criminalRecord) {
      return _scanText(image, documentType);
    }

    final liveQrPayload = detectedQrPayload?.trim();
    if (liveQrPayload != null && liveQrPayload.isNotEmpty) {
      final liveResult = documentType == IdentityOcrDocumentType.idCard
          ? parseCccdQrPayload(liveQrPayload)
          : parseDrivingLicenseQrPayload(liveQrPayload);
      if (liveResult != null) return liveResult;
      if (!allowOcrFallback) throw const QrPayloadInvalidException();
      await onQrDetectedButInvalid?.call();
      return _scanText(image, documentType);
    }

    final qrOutcome = await _scanQrWithFallback(
      image,
      qrFallbackImage,
      documentType,
    );
    if (qrOutcome.result != null) return qrOutcome.result!;

    if (!allowOcrFallback) {
      if (qrOutcome.detected) throw const QrPayloadInvalidException();
      throw const QrNotDetectedException();
    }

    if (qrOutcome.detected) {
      await onQrDetectedButInvalid?.call();
    } else {
      await onQrNotDetected?.call();
    }
    return _scanText(image, documentType);
  }

  Future<_QrScanOutcome> _scanQrWithFallback(
    File image,
    File? fallbackImage,
    IdentityOcrDocumentType documentType,
  ) async {
    final primaryResult = await _scanQr(image, documentType);
    if (primaryResult.result != null) return primaryResult;
    if (fallbackImage == null || fallbackImage.path == image.path) {
      final enhancedResult = await _scanEnhancedQrRegions(image, documentType);
      return _QrScanOutcome(
        detected: primaryResult.detected || enhancedResult.detected,
        result: enhancedResult.result,
      );
    }
    final fallbackResult = await _scanQr(fallbackImage, documentType);
    if (fallbackResult.result != null) return fallbackResult;

    final enhancedResult = await _scanEnhancedQrRegions(
      fallbackImage,
      documentType,
    );
    return _QrScanOutcome(
      detected:
          primaryResult.detected ||
          fallbackResult.detected ||
          enhancedResult.detected,
      result: enhancedResult.result,
    );
  }

  Future<_QrScanOutcome> _scanEnhancedQrRegions(
    File image,
    IdentityOcrDocumentType documentType,
  ) async {
    final variants = await _createQrRegionVariants(image);
    var detected = false;
    try {
      for (final variant in variants) {
        final outcome = await _scanQr(variant, documentType);
        detected = detected || outcome.detected;
        if (outcome.result != null) return outcome;
      }
      return _QrScanOutcome(detected: detected);
    } finally {
      for (final variant in variants) {
        try {
          if (await variant.exists()) await variant.delete();
        } catch (_) {
          // Temporary scan variants are best-effort cleanup only.
        }
      }
    }
  }

  Future<List<File>> _createQrRegionVariants(File image) async {
    final bytes = await image.readAsBytes();
    final decoded = await _decodeImage(bytes);
    final width = decoded.width.toDouble();
    final height = decoded.height.toDouble();
    final regions = <Rect>[
      Rect.fromLTWH(0, height * 0.25, width * 0.62, height * 0.75),
      Rect.fromLTWH(width * 0.38, height * 0.25, width * 0.62, height * 0.75),
      Rect.fromLTWH(0, 0, width * 0.62, height * 0.75),
      Rect.fromLTWH(width * 0.38, 0, width * 0.62, height * 0.75),
    ];
    final variants = <File>[];
    try {
      for (var index = 0; index < regions.length; index++) {
        final region = regions[index];
        final scale = (1200 / region.width).clamp(2.0, 4.0);
        final outputWidth = (region.width * scale).round();
        final outputHeight = (region.height * scale).round();
        final recorder = PictureRecorder();
        final canvas = Canvas(recorder);
        canvas.drawImageRect(
          decoded,
          region,
          Rect.fromLTWH(
            0,
            0,
            outputWidth.toDouble(),
            outputHeight.toDouble(),
          ),
          Paint()..filterQuality = FilterQuality.none,
        );
        final rendered = await recorder.endRecording().toImage(
          outputWidth,
          outputHeight,
        );
        final png = await rendered.toByteData(format: ImageByteFormat.png);
        rendered.dispose();
        if (png == null) continue;
        final variant = File('${image.path}.qr_$index.png');
        await variant.writeAsBytes(png.buffer.asUint8List(), flush: true);
        variants.add(variant);
      }
      return variants;
    } finally {
      decoded.dispose();
    }
  }

  Future<Image> _decodeImage(Uint8List bytes) {
    final completer = Completer<Image>();
    decodeImageFromList(bytes, completer.complete);
    return completer.future;
  }

  Future<IdentityOcrResult> _scanText(
    File image,
    IdentityOcrDocumentType documentType,
  ) async {
    final recognizer = TextRecognizer(script: TextRecognitionScript.latin);
    try {
      final inputImage = InputImage.fromFilePath(image.path);
      final recognizedText = await recognizer.processImage(inputImage);
      return _parse(documentType, recognizedText);
    } finally {
      await recognizer.close();
    }
  }

  Future<_QrScanOutcome> _scanQr(
    File image,
    IdentityOcrDocumentType documentType,
  ) async {
    final scanner = BarcodeScanner(formats: [BarcodeFormat.qrCode]);
    try {
      final inputImage = InputImage.fromFilePath(image.path);
      final barcodes = await scanner.processImage(inputImage);
      for (final barcode in barcodes) {
        final payload = barcode.rawValue ?? '';
        final parsed = documentType == IdentityOcrDocumentType.idCard
            ? parseCccdQrPayload(payload)
            : parseDrivingLicenseQrPayload(payload);
        if (parsed != null) {
          return _QrScanOutcome(detected: true, result: parsed);
        }
      }
      return _QrScanOutcome(detected: barcodes.isNotEmpty);
    } finally {
      await scanner.close();
    }
  }

  IdentityOcrResult? parseCccdQrPayload(String payload) {
    final fields = payload.split('|').map((value) => value.trim()).toList();
    if (fields.length < 5) return null;

    final hasLegacyIdentityNumber = fields.length >= 7;
    final documentNumber = fields[0].replaceAll(RegExp(r'\D'), '');
    final fullNameIndex = hasLegacyIdentityNumber ? 2 : 1;
    final dateOfBirthIndex = hasLegacyIdentityNumber ? 3 : 2;
    final genderIndex = hasLegacyIdentityNumber ? 4 : 3;
    final addressIndex = hasLegacyIdentityNumber ? 5 : 4;

    if (documentNumber.length != 12) return null;
    final fullName = _normalizePersonName(fields[fullNameIndex]);
    if (!_isLikelyName(fullName)) return null;
    final dateOfBirth = _parseCompactQrDate(fields[dateOfBirthIndex]);
    final gender = _normalizeQrGender(fields[genderIndex]);
    final address = fields[addressIndex].trim();
    if (dateOfBirth == null || gender == null || address.isEmpty) return null;

    return IdentityOcrResult(
      documentType: IdentityOcrDocumentType.idCard,
      scanMethod: IdentityScanMethod.qr,
      rawText: payload,
      confidence: 1,
      documentNumber: documentNumber,
      fullName: fullName,
      dateOfBirth: dateOfBirth,
      gender: gender,
      address: address,
      issueDate: hasLegacyIdentityNumber
          ? _parseCompactQrDate(fields[6])
          : null,
    );
  }

  IdentityOcrResult? parseDrivingLicenseQrPayload(String payload) {
    final fields = payload
        .split(';')
        .map((value) => value.trim())
        .where((value) => value.isNotEmpty)
        .toList();
    if (fields.length < 4) return null;

    final licenseClassIndex = fields.indexWhere(
      (field) => _normalizeQrLicenseClass(field) != null,
    );
    if (licenseClassIndex < 0) return null;
    final licenseClass = _normalizeQrLicenseClass(fields[licenseClassIndex]);

    final dateFields = <({int index, DateTime value})>[];
    for (var index = 0; index < fields.length; index++) {
      final date = _parseCompactQrDate(fields[index]);
      if (date != null) dateFields.add((index: index, value: date));
    }
    final birthDateFields = dateFields
        .where((item) => item.index < licenseClassIndex)
        .toList();
    if (birthDateFields.isEmpty) return null;
    final birthDateField = birthDateFields.first;

    final documentIndex = fields.indexWhere((field) {
      final normalized = field.replaceAll(RegExp(r'\s'), '').toUpperCase();
      return RegExp(r'^[A-Z0-9]{6,20}$').hasMatch(normalized) &&
          RegExp(r'\d').hasMatch(normalized) &&
          _parseCompactQrDate(normalized) == null;
    });
    if (documentIndex < 0) return null;
    final documentNumber = fields[documentIndex]
        .replaceAll(RegExp(r'\s'), '')
        .toUpperCase();

    String? fullName;
    for (var index = documentIndex + 1;
        index < birthDateField.index;
        index++) {
      final candidate = _normalizePersonName(fields[index]);
      if (_isLikelyName(candidate)) {
        fullName = candidate;
        break;
      }
    }
    if (fullName == null) return null;

    final datesAfterClass = dateFields
        .where((item) => item.index > licenseClassIndex)
        .toList();
    final issueDate = datesAfterClass.isEmpty
        ? null
        : datesAfterClass.first.value;
    final hasNoExpiryDate = fields
            .skip(licenseClassIndex + 1)
            .any(_isUnlimitedExpiry) ||
        datesAfterClass.length < 2;
    final expiryDate = hasNoExpiryDate ? null : datesAfterClass[1].value;
    if (licenseClass == null ||
        (!hasNoExpiryDate && expiryDate == null) ||
        (issueDate != null &&
            expiryDate != null &&
            expiryDate.isBefore(issueDate))) {
      return null;
    }

    return IdentityOcrResult(
      documentType: IdentityOcrDocumentType.drivingLicense,
      scanMethod: IdentityScanMethod.qr,
      rawText: payload,
      confidence: 1,
      documentNumber: documentNumber,
      fullName: fullName,
      dateOfBirth: birthDateField.value,
      licenseClass: licenseClass,
      issueDate: issueDate,
      expiryDate: expiryDate,
      hasNoExpiryDate: hasNoExpiryDate,
    );
  }

  DateTime? _parseCompactQrDate(String value) {
    final digits = value.replaceAll(RegExp(r'\D'), '');
    if (digits.length != 8) return null;

    final day = int.tryParse(digits.substring(0, 2));
    final month = int.tryParse(digits.substring(2, 4));
    final year = int.tryParse(digits.substring(4, 8));
    if (day == null || month == null || year == null) return null;

    final date = DateTime(year, month, day);
    return date.day == day && date.month == month && date.year == year
        ? date
        : null;
  }

  String? _normalizeQrGender(String value) {
    return switch (_normalize(value).trim()) {
      'NAM' || 'MALE' => 'Male',
      'NU' || 'NỮ' || 'FEMALE' => 'Female',
      'KHAC' || 'KHÁC' || 'OTHER' => 'Other',
      _ => null,
    };
  }

  String? _normalizeQrLicenseClass(String value) {
    final normalized = _normalize(value).replaceAll(RegExp(r'\s+'), '');
    return const {'A1', 'A2', 'A', 'B1', 'B2', 'B'}.contains(normalized)
        ? normalized
        : null;
  }

  bool _isUnlimitedExpiry(String value) {
    final normalized = _normalizePersonName(value);
    return const {
      'KHONG THOI HAN',
      'VO THOI HAN',
      'UNLIMITED',
      'NO EXPIRY',
      'PERMANENT',
    }.any(normalized.contains);
  }

  IdentityOcrResult _parse(
    IdentityOcrDocumentType documentType,
    RecognizedText recognizedText,
  ) {
    final rawText = recognizedText.text.trim();
    final normalizedText = _normalize(rawText);
    final extractedFullName = _extractFullName(rawText, recognizedText);
    final fullName = extractedFullName == null
        ? null
        : _normalizePersonName(extractedFullName);
    final dates = _extractDates(rawText);
    final documentNumber = switch (documentType) {
      IdentityOcrDocumentType.idCard => _firstMatch(
        _idCardNumberRegex,
        normalizedText,
      ),
      IdentityOcrDocumentType.drivingLicense => _firstMatch(
        _licenseNumberRegex,
        normalizedText,
      ),
      IdentityOcrDocumentType.criminalRecord => _firstMatch(
        _criminalRecordNumberRegex,
        normalizedText,
      ),
    };
    final licenseClass = documentType == IdentityOcrDocumentType.drivingLicense
        ? _extractLicenseClass(normalizedText)
        : null;
    final dateOfBirth = documentType == IdentityOcrDocumentType.idCard
        ? _extractDateOfBirth(rawText)
        : null;
    final hasNoExpiryDate =
        documentType == IdentityOcrDocumentType.drivingLicense &&
        _isUnlimitedExpiry(rawText);
    final issueDate = switch (documentType) {
      IdentityOcrDocumentType.drivingLicense => _extractLabeledDate(
        rawText,
        r'(?:ng[aà]y\s*c[aấ]p|date\s*of\s*issue|issue\s*date)',
      ),
      IdentityOcrDocumentType.criminalRecord => dates.isEmpty
          ? null
          : dates.first,
      IdentityOcrDocumentType.idCard => null,
    };
    final expiryDate =
        documentType == IdentityOcrDocumentType.drivingLicense &&
            !hasNoExpiryDate
        ? _extractLabeledDate(
            rawText,
            r'(?:c[oó]\s*gi[aá]\s*tr[iị]\s*(?:đ[eế]n)?|date\s*of\s*expiry|expiry\s*date|expires)',
          )
        : null;

    return IdentityOcrResult(
      documentType: documentType,
      rawText: rawText,
      confidence: _estimateConfidence(recognizedText),
      documentNumber: documentNumber,
      fullName: fullName,
      licenseClass: licenseClass,
      issueDate: issueDate,
      expiryDate: expiryDate,
      hasNoExpiryDate: hasNoExpiryDate,
      dateOfBirth: dateOfBirth,
      gender: documentType == IdentityOcrDocumentType.idCard
          ? _extractGender(normalizedText)
          : null,
      address: documentType == IdentityOcrDocumentType.idCard
          ? _extractAddress(rawText)
          : null,
    );
  }

  DateTime? _extractDateOfBirth(String value) {
    final match = RegExp(
      r'(?:ng[aà]y\s*sinh|date\s*of\s*birth|dob)[^\d]{0,12}'
      r'(?<day>\d{1,2})[\/\-.](?<month>\d{1,2})[\/\-.](?<year>\d{4})',
      caseSensitive: false,
    ).firstMatch(value);
    return match == null ? null : _tryCreateDate(match);
  }

  DateTime? _extractLabeledDate(String value, String labelPattern) {
    final match = RegExp(
      '$labelPattern' r'[^\d]{0,24}'
      r'(?<day>\d{1,2})[\/\-.](?<month>\d{1,2})[\/\-.](?<year>\d{4})',
      caseSensitive: false,
    ).firstMatch(value);
    return match == null ? null : _tryCreateDate(match);
  }

  String? _extractGender(String value) {
    final match = RegExp(
      r'(?:GIOI\s*TINH|SEX)[^A-ZÀ-Ỹ]{0,8}(NAM|NU|NỮ|MALE|FEMALE)',
      caseSensitive: false,
    ).firstMatch(value);
    final gender = match?.group(1)?.toUpperCase();
    if (gender == 'NAM' || gender == 'MALE') return 'Male';
    if (gender == 'NU' || gender == 'NỮ' || gender == 'FEMALE') return 'Female';
    return null;
  }

  String? _extractAddress(String value) {
    final lines = value
        .split(RegExp(r'[\r\n]+'))
        .map((line) => line.trim())
        .where((line) => line.isNotEmpty)
        .toList();
    final label = RegExp(
      r'(?:n[oơ]i\s*th[uư][oờ]ng\s*tr[uú]|place\s*of\s*residence|address)',
      caseSensitive: false,
    );
    for (var i = 0; i < lines.length; i++) {
      final match = label.firstMatch(lines[i]);
      if (match == null) continue;
      final parts = <String>[];
      final sameLine = lines[i].substring(match.end).replaceFirst(RegExp(r'^\s*[:/-]?\s*'), '');
      if (sameLine.isNotEmpty) parts.add(sameLine);
      for (var j = i + 1; j < lines.length && parts.length < 2; j++) {
        if (RegExp(r'(?:c[oó]\s*gi[aá]\s*tr[iị]|date\s*of\s*expiry)', caseSensitive: false)
            .hasMatch(lines[j])) break;
        parts.add(lines[j]);
      }
      final address = parts.join(', ').trim();
      return address.isEmpty ? null : address;
    }
    return null;
  }

  double _estimateConfidence(RecognizedText recognizedText) {
    final words = recognizedText.blocks
        .expand((block) => block.lines)
        .expand((line) => line.elements)
        .toList();

    if (words.isEmpty) return 0;

    final withConfidence = words
        .map((word) => word.confidence)
        .whereType<double>()
        .toList();

    if (withConfidence.isEmpty) {
      return recognizedText.text.trim().isEmpty ? 0 : 0.8;
    }

    final total = withConfidence.fold<double>(0, (sum, value) => sum + value);
    return double.parse((total / withConfidence.length).toStringAsFixed(3));
  }

  String? _firstMatch(RegExp regex, String value) {
    final match = regex.firstMatch(value);
    final text = match?.group(0)?.trim();
    return text == null || text.isEmpty ? null : text;
  }

  List<DateTime> _extractDates(String value) {
    final dates = <DateTime>[];

    for (final match in _dateRegex.allMatches(value)) {
      final date = _tryCreateDate(match);
      if (date != null) dates.add(date);
    }

    for (final match in _isoDateRegex.allMatches(value)) {
      final date = _tryCreateDate(match);
      if (date != null) dates.add(date);
    }

    return dates;
  }

  DateTime? _tryCreateDate(RegExpMatch match) {
    final day = int.tryParse(match.namedGroup('day') ?? '');
    final month = int.tryParse(match.namedGroup('month') ?? '');
    final year = int.tryParse(match.namedGroup('year') ?? '');

    if (day == null || month == null || year == null) return null;
    if (year < 1900 || year > 2200 || month < 1 || month > 12) return null;

    try {
      return DateTime(year, month, day);
    } on ArgumentError {
      return null;
    }
  }

  String? _extractLicenseClass(String value) {
    final patterns = <String, String>{
      r'\bB2\b': 'B2',
      r'\bB1\b': 'B1',
      r'\bA2\b': 'A2',
      r'\bA1\b': 'A1',
      r'\bC\b': 'C',
      r'\bD\b': 'D',
      r'\bE\b': 'E',
      r'\bB\b': 'B',
      r'\bA\b': 'A',
    };

    for (final entry in patterns.entries) {
      if (RegExp(entry.key).hasMatch(value)) {
        return entry.value;
      }
    }

    return null;
  }

  /// Extract full name using both raw text lines AND spatial block analysis.
  ///
  /// On Vietnamese driving licenses, the label "Họ tên/Full name" and the
  /// actual name are on the same visual row but separated by a gap. Google
  /// ML Kit often splits them into separate text blocks. This method:
  /// 1. Tries spatial block matching (finds blocks on the same Y-coordinate)
  /// 2. Falls back to raw text line analysis
  String? _extractFullName(String value, RecognizedText recognizedText) {
    // Strategy 1: Spatial block analysis using ML Kit's recognized blocks.
    final spatialResult = _extractFullNameFromBlocks(recognizedText);
    if (spatialResult != null) return spatialResult;

    // Strategy 2: Line-based analysis on raw text (fallback).
    final lines = value
        .split(RegExp(r'[\r\n]+'))
        .map((line) => line.trim())
        .where((line) => line.isNotEmpty)
        .toList();

    for (var i = 0; i < lines.length; i++) {
      final hasNameLabel = _nameLabelRegex.hasMatch(lines[i]);
      if (!hasNameLabel) continue;

      final sameLineName = _extractNameAfterLabel(lines[i]);
      if (_isLikelyName(sameLineName)) return sameLineName;

      for (var j = i + 1; j < lines.length && j <= i + 2; j++) {
        final nextLineName = _cleanNameCandidate(lines[j]);
        if (_isLikelyName(nextLineName)) return nextLineName;
      }
    }

    return null;
  }

  /// Use ML Kit block/line bounding boxes to find name text that shares the
  /// same visual row as the name label.
  String? _extractFullNameFromBlocks(RecognizedText recognizedText) {
    // Collect all lines with their bounding boxes
    final allLines = <({String text, Rect boundingBox})>[];
    for (final block in recognizedText.blocks) {
      for (final line in block.lines) {
        allLines.add((text: line.text, boundingBox: line.boundingBox));
      }
    }

    // Find lines that contain the name label
    for (var i = 0; i < allLines.length; i++) {
      final line = allLines[i];
      if (!_nameLabelRegex.hasMatch(line.text)) continue;

      // First: try to get the name from the same line's text after the label
      final sameLineName = _extractNameAfterLabel(line.text);
      if (_isLikelyName(sameLineName)) return sameLineName;

      // Second: look for other lines/blocks on the same visual row.
      // Two lines are on the "same row" if their vertical centers are close.
      final labelCenterY = line.boundingBox.center.dy;
      final labelHeight = line.boundingBox.height;
      // Tolerance: within 70% of the label's height (accommodates slight skew)
      final yTolerance = labelHeight * 0.7;
      // Also require the candidate to be to the RIGHT of the label
      final labelRight = line.boundingBox.right;

      final sameRowCandidates = <({String text, double x})>[];
      for (var j = 0; j < allLines.length; j++) {
        if (j == i) continue;
        final candidate = allLines[j];
        final candidateCenterY = candidate.boundingBox.center.dy;
        final candidateLeft = candidate.boundingBox.left;

        if ((candidateCenterY - labelCenterY).abs() <= yTolerance &&
            candidateLeft >= labelRight - 10) {
          sameRowCandidates.add(
            (text: candidate.text, x: candidate.boundingBox.left),
          );
        }
      }

      // Sort by X position (left to right) and join
      sameRowCandidates.sort((a, b) => a.x.compareTo(b.x));
      if (sameRowCandidates.isNotEmpty) {
        final combinedName = sameRowCandidates.map((c) => c.text).join(' ');
        final cleaned = _cleanNameCandidate(combinedName);
        if (_isLikelyName(cleaned)) return cleaned;
      }

      // Third: look for lines directly below (next visual row)
      final belowCandidates = <({String text, double y})>[];
      for (var j = 0; j < allLines.length; j++) {
        if (j == i) continue;
        final candidate = allLines[j];
        final candidateTop = candidate.boundingBox.top;
        // Must be below the label, within 2x label height
        if (candidateTop > line.boundingBox.bottom - 5 &&
            candidateTop < line.boundingBox.bottom + labelHeight * 2) {
          belowCandidates.add(
            (text: candidate.text, y: candidate.boundingBox.top),
          );
        }
      }

      belowCandidates.sort((a, b) => a.y.compareTo(b.y));
      for (final candidate in belowCandidates) {
        final cleaned = _cleanNameCandidate(candidate.text);
        if (_isLikelyName(cleaned)) return cleaned;
      }
    }

    return null;
  }

  String _extractNameAfterLabel(String value) {
    final matches = _nameLabelRegex.allMatches(value).toList();
    if (matches.isEmpty) return _cleanNameCandidate(value);

    final afterLastLabel = value.substring(matches.last.end);
    return _cleanNameCandidate(afterLastLabel);
  }

  String _cleanNameCandidate(String value) {
    return value
        .replaceAll(_nameLabelRegex, '')
        .replaceAll(RegExp(r'[\/\\]'), ' ')
        .replaceAll(RegExp(r'[:：]'), '')
        .replaceAll(RegExp(r'\s+'), ' ')
        .trim();
  }

  bool _isLikelyName(String value) {
    if (value.length < 5 || value.length > 80) return false;
    if (RegExp(r'\d').hasMatch(value)) return false;
    if (value.contains('/') || value.contains('\\')) return false;

    final normalized = _normalize(value);
    const blocked = {
      'CONG HOA XA HOI CHU NGHIA VIET NAM',
      'DOC LAP TU DO HANH PHUC',
      'CAN CUOC CONG DAN',
      'GIAY PHEP LAI XE',
      'DRIVING LICENSE',
      'SOCIALIST REPUBLIC OF VIET NAM',
    };
    if (blocked.any(normalized.contains)) return false;

    const blockedFieldLabels = {
      'FULL NAME',
      'HO TEN',
      'HỌ TÊN',
      'HO VA TEN',
      'HỌ VÀ TÊN',
      'DATE OF BIRTH',
      'BIRTH',
      'DOB',
      'NGAY SINH',
      'NGÀY SINH',
      'NOI CU TRU',
      'NƠI CƯ TRÚ',
      'PLACE OF RESIDENCE',
      'ADDRESS',
      'QUOC TICH',
      'QUỐC TỊCH',
      'NATIONALITY',
      'GIOI TINH',
      'GIỚI TÍNH',
      'SEX',
      'HẠNG',
      'CLASS',
      'ISSUED',
      'ISSUE DATE',
      'EXPIRY',
      'VALID',
      'CO GIA TRI',
    };
    if (blockedFieldLabels.any(normalized.contains)) return false;

    return RegExp(r'[A-ZÀ-Ỹa-zà-ỹ]{2,}\s+[A-ZÀ-Ỹa-zà-ỹ]{2,}').hasMatch(value);
  }

  String _normalize(String value) {
    return value
        .replaceAll('Số', 'So')
        .replaceAll('Hạng', 'Hang')
        .replaceAll('Ngày', 'Ngay')
        .toUpperCase();
  }

  String _normalizePersonName(String value) {
    const replacements = <String, String>{
      'ÀÁẠẢÃÂẦẤẬẨẪĂẰẮẶẲẴ': 'A',
      'ÈÉẸẺẼÊỀẾỆỂỄ': 'E',
      'ÌÍỊỈĨ': 'I',
      'ÒÓỌỎÕÔỒỐỘỔỖƠỜỚỢỞỠ': 'O',
      'ÙÚỤỦŨƯỪỨỰỬỮ': 'U',
      'ỲÝỴỶỸ': 'Y',
      'Đ': 'D',
    };
    var normalized = value.trim().toUpperCase();
    for (final entry in replacements.entries) {
      for (final character in entry.key.split('')) {
        normalized = normalized.replaceAll(character, entry.value);
      }
    }
    return normalized.replaceAll(RegExp(r'\s+'), ' ');
  }
}
