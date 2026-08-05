import 'package:flutter/material.dart';
import 'dart:async';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:provider/provider.dart';

import 'core/localization/locale_provider.dart';
import 'core/theme/app_theme.dart';
import 'core/services/connectivity_service.dart';
import 'core/session/session_coordinator.dart';
import 'dependency_injection/injection.dart';

import 'features/shared/onboarding/presentation/pages/splash_page.dart';
import 'features/auth/presentation/providers/auth_provider.dart';
import 'features/trip_sharing/trip_share_deep_link_coordinator.dart';
import 'l10n/generated/app_localizations.dart';

class MyApp extends StatefulWidget {
  const MyApp({super.key});

  @override
  State<MyApp> createState() => _MyAppState();
}

class _MyAppState extends State<MyApp> {
  late final ConnectivityService _connectivityService;

  @override
  void initState() {
    super.initState();
    _connectivityService = getIt<ConnectivityService>();
    _connectivityService.initialize();
    getIt<SessionCoordinator>().start();
    unawaited(
      getIt<TripShareDeepLinkCoordinator>().start(getIt<AuthProvider>()),
    );
  }

  @override
  void dispose() {
    _connectivityService.dispose();
    unawaited(getIt<TripShareDeepLinkCoordinator>().dispose());
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final locale = context.watch<LocaleProvider>().locale;
    return MaterialApp(
      debugShowCheckedModeBanner: false,
      onGenerateTitle: (context) => AppLocalizations.of(context).appName,
      theme: AppTheme.lightTheme,
      locale: locale,
      supportedLocales: LocaleProvider.supportedLocales,
      localizationsDelegates: const [
        AppLocalizations.delegate,
        GlobalMaterialLocalizations.delegate,
        GlobalWidgetsLocalizations.delegate,
        GlobalCupertinoLocalizations.delegate,
      ],
      navigatorKey: SessionCoordinator.navigatorKey,
      scaffoldMessengerKey: _connectivityService.messengerKey,
      home: SplashPage(),
    );
  }
}
