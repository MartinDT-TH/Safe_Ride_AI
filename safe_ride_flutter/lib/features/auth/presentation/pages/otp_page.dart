import 'package:flutter/material.dart';
import 'package:pin_code_fields/pin_code_fields.dart';

import '../../../../core/constants/app_colors.dart';
import '../../../../core/widgets/custom_button.dart';
import '../../../onboarding/presentation/pages/role_selection_page.dart';

class OtpPage extends StatefulWidget {
  const OtpPage({super.key});

  @override
  State<OtpPage> createState() => _OtpPageState();
}

class _OtpPageState extends State<OtpPage> {
  String otpCode = '';

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(),

      body: Padding(
        padding: const EdgeInsets.all(24),

        child: Column(
          children: [
            const SizedBox(height: 40),

            const Text(
              'Xác thực mã OTP',
              style: TextStyle(fontSize: 30, fontWeight: FontWeight.bold),
            ),

            const SizedBox(height: 12),

            const Text('Nhập mã OTP gồm 6 chữ số'),

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

                fieldHeight: 60,
                fieldWidth: 50,

                activeColor: AppColors.primary,

                selectedColor: AppColors.primary,

                inactiveColor: Colors.grey.shade300,
              ),
            ),

            const Spacer(),

            CustomButton(
              text: 'Xác thực',

              onPressed: () {
                if (otpCode == '123456') {
                  Navigator.pushReplacement(
                    context,

                    MaterialPageRoute(
                      builder: (_) => const RoleSelectionPage(),
                    ),
                  );
                } else {
                  ScaffoldMessenger.of(context).showSnackBar(
                    const SnackBar(content: Text('OTP không đúng')),
                  );
                }
              },
            ),
          ],
        ),
      ),
    );
  }
}
