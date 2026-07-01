import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';

import '../config/mobile_config.dart';
import '../constants/app_strings.dart';

class MobileConfigService {
  MobileConfigService({Dio? dio})
    : _dio =
          dio ??
          Dio(
            BaseOptions(
              baseUrl: AppConfig.apiBaseUrl,
              connectTimeout: const Duration(seconds: 5),
              receiveTimeout: const Duration(seconds: 10),
            ),
          );

  final Dio _dio;
  MobileConfig _config = MobileConfig.fallback;
  bool _loadedFromRemote = false;

  MobileConfig get config => _config;

  bool get loadedFromRemote => _loadedFromRemote;

  Future<void> load({bool force = false}) async {
    if (_loadedFromRemote && !force) {
      return;
    }

    try {
      final response = await _dio.get('/mobile-config');
      final data = response.data;
      if (data is Map) {
        _config = MobileConfig.fromJson(Map<String, dynamic>.from(data));
        _loadedFromRemote = true;
      }
    } catch (error) {
      _config = MobileConfig.fallback;
      _loadedFromRemote = false;
      debugPrint('Mobile config fallback active: $error');
    }
  }
}
