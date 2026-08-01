import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../../l10n/generated/app_localizations.dart';

class LocaleProvider extends ChangeNotifier {
  static const _storageKey = 'settings.locale';
  static const defaultLocale = Locale('vi');
  static const supportedLocales = <Locale>[
    Locale('vi'),
    Locale('en'),
    Locale('ko'),
    Locale('ja'),
    Locale('zh'),
  ];

  Locale _locale = defaultLocale;

  static Locale currentLocale = defaultLocale;
  static AppLocalizations get currentLocalizations =>
      lookupAppLocalizations(currentLocale);

  Locale get locale => _locale;

  Future<void> load() async {
    final preferences = await SharedPreferences.getInstance();
    final languageCode = preferences.getString(_storageKey);
    if (languageCode == null || !_isSupported(languageCode)) return;
    _locale = Locale(languageCode);
    currentLocale = _locale;
  }

  Future<void> setLocale(Locale locale) async {
    if (!_isSupported(locale.languageCode) || locale == _locale) return;
    _locale = Locale(locale.languageCode);
    currentLocale = _locale;
    notifyListeners();
    final preferences = await SharedPreferences.getInstance();
    await preferences.setString(_storageKey, _locale.languageCode);
  }

  bool _isSupported(String languageCode) =>
      supportedLocales.any((locale) => locale.languageCode == languageCode);
}
