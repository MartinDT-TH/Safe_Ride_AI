import 'dart:async';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:qr_flutter/qr_flutter.dart';

import '../../../../../core/constants/app_colors.dart';
import '../../../../../core/constants/app_strings.dart';
import '../../../../../core/network/auth_header.dart';
import '../../../../../core/network/dio_client.dart';
import '../../../../auth/presentation/providers/auth_provider.dart';
import '../../data/models/payment_models.dart';

enum _DriverPaymentMode { qr, cash }

class DriverTripPaymentPage extends StatefulWidget {
  const DriverTripPaymentPage({
    super.key,
    required this.tripId,
  });

  final int tripId;

  @override
  State<DriverTripPaymentPage> createState() => _DriverTripPaymentPageState();
}

class _DriverTripPaymentPageState extends State<DriverTripPaymentPage> {
  final Dio _dio = DioClient().dio;
  Timer? _statusTimer;

  QrPaymentResult? _qrPayment;
  PaymentStatusResult? _paymentStatus;
  _DriverPaymentMode? _selectedMode;
  bool _isLoading = false;
  bool _isRefreshing = false;
  bool _isConfirmingCash = false;
  String? _errorMessage;

  static const _surface = Color(0xFFFBF9F8);
  static const _primary = AppColors.primary;
  static const _primaryDark = Color(0xFF005A64);
  static const _muted = Color(0xFF475255);

  @override
  void initState() {
    super.initState();
  }

