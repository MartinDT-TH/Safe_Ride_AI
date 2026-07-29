import 'package:fl_chart/fl_chart.dart';
import 'package:cached_network_image/cached_network_image.dart';
import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:provider/provider.dart';

import '../../../../auth/presentation/providers/auth_provider.dart';
import '../../data/models/driver_wallet_model.dart';
import '../providers/driver_wallet_provider.dart';

const _teal = Color(0xFF007985);
const _ink = Color(0xFF202020);
const _muted = Color(0xFF687174);
const _border = Color(0xFFB9C9CC);

class DriverWalletPage extends StatefulWidget {
  const DriverWalletPage({super.key});

  @override
  State<DriverWalletPage> createState() => _DriverWalletPageState();
}

class _DriverWalletPageState extends State<DriverWalletPage> {
  int _period = 1;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _loadWallet());
  }

  Future<void> _loadWallet() => context.read<DriverWalletProvider>().load(
        context.read<AuthProvider>().token,
        period: const ['Day', 'Week', 'Month'][_period],
      );

  Future<void> _openWithdrawalForm() async {
    final token = context.read<AuthProvider>().token;
    if (token == null || token.isEmpty) {
      _showMessage('Phiên đăng nhập đã hết hạn.');
      return;
    }

    final provider = context.read<DriverWalletProvider>();
    final loaded = await provider.loadBanks();
    if (!mounted) return;
    if (!loaded) {
      _showMessage(
        provider.errorMessage ?? 'Không thể tải danh sách ngân hàng.',
      );
      return;
    }

    final created = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      useSafeArea: true,
      backgroundColor: Colors.transparent,
      builder: (_) => _WithdrawalSheet(
        savedAccount:
            provider.wallet?.savedBankAccount,
        banks: provider.banks,
        onSubmit: ({
          required amount,
          required bankName,
          required bankAccountNumber,
          required bankAccountName,
        }) => context.read<DriverWalletProvider>().withdraw(
              token,
              amount: amount,
              bankName: bankName,
              bankAccountNumber: bankAccountNumber,
              bankAccountName: bankAccountName,
            ),
      ),
    );
    if (created == true && mounted) {
      _showMessage('Đã gửi yêu cầu rút tiền.');
    }
  }

  void _showMessage(String message) {
    ScaffoldMessenger.of(context)
      ..hideCurrentSnackBar()
      ..showSnackBar(SnackBar(content: Text(message)));
  }

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<DriverWalletProvider>();
    final wallet = provider.wallet;
    return ColoredBox(
      color: const Color(0xFFFCF9F9),
      child: SafeArea(
        bottom: false,
        child: RefreshIndicator(
          onRefresh: _loadWallet,
          color: _teal,
          child: SingleChildScrollView(
          physics: const BouncingScrollPhysics(),
          padding: const EdgeInsets.fromLTRB(20, 18, 20, 30),
          child: Center(
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 680),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const _Header(),
                  const SizedBox(height: 24),
                  if (wallet == null && provider.errorMessage == null)
                    const Center(
                      child: Padding(
                        padding: EdgeInsets.all(48),
                        child: CircularProgressIndicator(color: _teal),
                      ),
                    )
                  else if (provider.errorMessage != null && wallet == null)
                    _WalletError(
                      message: provider.errorMessage!,
                      onRetry: _loadWallet,
                    )
                  else ...[
                  _BalanceCard(
                    balance: wallet?.availableBalance ?? 0,
                    onWithdraw: _openWithdrawalForm,
                  ),
                  const SizedBox(height: 30),
                  _IncomeHeader(
                    selected: _period,
                    onChanged: (value) {
                      if (value == _period) return;
                      setState(() => _period = value);
                      _loadWallet();
                    },
                  ),
                  const SizedBox(height: 16),
                  _IncomeCard(
                    key: ValueKey(_period),
                    income: wallet!.income,
                  ),
                  const SizedBox(height: 26),
                  const _RecentHeader(),
                  const SizedBox(height: 14),
                  _Transactions(items: wallet.recentTransactions),
                  ],
                ],
              ),
            ),
          ),
          ),
        ),
      ),
    );
  }
}

