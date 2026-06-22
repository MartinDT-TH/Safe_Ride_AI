import 'dart:io';
import 'dart:ui';

import 'package:google_mlkit_text_recognition/google_mlkit_text_recognition.dart';

enum IdentityOcrDocumentType { idCard, drivingLicense, criminalRecord }

class IdentityOcrResult {
  final IdentityOcrDocumentType documentType;
  final String rawText;
  final double confidence;
  final String? documentNumber;
  final String? fullName;
  final String? licenseClass;
  final DateTime? issueDate;
  final DateTime? expiryDate;

  const IdentityOcrResult({
    required this.documentType,
    required this.rawText,
    required this.confidence,
    this.documentNumber,
    this.fullName,
    this.licenseClass,
    this.issueDate,
    this.expiryDate,
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
    required IdentityOcrDocumentType documentType,
  }) async {
    final recognizer = TextRecognizer(script: TextRecognitionScript.latin);
    try {
      final inputImage = InputImage.fromFilePath(image.path);
      final recognizedText = await recognizer.processImage(inputImage);
      return _parse(documentType, recognizedText);
    } finally {
      await recognizer.close();
    }
  }

  IdentityOcrResult _parse(
    IdentityOcrDocumentType documentType,
    RecognizedText recognizedText,
  ) {
    final rawText = recognizedText.text.trim();
    final normalizedText = _normalize(rawText);
    final fullName = _extractFullName(rawText, recognizedText);
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

    return IdentityOcrResult(
      documentType: documentType,
      rawText: rawText,
      confidence: _estimateConfidence(recognizedText),
      documentNumber: documentNumber,
      fullName: fullName,
      licenseClass: licenseClass,
      issueDate: dates.isNotEmpty ? dates.first : null,
      expiryDate:
          documentType == IdentityOcrDocumentType.drivingLicense &&
              dates.length > 1
          ? dates.last
          : null,
    );
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
}
