import 'dart:async';

import 'package:flutter/material.dart';
import 'package:pin_code_fields/pin_code_fields.dart';
import 'package:provider/provider.dart';

import '../../../../../core/constants/app_strings.dart';
import '../../../../../core/localization/localization_extensions.dart';
import '../../../../../core/widgets/custom_button.dart';
import '../providers/auth_provider.dart';
import '../../../shared/onboarding/presentation/pages/role_selection_page.dart';
import '../../../customer/home/presentation/pages/customer_home_page.dart';
import '../../../shared/profile/presentation/pages/edit_profile_page.dart';
import '../../../shared/onboarding/presentation/providers/role_provider.dart';
import '../../../customer/booking/presentation/providers/booking_provider.dart';
import '../../../driver/dashboard/presentation/pages/driver_dashboard_page.dart';

class OtpPage extends StatefulWidget {
  final String phoneNumber;

  OtpPage({super.key, required this.phoneNumber});

  @override
  State<OtpPage> createState() => _OtpPageState();
}

class _OtpPageState extends State<OtpPage> {
  String otpCode = '';
  Timer? _resendTimer;
  Timer? _otpLockTimer;
  int _resendRemainingSeconds = 60;
  int _otpLockRemainingSeconds = 0;

  static const String _otpAttemptsExceededCode = 'auth.otp_attempts_exceeded';
  static const int _fallbackOtpLockSeconds = 30;

  bool get _canResendOtp => _resendRemainingSeconds == 0;
  bool get _canVerifyOtp => _otpLockRemainingSeconds == 0;

  @override
  void initState() {
    super.initState();
    _startResendTimer();
  }

  @override
  void dispose() {
    _resendTimer?.cancel();
    _otpLockTimer?.cancel();
    super.dispose();
  }

  void _startResendTimer() {
    _resendTimer?.cancel();
    setState(() => _resendRemainingSeconds = 60);
    _resendTimer = Timer.periodic(Duration(seconds: 1), (timer) {
      if (!mounted) {
        timer.cancel();
        return;
      }

      if (_resendRemainingSeconds <= 1) {
        timer.cancel();
        setState(() => _resendRemainingSeconds = 0);
        return;
      }

      setState(() => _resendRemainingSeconds--);
    });
  }

  String _formatResendTime() {
    return _formatDuration(_resendRemainingSeconds);
  }

  void _startOtpLockTimer(int seconds) {
    _otpLockTimer?.cancel();
    setState(() => _otpLockRemainingSeconds = seconds);
    _otpLockTimer = Timer.periodic(Duration(seconds: 1), (timer) {
      if (!mounted) {
        timer.cancel();
        return;
      }

      if (_otpLockRemainingSeconds <= 1) {
        timer.cancel();
        setState(() => _otpLockRemainingSeconds = 0);
        return;
      }

      setState(() => _otpLockRemainingSeconds--);
    });
  }

  String _formatOtpLockTime() {
    return _formatDuration(_otpLockRemainingSeconds);
  }

  String _formatDuration(int totalSeconds) {
    final minutes = (totalSeconds ~/ 60).toString().padLeft(2, '0');
    final seconds = (totalSeconds % 60).toString().padLeft(2, '0');
    return '$minutes:$seconds';
  }

