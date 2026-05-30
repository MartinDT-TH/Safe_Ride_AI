// import 'package:flutter/material.dart';
//
// import '../../domain/repositories/auth_repository.dart';
//
// class AuthProvider extends ChangeNotifier {
//   final AuthRepository repository;
//
//   AuthProvider(this.repository);
//
//   bool _isLoading = false;
//
//   bool get isLoading => _isLoading;
//
//   Future<void> login(String phone) async {
//     try {
//       _isLoading = true;
//       notifyListeners();
//
//       await repository.login(phone);
//     } catch(e) {
//       debugPrint(e.toString());
//     } finally {
//       _isLoading = false;
//       notifyListeners();
//     }
//   }
// }


import 'package:flutter/material.dart';

import '../../domain/repositories/auth_repository.dart';

class AuthProvider extends ChangeNotifier {

  final AuthRepository repository;

  AuthProvider(this.repository);

  bool _isLoading = false;

  bool get isLoading => _isLoading;

  String? _token;

  String? get token => _token;

  Future<bool> login(String phone) async {

    try {

      _isLoading = true;

      notifyListeners();

      final response = await repository.login(phone);

      _token = response['data']['token'];

      debugPrint(_token);

      // response.data['success'];
      return true;

    } catch (e) {

      debugPrint(e.toString());

      return false;

    } finally {

      _isLoading = false;

      notifyListeners();
    }
  }
}