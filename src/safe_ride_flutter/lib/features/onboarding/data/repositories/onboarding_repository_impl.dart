import '../../domain/repositories/onboarding_repository.dart';

import '../datasources/onboarding_remote_datasource.dart';

class OnboardingRepositoryImpl implements OnboardingRepository {
  final OnboardingRemoteDatasource datasource;

  OnboardingRepositoryImpl(this.datasource);

  @override
  Future<void> selectRole(String role) {
    return datasource.selectRole(role);
  }
}
