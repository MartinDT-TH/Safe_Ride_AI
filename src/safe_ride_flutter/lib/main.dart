import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:intl/date_symbol_data_local.dart';

import 'app.dart';

import 'dependency_injection/injection.dart';

import 'features/auth/presentation/providers/auth_provider.dart';
import 'features/onboarding/presentation/providers/role_provider.dart';
import 'features/home/presentation/providers/home_provider.dart';
import 'features/booking/presentation/providers/booking_provider.dart';
import 'features/profile/presentation/providers/vehicle_provider.dart';
import 'features/activity/presentation/providers/activity_provider.dart';

void main() async {
  WidgetsFlutterBinding.ensureInitialized();

  await initializeDateFormatting('vi_VN', null);
  await setupDependencies();

  runApp(
    MultiProvider(
      providers: [
        ChangeNotifierProvider(create: (_) => getIt<AuthProvider>()),
        ChangeNotifierProvider(create: (_) => getIt<AuthProvider>()),

        ChangeNotifierProvider(create: (_) => getIt<RoleProvider>()),
        ChangeNotifierProvider(create: (_) => getIt<RoleProvider>()),

        ChangeNotifierProvider(create: (_) => getIt<HomeProvider>()),
        ChangeNotifierProvider(create: (_) => getIt<BookingProvider>()),
        ChangeNotifierProvider(create: (_) => getIt<HomeProvider>()),

        ChangeNotifierProvider(create: (_) => getIt<VehicleProvider>()),
        ChangeNotifierProvider(create: (_) => getIt<ActivityProvider>()),
      ],
      child: const MyApp(),
    ),
  );
}

