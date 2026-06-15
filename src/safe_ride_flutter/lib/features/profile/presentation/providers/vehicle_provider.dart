import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';

import '../../../../core/storage/secure_storage_service.dart';
import '../../data/models/vehicle_model.dart';
import '../../domain/repositories/vehicle_repository.dart';

class VehicleProvider extends ChangeNotifier {
  final VehicleRepository _repository;
  final SecureStorageService _storage;

  VehicleProvider(this._repository, this._storage);

  final List<VehicleModel> _vehicles = [];
  List<VehicleModel> get vehicles => List.unmodifiable(_vehicles);

  bool _isLoading = false;
  bool get isLoading => _isLoading;

  bool _isMutating = false;
  bool get isMutating => _isMutating;

  String? _errorMessage;
  String? get errorMessage => _errorMessage;

  Future<void> loadVehicles() async {
    _isLoading = true;
    _errorMessage = null;
    notifyListeners();
    try {
      final token = await _requireAccessToken();
      _vehicles
        ..clear()
        ..addAll(await _repository.getVehicles(token));
    } catch (error) {
      _errorMessage = _messageFrom(error);
    } finally {
      _isLoading = false;
      notifyListeners();
    }
  }

  Future<bool> saveVehicle(VehicleModel vehicle) async {
    return _mutate(() async {
      final token = await _requireAccessToken();
      final saved = vehicle.id == 0
          ? await _repository.createVehicle(token, vehicle)
          : await _repository.updateVehicle(token, vehicle);
      final index = _vehicles.indexWhere((item) => item.id == saved.id);
      if (index == -1) {
        _vehicles.add(saved);
      } else {
        _vehicles[index] = saved;
      }
      _sortVehicles();
    });
  }

  Future<bool> deleteVehicle(int id) async {
    return _mutate(() async {
      final token = await _requireAccessToken();
      await _repository.deleteVehicle(token, id);
      await _reloadAfterMutation(token);
    });
  }

  Future<bool> _mutate(Future<void> Function() action) async {
    _isMutating = true;
    _errorMessage = null;
    notifyListeners();
    try {
      await action();
      return true;
    } catch (error) {
      _errorMessage = _messageFrom(error);
      return false;
    } finally {
      _isMutating = false;
      notifyListeners();
    }
  }

  Future<void> _reloadAfterMutation(String token) async {
    _vehicles
      ..clear()
      ..addAll(await _repository.getVehicles(token));
  }

  Future<String> _requireAccessToken() async {
    final token = await _storage.readAccessToken();
    if (token == null || token.isEmpty) {
      throw StateError('Phiên đăng nhập đã hết hạn.');
    }
    return token;
  }

  void _sortVehicles() {
    _vehicles.sort((left, right) => left.id.compareTo(right.id));
  }

  String _messageFrom(Object error) {
    if (error is DioException) {
      final data = error.response?.data;
      if (data is Map) {
        return data['message']?.toString() ??
            data['detail']?.toString() ??
            'Không thể xử lý yêu cầu.';
      }
    }
    if (error is StateError) {
      return error.message;
    }
    return 'Không thể kết nối đến máy chủ. Vui lòng thử lại.';
  }
}
