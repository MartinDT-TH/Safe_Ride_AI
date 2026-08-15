import 'package:flutter/material.dart';
import '../constants/app_colors.dart';
import '../localization/localization_extensions.dart';

class AppDialog extends StatelessWidget {
  final IconData icon;
  final Color iconColor;
  final Color iconBackgroundColor;
  final String title;
  final String description;
  final String confirmText;
  final String cancelText;
  final VoidCallback onConfirm;
  final VoidCallback? onCancel;

  AppDialog({
    super.key,
    required this.icon,
    this.iconColor = Colors.red,
    this.iconBackgroundColor = const Color(0xFFFFEBEE),
    required this.title,
    required this.description,
    this.confirmText = '',
    this.cancelText = '',
    required this.onConfirm,
    this.onCancel,
  });

  @override
  Widget build(BuildContext context) {
    return Dialog(
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(24)),
      elevation: 0,
      backgroundColor: Colors.white,
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            // Icon hình tròn phía trên
            Container(
              padding: const EdgeInsets.all(16),
              decoration: BoxDecoration(
                color: iconBackgroundColor,
                shape: BoxShape.circle,
              ),
              child: Icon(icon, color: iconColor, size: 32),
            ),
            SizedBox(height: 24),
            // Tiêu đề
            Text(
              title,
              style: TextStyle(
                fontSize: 20,
                fontWeight: FontWeight.bold,
                color: Color(0xFF1A1A1A),
              ),
              textAlign: TextAlign.center,
            ),
            SizedBox(height: 12),
            // Nội dung mô tả
            Text(
              description,
              style: TextStyle(
                fontSize: 15,
                color: Color(0xFF666666),
                height: 1.5,
              ),
              textAlign: TextAlign.center,
            ),
            SizedBox(height: 32),
            // Hàng nút bấm
            Column(
              children: [
                // Nút Xác nhận (màu Teal chủ đạo)
                SizedBox(
                  width: double.infinity,
                  height: 52,
                  child: ElevatedButton(
                    onPressed: onConfirm,
                    style: ElevatedButton.styleFrom(
                      backgroundColor: AppColors.primary,
                      foregroundColor: Colors.white,
                      elevation: 0,
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(16),
                      ),
                    ),
                    child: Text(
                      confirmText.isEmpty ? context.l10n.confirm : confirmText,
                      style: TextStyle(
                        fontSize: 16,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                  ),
                ),
                SizedBox(height: 12),
                // Nút Hủy (Outlined)
                SizedBox(
                  width: double.infinity,
                  height: 52,
                  child: OutlinedButton(
                    onPressed: onCancel ?? () => Navigator.pop(context),
                    style: OutlinedButton.styleFrom(
                      side: BorderSide(color: Color(0xFFE0E0E0)),
                      foregroundColor: AppColors.primary,
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(16),
                      ),
                    ),
                    child: Text(
                      cancelText.isEmpty ? context.l10n.cancel : cancelText,
                      style: TextStyle(
                        fontSize: 16,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                  ),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }

  // Hàm static để gọi popup nhanh hơn
  static void show({
    required BuildContext context,
    required IconData icon,
    Color iconColor = Colors.red,
    Color iconBackgroundColor = const Color(0xFFFFEBEE),
    required String title,
    required String description,
    String confirmText = '',
    String cancelText = '',
    required VoidCallback onConfirm,
    VoidCallback? onCancel,
  }) {
    showDialog(
      context: context,
      barrierDismissible: true,
      builder: (context) => AppDialog(
        icon: icon,
        iconColor: iconColor,
        iconBackgroundColor: iconBackgroundColor,
        title: title,
        description: description,
        confirmText: confirmText,
        cancelText: cancelText,
        onConfirm: onConfirm,
        onCancel: onCancel,
      ),
    );
  }
}

