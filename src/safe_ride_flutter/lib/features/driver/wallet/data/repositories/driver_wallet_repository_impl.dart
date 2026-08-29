import '../../domain/repositories/driver_wallet_repository.dart';
import '../datasources/driver_wallet_remote_datasource.dart';
import '../models/driver_wallet_model.dart';

class DriverWalletRepositoryImpl implements DriverWalletRepository {
  const DriverWalletRepositoryImpl(this._remoteDatasource);
  final DriverWalletRemoteDatasource _remoteDatasource;

  @override
  Future<List<VietnamBankModel>> getVietnamBanks() =>
      _remoteDatasource.getVietnamBanks();

  @override
  Future<DriverWalletModel> getWallet(
    String accessToken, {
    required String period,
  }) => _remoteDatasource.getWallet(accessToken, period: period);

  @override
  Future<void> requestWithdrawal(
    String accessToken, {
    required num amount,
    required String bankName,
    required String bankAccountNumber,
    required String bankAccountName,
  }) => _remoteDatasource.requestWithdrawal(
    accessToken,
    amount: amount,
    bankName: bankName,
    bankAccountNumber: bankAccountNumber,
    bankAccountName: bankAccountName,
  );

  @override
  Future<Map<String, dynamic>> createTopUp(String token, {required num amount}) => _remoteDatasource.createTopUp(token, amount: amount);

  @override
  Future<Map<String, dynamic>> getTopUpStatus(String token, int id) => _remoteDatasource.getTopUpStatus(token, id);
}
