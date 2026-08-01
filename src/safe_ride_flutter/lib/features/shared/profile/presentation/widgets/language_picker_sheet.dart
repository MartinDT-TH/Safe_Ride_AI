import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../../../core/localization/locale_provider.dart';
import '../../../../../core/localization/localization_extensions.dart';

class LanguagePickerSheet extends StatelessWidget {
  LanguagePickerSheet({super.key});

  @override
  Widget build(BuildContext context) {
    final localeProvider = context.watch<LocaleProvider>();
    final l10n = context.l10n;
    final languages = <({Locale locale, String label})>[
      (locale: Locale('vi'), label: l10n.vietnamese),
      (locale: Locale('en'), label: l10n.english),
      (locale: Locale('ko'), label: l10n.korean),
      (locale: Locale('ja'), label: l10n.japanese),
      (locale: Locale('zh'), label: l10n.simplifiedChinese),
    ];

    return SafeArea(
      child: Padding(
        padding: const EdgeInsets.only(top: 12, bottom: 16),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(
              l10n.chooseLanguage,
              style: Theme.of(context).textTheme.titleLarge,
            ),
            SizedBox(height: 8),
            for (final language in languages)
              RadioListTile<String>(
                value: language.locale.languageCode,
                groupValue: localeProvider.locale.languageCode,
                title: Text(language.label),
                onChanged: (value) async {
                  if (value == null) return;
                  await localeProvider.setLocale(Locale(value));
                  if (context.mounted) Navigator.of(context).pop();
                },
              ),
          ],
        ),
      ),
    );
  }
}