class _Header extends StatelessWidget {
  const _Header();

  @override
  Widget build(BuildContext context) {
    final avatar = context.select<AuthProvider, String?>((p) => p.avatarUrl);
    final hasAvatar = avatar?.trim().isNotEmpty == true;
    return Row(
      children: [
        CircleAvatar(
          radius: 21,
          backgroundColor: const Color(0xFFE6EEEE),
          backgroundImage: hasAvatar ? NetworkImage(avatar!) : null,
          child: hasAvatar
              ? null
              : const Icon(Icons.person_rounded, color: _teal),
        ),
        const SizedBox(width: 12),
        const Expanded(
          child: Text(
            'Ví của tôi',
            style: TextStyle(
              fontSize: 27,
              fontWeight: FontWeight.w800,
              color: _ink,
            ),
          ),
        ),
        Material(
          color: const Color(0xFFF0EEEE),
          shape: const CircleBorder(),
          child: IconButton(
            onPressed: () {},
            icon: const Icon(Icons.notifications_rounded, color: _teal),
          ),
        ),
      ],
    );
  }
}

class _BalanceCard extends StatelessWidget {
  const _BalanceCard({required this.balance, required this.onWithdraw});
  final num balance;
  final VoidCallback onWithdraw;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.fromLTRB(24, 27, 24, 24),
      decoration: BoxDecoration(
        color: const Color(0xFF138C99),
        borderRadius: BorderRadius.circular(24),
        boxShadow: const [
          BoxShadow(
            color: Color(0x26000000),
            blurRadius: 15,
            offset: Offset(0, 8),
          ),
        ],
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            'SỐ DƯ KHẢ DỤNG',
            style: TextStyle(
              color: Color(0xFFD0E6E8),
              fontSize: 16,
              letterSpacing: 1.1,
            ),
          ),
          const SizedBox(height: 7),
          Text(
            _formatMoney(balance),
            style: TextStyle(
              color: Colors.white,
              fontSize: 32,
              fontWeight: FontWeight.w800,
            ),
          ),
          const SizedBox(height: 25),
          Row(
            children: [
              Expanded(
                child: _ActionButton(
                  icon: Icons.payments_outlined,
                  label: 'Rút tiền',
                  filled: true,
                  onPressed: onWithdraw,
                ),
              ),
              const SizedBox(width: 16),
              Expanded(
                child: _ActionButton(
                  icon: Icons.account_balance_rounded,
                  label: 'Nạp thẻ',
                  filled: false,
                  onPressed: () {},
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _ActionButton extends StatelessWidget {
  const _ActionButton({
    required this.icon,
    required this.label,
    required this.filled,
    required this.onPressed,
  });
  final IconData icon;
  final String label;
  final bool filled;
  final VoidCallback onPressed;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 48,
      child: OutlinedButton.icon(
        onPressed: onPressed,
        icon: Icon(icon, size: 22),
        label: Text(label),
        style: OutlinedButton.styleFrom(
          padding: const EdgeInsets.symmetric(horizontal: 8),
          backgroundColor: filled ? Colors.white : const Color(0xFF006C76),
          foregroundColor: filled ? _teal : Colors.white,
          side: BorderSide(
            color: filled ? Colors.white : const Color(0xFF3B969D),
          ),
          textStyle: const TextStyle(fontSize: 16, fontWeight: FontWeight.w700),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(13),
          ),
        ),
      ),
    );
  }
}

class _IncomeHeader extends StatelessWidget {
  const _IncomeHeader({required this.selected, required this.onChanged});
  final int selected;
  final ValueChanged<int> onChanged;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        const Expanded(
          child: Text(
            'Thu nhập',
            style: TextStyle(
              fontSize: 25,
              fontWeight: FontWeight.w800,
              color: _ink,
            ),
          ),
        ),
        Container(
          padding: const EdgeInsets.all(4),
          decoration: BoxDecoration(
            color: const Color(0xFFF0EEEE),
            borderRadius: BorderRadius.circular(11),
          ),
          child: Row(
            children: List.generate(3, (index) {
              final active = index == selected;
              return GestureDetector(
                onTap: () => onChanged(index),
                child: AnimatedContainer(
                  duration: const Duration(milliseconds: 180),
                  padding: const EdgeInsets.symmetric(
                    horizontal: 15,
                    vertical: 9,
                  ),
                  decoration: BoxDecoration(
                    color: active ? Colors.white : Colors.transparent,
                    borderRadius: BorderRadius.circular(8),
                  ),
                  child: Text(
                    const ['Ngày', 'Tuần', 'Tháng'][index],
                    style: const TextStyle(fontWeight: FontWeight.w700),
                  ),
                ),
              );
            }),
          ),
        ),
      ],
    );
  }
}