  Future<Widget> _getDestination(
    BuildContext context,
    AuthProvider auth,
    RoleProvider roleProvider,
  ) async {
    // 1. Check for active booking first
    final bookingProvider = context.read<BookingProvider>();
    final activeBooking = await bookingProvider.loadActiveBooking(auth.token!);

    if (activeBooking != null) {
      // Force customer role
      roleProvider.setRole(AppValues.roleCustomer);
      return CustomerHomePage();
    }

    // 2. No active booking, fallback to role logic
    if (roleProvider.isDriver) {
      return DriverDashboardPage();
    }
    return CustomerHomePage();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: Colors.white,
      appBar: AppBar(
        backgroundColor: Colors.white,
        elevation: 0,
        leading: IconButton(
          icon: Icon(Icons.arrow_back, color: Color(0xFF006B70)),
          onPressed: () => Navigator.pop(context),
        ),
        title: Text(
          context.l10n.appName,
          style: TextStyle(
            color: Color(0xFF006B70),
            fontWeight: FontWeight.bold,
          ),
        ),
        centerTitle: true,
        bottom: PreferredSize(
          preferredSize: const Size.fromHeight(1),
          child: Container(color: Colors.grey.shade100, height: 1),
        ),
      ),
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 24),
          child: Column(
            children: [
              Expanded(
                child: SingleChildScrollView(
                  child: Column(
                    children: [
                      SizedBox(height: 48),
                      // Lock Icon circular container
                      Center(
                        child: Container(
                          width: 120,
                          height: 120,
                          decoration: BoxDecoration(
                            color: Color(0xFFE8F2F2),
                            shape: BoxShape.circle,
                          ),
                          child: Icon(
                            Icons.lock_person_outlined,
                            size: 60,
                            color: Color(0xFF006B70),
                          ),
                        ),
                      ),
                      SizedBox(height: 32),
                      Text(
                        context.l10n.otpTitle,
                        style: TextStyle(
                          fontSize: 28,
                          fontWeight: FontWeight.bold,
                          color: Color(0xFF1A1A1A),
                        ),
                      ),
                      SizedBox(height: 16),
                      Text(
                        context.l10n.otpDescription(widget.phoneNumber),
                        textAlign: TextAlign.center,
                        style: TextStyle(
                          fontSize: 15,
                          color: Color(0xFF666666),
                          height: 1.5,
                        ),
                      ),
                      SizedBox(height: 40),
                      PinCodeTextField(
                        appContext: context,
                        length: 6,
                        enabled: _canVerifyOtp,
                        keyboardType: TextInputType.number,
                        onChanged: (value) {
                          otpCode = value;
                        },
                        pinTheme: PinTheme(
                          shape: PinCodeFieldShape.box,
                          borderRadius: BorderRadius.circular(12),
                          fieldHeight: 56,
                          fieldWidth: 46,
                          activeColor: Color(0xFF006B70),
                          selectedColor: Color(0xFF006B70),
                          inactiveColor: Colors.grey.shade300,
                          activeFillColor: Colors.white,
                          inactiveFillColor: Colors.white,
                          selectedFillColor: Colors.white,
                          borderWidth: 1,
                        ),
                        enableActiveFill: true,
                        cursorColor: Color(0xFF006B70),
                        animationType: AnimationType.fade,
                        mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      ),
                      SizedBox(height: 16),
                      AnimatedSwitcher(
                        duration: Duration(milliseconds: 200),
                        child: _canVerifyOtp
                            ? SizedBox(height: 20)
                            : Text(
                                '${context.l10n.otpLockedPrefix}${_formatOtpLockTime()}',
                                key: ValueKey('otp-lock-countdown'),
                                textAlign: TextAlign.center,
                                style: TextStyle(
                                  fontSize: 14,
                                  color: Color(0xFFC62828),
                                  fontWeight: FontWeight.w600,
                                ),
                              ),
                      ),
                      SizedBox(height: 16),
                      RichText(
                        text: TextSpan(
                          style: TextStyle(
                            fontSize: 15,
                            color: Colors.grey,
                          ),
                          children: [
                            TextSpan(text: context.l10n.resendAfter),
                            TextSpan(
                              text: _formatResendTime(),
                              style: TextStyle(
                                color: Color(0xFF006B70),
                                fontWeight: FontWeight.bold,
                              ),
                            ),
                          ],
                        ),
                      ),
                      SizedBox(height: 8),
                      TextButton(
                        onPressed: _canResendOtp
                            ? () async {
                                final provider = context.read<AuthProvider>();
                                final ok = await provider.login(
                                  widget.phoneNumber,
                                );
                                if (!context.mounted) return;
                                if (ok) {
                                  _startResendTimer();
                                }
                                ScaffoldMessenger.of(context).showSnackBar(
                                  SnackBar(
                                    content: Text(
                                      ok
                                          ? context.l10n.otpResent
                                          : context.l10n.resendOtpFailed,
                                    ),
                                  ),
                                );
                              }
                            : null,
                        child: Text(
                          context.l10n.resendOtp,
                          style: TextStyle(
                            color: _canResendOtp
                                ? Color(0xFF006B70)
                                : Colors.grey,
                            fontSize: 15,
                            fontWeight: _canResendOtp
                                ? FontWeight.w700
                                : FontWeight.w500,
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
              ),
              Consumer<AuthProvider>(
                builder: (_, provider, child) {
                  return CustomButton(
                    text: context.l10n.confirm,
                    isLoading: provider.isLoading,
                    onPressed: _canVerifyOtp
                        ? () async {
                            if (otpCode.length != 6) {
                              ScaffoldMessenger.of(context).showSnackBar(
                                SnackBar(
                                  content: Text(context.l10n.otpRequired),
                                ),
                              );
                              return;
                            }

                            final ok = await provider.verifyOtp(
                              widget.phoneNumber,
                              otpCode,
                            );
                            if (!context.mounted) return;

                            if (ok) {
                              final roleProvider = context.read<RoleProvider>();
                              if (provider.lastSelectedRole != null) {
                                roleProvider.setRole(
                                  provider.lastSelectedRole!,
                                );
                              } else if (provider.roles.isNotEmpty &&
                                  provider.roles.length == 1) {
                                roleProvider.setRole(provider.roles.first);
                              }

                              final Widget destination = switch (provider
                                  .nextStep) {
                                AuthNextStep.completeProfile => EditProfilePage(
                                  requiredCompletion: true,
                                  phoneNumber:
                                      provider.phoneNumber ??
                                      widget.phoneNumber,
                                ),
                                AuthNextStep.selectRole =>
                                  RoleSelectionPage(),
                                AuthNextStep.customerHome =>
                                  await _getDestination(
                                    context,
                                    provider,
                                    roleProvider,
                                  ),
                              };

                              if (!context.mounted) return;
                              Navigator.of(context).pushAndRemoveUntil(
                                MaterialPageRoute(builder: (_) => destination),
                                (_) => false,
                              );
                            } else {
                              final retryAfterSeconds =
                                  provider.otpRetryAfterSeconds ??
                                  (provider.lastErrorCode ==
                                          _otpAttemptsExceededCode
                                      ? _fallbackOtpLockSeconds
                                      : null);
                              if (retryAfterSeconds != null) {
                                _startOtpLockTimer(retryAfterSeconds);
                              }

                              ScaffoldMessenger.of(context).showSnackBar(
                                SnackBar(
                                  content: Text(
                                    retryAfterSeconds == null
                                        ? context.l10n.invalidOtp
                                        : '${context.l10n.otpLockedPrefix}${_formatDuration(retryAfterSeconds)}',
                                  ),
                                ),
                              );
                            }
                          }
                        : null,
                  );
                },
              ),
              SizedBox(height: 24),
            ],
          ),
        ),
      ),
    );
  }
}
