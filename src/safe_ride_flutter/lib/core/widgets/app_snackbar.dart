import 'package:flutter/material.dart';

enum AppSnackBarType { success, error, warning, info, serverError }

class AppSnackBar {
  static void show(
    BuildContext context, {
    required String message,
    required AppSnackBarType type,
    String? title,
    String? actionLabel,
    VoidCallback? onAction,
    Duration duration = const Duration(seconds: 4),
  }) {
    final messenger = ScaffoldMessenger.of(context);
    _showWithMessenger(
      messenger,
      message: message,
      type: type,
      title: title,
      actionLabel: actionLabel,
      onAction: onAction,
      duration: duration,
    );
  }

  static void showGlobal(
    GlobalKey<ScaffoldMessengerState> messengerKey, {
    required String message,
    required AppSnackBarType type,
    String? title,
    String? actionLabel,
    VoidCallback? onAction,
    Duration duration = const Duration(seconds: 4),
  }) {
    final messenger = messengerKey.currentState;
    if (messenger == null) return;
    _showWithMessenger(
      messenger,
      message: message,
      type: type,
      title: title,
      actionLabel: actionLabel,
      onAction: onAction,
      duration: duration,
    );
  }

  static void _showWithMessenger(
    ScaffoldMessengerState messenger, {
    required String message,
    required AppSnackBarType type,
    String? title,
    String? actionLabel,
    VoidCallback? onAction,
    Duration duration = const Duration(seconds: 4),
  }) {
    messenger.hideCurrentSnackBar();

    final IconData icon;
    final String defaultTitle;
    final List<Color> gradientColors;

    switch (type) {
      case AppSnackBarType.success:
        icon = Icons.check_circle_outline_rounded;
        defaultTitle = 'Thành công';
        gradientColors = [const Color(0xFF007A87), const Color(0xFF00897B)];
        break;
      case AppSnackBarType.error:
        icon = Icons.error_outline_rounded;
        defaultTitle = 'Lỗi';
        gradientColors = [const Color(0xFFC62828), const Color(0xFFE53935)];
        break;
      case AppSnackBarType.warning:
        icon = Icons.warning_amber_rounded;
        defaultTitle = 'Cảnh báo';
        gradientColors = [const Color(0xFFEF6C00), const Color(0xFFFF9800)];
        break;
      case AppSnackBarType.info:
        icon = Icons.info_outline_rounded;
        defaultTitle = 'Thông báo';
        gradientColors = [const Color(0xFF1565C0), const Color(0xFF1E88E5)];
        break;
      case AppSnackBarType.serverError:
        icon = Icons.cloud_off_rounded;
        defaultTitle = 'Lỗi kết nối / Máy chủ';
        gradientColors = [const Color(0xFF37474F), const Color(0xFF546E7A)];
        break;
    }

    final resolvedTitle = title ?? defaultTitle;

    messenger.showSnackBar(
      SnackBar(
        behavior: SnackBarBehavior.floating,
        backgroundColor: Colors.transparent,
        elevation: 0,
        duration:
            type == AppSnackBarType.serverError && onAction != null
                ? const Duration(seconds: 8)
                : duration,
        margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 16),
        content: Container(
          decoration: BoxDecoration(
            gradient: LinearGradient(
              colors: gradientColors,
              begin: Alignment.topLeft,
              end: Alignment.bottomRight,
            ),
            borderRadius: BorderRadius.circular(16),
            boxShadow: [
              BoxShadow(
                color: Colors.black.withOpacity(0.18),
                blurRadius: 12,
                offset: const Offset(0, 6),
              ),
            ],
            border: Border.all(color: Colors.white.withOpacity(0.12), width: 1),
          ),
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
          child: Row(
            children: [
              Container(
                padding: const EdgeInsets.all(8),
                decoration: BoxDecoration(
                  color: Colors.white.withOpacity(0.18),
                  shape: BoxShape.circle,
                ),
                child: Icon(icon, color: Colors.white, size: 24),
              ),
              const SizedBox(width: 16),
              Expanded(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      resolvedTitle,
                      style: const TextStyle(
                        color: Colors.white,
                        fontWeight: FontWeight.bold,
                        fontSize: 16,
                      ),
                    ),
                    const SizedBox(height: 2),
                    Text(
                      message,
                      style: TextStyle(
                        color: Colors.white.withOpacity(0.9),
                        fontSize: 13,
                        height: 1.3,
                      ),
                    ),
                  ],
                ),
              ),
              if (actionLabel != null && onAction != null) ...[
                const SizedBox(width: 12),
                TextButton(
                  onPressed: () {
                    messenger.hideCurrentSnackBar();
                    onAction();
                  },
                  style: TextButton.styleFrom(
                    backgroundColor: Colors.white.withOpacity(0.25),
                    foregroundColor: Colors.white,
                    padding: const EdgeInsets.symmetric(
                      horizontal: 16,
                      vertical: 8,
                    ),
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(12),
                    ),
                  ),
                  child: Text(
                    actionLabel,
                    style: const TextStyle(
                      fontWeight: FontWeight.bold,
                      fontSize: 13,
                    ),
                  ),
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}
