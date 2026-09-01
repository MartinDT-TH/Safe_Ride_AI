import '../../data/models/driver_wallet_model.dart';

abstract class DriverWalletRepository {
  Future<List<VietnamBankModel>> getVietnamBanks();

  Future<DriverWalletModel> getWallet(
    String accessToken, {
    required String period,
  });

  Future<void> requestWithdrawal(
    String accessToken, {
    required num amount,
    required String bankName,
    required String bankAccountNumber,
    required String bankAccountName,
  });
  Future<Map<String, dynamic>> createTopUp(String accessToken, {required num amount});
  Future<Map<String, dynamic>> getTopUpStatus(String accessToken, int id);
}
