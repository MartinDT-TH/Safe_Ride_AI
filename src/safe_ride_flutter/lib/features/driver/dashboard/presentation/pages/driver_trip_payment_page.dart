import 'dart:async';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:qr_flutter/qr_flutter.dart';
import 'package:intl/intl.dart';

import '../../../../../core/constants/app_colors.dart';
import '../../../../../core/constants/app_strings.dart';
import '../../../../../core/localization/localization_extensions.dart';
import '../../../../../core/localization/locale_provider.dart';
import '../../../../../core/network/auth_header.dart';
import '../../../../../core/network/dio_client.dart';
import '../../../../auth/presentation/providers/auth_provider.dart';
import '../../data/models/payment_models.dart';

enum _DriverPaymentMode { qr, cash }

class DriverTripPaymentPage extends StatefulWidget {
  DriverTripPaymentPage({super.key, required this.tripId});

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
  bool _returnedToDashboard = false;
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

    return PopScope(
      canPop: isPaid,
      onPopInvokedWithResult: (didPop, result) {
        if (didPop) return;
        if (_selectedMode != null) {
          setState(() {
            _selectedMode = null;
          });
        } else {
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(
              content: Text(context.l10n.completePaymentBeforeExit),
            ),
          );
        }
      },
      child: Scaffold(
        backgroundColor: _surface,
        appBar: AppBar(
          backgroundColor: _surface,
          elevation: 0.8,
          leading: IconButton(
            icon: Icon(Icons.arrow_back_ios_new_rounded),
            color: _primary,
            onPressed: () {
              if (isPaid) {
                _finishAndReturnToDashboard();
                return;
              }
              if (_selectedMode != null) {
                _statusTimer?.cancel();
                setState(() {
                  _selectedMode = null;
                });
              } else {
                ScaffoldMessenger.of(context).showSnackBar(
                  SnackBar(
                    content: Text(context.l10n.completePayment),
                    duration: Duration(seconds: 2),
                  ),
                );
              }
            },
          ),
          title: Text(
            context.l10n.tripPayment,
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
                  context.l10n.customerPaymentAmount,
                  textAlign: TextAlign.center,
                  style: TextStyle(
                    color: _muted,
                    fontSize: 18,
                    fontWeight: FontWeight.w800,
                  ),
                ),
                SizedBox(height: 12),
                Text(
                  _formatCurrency(amount),
                  textAlign: TextAlign.center,
                  style: TextStyle(
                    color: _primaryDark,
                    fontSize: 44,
                    height: 1,
                    fontWeight: FontWeight.w900,
                  ),
                ),
                SizedBox(height: 38),
                Expanded(
                  child: Center(
                    child: AnimatedSwitcher(
                      duration: Duration(milliseconds: 220),
                      child: _buildPaymentContent(qrData, isPaid),
                    ),
                  ),
                ),
                SizedBox(height: 20),
                if (_selectedMode == _DriverPaymentMode.qr)
                  SizedBox(
                    width: double.infinity,
                    height: 58,
                    child: OutlinedButton.icon(
                      onPressed: _isRefreshing || isPaid
                          ? null
                          : _refreshStatus,
                      icon: _isRefreshing
                          ? SizedBox(
                              width: 20,
                              height: 20,
                              child: CircularProgressIndicator(
                                strokeWidth: 2.2,
                              ),
                            )
                          : Icon(Icons.sync_rounded),
                      label: Text(
                        isPaid ? context.l10n.paid : context.l10n.checkAgain,
                      ),
                      style: OutlinedButton.styleFrom(
                        foregroundColor: _primary,
                        side: BorderSide(color: _primary, width: 2),
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
      ),
    );
  }

  Widget _buildPaymentContent(String? qrData, bool isPaid) {
    if (_selectedMode == null) {
      return _PaymentChoicePanel(
        key: ValueKey('choice'),
        onQrPressed: _createQrPayment,
        onCashPressed: _confirmCashPayment,
        isConfirmingCash: _isConfirmingCash,
      );
    }

    if (_isLoading) {
      return SizedBox(
        key: ValueKey('loading'),
        width: 42,
        height: 42,
        child: CircularProgressIndicator(strokeWidth: 3),
      );
    }

    if (isPaid) {
      final isCash = _paymentStatus?.paymentMethod?.toLowerCase() == 'cash';
      return Column(
        key: ValueKey('paid'),
        mainAxisSize: MainAxisSize.min,
        children: [
          Container(
            width: 96,
            height: 96,
            decoration: BoxDecoration(
              color: Color(0xFFE5F5F0),
              shape: BoxShape.circle,
            ),
            child: Icon(
              Icons.check_circle_rounded,
              color: Color(0xFF0A8F62),
              size: 64,
            ),
          ),
          SizedBox(height: 22),
          Text(
            isCash ? context.l10n.cashConfirmed : context.l10n.customerPaid,
            textAlign: TextAlign.center,
            style: TextStyle(
              color: _primaryDark,
              fontSize: 24,
              fontWeight: FontWeight.w900,
            ),
          ),
          SizedBox(height: 32),
          SizedBox(
            width: double.infinity,
            height: 54,
            child: ElevatedButton(
              onPressed: _finishAndReturnToDashboard,
              style: ElevatedButton.styleFrom(
                backgroundColor: _primary,
                foregroundColor: Colors.white,
                shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(14),
                ),
              ),
              child: Text(
                context.l10n.backToHome,
                style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
              ),
            ),
          ),
        ],
      );
    }

    if (_errorMessage != null || qrData == null || qrData.isEmpty) {
      return Column(
        key: ValueKey('error'),
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(Icons.error_outline_rounded, color: Colors.red, size: 54),
          SizedBox(height: 16),
          Text(
            _errorMessage ?? context.l10n.paymentQrCreateFailed,
            textAlign: TextAlign.center,
            style: TextStyle(
              color: _muted,
              fontSize: 17,
              fontWeight: FontWeight.w700,
            ),
          ),
          SizedBox(height: 20),
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
                  ? context.l10n.reconfirmCash
                  : context.l10n.recreateQr,
            ),
          ),
          SizedBox(height: 12),
          TextButton(
            onPressed: () {
              _statusTimer?.cancel();
              setState(() {
                _selectedMode = null;
              });
            },
            child: Text(context.l10n.switchPaymentMethod),
          ),
        ],
      );
    }

    return Column(
      key: ValueKey('qr'),
      mainAxisSize: MainAxisSize.min,
      children: [
        Container(
          width: 284,
          height: 284,
          padding: const EdgeInsets.all(28),
          decoration: BoxDecoration(
            color: Colors.white,
            borderRadius: BorderRadius.circular(32),
            border: Border.all(color: Color(0xFFE4E1DF), width: 1.2),
            boxShadow: [
              BoxShadow(
                color: Colors.black.withValues(alpha: 0.14),
                blurRadius: 22,
                offset: Offset(0, 12),
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
        SizedBox(height: 28),
        Container(
          height: 46,
          padding: const EdgeInsets.symmetric(horizontal: 22),
          decoration: BoxDecoration(
            color: Color(0xFFDDE8EA),
            borderRadius: BorderRadius.circular(24),
          ),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(Icons.qr_code_scanner_rounded, color: _muted, size: 24),
              SizedBox(width: 8),
              Text(
                context.l10n.customerScanQr,
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
        SizedBox(height: 16),
        TextButton(
          onPressed: () {
            _statusTimer?.cancel();
            setState(() {
              _selectedMode = null;
            });
          },
          child: Text(
            context.l10n.switchPaymentMethod,
            style: TextStyle(
              color: _primary,
              fontSize: 16,
              fontWeight: FontWeight.w600,
              decoration: TextDecoration.underline,
            ),
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
        _errorMessage = context.l10n.sessionExpired;
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
        _errorMessage = context.l10n.sessionExpired;
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
      if (status.isSuccess) {
        _finishAndReturnToDashboard();
      }
    } on DioException catch (exception) {
      if (!mounted) return;
      setState(() {
        _isLoading = false;
        _isConfirmingCash = false;
        _errorMessage = _extractError(
          exception,
          fallback: context.l10n.cashPaymentConfirmFailed,
        );
      });
    }
  }

  void _startStatusPolling(String token) {
    _statusTimer?.cancel();
    _statusTimer = Timer.periodic(Duration(seconds: 5), (_) async {
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
        _finishAndReturnToDashboard();
      }
    } on DioException catch (_) {
      if (!mounted || showLoading) return;
    }
  }

  static String _extractError(DioException exception, {String? fallback}) {
    final data = exception.response?.data;
    if (data is Map) {
      final detail = data[ApiKeys.detail]?.toString();
      if (detail != null && detail.isNotEmpty) {
        return detail;
      }
    }
    return fallback ??
        LocaleProvider.currentLocalizations.paymentQrCreateFailed;
  }

  void _finishAndReturnToDashboard() {
    if (!mounted || _returnedToDashboard) {
      return;
    }
    _returnedToDashboard = true;
    _statusTimer?.cancel();
    Navigator.of(context).pop(true);
  }

  static String _formatCurrency(double value) {
    return NumberFormat.currency(
      locale: LocaleProvider.currentLocale.toLanguageTag(),
      symbol: 'VND',
      decimalDigits: 0,
    ).format(value);
  }
}

class _PaymentChoicePanel extends StatelessWidget {
  _PaymentChoicePanel({
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
        Icon(
          Icons.payments_rounded,
          color: _DriverTripPaymentPageState._primary,
          size: 64,
        ),
        SizedBox(height: 18),
        Text(
          context.l10n.chooseCustomerPaymentMethod,
          textAlign: TextAlign.center,
          style: TextStyle(
            color: _DriverTripPaymentPageState._primaryDark,
            fontSize: 22,
            fontWeight: FontWeight.w900,
          ),
        ),
        SizedBox(height: 28),
        SizedBox(
          width: double.infinity,
          height: 58,
          child: ElevatedButton.icon(
            onPressed: onQrPressed,
            icon: Icon(Icons.qr_code_2_rounded),
            label: Text(context.l10n.qrPayment),
            style: ElevatedButton.styleFrom(
              backgroundColor: _DriverTripPaymentPageState._primary,
              foregroundColor: Colors.white,
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(14),
              ),
            ),
          ),
        ),
        SizedBox(height: 14),
        SizedBox(
          width: double.infinity,
          height: 58,
          child: OutlinedButton.icon(
            onPressed: isConfirmingCash ? null : onCashPressed,
            icon: isConfirmingCash
                ? SizedBox(
                    width: 20,
                    height: 20,
                    child: CircularProgressIndicator(strokeWidth: 2.2),
                  )
                : Icon(Icons.attach_money_rounded),
            label: Text(context.l10n.cashPayment),
            style: OutlinedButton.styleFrom(
              foregroundColor: _DriverTripPaymentPageState._primary,
              side: BorderSide(
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
