import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:provider/provider.dart';
import 'package:intl/date_symbol_data_local.dart';

import 'app.dart';

import 'dependency_injection/injection.dart';

import 'core/services/mobile_config_service.dart';
import 'features/auth/presentation/providers/auth_provider.dart';
import 'features/shared/onboarding/presentation/providers/role_provider.dart';
import 'features/customer/home/presentation/providers/home_provider.dart';
import 'features/customer/booking/presentation/providers/booking_provider.dart';
import 'features/shared/profile/presentation/providers/vehicle_provider.dart';
import 'features/shared/history/presentation/providers/history_provider.dart';
import 'features/driver/dashboard/presentation/providers/driver_dashboard_provider.dart';
import 'features/shared/chat/presentation/providers/trip_chat_provider.dart';

void main() async {
  WidgetsFlutterBinding.ensureInitialized();

  await SystemChrome.setEnabledSystemUIMode(SystemUiMode.immersiveSticky);

  await initializeDateFormatting('vi_VN', null);
  await setupDependencies();
  await getIt<MobileConfigService>().load();

  runApp(
    MultiProvider(
      providers: [
        ChangeNotifierProvider(create: (_) => getIt<AuthProvider>()),
        ChangeNotifierProvider(create: (_) => getIt<RoleProvider>()),
        ChangeNotifierProvider(create: (_) => getIt<HomeProvider>()),
        ChangeNotifierProvider(create: (_) => getIt<BookingProvider>()),
        ChangeNotifierProvider(create: (_) => getIt<VehicleProvider>()),
        ChangeNotifierProvider(create: (_) => getIt<HistoryProvider>()),
        ChangeNotifierProvider(create: (_) => getIt<DriverDashboardProvider>()),
        ChangeNotifierProvider(create: (_) => getIt<TripChatProvider>()),
      ],
      child: const MyApp(),
    ),
  );
}
