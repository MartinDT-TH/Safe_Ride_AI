import 'dart:io';
import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';

void main() {
  test('Customer and Driver UI contains no direct Vietnamese text', () {
    final roots = [
      Directory('lib/core'),
      Directory('lib/features/auth/presentation'),
      Directory('lib/features/shared'),
      Directory('lib/features/customer'),
      Directory('lib/features/driver'),
    ];
    final vietnamese = RegExp(r'[À-ỹĐđ]');
    final violations = <String>[];

    for (final root in roots.where((directory) => directory.existsSync())) {
      for (final entity in root.listSync(recursive: true)) {
        if (entity is! File || !entity.path.endsWith('.dart')) continue;
        final normalized = entity.path.replaceAll('\\', '/');
        if (normalized.endsWith('/core/constants/app_strings.dart')) {
          continue;
        }

        final lines = entity.readAsLinesSync();
        for (var index = 0; index < lines.length; index++) {
          final sourceLine = lines[index].trim();
          if (sourceLine.startsWith('//') || sourceLine.startsWith('///')) {
            continue;
          }
          final line = sourceLine.split('//').first.trim();
          final isDiacriticNormalizationEntry = RegExp(
            r"^'[À-ỹĐđ]':\s*'[a-zA-Z]'[,]?$",
          ).hasMatch(line);
          final isIdentityOcrVocabulary = normalized.endsWith(
            '/driver/registration/application/services/identity_ocr_scanner.dart',
          );
          final isGeographicAddressSample =
              line.startsWith('pickupAddress:') ||
              line.startsWith('destinationAddress:');
          if (vietnamese.hasMatch(line) &&
              !isDiacriticNormalizationEntry &&
              !isIdentityOcrVocabulary &&
              !isGeographicAddressSample) {
            violations.add('$normalized:${index + 1}: $line');
          }
        }
      }
    }

    expect(
      violations,
      isEmpty,
      reason: 'Move every user-facing string to ARB:\n${violations.join('\n')}',
    );
  });

  test('all supported ARB files have identical message keys', () {
    final files = ['vi', 'en', 'ko', 'ja', 'zh']
        .map((code) => File('lib/l10n/app_$code.arb'));
    final keySets = <String, Set<String>>{};
    for (final file in files) {
      final values = jsonDecode(file.readAsStringSync()) as Map<String, dynamic>;
      keySets[file.path] = values.keys
          .where((key) => !key.startsWith('@'))
          .toSet();
    }

    final template = keySets['lib/l10n/app_vi.arb']!;
    for (final entry in keySets.entries) {
      expect(entry.value.difference(template), isEmpty,
          reason: '${entry.key} has unexpected keys');
      expect(template.difference(entry.value), isEmpty,
          reason: '${entry.key} is missing keys');
    }
  });

  test('non-Vietnamese ARB files contain no Vietnamese fallback text', () {
    final vietnamese = RegExp(
      r'[ÀÁẠẢÃÂẦẤẬẨẪĂẰẮẶẲẴÈÉẸẺẼÊỀẾỆỂỄÌÍỊỈĨÒÓỌỎÕÔỒỐỘỔỖƠỜỚỢỞỠÙÚỤỦŨƯỪỨỰỬỮỲÝỴỶỸĐ'
      r'àáạảãâầấậẩẫăằắặẳẵèéẹẻẽêềếệểễìíịỉĩòóọỏõôồốộổỗơờớợởỡùúụủũưừứựửữỳýỵỷỹđ]',
    );
    final violations = <String>[];
    for (final code in ['en', 'ko', 'ja', 'zh']) {
      final path = 'lib/l10n/app_$code.arb';
      final values = jsonDecode(File(path).readAsStringSync())
          as Map<String, dynamic>;
      for (final entry in values.entries) {
        if (entry.key.startsWith('@') || entry.key == 'vietnamese') continue;
        if (entry.value is String && vietnamese.hasMatch(entry.value as String)) {
          violations.add('$path:${entry.key}=${entry.value}');
        }
      }
    }
    expect(violations, isEmpty, reason: violations.join('\n'));
  });
}
