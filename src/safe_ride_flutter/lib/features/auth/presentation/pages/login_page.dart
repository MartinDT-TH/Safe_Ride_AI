import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'otp_page.dart';
import '../../../home/presentation/pages/customer_home_page.dart';
import '../../../onboarding/presentation/pages/role_selection_page.dart';
import '../../../profile/presentation/pages/edit_profile_page.dart';

import '../../../../core/constants/app_colors.dart';
import '../../../../core/widgets/custom_button.dart';
import '../../../../core/widgets/custom_textfield.dart';
import '../providers/auth_provider.dart';

class LoginPage extends StatefulWidget {
  const LoginPage({super.key});

  @override
  State<LoginPage> createState() => _LoginPageState();
}

class _LoginPageState extends State<LoginPage> {
  final phoneController = TextEditingController();

  String _normalizePhone(String value) {
    final digits = value.replaceAll(RegExp(r'\D'), '');
    if (digits.length == 9) {
      return '+84$digits';
    }
    if (digits.length == 10 && digits.startsWith('0')) {
      return '+84${digits.substring(1)}';
    }
    return digits;
  }

  @override
  void dispose() {
    phoneController.dispose();
    super.dispose();
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

                // Logo container
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
                        Image.network(
                          'https://res.cloudinary.com/dj7y3ikck/image/upload/v1781487774/logo_poxclo.png',
                          height: 60,
                        ),
                        const SizedBox(height: 8),
                      ],
                    ),
                  ),
                ),
                const SizedBox(height: 32),

                const Text(
                  'SafeRide',
                  style: TextStyle(
                    fontSize: 42,
                    fontWeight: FontWeight.bold,
                    color: AppColors.primary,
                  ),
                ),

                const SizedBox(height: 8),

                const Text(
                  'Chuyến đi an toàn, tin cậy tuyệt đối', // Updated slogan
                  style: TextStyle(
                    fontSize: 16,
                    color: AppColors.textSecondary,
                  ),
                ),

                const SizedBox(height: 48),

                Align(
                  alignment: Alignment.centerLeft,
                  child: Text(
                    'Số điện thoại',
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
                  hintText: 'Nhập số điện thoại',
                  keyboardType: TextInputType.phone,
                  prefixText: '+84 ',
                ),

                const SizedBox(height: 24),

                Consumer<AuthProvider>(
                  builder: (context, provider, child) {
                    return CustomButton(
                      text: 'Tiếp tục / Đăng ký',
                      isLoading: provider.isLoading,
                      onPressed: () async {
                        final rawPhone = phoneController.text.trim();
                        final normalizedPhone = _normalizePhone(rawPhone);

                        if (rawPhone.isEmpty) {
                          ScaffoldMessenger.of(context).showSnackBar(
                            const SnackBar(
                              content: Text('Vui lòng nhập số điện thoại'),
                            ),
                          );
                          return;
                        }

                        if (normalizedPhone.length != 12 ||
                            !normalizedPhone.startsWith('+84')) {
                          ScaffoldMessenger.of(context).showSnackBar(
                            const SnackBar(
                              content: Text('Số điện thoại không hợp lệ'),
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
                              content: Text(
                                'Không thể gửi OTP. Kiểm tra API hoặc số điện thoại.',
                              ),
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
                        'HOẶC',
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
                          final Widget destination =
                              switch (provider.nextStep) {
                                AuthNextStep.completeProfile => EditProfilePage(
                                  requiredCompletion: true,
                                  phoneNumber: provider.phoneNumber,
                                ),
                                AuthNextStep.selectRole =>
                                  const RoleSelectionPage(),
                                AuthNextStep.customerHome =>
                                  const CustomerHomePage(),
                              };
                          Navigator.of(context).pushAndRemoveUntil(
                            MaterialPageRoute(builder: (_) => destination),
                            (_) => false,
                          );
                        } else {
                          ScaffoldMessenger.of(context).showSnackBar(
                            const SnackBar(
                              content: Text('Đăng nhập Google thất bại'),
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
                      icon: Image.network(
                        'https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcQ2sSeQqjaUTuZ3gRgkKjidpaipF_l6s72lBw&s',
                        height: 24,
                      ),
                      label: const Text(
                        'Google',
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
                    text: 'Bằng việc tiếp tục, bạn đồng ý với ',
                    style: TextStyle(
                      fontSize: 14,
                      color: AppColors.textSecondary,
                      height: 1.5,
                    ),
                    children: [
                      TextSpan(
                        text: 'Điều khoản dịch vụ',
                        style: TextStyle(
                          color: AppColors.primary,
                          fontWeight: FontWeight.bold,
                        ),
                        // Add onTap functionality for navigation if needed
                      ),
                      TextSpan(
                        text: ' và ',
                        style: TextStyle(color: AppColors.textSecondary),
                      ),
                      TextSpan(
                        text: 'Chính sách bảo mật',
                        style: TextStyle(
                          color: AppColors.primary,
                          fontWeight: FontWeight.bold,
                        ),
                        // Add onTap functionality for navigation if needed
                      ),
                      TextSpan(
                        text: ' của chúng tôi.',
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
