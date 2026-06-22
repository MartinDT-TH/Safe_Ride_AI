import 'package:flutter/material.dart';
import 'package:pin_code_fields/pin_code_fields.dart';
import 'package:provider/provider.dart';

import '../../../../../core/constants/app_strings.dart';
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

  const OtpPage({super.key, required this.phoneNumber});

  @override
  State<OtpPage> createState() => _OtpPageState();
}

class _OtpPageState extends State<OtpPage> {
  String otpCode = '';

  Future<Widget> _getDestination(BuildContext context, AuthProvider auth, RoleProvider roleProvider) async {
    // 1. Check for active booking first
    final bookingProvider = context.read<BookingProvider>();
    final activeBooking = await bookingProvider.loadActiveBooking(auth.token!);
    
    if (activeBooking != null) {
      // Force customer role
      roleProvider.setRole(AppValues.roleCustomer);
      return const CustomerHomePage();
    }

    // 2. No active booking, fallback to role logic
    if (roleProvider.isDriver) {
      return const DriverDashboardPage();
    }
    return const CustomerHomePage();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: Colors.white,
      appBar: AppBar(
        backgroundColor: Colors.white,
        elevation: 0,
        leading: IconButton(
          icon: const Icon(Icons.arrow_back, color: Color(0xFF006B70)),
          onPressed: () => Navigator.pop(context),
        ),
        title: const Text(
          AppStrings.appName,
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
                      const SizedBox(height: 48),
                      // Lock Icon circular container
                      Center(
                        child: Container(
                          width: 120,
                          height: 120,
                          decoration: const BoxDecoration(
                            color: Color(0xFFE8F2F2),
                            shape: BoxShape.circle,
                          ),
                          child: const Icon(
                            Icons.lock_person_outlined,
                            size: 60,
                            color: Color(0xFF006B70),
                          ),
                        ),
                      ),
                      const SizedBox(height: 32),
                      const Text(
                        AuthStrings.otpTitle,
                        style: TextStyle(
                          fontSize: 28,
                          fontWeight: FontWeight.bold,
                          color: Color(0xFF1A1A1A),
                        ),
                      ),
                      const SizedBox(height: 16),
                      Text(
                        AuthStrings.otpDescription(widget.phoneNumber),
                        textAlign: TextAlign.center,
                        style: const TextStyle(
                          fontSize: 15,
                          color: Color(0xFF666666),
                          height: 1.5,
                        ),
                      ),
                      const SizedBox(height: 40),
                      PinCodeTextField(
                        appContext: context,
                        length: 6,
                        keyboardType: TextInputType.number,
                        onChanged: (value) {
                          otpCode = value;
                        },
                        pinTheme: PinTheme(
                          shape: PinCodeFieldShape.box,
                          borderRadius: BorderRadius.circular(12),
                          fieldHeight: 56,
                          fieldWidth: 46,
                          activeColor: const Color(0xFF006B70),
                          selectedColor: const Color(0xFF006B70),
                          inactiveColor: Colors.grey.shade300,
                          activeFillColor: Colors.white,
                          inactiveFillColor: Colors.white,
                          selectedFillColor: Colors.white,
                          borderWidth: 1,
                        ),
                        enableActiveFill: true,
                        cursorColor: const Color(0xFF006B70),
                        animationType: AnimationType.fade,
                        mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      ),
                      const SizedBox(height: 32),
                      RichText(
                        text: const TextSpan(
                          style: TextStyle(fontSize: 15, color: Colors.grey),
                          children: [
                            TextSpan(text: AuthStrings.resendAfter),
                            TextSpan(
                              text: AuthStrings.resendTimer,
                              style: TextStyle(
                                color: Color(0xFF006B70),
                                fontWeight: FontWeight.bold,
                              ),
                            ),
                          ],
                        ),
                      ),
                      const SizedBox(height: 8),
                      TextButton(
                        onPressed: () async {
                          final provider = context.read<AuthProvider>();
                          final ok = await provider.login(widget.phoneNumber);
                          if (!context.mounted) return;
                          ScaffoldMessenger.of(context).showSnackBar(
                            SnackBar(
                              content: Text(
                                ok
                                    ? AuthStrings.otpResent
                                    : AuthStrings.resendOtpFailed,
                              ),
                            ),
                          );
                        },
                        child: const Text(
                          AuthStrings.resendOtp,
                          style: TextStyle(color: Colors.grey, fontSize: 15),
                        ),
                      ),
                    ],
                  ),
                ),
              ),
              Consumer<AuthProvider>(
                builder: (_, provider, child) {
                  return CustomButton(
                    text: AppStrings.confirm,
                    isLoading: provider.isLoading,
                    onPressed: () async {
                      if (otpCode.length != 6) {
                        ScaffoldMessenger.of(context).showSnackBar(
                          const SnackBar(
                            content: Text(AuthStrings.otpRequired),
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
                          roleProvider.setRole(provider.lastSelectedRole!);
                        } else if (provider.roles.isNotEmpty &&
                            provider.roles.length == 1) {
                          roleProvider.setRole(provider.roles.first);
                        }

                        final Widget destination = switch (provider.nextStep) {
                          AuthNextStep.completeProfile => EditProfilePage(
                            requiredCompletion: true,
                            phoneNumber:
                                provider.phoneNumber ?? widget.phoneNumber,
                          ),
                          AuthNextStep.selectRole => const RoleSelectionPage(),
                          AuthNextStep.customerHome =>
                            await _getDestination(context, provider, roleProvider),
                        };

                        if (!context.mounted) return;
                        Navigator.of(context).pushAndRemoveUntil(
                          MaterialPageRoute(builder: (_) => destination),
                          (_) => false,
                        );
                      } else {
                        ScaffoldMessenger.of(context).showSnackBar(
                          const SnackBar(content: Text(AuthStrings.invalidOtp)),
                        );
                      }
                    },
                  );
                },
              ),
              const SizedBox(height: 24),
            ],
          ),
        ),
      ),
    );
  }
}
