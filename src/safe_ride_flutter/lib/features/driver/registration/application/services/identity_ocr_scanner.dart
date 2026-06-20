import 'dart:io';

import 'package:google_mlkit_text_recognition/google_mlkit_text_recognition.dart';

enum IdentityOcrDocumentType {
  idCard,
  drivingLicense,
  criminalRecord,
}

class IdentityOcrResult {
  final IdentityOcrDocumentType documentType;
  final String rawText;
  final double confidence;
  final String? documentNumber;
  final String? licenseClass;
  final DateTime? issueDate;
  final DateTime? expiryDate;

  const IdentityOcrResult({
    required this.documentType,
    required this.rawText,
    required this.confidence,
    this.documentNumber,
    this.licenseClass,
    this.issueDate,
    this.expiryDate,
  });
}

class IdentityOcrScanner {
  static final RegExp _idCardNumberRegex = RegExp(r'\b\d{12}\b');
  static final RegExp _licenseNumberRegex = RegExp(r'\b\d{9,12}\b');
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
    final dates = _extractDates(rawText);
    final documentNumber = switch (documentType) {
      IdentityOcrDocumentType.idCard =>
        _firstMatch(_idCardNumberRegex, normalizedText),
      IdentityOcrDocumentType.drivingLicense =>
        _firstMatch(_licenseNumberRegex, normalizedText),
      IdentityOcrDocumentType.criminalRecord =>
        _firstMatch(_criminalRecordNumberRegex, normalizedText),
    };
    final licenseClass = documentType == IdentityOcrDocumentType.drivingLicense
        ? _extractLicenseClass(normalizedText)
        : null;

    return IdentityOcrResult(
      documentType: documentType,
      rawText: rawText,
      confidence: _estimateConfidence(recognizedText),
      documentNumber: documentNumber,
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

  String _normalize(String value) {
    return value
        .replaceAll('Số', 'So')
        .replaceAll('Hạng', 'Hang')
        .replaceAll('Ngày', 'Ngay')
        .toUpperCase();
  }
}

