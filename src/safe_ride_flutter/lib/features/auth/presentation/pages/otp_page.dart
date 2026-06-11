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
      backgroundColor: Colors.white,
      appBar: AppBar(
        backgroundColor: Colors.white,
        elevation: 0,
        leading: IconButton(
          icon: const Icon(Icons.arrow_back, color: Color(0xFF006B70)),
          onPressed: () => Navigator.pop(context),
        ),
        title: const Text(
          'SafeRide',
          style: TextStyle(
            color: Color(0xFF006B70),
            fontWeight: FontWeight.bold,
          ),
        ),
        centerTitle: true,
        bottom: PreferredSize(
          preferredSize: const Size.fromHeight(1),
          child: Container(
            color: Colors.grey.shade100,
            height: 1,
          ),
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
                        'Xác thực mã OTP',
                        style: TextStyle(
                          fontSize: 28,
                          fontWeight: FontWeight.bold,
                          color: Color(0xFF1A1A1A),
                        ),
                      ),
                      const SizedBox(height: 16),
                      const Text(
                        'Vui lòng nhập mã gồm 6 chữ số đã được\ngửi đến số điện thoại của bạn.',
                        textAlign: TextAlign.center,
                        style: TextStyle(
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
                            TextSpan(text: 'Gửi lại sau '),
                            TextSpan(
                              text: '00:57',
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
                        onPressed: () {
                          // Resend OTP logic
                        },
                        child: const Text(
                          'Gửi lại OTP',
                          style: TextStyle(
                            color: Colors.grey,
                            fontSize: 15,
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
              ),
              CustomButton(
                text: 'Xác nhận',
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
              const SizedBox(height: 24),
            ],
          ),
        ),
      ),
    );
  }
}
