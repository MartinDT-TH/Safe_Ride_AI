import 'package:flutter/material.dart';
import '../constants/app_colors.dart';

class AppLoadingScreen extends StatelessWidget {
  final String? message;

  AppLoadingScreen({super.key, this.message});

  static BuildContext? _dialogContext;

  static Future<void> show(BuildContext context, {String? message}) {
    return showDialog<void>(
      context: context,
      barrierDismissible: false,
      builder: (dialogContext) {
        _dialogContext = dialogContext;
        return Center(
          child: Material(
            color: Colors.transparent,
            child: Container(
              width: 220,
              padding: const EdgeInsets.all(24),
              decoration: BoxDecoration(
                color: Colors.white,
                borderRadius: BorderRadius.circular(16),
              ),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  CircularProgressIndicator(
                    valueColor: AlwaysStoppedAnimation<Color>(
                      AppColors.primary,
                    ),
                  ),
                  if (message != null) ...[
                    SizedBox(height: 16),
                    Text(
                      message,
                      textAlign: TextAlign.center,
                      style: TextStyle(
                        fontSize: 14,
                        color: Color(0xFF667174),
                        fontWeight: FontWeight.w500,
                      ),
                    ),
                  ],
                ],
              ),
            ),
          ),
        );
      },
    );
  }

  static void hide([BuildContext? context]) {
    final dialogContext = _dialogContext ?? context;
    if (dialogContext == null) return;

    final navigator = Navigator.of(dialogContext, rootNavigator: true);
    if (navigator.canPop()) {
      navigator.pop();
    }
    _dialogContext = null;
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: Color(0xFFF7FAFA), // Màu nền nhạt phía sau
      body: SafeArea(
        bottom: false,
        child: Column(
          children: [
            // Khoảng trống phía trên (thay thế cho map/content nền)
            Spacer(flex: 3),

            // Panel trắng bo tròn phía dưới
            Expanded(
              flex: 7,
              child: Container(
                width: double.infinity,
                decoration: BoxDecoration(
                  color: Colors.white,
                  borderRadius: BorderRadius.vertical(
                    top: Radius.circular(32), // Bo tròn góc giống hình mẫu
                  ),
                  boxShadow: [
                    BoxShadow(
                      color: Colors.black12,
                      blurRadius: 15,
                      offset: Offset(0, -5),
                    ),
                  ],
                ),
                child: Column(
                  children: [
                    SizedBox(height: 12),
                    // Thanh handle bar giống hình mẫu
                    Container(
                      width: 40,
                      height: 5,
                      decoration: BoxDecoration(
                        color: Color(0xFFD8DCDD),
                        borderRadius: BorderRadius.circular(10),
                      ),
                    ),
                    Spacer(flex: 2),
                    CircularProgressIndicator(
                      valueColor: AlwaysStoppedAnimation<Color>(
                        AppColors.primary,
                      ),
                      strokeWidth: 3,
                    ),
                    if (message != null) ...[
                      SizedBox(height: 24),
                      Padding(
                        padding: const EdgeInsets.symmetric(horizontal: 32),
                        child: Text(
                          message!,
                          textAlign: TextAlign.center,
                          style: TextStyle(
                            fontSize: 16,
                            color: Color(0xFF667174),
                            fontWeight: FontWeight.w500,
                          ),
                        ),
                      ),
                    ],
                    Spacer(flex: 3),
                  ],
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
