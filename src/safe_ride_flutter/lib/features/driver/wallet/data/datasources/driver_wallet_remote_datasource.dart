import 'package:dio/dio.dart';

import '../../../../../core/constants/app_strings.dart';
import '../../../../../core/network/auth_header.dart';
import '../../../../../core/network/dio_client.dart';
import '../../../../../core/localization/locale_provider.dart';
import '../models/driver_wallet_model.dart';

class DriverWalletRemoteDatasource {
  DriverWalletRemoteDatasource({Dio? dio}) : _dio = dio ?? DioClient().dio;
  final Dio _dio;
  List<VietnamBankModel>? _bankCache;

  Future<List<VietnamBankModel>> getVietnamBanks() async {
    final cached = _bankCache;
    if (cached != null) return cached;

    try {
      final response = await _dio.get('https://api.vietqr.io/v2/banks');
      final payload = response.data;
      final data = payload is Map ? payload['data'] : null;
      if (data is! List) {
        throw DriverWalletApiException(
          LocaleProvider.currentLocalizations.bankListLoadFailed,
        );
      }
      final banks =
          data
              .map(
                (item) => VietnamBankModel.fromJson(
                  Map<String, dynamic>.from(item as Map),
                ),
              )
              .where((bank) => bank.code.isNotEmpty && bank.name.isNotEmpty)
              .toList(growable: false)
            ..sort((a, b) => a.shortName.compareTo(b.shortName));
      _bankCache = banks;
      return banks;
    } on DriverWalletApiException {
      rethrow;
    } on DioException {
      throw DriverWalletApiException(
        LocaleProvider.currentLocalizations.bankListLoadFailed,
      );
    }
  }

  Future<DriverWalletModel> getWallet(
    String accessToken, {
    required String period,
  }) async {
    try {
      final response = await _dio.get(
        ApiEndpoints.driverWallet,
        queryParameters: {
          'period': period,
          'utcOffsetMinutes': DateTime.now().timeZoneOffset.inMinutes,
          'recentLimit': 10,
        },
        options: Options(
          headers: {ApiKeys.authorization: AuthHeader.bearer(accessToken)},
        ),
      );
      return DriverWalletModel.fromJson(
        Map<String, dynamic>.from(response.data as Map),
      );
    } on DioException catch (error) {
      throw DriverWalletApiException(_messageFrom(error));
    }
  }

  Future<void> requestWithdrawal(
    String accessToken, {
    required num amount,
    required String bankName,
    required String bankAccountNumber,
    required String bankAccountName,
  }) async {
    try {
      await _dio.post(
        ApiEndpoints.driverWithdrawals,
        data: {
          'amount': amount,
          'bankName': bankName,
          'bankAccountNumber': bankAccountNumber,
          'bankAccountName': bankAccountName,
        },
        options: Options(
          headers: {ApiKeys.authorization: AuthHeader.bearer(accessToken)},
        ),
      );
    } on DioException catch (error) {
      throw DriverWalletApiException(_messageFrom(error));
    }
  }

  static String _messageFrom(DioException error) {
    final data = error.response?.data;
    return data is Map && data['detail'] != null
        ? data['detail'].toString()
        : LocaleProvider.currentLocalizations.genericError;
  }
}

class DriverWalletApiException implements Exception {
  const DriverWalletApiException(this.message);
  final String message;
}
