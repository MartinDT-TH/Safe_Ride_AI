import 'package:flutter/material.dart';

import 'core/theme/app_theme.dart';

import 'features/auth/presentation/pages/login_page.dart';

class MyApp extends StatelessWidget {
  const MyApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      debugShowCheckedModeBanner: false,
      title: 'SafeRide',
      theme: AppTheme.lightTheme,
      home: const LoginPage(),
    );
  }
}