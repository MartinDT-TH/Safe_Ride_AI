class DriverWalletModel {
  const DriverWalletModel({
    required this.availableBalance,
    required this.income,
    required this.recentTransactions,
    this.savedBankAccount,
  });

  final num availableBalance;
  final WalletIncomeModel income;
  final List<WalletTransactionModel> recentTransactions;
  final SavedBankAccountModel? savedBankAccount;

  factory DriverWalletModel.fromJson(Map<String, dynamic> json) {
    return DriverWalletModel(
      availableBalance: json['availableBalance'] as num? ?? 0,
      income: WalletIncomeModel.fromJson(
        Map<String, dynamic>.from(json['income'] as Map),
      ),
      recentTransactions: (json['recentTransactions'] as List? ?? const [])
          .map((item) => WalletTransactionModel.fromJson(
                Map<String, dynamic>.from(item as Map),
              ))
          .toList(growable: false),
      savedBankAccount: json['savedBankAccount'] is Map
          ? SavedBankAccountModel.fromJson(
              Map<String, dynamic>.from(json['savedBankAccount'] as Map),
            )
          : null,
    );
  }
}

class WalletIncomeModel {
  const WalletIncomeModel({
    required this.period,
    required this.total,
    required this.changePercentage,
    required this.chart,
  });
  final String period;
  final num total;
  final num? changePercentage;
  final List<WalletChartPointModel> chart;

  factory WalletIncomeModel.fromJson(Map<String, dynamic> json) {
    return WalletIncomeModel(
      period: json['period']?.toString() ?? 'Week',
      total: json['total'] as num? ?? 0,
      changePercentage: json['changePercentage'] as num?,
      chart: (json['chart'] as List? ?? const [])
          .map((item) => WalletChartPointModel.fromJson(
                Map<String, dynamic>.from(item as Map),
              ))
          .toList(growable: false),
    );
  }
}

class WalletChartPointModel {
  const WalletChartPointModel({required this.label, required this.amount});
  final String label;
  final num amount;

  factory WalletChartPointModel.fromJson(Map<String, dynamic> json) =>
      WalletChartPointModel(
        label: json['label']?.toString() ?? '',
        amount: json['amount'] as num? ?? 0,
      );
}

class WalletTransactionModel {
  const WalletTransactionModel({
    required this.tripId,
    required this.type,
    required this.amount,
    required this.isCredit,
    required this.title,
    required this.createdAt,
  });
  final int? tripId;
  final String type;
  final num amount;
  final bool isCredit;
  final String title;
  final DateTime createdAt;

  factory WalletTransactionModel.fromJson(Map<String, dynamic> json) =>
      WalletTransactionModel(
        tripId: (json['tripId'] as num?)?.toInt(),
        type: json['type']?.toString() ?? '',
        amount: json['amount'] as num? ?? 0,
        isCredit: json['isCredit'] == true,
        title: json['title']?.toString() ?? 'Giao dịch Ví',
        createdAt: DateTime.tryParse(json['createdAt']?.toString() ?? '')
                ?.toLocal() ??
            DateTime.now(),
      );
}

class SavedBankAccountModel {
  const SavedBankAccountModel({
    required this.bankName,
    required this.bankAccountNumber,
    required this.bankAccountName,
  });
  final String bankName;
  final String bankAccountNumber;
  final String bankAccountName;

  factory SavedBankAccountModel.fromJson(Map<String, dynamic> json) =>
      SavedBankAccountModel(
        bankName: json['bankName']?.toString() ?? '',
        bankAccountNumber: json['bankAccountNumber']?.toString() ?? '',
        bankAccountName: json['bankAccountName']?.toString() ?? '',
      );
}

class VietnamBankModel {
  const VietnamBankModel({
    required this.bin,
    required this.code,
    required this.shortName,
    required this.name,
    required this.logo,
  });
  final String bin;
  final String code;
  final String shortName;
  final String name;
  final String logo;

  factory VietnamBankModel.fromJson(Map<String, dynamic> json) =>
      VietnamBankModel(
        bin: json['bin']?.toString() ?? '',
        code: json['code']?.toString() ?? '',
        shortName: json['shortName']?.toString() ?? '',
        name: json['name']?.toString() ?? '',
        logo: json['logo']?.toString() ?? '',
      );
}