  @override
  void dispose() {
    _statusTimer?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final qrData = _qrPayment?.qrCode ?? _qrPayment?.checkoutUrl;
    final amount = _paymentStatus?.amount ?? _qrPayment?.amount ?? 0;
    final isPaid =
        _paymentStatus?.isSuccess == true || _qrPayment?.isSuccess == true;

    return Scaffold(
      backgroundColor: _surface,
      appBar: AppBar(
        backgroundColor: _surface,
        elevation: 0.8,
        leading: IconButton(
          icon: const Icon(Icons.arrow_back_ios_new_rounded),
          color: _primary,
          onPressed: () => Navigator.of(context).pop(),
        ),
        title: const Text(
          'Thanh toán chuyến đi',
          maxLines: 1,
          overflow: TextOverflow.ellipsis,
          style: TextStyle(
            color: _primary,
            fontSize: 22,
            fontWeight: FontWeight.w900,
          ),
        ),
        centerTitle: true,
      ),
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.fromLTRB(24, 28, 24, 24),
          child: Column(
            children: [
              Text(
                'Số tiền khách cần thanh toán',
                textAlign: TextAlign.center,
                style: TextStyle(
                  color: _muted,
                  fontSize: 18,
                  fontWeight: FontWeight.w800,
                ),
              ),
              const SizedBox(height: 12),
              Text(
                _formatCurrency(amount),
                textAlign: TextAlign.center,
                style: const TextStyle(
                  color: _primaryDark,
                  fontSize: 44,
                  height: 1,
                  fontWeight: FontWeight.w900,
                ),
              ),
              const SizedBox(height: 38),
              Expanded(
                child: Center(
                  child: AnimatedSwitcher(
                    duration: const Duration(milliseconds: 220),
                    child: _buildPaymentContent(qrData, isPaid),
                  ),
                ),
              ),
              const SizedBox(height: 20),
              if (_selectedMode == _DriverPaymentMode.qr)
                SizedBox(
                  width: double.infinity,
                  height: 58,
                  child: OutlinedButton.icon(
                    onPressed: _isRefreshing || isPaid ? null : _refreshStatus,
                    icon: _isRefreshing
                        ? const SizedBox(
                            width: 20,
                            height: 20,
                            child: CircularProgressIndicator(strokeWidth: 2.2),
                          )
                        : const Icon(Icons.sync_rounded),
                    label: Text(isPaid ? 'Đã thanh toán' : 'Kiểm tra lại'),
                    style: OutlinedButton.styleFrom(
                      foregroundColor: _primary,
                      side: const BorderSide(color: _primary, width: 2),
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(14),
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

  Widget _buildPaymentContent(String? qrData, bool isPaid) {
    if (_selectedMode == null) {
      return _PaymentChoicePanel(
        key: const ValueKey('choice'),
        onQrPressed: _createQrPayment,
        onCashPressed: _confirmCashPayment,
        isConfirmingCash: _isConfirmingCash,
      );
    }

    if (_isLoading) {
      return const SizedBox(
        key: ValueKey('loading'),
        width: 42,
        height: 42,
        child: CircularProgressIndicator(strokeWidth: 3),
      );
    }

    if (isPaid) {
      final isCash = _paymentStatus?.paymentMethod?.toLowerCase() == 'cash';
      return Column(
        key: const ValueKey('paid'),
        mainAxisSize: MainAxisSize.min,
        children: [
          Container(
            width: 96,
            height: 96,
            decoration: const BoxDecoration(
              color: Color(0xFFE5F5F0),
              shape: BoxShape.circle,
            ),
            child: const Icon(
              Icons.check_circle_rounded,
              color: Color(0xFF0A8F62),
              size: 64,
            ),
          ),
          const SizedBox(height: 22),
          Text(
            isCash ? 'Đã xác nhận tiền mặt' : 'Khách đã thanh toán',
            textAlign: TextAlign.center,
            style: const TextStyle(
              color: _primaryDark,
              fontSize: 24,
              fontWeight: FontWeight.w900,
            ),
          ),
        ],
      );
    }

    if (_errorMessage != null || qrData == null || qrData.isEmpty) {
      return Column(
        key: const ValueKey('error'),
        mainAxisSize: MainAxisSize.min,
        children: [
          const Icon(Icons.error_outline_rounded, color: Colors.red, size: 54),
          const SizedBox(height: 16),
          Text(
            _errorMessage ?? 'Không thể tạo mã QR thanh toán.',
            textAlign: TextAlign.center,
            style: const TextStyle(
              color: _muted,
              fontSize: 17,
              fontWeight: FontWeight.w700,
            ),
          ),
          const SizedBox(height: 20),
          ElevatedButton(
            onPressed: _selectedMode == _DriverPaymentMode.cash
                ? _confirmCashPayment
                : _createQrPayment,
            style: ElevatedButton.styleFrom(
              backgroundColor: _primary,
              foregroundColor: Colors.white,
            ),
            child: Text(
              _selectedMode == _DriverPaymentMode.cash
                  ? 'Xác nhận lại tiền mặt'
                  : 'Tạo lại mã QR',
            ),
          ),
        ],
      );
    }

    return Column(
      key: const ValueKey('qr'),
      mainAxisSize: MainAxisSize.min,
      children: [
        Container(
          width: 284,
          height: 284,
          padding: const EdgeInsets.all(28),
          decoration: BoxDecoration(
            color: Colors.white,
            borderRadius: BorderRadius.circular(32),
            border: Border.all(color: const Color(0xFFE4E1DF), width: 1.2),
            boxShadow: [
              BoxShadow(
                color: Colors.black.withValues(alpha: 0.14),
                blurRadius: 22,
                offset: const Offset(0, 12),
              ),
            ],
          ),
          child: RepaintBoundary(
            child: QrImageView(
              data: qrData,
              version: QrVersions.auto,
              gapless: true,
              padding: EdgeInsets.zero,
              backgroundColor: Colors.white,
            ),
          ),
        ),
        const SizedBox(height: 28),
        Container(
          height: 46,
          padding: const EdgeInsets.symmetric(horizontal: 22),
          decoration: BoxDecoration(
            color: const Color(0xFFDDE8EA),
            borderRadius: BorderRadius.circular(24),
          ),
          child: const Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(
                Icons.qr_code_scanner_rounded,
                color: _muted,
                size: 24,
              ),
              SizedBox(width: 8),
              Text(
                'Đưa khách quét mã này',
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(
                  color: _muted,
                  fontSize: 17,
                  fontWeight: FontWeight.w900,
                ),
              ),
            ],
          ),
        ),
      ],
    );
  }

  Future<void> _createQrPayment() async {
    final token = context.read<AuthProvider>().token;
    if (token == null || token.isEmpty) {
      setState(() {
        _isLoading = false;
        _errorMessage = 'Phiên đăng nhập đã hết hạn.';
      });
      return;
    }

    setState(() {
      _selectedMode = _DriverPaymentMode.qr;
      _isLoading = true;
      _errorMessage = null;
    });

    try {
      final response = await _dio.post(
        ApiEndpoints.createDriverTripQrPayment(widget.tripId),
        data: const <String, dynamic>{},
        options: Options(
          headers: {ApiKeys.authorization: AuthHeader.bearer(token)},
        ),
      );
      final payment = QrPaymentResult.fromJson(
        Map<String, dynamic>.from(response.data as Map),
      );
      if (!mounted) return;
      setState(() {
        _qrPayment = payment;
        _isLoading = false;
      });
      _startStatusPolling(token);
    } on DioException catch (exception) {
      if (!mounted) return;
      setState(() {
        _isLoading = false;
        _errorMessage = _extractError(exception);
      });
    }
  }

  Future<void> _confirmCashPayment() async {
    final token = context.read<AuthProvider>().token;
    if (token == null || token.isEmpty) {
      setState(() {
        _selectedMode = _DriverPaymentMode.cash;
        _errorMessage = 'Phiên đăng nhập đã hết hạn.';
      });
      return;
    }

    setState(() {
      _selectedMode = _DriverPaymentMode.cash;
      _isConfirmingCash = true;
      _isLoading = true;
      _errorMessage = null;
    });

    try {
      final response = await _dio.post(
        ApiEndpoints.confirmDriverTripCashPayment(widget.tripId),
        data: const <String, dynamic>{},
        options: Options(
          headers: {ApiKeys.authorization: AuthHeader.bearer(token)},
        ),
      );
      final status = PaymentStatusResult.fromJson(
        Map<String, dynamic>.from(response.data as Map),
      );
      if (!mounted) return;
      setState(() {
        _paymentStatus = status;
        _isLoading = false;
        _isConfirmingCash = false;
      });
    } on DioException catch (exception) {
      if (!mounted) return;
      setState(() {
        _isLoading = false;
        _isConfirmingCash = false;
        _errorMessage = _extractError(
          exception,
          fallback: 'Không thể xác nhận thanh toán tiền mặt.',
        );
      });
    }
  }

  void _startStatusPolling(String token) {
    _statusTimer?.cancel();
    _statusTimer = Timer.periodic(const Duration(seconds: 2), (_) async {
      await _loadStatus(token, showLoading: false);
    });
  }

  Future<void> _refreshStatus() async {
    final token = context.read<AuthProvider>().token;
    if (token == null || token.isEmpty) {
      return;
    }
    setState(() => _isRefreshing = true);
    await _loadStatus(token, showLoading: false);
    if (mounted) {
      setState(() => _isRefreshing = false);
    }
  }

  Future<void> _loadStatus(String token, {required bool showLoading}) async {
    try {
      final response = await _dio.get(
        ApiEndpoints.driverTripPaymentStatus(widget.tripId),
        options: Options(
          headers: {ApiKeys.authorization: AuthHeader.bearer(token)},
        ),
      );
      final status = PaymentStatusResult.fromJson(
        Map<String, dynamic>.from(response.data as Map),
      );
      if (!mounted) return;
      setState(() => _paymentStatus = status);
      if (status.isSuccess) {
        _statusTimer?.cancel();
      }
    } on DioException catch (_) {
      if (!mounted || showLoading) return;
    }
  }

  static String _extractError(
    DioException exception, {
    String fallback = 'Không thể tạo mã QR thanh toán.',
  }) {
    final data = exception.response?.data;
    if (data is Map) {
      final detail = data[ApiKeys.detail]?.toString();
      if (detail != null && detail.isNotEmpty) {
        return detail;
      }
    }
    return fallback;
  }

  static String _formatCurrency(double value) {
    final formatter = value.round().toString().replaceAllMapped(
      RegExp(r'(\d{1,3})(?=(\d{3})+(?!\d))'),
      (match) => '${match[1]}.',
    );
    return '${formatter}đ';
  }
}

class _PaymentChoicePanel extends StatelessWidget {
  const _PaymentChoicePanel({
    super.key,
    required this.onQrPressed,
    required this.onCashPressed,
    required this.isConfirmingCash,
  });

  final VoidCallback onQrPressed;
  final VoidCallback onCashPressed;
  final bool isConfirmingCash;

  @override
  Widget build(BuildContext context) {
    return Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        const Icon(
          Icons.payments_rounded,
          color: _DriverTripPaymentPageState._primary,
          size: 64,
        ),
        const SizedBox(height: 18),
        const Text(
          'Chọn phương thức khách thanh toán',
          textAlign: TextAlign.center,
          style: TextStyle(
            color: _DriverTripPaymentPageState._primaryDark,
            fontSize: 22,
            fontWeight: FontWeight.w900,
          ),
        ),
        const SizedBox(height: 28),
        SizedBox(
          width: double.infinity,
          height: 58,
          child: ElevatedButton.icon(
            onPressed: onQrPressed,
            icon: const Icon(Icons.qr_code_2_rounded),
            label: const Text('Thanh toán QR'),
            style: ElevatedButton.styleFrom(
              backgroundColor: _DriverTripPaymentPageState._primary,
              foregroundColor: Colors.white,
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(14),
              ),
            ),
          ),
        ),
        const SizedBox(height: 14),
        SizedBox(
          width: double.infinity,
          height: 58,
          child: OutlinedButton.icon(
            onPressed: isConfirmingCash ? null : onCashPressed,
            icon: isConfirmingCash
                ? const SizedBox(
                    width: 20,
                    height: 20,
                    child: CircularProgressIndicator(strokeWidth: 2.2),
                  )
                : const Icon(Icons.attach_money_rounded),
            label: const Text('Trả tiền mặt'),
            style: OutlinedButton.styleFrom(
              foregroundColor: _DriverTripPaymentPageState._primary,
              side: const BorderSide(
                color: _DriverTripPaymentPageState._primary,
                width: 2,
              ),
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(14),
              ),
            ),
          ),
        ),
      ],
    );
  }
}
