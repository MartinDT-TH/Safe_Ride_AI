import 'package:flutter/material.dart';
import 'dart:async';

import 'core/constants/app_strings.dart';
import 'core/theme/app_theme.dart';
import 'core/services/connectivity_service.dart';
import 'core/session/session_coordinator.dart';
import 'dependency_injection/injection.dart';

import 'features/shared/onboarding/presentation/pages/splash_page.dart';
import 'features/auth/presentation/providers/auth_provider.dart';
import 'features/trip_sharing/trip_share_deep_link_coordinator.dart';

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
    return MaterialApp(
      debugShowCheckedModeBanner: false,
      title: AppStrings.appName,
      theme: AppTheme.lightTheme,
      navigatorKey: SessionCoordinator.navigatorKey,
      scaffoldMessengerKey: _connectivityService.messengerKey,
      home: const SplashPage(),
    );
  }
}