class _IncomeCard extends StatelessWidget {
  const _IncomeCard({super.key, required this.income});
  final WalletIncomeModel income;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.fromLTRB(16, 20, 16, 13),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: _border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            'Tổng thu nhập\n${_periodLabel(income.period)}',
            style: TextStyle(fontSize: 18, height: 1.45, color: _muted),
          ),
          const SizedBox(height: 4),
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                _formatMoney(income.total),
                style: TextStyle(
                  fontSize: 23,
                  fontWeight: FontWeight.w800,
                  color: _teal,
                ),
              ),
              DecoratedBox(
                decoration: BoxDecoration(
                  color: Color(0xFFF0EEEE),
                  borderRadius: BorderRadius.all(Radius.circular(7)),
                ),
                child: Padding(
                  padding: EdgeInsets.symmetric(horizontal: 11, vertical: 7),
                  child: Text(
                    _changeLabel(income.changePercentage),
                    textAlign: TextAlign.center,
                    style: TextStyle(fontSize: 15, height: 1.35),
                  ),
                ),
              ),
            ],
          ),
          SizedBox(
            height: 160,
            child: BarChart(
              BarChartData(
                minY: 0,
                maxY: _maxChartValue(income.chart),
                alignment: BarChartAlignment.spaceAround,
                gridData: const FlGridData(show: false),
                borderData: FlBorderData(
                  show: true,
                  border: const Border(
                    bottom: BorderSide(color: Color(0xFFE4E4E4)),
                  ),
                ),
                barTouchData: BarTouchData(enabled: false),
                titlesData: FlTitlesData(
                  topTitles: const AxisTitles(
                    sideTitles: SideTitles(showTitles: false),
                  ),
                  leftTitles: const AxisTitles(
                    sideTitles: SideTitles(showTitles: false),
                  ),
                  rightTitles: const AxisTitles(
                    sideTitles: SideTitles(showTitles: false),
                  ),
                  bottomTitles: AxisTitles(
                    sideTitles: SideTitles(
                      showTitles: true,
                      reservedSize: 34,
                      getTitlesWidget: (value, meta) {
                        final index = value.toInt();
                        return Padding(
                          padding: const EdgeInsets.only(top: 8),
                          child: Text(
                            income.chart[index].label,
                            style: TextStyle(
                              fontSize: 16,
                              fontWeight: FontWeight.w500,
                              color: _muted,
                            ),
                          ),
                        );
                      },
                    ),
                  ),
                ),
                barGroups: List.generate(
                  income.chart.length,
                  (index) => BarChartGroupData(
                    x: index,
                    barRods: [
                      BarChartRodData(
                        toY: income.chart[index].amount.toDouble(),
                        width: 30,
                        color: _teal,
                        borderRadius: const BorderRadius.vertical(
                          top: Radius.circular(6),
                        ),
                      ),
                    ],
                  ),
                ),
              ),
              duration: const Duration(milliseconds: 700),
              curve: Curves.easeOutCubic,
            ),
          ),
        ],
      ),
    );
  }
}

class _RecentHeader extends StatelessWidget {
  const _RecentHeader();

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: [
        const Text(
          'Giao dịch gần đây',
          style: TextStyle(fontSize: 24, fontWeight: FontWeight.w800),
        ),
        TextButton(
          onPressed: () {},
          child: const Text(
            'Xem tất cả',
            style: TextStyle(color: _teal, fontWeight: FontWeight.w700),
          ),
        ),
      ],
    );
  }
}

