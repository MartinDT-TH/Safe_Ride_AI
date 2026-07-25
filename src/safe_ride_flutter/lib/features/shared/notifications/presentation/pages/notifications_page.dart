import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:provider/provider.dart';

import '../../../../auth/presentation/providers/auth_provider.dart';
import '../providers/notification_provider.dart';

class NotificationsPage extends StatefulWidget {
  const NotificationsPage({super.key});

  @override
  State<NotificationsPage> createState() => _NotificationsPageState();
}

class _NotificationsPageState extends State<NotificationsPage> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) {
        return;
      }

      final token = context.read<AuthProvider>().token;
      context.read<NotificationProvider>().initialize(token);
    });
  }

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<NotificationProvider>();
    final auth = context.watch<AuthProvider>();

    return Scaffold(
      backgroundColor: const Color(0xFFFCF9F9),
      appBar: AppBar(
        backgroundColor: Colors.white,
        elevation: 0.5,
        title: const Text(
          'Thông báo',
          style: TextStyle(
            color: Color(0xFF1A1A1A),
            fontWeight: FontWeight.bold,
          ),
        ),
        centerTitle: true,
      ),
      body: RefreshIndicator(
        onRefresh: () => provider.refresh(auth.token),
        color: const Color(0xFF006B70),
        child: Builder(
          builder: (context) {
            if (provider.isLoading && provider.notifications.isEmpty) {
              return const Center(
                child: CircularProgressIndicator(color: Color(0xFF006B70)),
              );
            }

            if (provider.errorMessage != null && provider.notifications.isEmpty) {
              return ListView(
                physics: const AlwaysScrollableScrollPhysics(),
                padding: const EdgeInsets.all(24),
                children: [
                  _EmptyState(
                    title: 'Không thể tải thông báo',
                    message: provider.errorMessage!,
                    icon: Icons.error_outline_rounded,
                    actionLabel: 'Thử lại',
                    onAction: () => provider.refresh(auth.token),
                  ),
                ],
              );
            }

            if (provider.notifications.isEmpty) {
              return ListView(
                physics: const AlwaysScrollableScrollPhysics(),
                padding: const EdgeInsets.all(24),
                children: const [
                  _EmptyState(
                    title: 'Chưa có thông báo',
                    message:
                        'Các thông báo hệ thống được duyệt sẽ xuất hiện tại đây để bạn theo dõi.',
                    icon: Icons.notifications_none_rounded,
                  ),
                ],
              );
            }

            return ListView.separated(
              physics: const AlwaysScrollableScrollPhysics(),
              padding: const EdgeInsets.fromLTRB(16, 16, 16, 32),
              itemCount:
                  provider.notifications.length + (provider.hasMore ? 1 : 0),
              separatorBuilder: (_, __) => const SizedBox(height: 12),
              itemBuilder: (context, index) {
                if (index >= provider.notifications.length) {
                  return _LoadMoreCard(
                    isLoading: provider.isLoadingMore,
                    onPressed: provider.loadMore,
                  );
                }

                final item = provider.notifications[index];
                return _NotificationCard(
                  item: item,
                  onTap: () => provider.markAsRead(item.id),
                );
              },
            );
          },
        ),
      ),
    );
  }
}

class _NotificationCard extends StatelessWidget {
  const _NotificationCard({
    required this.item,
    required this.onTap,
  });

