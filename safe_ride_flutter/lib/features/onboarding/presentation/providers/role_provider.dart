import 'package:flutter/material.dart';

import '../../domain/repositories/onboarding_repository.dart';

class RoleProvider extends ChangeNotifier {
  final OnboardingRepository repository;

  RoleProvider(this.repository);

  String? _selectedRole;

  String? get selectedRole => _selectedRole;

  bool _rememberRole = true;

  bool get rememberRole => _rememberRole;

  bool _isLoading = false;

  bool get isLoading => _isLoading;

  Future<void> selectRole(String role) async {
    _isLoading = true;

    notifyListeners();

    try {
      await repository.selectRole(role);

      _selectedRole = role;
    } catch (e) {
      debugPrint(e.toString());
    } finally {
      _isLoading = false;

      notifyListeners();
    }
  }

  void setRememberRole(bool value) {
    _rememberRole = value;

    notifyListeners();
  }
}