class _WithdrawalSheet extends StatefulWidget {
  const _WithdrawalSheet({
    required this.savedAccount,
    required this.banks,
    required this.onSubmit,
  });

  final SavedBankAccountModel? savedAccount;
  final List<VietnamBankModel> banks;
  final Future<bool> Function({
    required num amount,
    required String bankName,
    required String bankAccountNumber,
    required String bankAccountName,
  }) onSubmit;

  @override
  State<_WithdrawalSheet> createState() => _WithdrawalSheetState();
}

class _WithdrawalSheetState extends State<_WithdrawalSheet> {
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _amountController;
  late final TextEditingController _accountNumberController;
  late final TextEditingController _accountNameController;
  String? _selectedBank;
  bool _submitting = false;

  @override
  void initState() {
    super.initState();
    final saved = widget.savedAccount;
    final savedBank = saved?.bankName;
    _selectedBank = widget.banks.any((bank) => bank.shortName == savedBank)
        ? savedBank
        : null;
    _amountController = TextEditingController();
    _accountNumberController = TextEditingController(
      text: saved?.bankAccountNumber ?? '',
    );
    _accountNameController = TextEditingController(
      text: saved?.bankAccountName ?? '',
    );
  }

  @override
  void dispose() {
    _amountController.dispose();
    _accountNumberController.dispose();
    _accountNameController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate() || _submitting) return;
    setState(() => _submitting = true);
    try {
      final amount = int.parse(_amountController.text.replaceAll(',', ''));
      final success = await widget.onSubmit(
        amount: amount,
        bankName: _selectedBank!,
        bankAccountNumber: _accountNumberController.text.trim(),
        bankAccountName: _accountNameController.text.trim().toUpperCase(),
      );
      if (!mounted) return;
      if (success) {
        Navigator.of(context).pop(true);
      } else {
        final message = context.read<DriverWalletProvider>().errorMessage ??
            'Không thể gửi yêu cầu rút tiền.';
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(message)),
        );
      }
    } finally {
      if (mounted) setState(() => _submitting = false);
    }
  }

  VietnamBankModel? get _selectedBankModel {
    for (final bank in widget.banks) {
      if (bank.shortName == _selectedBank) return bank;
    }
    return null;
  }

  Future<VietnamBankModel?> _pickBank() {
    return showModalBottomSheet<VietnamBankModel>(
      context: context,
      isScrollControlled: true,
      useSafeArea: true,
      backgroundColor: Colors.transparent,
      builder: (_) => _BankPickerSheet(
        banks: widget.banks,
        selectedCode: _selectedBankModel?.code,
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: const BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.vertical(top: Radius.circular(24)),
      ),
      padding: EdgeInsets.fromLTRB(
        20,
        12,
        20,
        20 + MediaQuery.viewInsetsOf(context).bottom,
      ),
      child: SingleChildScrollView(
        child: Form(
          key: _formKey,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Center(
                child: Container(
                  width: 42,
                  height: 4,
                  decoration: BoxDecoration(
                    color: const Color(0xFFD7D7D7),
                    borderRadius: BorderRadius.circular(2),
                  ),
                ),
              ),
              const SizedBox(height: 20),
              const Text(
                'Rút tiền về ngân hàng',
                style: TextStyle(fontSize: 22, fontWeight: FontWeight.w800),
              ),
              const SizedBox(height: 6),
              Text(
                widget.savedAccount == null
                    ? 'Thông tin này sẽ được lưu cho lần rút tiếp theo.'
                    : 'Tài khoản gần nhất đã được điền sẵn.',
                style: const TextStyle(color: _muted),
              ),
              const SizedBox(height: 20),
              FormField<String>(
                initialValue: _selectedBank,
                validator: (value) => value == null
                    ? 'Vui lòng chọn ngân hàng'
                    : null,
                builder: (field) {
                  final selected = _selectedBankModel;
                  return InkWell(
                    borderRadius: BorderRadius.circular(12),
                    onTap: () async {
                      final bank = await _pickBank();
                      if (bank == null || !mounted) return;
                      setState(() => _selectedBank = bank.shortName);
                      field.didChange(bank.shortName);
                    },
                    child: InputDecorator(
                      decoration: _inputDecoration('Ngân hàng').copyWith(
                        errorText: field.errorText,
                      ),
                      isEmpty: selected == null,
                      child: selected == null
                          ? const Text(
                              'Tìm và chọn ngân hàng',
                              style: TextStyle(color: _muted),
                            )
                          : Row(
                              children: [
                                _BankLogo(bank: selected, size: 32),
                                const SizedBox(width: 10),
                                Expanded(
                                  child: Text(
                                    selected.shortName,
                                    maxLines: 1,
                                    overflow: TextOverflow.ellipsis,
                                    style: const TextStyle(
                                      fontWeight: FontWeight.w700,
                                    ),
                                  ),
                                ),
                                const Icon(
                                  Icons.keyboard_arrow_down_rounded,
                                  color: _muted,
                                ),
                              ],
                            ),
                    ),
                  );
                },
              ),
              const SizedBox(height: 14),
              TextFormField(
                controller: _accountNumberController,
                keyboardType: TextInputType.number,
                decoration: _inputDecoration('Số tài khoản'),
                validator: (value) {
                  final number = value?.trim() ?? '';
                  if (!RegExp(r'^\d{4,50}$').hasMatch(number)) {
                    return 'Số tài khoản không hợp lệ';
                  }
                  return null;
                },
              ),
              const SizedBox(height: 14),
              TextFormField(
                controller: _accountNameController,
                textCapitalization: TextCapitalization.characters,
                decoration: _inputDecoration('Tên chủ tài khoản'),
                validator: (value) => value == null || value.trim().isEmpty
                    ? 'Vui lòng nhập tên chủ tài khoản'
                    : null,
              ),
              const SizedBox(height: 14),
              TextFormField(
                controller: _amountController,
                keyboardType: TextInputType.number,
                decoration: _inputDecoration('Số tiền muốn rút').copyWith(
                  prefixText: '₫  ',
                ),
                validator: (value) {
                  final amount = int.tryParse(
                    (value ?? '').replaceAll(',', ''),
                  );
                  if (amount == null || amount < 10000) {
                    return 'Số tiền tối thiểu là 10.000đ';
                  }
                  return null;
                },
              ),
              const SizedBox(height: 22),
              SizedBox(
                width: double.infinity,
                height: 52,
                child: FilledButton(
                  onPressed: _submitting ? null : _submit,
                  style: FilledButton.styleFrom(
                    backgroundColor: _teal,
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(13),
                    ),
                  ),
                  child: _submitting
                      ? const SizedBox(
                          width: 22,
                          height: 22,
                          child: CircularProgressIndicator(
                            strokeWidth: 2.5,
                            color: Colors.white,
                          ),
                        )
                      : const Text(
                          'Xác nhận rút tiền',
                          style: TextStyle(
                            fontSize: 16,
                            fontWeight: FontWeight.w700,
                          ),
                        ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  static InputDecoration _inputDecoration(String label) {
    return InputDecoration(
      labelText: label,
      filled: true,
      fillColor: const Color(0xFFFAFAFA),
      border: OutlineInputBorder(
        borderRadius: BorderRadius.circular(12),
        borderSide: const BorderSide(color: _border),
      ),
      enabledBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(12),
        borderSide: const BorderSide(color: _border),
      ),
      focusedBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(12),
        borderSide: const BorderSide(color: _teal, width: 1.5),
      ),
    );
  }
}

class _BankPickerSheet extends StatefulWidget {
  const _BankPickerSheet({required this.banks, required this.selectedCode});
  final List<VietnamBankModel> banks;
  final String? selectedCode;

  @override
  State<_BankPickerSheet> createState() => _BankPickerSheetState();
}

class _BankPickerSheetState extends State<_BankPickerSheet> {
  final _searchController = TextEditingController();
  String _query = '';

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  List<VietnamBankModel> get _filteredBanks {
    final query = _query.trim().toLowerCase();
    if (query.isEmpty) return widget.banks;
    return widget.banks.where((bank) {
      return bank.shortName.toLowerCase().contains(query) ||
          bank.name.toLowerCase().contains(query) ||
          bank.code.toLowerCase().contains(query) ||
          bank.bin.contains(query);
    }).toList(growable: false);
  }

  @override
  Widget build(BuildContext context) {
    final banks = _filteredBanks;
    return FractionallySizedBox(
      heightFactor: .82,
      child: Container(
        decoration: const BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.vertical(top: Radius.circular(24)),
        ),
        child: Column(
          children: [
            const SizedBox(height: 10),
            Container(
              width: 42,
              height: 4,
              decoration: BoxDecoration(
                color: const Color(0xFFD7D7D7),
                borderRadius: BorderRadius.circular(2),
              ),
            ),
            const Padding(
              padding: EdgeInsets.fromLTRB(20, 18, 20, 12),
              child: Align(
                alignment: Alignment.centerLeft,
                child: Text(
                  'Chọn ngân hàng',
                  style: TextStyle(fontSize: 22, fontWeight: FontWeight.w800),
                ),
              ),
            ),
            Padding(
              padding: const EdgeInsets.fromLTRB(20, 0, 20, 12),
              child: TextField(
                controller: _searchController,
                autofocus: true,
                onChanged: (value) => setState(() => _query = value),
                decoration: InputDecoration(
                  hintText: 'Tìm theo tên, mã hoặc BIN',
                  prefixIcon: const Icon(Icons.search_rounded),
                  suffixIcon: _query.isEmpty
                      ? null
                      : IconButton(
                          onPressed: () {
                            _searchController.clear();
                            setState(() => _query = '');
                          },
                          icon: const Icon(Icons.close_rounded),
                        ),
                  filled: true,
                  fillColor: const Color(0xFFF5F6F6),
                  border: OutlineInputBorder(
                    borderRadius: BorderRadius.circular(12),
                    borderSide: BorderSide.none,
                  ),
                ),
              ),
            ),
            Expanded(
              child: banks.isEmpty
                  ? const Center(child: Text('Không tìm thấy ngân hàng.'))
                  : ListView.separated(
                      keyboardDismissBehavior:
                          ScrollViewKeyboardDismissBehavior.onDrag,
                      padding: const EdgeInsets.fromLTRB(12, 0, 12, 20),
                      itemCount: banks.length,
                      separatorBuilder: (_, __) => const Divider(height: 1),
                      itemBuilder: (context, index) {
                        final bank = banks[index];
                        final selected = bank.code == widget.selectedCode;
                        return ListTile(
                          onTap: () => Navigator.of(context).pop(bank),
                          contentPadding: const EdgeInsets.symmetric(
                            horizontal: 8,
                            vertical: 5,
                          ),
                          leading: _BankLogo(bank: bank, size: 42),
                          title: Text(
                            bank.shortName,
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                            style: const TextStyle(fontWeight: FontWeight.w700),
                          ),
                          subtitle: Text(
                            bank.name,
                            maxLines: 2,
                            overflow: TextOverflow.ellipsis,
                          ),
                          trailing: selected
                              ? const Icon(Icons.check_circle, color: _teal)
                              : null,
                        );
                      },
                    ),
            ),
          ],
        ),
      ),
    );
  }
}

class _BankLogo extends StatelessWidget {
  const _BankLogo({required this.bank, required this.size});
  final VietnamBankModel bank;
  final double size;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: size,
      height: size,
      padding: const EdgeInsets.all(5),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(9),
        border: Border.all(color: const Color(0xFFE7EBEC)),
      ),
      child: bank.logo.isEmpty
          ? const Icon(Icons.account_balance_rounded, color: _teal)
          : CachedNetworkImage(
              imageUrl: bank.logo,
              fit: BoxFit.contain,
              fadeInDuration: const Duration(milliseconds: 120),
              placeholder: (_, __) => const SizedBox.shrink(),
              errorWidget: (_, __, ___) => const Icon(
                Icons.account_balance_rounded,
                color: _teal,
              ),
            ),
    );
  }
}

