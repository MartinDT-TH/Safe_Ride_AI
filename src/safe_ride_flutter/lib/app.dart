import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:provider/provider.dart';

import 'core/localization/locale_provider.dart';
import 'core/theme/app_theme.dart';
import 'core/services/connectivity_service.dart';
import 'core/services/socket_service.dart';
import 'core/session/session_coordinator.dart';
import 'dependency_injection/injection.dart';

import 'features/shared/onboarding/presentation/pages/splash_page.dart';
import 'features/auth/presentation/providers/auth_provider.dart';
import 'features/shared/chat/presentation/providers/chat_unread_provider.dart';
import 'features/trip_sharing/trip_share_deep_link_coordinator.dart';
import 'l10n/generated/app_localizations.dart';

class MyApp extends StatefulWidget {
  const MyApp({super.key});

  @override
  State<MyApp> createState() => _MyAppState();
}

class _MyAppState extends State<MyApp> with WidgetsBindingObserver {
  late final ConnectivityService _connectivityService;
  late final SocketService _socketService;
  String? _chatSessionSignature;
  double _lastKeyboardInset = 0;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    _connectivityService = getIt<ConnectivityService>();
    _connectivityService.initialize();
    _socketService = getIt<SocketService>();
    _socketService.addConnectionLostHandler(_handleSocketConnectionLost);
    getIt<SessionCoordinator>().start();
    unawaited(
      getIt<TripShareDeepLinkCoordinator>().start(getIt<AuthProvider>()),
    );
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    _socketService.removeConnectionLostHandler(_handleSocketConnectionLost);
    _connectivityService.dispose();
    unawaited(getIt<TripShareDeepLinkCoordinator>().dispose());
    super.dispose();
  }

  void _handleSocketConnectionLost() {
    unawaited(_connectivityService.handleRealtimeConnectionLost());
  }

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    final auth = context.watch<AuthProvider>();
    final signature = '${auth.token}|${auth.userId}|${auth.roles.join(',')}';
    if (_chatSessionSignature == signature) return;
    _chatSessionSignature = signature;
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) return;
      context.read<ChatUnreadProvider>().updateSession(auth);
    });
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed) {
      SystemChrome.restoreSystemUIOverlays();
      context.read<ChatUnreadProvider>().refresh();
    }
  }

  @override
  void didChangeMetrics() {
    final views = WidgetsBinding.instance.platformDispatcher.views;
    if (views.isEmpty) return;

    final keyboardInset = views.first.viewInsets.bottom;
    final keyboardWasClosed = _lastKeyboardInset > 0 && keyboardInset == 0;
    _lastKeyboardInset = keyboardInset;
    if (!keyboardWasClosed) return;

    Future<void>.delayed(const Duration(seconds: 1), () {
      if (mounted) {
        SystemChrome.restoreSystemUIOverlays();
      }
    });
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
