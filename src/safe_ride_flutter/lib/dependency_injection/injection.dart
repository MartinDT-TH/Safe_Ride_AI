import 'package:get_it/get_it.dart';

import '../core/services/device_identity_service.dart';
import '../core/services/location_service.dart';
import '../core/storage/secure_storage_service.dart';
import '../features/auth/data/datasources/auth_remote_datasource.dart';
import '../features/auth/data/repositories/auth_repository_impl.dart';
import '../features/auth/domain/repositories/auth_repository.dart';
import '../features/auth/presentation/providers/auth_provider.dart';

import '../features/onboarding/data/datasources/onboarding_remote_datasource.dart';
import '../features/onboarding/data/repositories/onboarding_repository_impl.dart';
import '../features/onboarding/domain/repositories/onboarding_repository.dart';
import '../features/onboarding/presentation/providers/role_provider.dart';

import '../features/home/data/datasources/home_remote_datasource.dart';
import '../features/home/data/repositories/home_repository_impl.dart';
import '../features/home/domain/repositories/home_repository.dart';
import '../features/home/presentation/providers/home_provider.dart';
import '../features/booking/data/datasources/booking_catalog_datasource.dart';
import '../features/booking/data/datasources/booking_remote_datasource.dart';
import '../features/booking/data/repositories/booking_repository_impl.dart';
import '../features/booking/domain/repositories/booking_repository.dart';
import '../features/booking/presentation/providers/booking_provider.dart';

final getIt = GetIt.instance;

Future<void> setupDependencies() async {
  getIt.registerLazySingleton<SecureStorageService>(
    () => SecureStorageService(),
  );

  getIt.registerLazySingleton<DeviceIdentityService>(
    () => DeviceIdentityService(getIt<SecureStorageService>()),
  );
  getIt.registerLazySingleton<LocationService>(() => LocationService());

  getIt.registerLazySingleton<AuthRemoteDatasource>(
    () => AuthRemoteDatasource(),
  );

  getIt.registerLazySingleton<AuthRepository>(
    () => AuthRepositoryImpl(getIt<AuthRemoteDatasource>()),
  );

  getIt.registerFactory<AuthProvider>(
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
}
