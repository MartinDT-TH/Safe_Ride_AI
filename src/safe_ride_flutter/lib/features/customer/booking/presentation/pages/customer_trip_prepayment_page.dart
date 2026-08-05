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
import '../../../../driver/dashboard/data/models/payment_models.dart';

class CustomerTripPrepaymentPage extends StatefulWidget {
  CustomerTripPrepaymentPage({super.key, required this.tripId});

  final int tripId;

  @override
  State<CustomerTripPrepaymentPage> createState() =>
      _CustomerTripPrepaymentPageState();
}

class _CustomerTripPrepaymentPageState
    extends State<CustomerTripPrepaymentPage> {
  final Dio _dio = DioClient().dio;
  Timer? _statusTimer;
  QrPaymentResult? _qrPayment;
  PaymentStatusResult? _paymentStatus;
  bool _isLoading = true;
  bool _isRefreshing = false;
  String? _errorMessage;

  bool get _isPaid =>
      _qrPayment?.isSuccess == true || _paymentStatus?.isSuccess == true;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _createQrPayment());
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

    return Scaffold(
      backgroundColor: Color(0xFFFBF9F8),
      appBar: AppBar(
        backgroundColor: Color(0xFFFBF9F8),
        title: Text(context.l10n.prepayment),
      ),
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.fromLTRB(24, 24, 24, 28),
          child: Column(
            children: [
              Text(
                context.l10n.payosPaymentAmount,
                style: TextStyle(fontSize: 17, fontWeight: FontWeight.w700),
              ),
              SizedBox(height: 10),
              Text(
                _formatCurrency(amount),
                style: TextStyle(
                  color: AppColors.primary,
                  fontSize: 40,
                  fontWeight: FontWeight.w900,
                ),
              ),
              SizedBox(height: 24),
              Expanded(child: Center(child: _buildContent(qrData))),
              if (!_isPaid) ...[
                SizedBox(height: 16),
                SizedBox(
                  width: double.infinity,
                  height: 52,
                  child: OutlinedButton.icon(
                    onPressed: _isRefreshing ? null : _refreshStatus,
                    icon: _isRefreshing
                        ? SizedBox(
                            width: 18,
                            height: 18,
                            child: CircularProgressIndicator(strokeWidth: 2),
                          )
                        : Icon(Icons.sync_rounded),
                    label: Text(context.l10n.checkPayment),
                  ),
                ),
                SizedBox(height: 8),
                TextButton(
                  onPressed: () => Navigator.of(context).pop(false),
                  child: Text(context.l10n.payAfterTrip),
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildContent(String? qrData) {
    if (_isLoading) {
      return CircularProgressIndicator();
    }
    if (_isPaid) {
      return Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(
            Icons.check_circle_rounded,
            color: Color(0xFF0A8F62),
            size: 88,
          ),
          SizedBox(height: 16),
          Text(
            context.l10n.prepaid,
            style: TextStyle(fontSize: 24, fontWeight: FontWeight.w900),
          ),
          SizedBox(height: 24),
          ElevatedButton(
            onPressed: () => Navigator.of(context).pop(true),
            child: Text(context.l10n.backToTrip),
          ),
        ],
      );
    }
    if (_errorMessage != null || qrData == null || qrData.isEmpty) {
      return Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(Icons.error_outline_rounded, color: Colors.red, size: 56),
          SizedBox(height: 12),
          Text(
            _errorMessage ?? context.l10n.payosQrCreateFailed,
            textAlign: TextAlign.center,
          ),
          SizedBox(height: 16),
          ElevatedButton(
            onPressed: _createQrPayment,
            child: Text(context.l10n.tryAgain),
          ),
        ],
      );
    }

    return Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        Container(
          width: 280,
          height: 280,
          padding: const EdgeInsets.all(24),
          decoration: BoxDecoration(
            color: Colors.white,
            borderRadius: BorderRadius.circular(28),
            boxShadow: [BoxShadow(color: Colors.black12, blurRadius: 20)],
          ),
          child: QrImageView(data: qrData, backgroundColor: Colors.white),
        ),
        SizedBox(height: 20),
        Text(
          context.l10n.scanQrToPay,
          textAlign: TextAlign.center,
          style: TextStyle(fontWeight: FontWeight.w700),
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
      _isLoading = true;
      _errorMessage = null;
    });
    try {
      final response = await _dio.post(
        ApiEndpoints.createCustomerTripQrPayment(widget.tripId),
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
      if (!payment.isSuccess) _startPolling(token);
    } on DioException catch (error) {
      if (!mounted) return;
      setState(() {
        _isLoading = false;
        _errorMessage = _extractError(error);
      });
    }
  }

  void _startPolling(String token) {
    _statusTimer?.cancel();
    _statusTimer = Timer.periodic(
      Duration(seconds: 4),
      (_) => _loadStatus(token),
    );
  }

  Future<void> _refreshStatus() async {
    final token = context.read<AuthProvider>().token;
    if (token == null || token.isEmpty) return;
    setState(() => _isRefreshing = true);
    await _loadStatus(token);
    if (mounted) setState(() => _isRefreshing = false);
  }

  Future<void> _loadStatus(String token) async {
    try {
      final response = await _dio.get(
        ApiEndpoints.customerTripPaymentStatus(widget.tripId),
        options: Options(
          headers: {ApiKeys.authorization: AuthHeader.bearer(token)},
        ),
      );
      final status = PaymentStatusResult.fromJson(
        Map<String, dynamic>.from(response.data as Map),
      );
      if (!mounted) return;
      setState(() => _paymentStatus = status);
      if (status.isSuccess) _statusTimer?.cancel();
    } on DioException {
      // A transient polling failure must not discard the active PayOS QR.
    }
  }

  String _extractError(DioException exception) {
    final data = exception.response?.data;
    if (data is Map) {
      final detail = data[ApiKeys.detail]?.toString();
      if (detail != null && detail.isNotEmpty) return detail;
    }
    return context.l10n.payosQrCreateFailed;
  }

  String _formatCurrency(double value) {
    return NumberFormat.currency(
      locale: LocaleProvider.currentLocale.toLanguageTag(),
      symbol: 'VND',
      decimalDigits: 0,
    ).format(value);
  }
}
