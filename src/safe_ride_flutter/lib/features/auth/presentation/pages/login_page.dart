import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'otp_page.dart';
import '../../../customer/home/presentation/pages/customer_home_page.dart';
import '../../../shared/onboarding/presentation/pages/role_selection_page.dart';
import '../../../shared/profile/presentation/pages/edit_profile_page.dart';

import '../../../../../core/constants/app_colors.dart';
import '../../../../../core/constants/app_strings.dart';
import '../../../../../core/utils/validators.dart';
import '../../../../../core/widgets/custom_button.dart';
import '../../../../../core/widgets/custom_textfield.dart';
import '../providers/auth_provider.dart';
import '../../../shared/onboarding/presentation/providers/role_provider.dart';
import '../../../customer/booking/presentation/providers/booking_provider.dart';
import '../../../driver/dashboard/presentation/pages/driver_dashboard_page.dart';

class LoginPage extends StatefulWidget {
  const LoginPage({super.key});

  @override
  State<LoginPage> createState() => _LoginPageState();
}

class _LoginPageState extends State<LoginPage> {
  final phoneController = TextEditingController();
  String _selectedCountryCode = AppValues.vietnamCountryCode;

  @override
  void dispose() {
    phoneController.dispose();
    super.dispose();
  }

  Future<Widget> _getDestination(BuildContext context, AuthProvider auth, RoleProvider roleProvider) async {
    // 1. Check for active booking first
    final bookingProvider = context.read<BookingProvider>();
    final activeBooking = await bookingProvider.loadActiveBooking(auth.token!);
    
    if (activeBooking != null) {
      // Force customer role and go to tracking
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
      backgroundColor: AppColors.background,
      body: SafeArea(
        child: SingleChildScrollView(
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.center,
              children: [
                const SizedBox(height: 80),
                
                Container(
                  width: 150,
                  height: 150,
                  decoration: BoxDecoration(
                    color: Colors.white,
                    shape: BoxShape.circle,
                    boxShadow: [
                      BoxShadow(
                        color: Colors.grey.withValues(alpha: 0.2),
                        spreadRadius: 2,
                        blurRadius: 10,
                        offset: const Offset(0, 5),
                      ),
                    ],
                  ),
                  child: Center(
                    child: Column(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        Image.network(AppConfig.logoUrl, height: 60),
                        const SizedBox(height: 8),
                      ],
                    ),
                  ),
                ),
                const SizedBox(height: 32),

                const Text(
                  AppStrings.appName,
                  style: TextStyle(
                    fontSize: 42,
                    fontWeight: FontWeight.bold,
                    color: AppColors.primary,
                  ),
                ),

                const SizedBox(height: 8),

                const Text(
                  AuthStrings.slogan,
                  style: TextStyle(
                    fontSize: 16,
                    color: AppColors.textSecondary,
                  ),
                ),

                const SizedBox(height: 48),

                Align(
                  alignment: Alignment.centerLeft,
                  child: Text(
                    AuthStrings.phoneNumber,
                    style: TextStyle(
                      fontSize: 16,
                      fontWeight: FontWeight.w500,
                      color: AppColors.textPrimary,
                    ),
                  ),
                ),
                const SizedBox(height: 12),

                CustomTextField(
                  controller: phoneController,
                  hintText: AuthStrings.phoneHint,
                  keyboardType: TextInputType.phone,
                  prefixIcon: _CountryCodePicker(
                    value: _selectedCountryCode,
                    onChanged: (value) {
                      if (value == null) return;
                      setState(() => _selectedCountryCode = value);
                    },
                  ),
                ),

                const SizedBox(height: 24),

                Consumer<AuthProvider>(
                  builder: (context, provider, child) {
                    return CustomButton(
                      text: AuthStrings.continueOrRegister,
                      isLoading: provider.isLoading,
                      onPressed: () async {
                        final rawPhone = phoneController.text.trim();
                        final normalizedPhone =
                            PhoneNumberValidator.normalizePhone(
                              rawPhone,
                              countryCode: _selectedCountryCode,
                            );

                        if (rawPhone.isEmpty) {
                          ScaffoldMessenger.of(context).showSnackBar(
                            const SnackBar(
                              content: Text(AuthStrings.phoneRequired),
                            ),
                          );
                          return;
                        }

                        if (normalizedPhone.isEmpty) {
                          ScaffoldMessenger.of(context).showSnackBar(
                            const SnackBar(
                              content: Text(AuthStrings.invalidPhone),
                            ),
                          );
                          return;
                        }

                        final success = await provider.login(normalizedPhone);
                        if (success && context.mounted) {
                          Navigator.push(
                            context,
                            MaterialPageRoute(
                              builder: (_) =>
                                  OtpPage(phoneNumber: normalizedPhone),
                            ),
                          );
                        } else if (context.mounted) {
                          ScaffoldMessenger.of(context).showSnackBar(
                            const SnackBar(
                              content: Text(AuthStrings.sendOtpFailed),
                            ),
                          );
                        }
                      },
                    );
                  },
                ),
                const SizedBox(height: 32),

                Row(
                  children: [
                    const Expanded(
                      child: Divider(color: AppColors.border, thickness: 1),
                    ),
                    Padding(
                      padding: const EdgeInsets.symmetric(horizontal: 16),
                      child: Text(
                        AuthStrings.or,
                        style: TextStyle(
                          color: AppColors.textSecondary,
                          fontWeight: FontWeight.w500,
                        ),
                      ),
                    ),
                    const Expanded(
                      child: Divider(color: AppColors.border, thickness: 1),
                    ),
                  ],
                ),
                const SizedBox(height: 32),

                Consumer<AuthProvider>(
                  builder: (context, provider, child) {
                    return ElevatedButton.icon(
                      onPressed: () async {
                        final ok = await provider.signInWithGoogle();
                        if (!context.mounted) return;

                        if (ok) {
                          final roleProvider = context.read<RoleProvider>();
                          if (provider.lastSelectedRole != null) {
                            roleProvider.setRole(provider.lastSelectedRole!);
                          } else if (provider.roles.isNotEmpty &&
                              provider.roles.length == 1) {
                            roleProvider.setRole(provider.roles.first);
                          }

                          final Widget destination =
                              switch (provider.nextStep) {
                                AuthNextStep.completeProfile => EditProfilePage(
                                  requiredCompletion: true,
                                  phoneNumber: provider.phoneNumber,
                                ),
                                AuthNextStep.selectRole =>
                                  const RoleSelectionPage(),
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
                            const SnackBar(
                              content: Text(AuthStrings.googleLoginFailed),
                            ),
                          );
                        }
                      },
                      style: ElevatedButton.styleFrom(
                        backgroundColor: Colors.white,
                        foregroundColor: AppColors.textPrimary,
                        minimumSize: const Size(double.infinity, 56),
                        shape: RoundedRectangleBorder(
                          borderRadius: BorderRadius.circular(16),
                          side: const BorderSide(color: AppColors.border),
                        ),
                        elevation: 0,
                      ),
                      icon: Image.network(AppConfig.googleLogoUrl, height: 24),
                      label: const Text(
                        AuthStrings.google,
                        style: TextStyle(
                          fontSize: 16,
                          fontWeight: FontWeight.w500,
                        ),
                      ),
                    );
                  },
                ),
                const SizedBox(height: 48),

                RichText(
                  textAlign: TextAlign.center,
                  text: TextSpan(
                    text: AuthStrings.continueAgreement,
                    style: TextStyle(
                      fontSize: 14,
                      color: AppColors.textSecondary,
                      height: 1.5,
                    ),
                    children: [
                      TextSpan(
                        text: AuthStrings.termsOfService,
                        style: TextStyle(
                          color: AppColors.primary,
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                      TextSpan(
                        text: AuthStrings.and,
                        style: TextStyle(color: AppColors.textSecondary),
                      ),
                      TextSpan(
                        text: AuthStrings.privacyPolicy,
                        style: TextStyle(
                          color: AppColors.primary,
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                      TextSpan(
                        text: AuthStrings.agreementSuffix,
                        style: TextStyle(color: AppColors.textSecondary),
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _CountryCodePicker extends StatelessWidget {
  final String value;
  final ValueChanged<String?> onChanged;

  const _CountryCodePicker({required this.value, required this.onChanged});

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(left: 12, right: 8),
      child: DropdownButtonHideUnderline(
        child: DropdownButton<String>(
          value: value,
          isDense: true,
          items: PhoneNumberValidator.supportedCountryCodes
              .map(
                (code) =>
                    DropdownMenuItem<String>(value: code, child: Text(code)),
              )
              .toList(),
          onChanged: onChanged,
        ),
      ),
    );
  }
}
