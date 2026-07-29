import 'package:flutter/foundation.dart';

import '../../data/datasources/driver_wallet_remote_datasource.dart';
import '../../data/models/driver_wallet_model.dart';
import '../../domain/repositories/driver_wallet_repository.dart';

class DriverWalletProvider extends ChangeNotifier {
  DriverWalletProvider(this._repository);
  final DriverWalletRepository _repository;

  DriverWalletModel? _wallet;
  DriverWalletModel? get wallet => _wallet;
  bool _isLoading = false;
  bool get isLoading => _isLoading;
  bool _isSubmitting = false;
  bool get isSubmitting => _isSubmitting;
  String? _errorMessage;
  String? get errorMessage => _errorMessage;
  String _period = 'Week';
  String get period => _period;
  List<VietnamBankModel> _banks = const [];
  List<VietnamBankModel> get banks => _banks;
  bool _isLoadingBanks = false;
  bool get isLoadingBanks => _isLoadingBanks;

  Future<bool> loadBanks() async {
    if (_banks.isNotEmpty) return true;
    _isLoadingBanks = true;
    _errorMessage = null;
    notifyListeners();
    try {
      _banks = await _repository.getVietnamBanks();
      return _banks.isNotEmpty;
    } on DriverWalletApiException catch (error) {
      _errorMessage = error.message;
      return false;
    } catch (_) {
      _errorMessage = 'Không thể tải danh sách ngân hàng.';
      return false;
    } finally {
      _isLoadingBanks = false;
      notifyListeners();
    }
  }

  Future<void> load(String? token, {String? period}) async {
    if (token == null || token.isEmpty) {
      _errorMessage = 'Phiên đăng nhập đã hết hạn.';
      notifyListeners();
      return;
    }
    if (period != null) _period = period;
    _isLoading = true;
    _errorMessage = null;
    notifyListeners();
    try {
      _wallet = await _repository.getWallet(token, period: _period);
    } on DriverWalletApiException catch (error) {
      _errorMessage = error.message;
    } catch (_) {
      _errorMessage = 'Không thể tải dữ liệu Ví.';
    } finally {
      _isLoading = false;
      notifyListeners();
    }
  }

  Future<bool> withdraw(
    String? token, {
    required num amount,
    required String bankName,
    required String bankAccountNumber,
    required String bankAccountName,
  }) async {
    if (token == null || token.isEmpty) return false;
    _isSubmitting = true;
    _errorMessage = null;
    notifyListeners();
    try {
      await _repository.requestWithdrawal(
        token,
        amount: amount,
        bankName: bankName,
        bankAccountNumber: bankAccountNumber,
        bankAccountName: bankAccountName,
      );
      await load(token);
      return true;
    } on DriverWalletApiException catch (error) {
      _errorMessage = error.message;
      return false;
    } catch (_) {
      _errorMessage = 'Không thể gửi yêu cầu rút tiền.';
      return false;
    } finally {
      _isSubmitting = false;
      notifyListeners();
    }
  }
}
