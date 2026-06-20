import 'package:get_it/get_it.dart';

import '../core/services/connectivity_service.dart';
import '../core/services/device_identity_service.dart';
import '../core/services/location_service.dart';
import '../core/services/socket_service.dart';
import '../core/storage/secure_storage_service.dart';
import '../features/auth/data/datasources/auth_remote_datasource.dart';
import '../features/auth/data/repositories/auth_repository_impl.dart';
import '../features/auth/domain/repositories/auth_repository.dart';
import '../features/auth/presentation/providers/auth_provider.dart';

import '../features/shared/onboarding/data/datasources/onboarding_remote_datasource.dart';
import '../features/shared/onboarding/data/repositories/onboarding_repository_impl.dart';
import '../features/shared/onboarding/domain/repositories/onboarding_repository.dart';
import '../features/shared/onboarding/presentation/providers/role_provider.dart';

import '../features/customer/home/data/datasources/home_remote_datasource.dart';
import '../features/customer/home/data/repositories/home_repository_impl.dart';
import '../features/customer/home/domain/repositories/home_repository.dart';
import '../features/customer/home/presentation/providers/home_provider.dart';
import '../features/customer/booking/data/datasources/booking_catalog_datasource.dart';
import '../features/customer/booking/data/datasources/booking_remote_datasource.dart';
import '../features/customer/booking/data/repositories/booking_repository_impl.dart';
import '../features/customer/booking/domain/repositories/booking_repository.dart';
import '../features/customer/booking/presentation/providers/booking_provider.dart';
import '../features/shared/profile/data/datasources/vehicle_remote_datasource.dart';
import '../features/shared/profile/data/repositories/vehicle_repository_impl.dart';
import '../features/shared/profile/domain/repositories/vehicle_repository.dart';
import '../features/shared/profile/presentation/providers/vehicle_provider.dart';
import '../features/shared/history/presentation/providers/history_provider.dart';
import '../features/driver/dashboard/presentation/providers/driver_dashboard_provider.dart';
import '../features/driver/registration/data/datasources/identity_verification_remote_datasource.dart';

final getIt = GetIt.instance;

Future<void> setupDependencies() async {
  getIt.registerLazySingleton<SecureStorageService>(
    () => SecureStorageService(),
  );

  getIt.registerLazySingleton<DeviceIdentityService>(
    () => DeviceIdentityService(getIt<SecureStorageService>()),
  );
  getIt.registerLazySingleton<LocationService>(() => LocationService());
  getIt.registerLazySingleton<SocketService>(() => SocketService());
  getIt.registerLazySingleton<ConnectivityService>(() => ConnectivityService());

  getIt.registerLazySingleton<AuthRemoteDatasource>(
    () => AuthRemoteDatasource(),
  );

  getIt.registerLazySingleton<AuthRepository>(
    () => AuthRepositoryImpl(getIt<AuthRemoteDatasource>()),
  );

  getIt.registerLazySingleton<AuthProvider>(
    () => AuthProvider(
      getIt<AuthRepository>(),
      getIt<SecureStorageService>(),
      getIt<DeviceIdentityService>(),
    ),
  );

  getIt.registerLazySingleton<OnboardingRemoteDatasource>(
    () => OnboardingRemoteDatasource(),
  );

  getIt.registerLazySingleton<OnboardingRepository>(
    () => OnboardingRepositoryImpl(getIt<OnboardingRemoteDatasource>()),
  );

  getIt.registerFactory<RoleProvider>(
    () => RoleProvider(getIt<OnboardingRepository>()),
  );

  getIt.registerLazySingleton<HomeRemoteDatasource>(
    () => HomeRemoteDatasource(),
  );

  getIt.registerLazySingleton<HomeRepository>(
    () => HomeRepositoryImpl(getIt<HomeRemoteDatasource>()),
  );

  getIt.registerFactory<HomeProvider>(
    () => HomeProvider(getIt<HomeRepository>()),
  );

  getIt.registerLazySingleton<BookingRemoteDatasource>(
    () => BookingRemoteDatasource(),
  );
  getIt.registerLazySingleton<BookingCatalogDatasource>(
    () => BookingCatalogDatasource(),
  );
  getIt.registerLazySingleton<BookingRepository>(
    () => BookingRepositoryImpl(
      getIt<BookingRemoteDatasource>(),
      getIt<BookingCatalogDatasource>(),
    ),
  );
  getIt.registerFactory<BookingProvider>(
    () => BookingProvider(getIt<BookingRepository>(), getIt<LocationService>()),
  );

  getIt.registerLazySingleton<VehicleRemoteDatasource>(
    () => VehicleRemoteDatasource(),
  );

  getIt.registerLazySingleton<VehicleRepository>(
    () => VehicleRepositoryImpl(getIt<VehicleRemoteDatasource>()),
  );

  getIt.registerFactory<VehicleProvider>(
    () => VehicleProvider(
      getIt<VehicleRepository>(),
      getIt<SecureStorageService>(),
    ),
  );

  getIt.registerFactory<HistoryProvider>(() => HistoryProvider());

  getIt.registerFactory<DriverDashboardProvider>(
    () => DriverDashboardProvider(),
  );

  getIt.registerLazySingleton<IdentityVerificationRemoteDatasource>(
    () => IdentityVerificationRemoteDatasource(),
  );
}
