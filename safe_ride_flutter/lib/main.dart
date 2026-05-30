import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import 'app.dart';

import 'dependency_injection/injection.dart';

import 'features/auth/presentation/providers/auth_provider.dart';
import 'features/onboarding/presentation/providers/role_provider.dart';
import 'features/home/presentation/providers/home_provider.dart';

void main() async {
  WidgetsFlutterBinding.ensureInitialized();

  await setupDependencies();

  runApp(
    MultiProvider(
      providers: [
        ChangeNotifierProvider(
          create: (_) => getIt<AuthProvider>(),
        ),

        ChangeNotifierProvider(
            create: (_) => getIt<RoleProvider>()
        ),

        ChangeNotifierProvider(
            create: (_) => getIt<HomeProvider>()
        ),
      ],
      child: const MyApp(),
    ),
  );
}