class _Transactions extends StatelessWidget {
  const _Transactions({required this.items});
  final List<WalletTransactionModel> items;

  @override
  Widget build(BuildContext context) {
    if (items.isEmpty) {
      return const Center(
        child: Padding(
          padding: EdgeInsets.symmetric(vertical: 24),
          child: Text('Chưa có giao dịch nào.', style: TextStyle(color: _muted)),
        ),
      );
    }
    return Column(
      children: [
        for (var index = 0; index < items.length; index++) ...[
          _TransactionTile(data: items[index]),
          if (index != items.length - 1) const SizedBox(height: 10),
        ],
      ],
    );
  }
}

class _WalletError extends StatelessWidget {
  const _WalletError({required this.message, required this.onRetry});
  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) => Center(
        child: Padding(
          padding: const EdgeInsets.symmetric(vertical: 48),
          child: Column(
            children: [
              Text(message, textAlign: TextAlign.center),
              const SizedBox(height: 12),
              OutlinedButton(onPressed: onRetry, child: const Text('Thử lại')),
            ],
          ),
        ),
      );
}

String _formatMoney(num value) =>
    '₫ ${NumberFormat('#,##0', 'en_US').format(value)}';

String _periodLabel(String period) => switch (period.toLowerCase()) {
      'day' => 'hôm nay',
      'month' => 'tháng này',
      _ => 'tuần này',
    };