  final dynamic item;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final sentAtLabel = DateFormat(
      'HH:mm • dd/MM/yyyy',
      'vi_VN',
    ).format(item.sentAt.toLocal());

    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(20),
        child: Ink(
          decoration: BoxDecoration(
            color: item.isRead ? Colors.white : const Color(0xFFEFF9F8),
            borderRadius: BorderRadius.circular(20),
            border: Border.all(
              color: item.isRead
                  ? const Color(0xFFD8E4E4)
                  : const Color(0xFF8FD3CB),
            ),
            boxShadow: [
              BoxShadow(
                color: Colors.black.withValues(alpha: 0.04),
                blurRadius: 10,
                offset: const Offset(0, 4),
              ),
            ],
          ),
          child: Padding(
            padding: const EdgeInsets.all(18),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    _TypeBadge(type: item.notificationType),
                    const Spacer(),
                    if (!item.isRead)
                      Container(
                        width: 10,
                        height: 10,
                        decoration: const BoxDecoration(
                          color: Color(0xFFE53935),
                          shape: BoxShape.circle,
                        ),
                      ),
                  ],
                ),
                const SizedBox(height: 14),
                Text(
                  item.title,
                  style: const TextStyle(
                    fontSize: 16,
                    fontWeight: FontWeight.w700,
                    color: Color(0xFF132C2E),
                  ),
                ),
                const SizedBox(height: 8),
                Text(
                  item.content,
                  style: const TextStyle(
                    fontSize: 14,
                    height: 1.5,
                    color: Color(0xFF4A5A5B),
                  ),
                ),
                const SizedBox(height: 14),
                Row(
                  children: [
                    Text(
                      sentAtLabel,
                      style: const TextStyle(
                        fontSize: 12,
                        color: Color(0xFF7A8A8B),
                      ),
                    ),
                    const Spacer(),
                    Text(
                      item.isRead ? 'Đã đọc' : 'Chưa đọc',
                      style: TextStyle(
                        fontSize: 12,
                        fontWeight: FontWeight.w700,
                        color: item.isRead
                            ? const Color(0xFF889899)
                            : const Color(0xFF006B70),
                      ),
                    ),
                  ],
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _TypeBadge extends StatelessWidget {
  const _TypeBadge({required this.type});

  final String type;

  @override
  Widget build(BuildContext context) {
    final Color backgroundColor;
    final Color textColor;
    final String label;

    switch (type) {
      case 'Promotion':
        backgroundColor = const Color(0xFFE5F7E9);
        textColor = const Color(0xFF1B8A4B);
        label = 'Khuyến mãi';
        break;
      case 'Warning':
        backgroundColor = const Color(0xFFFFE6E0);
        textColor = const Color(0xFFBE4A23);
        label = 'Cảnh báo';
        break;
      default:
        backgroundColor = const Color(0xFFE4F2F3);
        textColor = const Color(0xFF006B70);
        label = 'Cập nhật hệ thống';
        break;
    }

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
      decoration: BoxDecoration(
        color: backgroundColor,
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(
        label,
        style: TextStyle(
          fontSize: 12,
          fontWeight: FontWeight.w700,
          color: textColor,
        ),
      ),
    );
  }
}

class _LoadMoreCard extends StatelessWidget {
  const _LoadMoreCard({
    required this.isLoading,
    required this.onPressed,
  });

  final bool isLoading;
  final VoidCallback onPressed;

  @override
  Widget build(BuildContext context) {
    return OutlinedButton(
      onPressed: isLoading ? null : onPressed,
      style: OutlinedButton.styleFrom(
        minimumSize: const Size.fromHeight(52),
        side: const BorderSide(color: Color(0xFFBFD0D1)),
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(16),
        ),
      ),
      child: isLoading
          ? const SizedBox(
              width: 20,
              height: 20,
              child: CircularProgressIndicator(
                strokeWidth: 2,
                color: Color(0xFF006B70),
              ),
            )
          : const Text(
              'Xem thêm thông báo',
              style: TextStyle(
                color: Color(0xFF006B70),
                fontWeight: FontWeight.w700,
              ),
            ),
    );
  }
}

class _EmptyState extends StatelessWidget {
  const _EmptyState({
    required this.title,
    required this.message,
    required this.icon,
    this.actionLabel,
    this.onAction,
  });

  final String title;
  final String message;
  final IconData icon;
  final String? actionLabel;
  final VoidCallback? onAction;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.only(top: 48),
        child: Column(
          children: [
            Icon(icon, size: 72, color: const Color(0xFF90A4A5)),
            const SizedBox(height: 16),
            Text(
              title,
              textAlign: TextAlign.center,
              style: const TextStyle(
                fontSize: 20,
                fontWeight: FontWeight.bold,
                color: Color(0xFF1A1A1A),
              ),
            ),
            const SizedBox(height: 10),
            Text(
              message,
              textAlign: TextAlign.center,
              style: const TextStyle(
                fontSize: 14,
                height: 1.5,
                color: Color(0xFF6B7A7B),
              ),
            ),
            if (actionLabel != null && onAction != null) ...[
              const SizedBox(height: 24),
              ElevatedButton(
                onPressed: onAction,
                style: ElevatedButton.styleFrom(
                  backgroundColor: const Color(0xFF006B70),
                  foregroundColor: Colors.white,
                  padding: const EdgeInsets.symmetric(
                    horizontal: 24,
                    vertical: 12,
                  ),
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(14),
                  ),
                ),
                child: Text(actionLabel!),
              ),
            ],
          ],
        ),
      ),
    );
  }
}
