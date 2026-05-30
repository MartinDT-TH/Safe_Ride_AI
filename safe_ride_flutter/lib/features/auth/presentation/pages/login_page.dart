import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'otp_page.dart';

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

  @override
  void dispose() {
    phoneController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: SafeArea(
        child: SingleChildScrollView(
          child: Padding(
            padding: const EdgeInsets.all(24),

            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,

              children: [
                const SizedBox(height: 100),

                const Center(
                  child: Text(
                    'SafeRide',

                    style: TextStyle(
                      fontSize: 42,
                      fontWeight: FontWeight.bold,
                      color: AppColors.primary,
                    ),
                  ),
                ),

                const SizedBox(height: 16),

                const Center(child: Text('Chuyến đi an toàn và tiện lợi')),

                const SizedBox(height: 48),

                const Text('Số điện thoại'),

                const SizedBox(height: 12),

                CustomTextField(
                  controller: phoneController,
                  hintText: 'Nhập số điện thoại',
                ),

                const SizedBox(height: 24),

                Consumer<AuthProvider>(
                  builder: (_, provider, __) {
                    return CustomButton(
                      text: 'Tiếp tục',

                      isLoading: provider.isLoading,

                      onPressed: () async {
                        final phone = phoneController.text.trim();
                        if (phone.isEmpty) {
                          ScaffoldMessenger.of(context).showSnackBar(
                            const SnackBar(
                              content: Text('Vui lòng nhập số điện thoại'),
                            ),
                          );
                          return;
                        }

                        if(phone.length != 10) {
                          ScaffoldMessenger.of(context).showSnackBar(
                            const SnackBar(
                              content: Text('Số điện thoại không hợp lệ'),
                            ),
                          );
                          return;
                        }

                        final success = await provider.login(
                            phoneController.text
                        );
                        if (success && context.mounted) {
                          Navigator.push(
                            context,
                            MaterialPageRoute(
                              builder: (__) => const OtpPage(),
                            ),
                          );
                        }
                      },
                    );
                  },
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