String _changeLabel(num? value) {
  if (value == null) return 'Chưa có dữ liệu\nkỳ trước';
  final sign = value >= 0 ? '+' : '';
  return '$sign${value.toStringAsFixed(1)}% so với\nkỳ trước';
}

double _maxChartValue(List<WalletChartPointModel> chart) {
  if (chart.isEmpty) return 1;
  final max = chart
      .map((point) => point.amount.toDouble())
      .reduce((a, b) => a > b ? a : b);
  return max <= 0 ? 1 : max * 1.15;
}

class _TransactionTile extends StatelessWidget {
  const _TransactionTile({required this.data});
  final WalletTransactionModel data;

  @override
  Widget build(BuildContext context) {
    final type = data.type.toLowerCase();
    final color = data.isCredit ? _teal : const Color(0xFFC51F25);
    final icon = data.tripId != null
        ? Icons.directions_car_rounded
        : type.contains('withdrawal')
            ? Icons.account_balance_rounded
            : type.contains('bonus')
                ? Icons.volunteer_activism_rounded
                : Icons.receipt_long_rounded;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 15),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(13),
        border: Border.all(color: _border),
      ),
      child: Row(
        children: [
          CircleAvatar(
            radius: 21,
            backgroundColor: data.isCredit
                ? const Color(0xFFF1EEEE)
                : const Color(0xFFFFD9D7),
            child: Icon(icon, color: color),
          ),
          const SizedBox(width: 14),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  data.title,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(
                    fontSize: 16,
                    fontWeight: FontWeight.w700,
                  ),
                ),
                const SizedBox(height: 3),
                Text(
                  DateFormat('dd/MM/yyyy, HH:mm').format(data.createdAt),
                  style: const TextStyle(color: _muted),
                ),
              ],
            ),
          ),
          const SizedBox(width: 8),
          Column(
            crossAxisAlignment: CrossAxisAlignment.end,
            children: [
              Text(
                '${data.isCredit ? '+' : '-'} ${_formatMoney(data.amount)}',
                style: TextStyle(
                  fontSize: 16,
                  fontWeight: FontWeight.w800,
                  color: color,
                ),
              ),
              const SizedBox(height: 5),
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
                decoration: BoxDecoration(
                  color: const Color(0xFFF0EEEE),
                  borderRadius: BorderRadius.circular(4),
                ),
                child: const Text(
                  'Hoàn thành',
                  style: TextStyle(
                    fontSize: 11,
                    fontWeight: FontWeight.w700,
                    color: _muted,
                  ),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}
