import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:provider/provider.dart';

import '../../../../../core/localization/locale_provider.dart';
import '../../../../../core/localization/localization_extensions.dart';
import '../../../../../core/utils/api_date_time.dart';
import '../../../../auth/presentation/providers/auth_provider.dart';
import '../../../../trip_sharing/presentation/pages/shared_trip_tracking_page.dart';
import '../providers/notification_provider.dart';

class NotificationsPage extends StatefulWidget {
  NotificationsPage({super.key});

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
      context.read<NotificationProvider>().initialize(
        token,
        refreshIfInitialized: true,
      );
    });
  }

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final provider = context.watch<NotificationProvider>();
    final auth = context.watch<AuthProvider>();

    return Scaffold(
      backgroundColor: Color(0xFFFCF9F9),
      appBar: AppBar(
        backgroundColor: Colors.white,
        elevation: 0.5,
        title: Text(
          l10n.notifications,
          style: TextStyle(
            color: Color(0xFF1A1A1A),
            fontWeight: FontWeight.bold,
          ),
        ),
        centerTitle: true,
      ),
      body: RefreshIndicator(
        onRefresh: () => provider.refresh(auth.token),
        color: Color(0xFF006B70),
        child: Builder(
          builder: (context) {
            if (provider.isLoading && provider.notifications.isEmpty) {
              return Center(
                child: CircularProgressIndicator(color: Color(0xFF006B70)),
              );
            }

            if (provider.errorMessage != null &&
                provider.notifications.isEmpty) {
              return ListView(
                physics: AlwaysScrollableScrollPhysics(),
                padding: const EdgeInsets.all(24),
                children: [
                  _EmptyState(
                    title: l10n.notificationsLoadFailed,
                    message: provider.errorMessage!,
                    icon: Icons.error_outline_rounded,
                    actionLabel: l10n.retry,
                    onAction: () => provider.refresh(auth.token),
                  ),
                ],
              );
            }

            if (provider.notifications.isEmpty) {
              return ListView(
                physics: AlwaysScrollableScrollPhysics(),
                padding: const EdgeInsets.all(24),
                children: [
                  _EmptyState(
                    title: l10n.noNotifications,
                    message: l10n.noNotificationsDescription,
                    icon: Icons.notifications_none_rounded,
                  ),
                ],
              );
            }

            return ListView.separated(
              physics: AlwaysScrollableScrollPhysics(),
              padding: const EdgeInsets.fromLTRB(16, 16, 16, 32),
              itemCount:
                  provider.notifications.length + (provider.hasMore ? 1 : 0),
              separatorBuilder: (_, __) => SizedBox(height: 12),
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
                  onTap: () async {
                    await provider.markAsRead(item.id);
                    if (!context.mounted ||
                        item.notificationType != 'TripShared' ||
                        item.referenceId == null) {
                      return;
                    }
                    await Navigator.of(context).push(
                      MaterialPageRoute(
                        builder: (_) => SharedTripTrackingPage(
                          tripShareId: item.referenceId!,
                        ),
                      ),
                    );
                  },
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
  _NotificationCard({required this.item, required this.onTap});

  final dynamic item;
  final Future<void> Function() onTap;

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final languageCode = context.watch<LocaleProvider>().locale.languageCode;
    final sentAtLabel = DateFormat(
      'HH:mm • dd/MM/yyyy',
      Localizations.localeOf(context).toLanguageTag(),
    ).format(toVietnamTime(item.sentAt));

    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: () => onTap(),
        borderRadius: BorderRadius.circular(20),
        child: Ink(
          decoration: BoxDecoration(
            color: item.isRead ? Colors.white : Color(0xFFEFF9F8),
            borderRadius: BorderRadius.circular(20),
            border: Border.all(
              color: item.isRead
                  ? Color(0xFFD8E4E4)
                  : Color(0xFF8FD3CB),
            ),
            boxShadow: [
              BoxShadow(
                color: Colors.black.withValues(alpha: 0.04),
                blurRadius: 10,
                offset: Offset(0, 4),
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
                    Spacer(),
                    if (!item.isRead)
                      Container(
                        width: 10,
                        height: 10,
                        decoration: BoxDecoration(
                          color: Color(0xFFE53935),
                          shape: BoxShape.circle,
                        ),
                      ),
                  ],
                ),
                SizedBox(height: 14),
                Text(
                  item.localizedTitle(languageCode),
                  style: TextStyle(
                    fontSize: 16,
                    fontWeight: FontWeight.w700,
                    color: Color(0xFF132C2E),
                  ),
                ),
                SizedBox(height: 8),
                Text(
                  item.localizedContent(languageCode),
                  style: TextStyle(
                    fontSize: 14,
                    height: 1.5,
                    color: Color(0xFF4A5A5B),
                  ),
                ),
                SizedBox(height: 14),
                Row(
                  children: [
                    Text(
                      sentAtLabel,
                      style: TextStyle(
                        fontSize: 12,
                        color: Color(0xFF7A8A8B),
                      ),
                    ),
                    Spacer(),
                    Text(
                      item.isRead ? l10n.read : l10n.unread,
                      style: TextStyle(
                        fontSize: 12,
                        fontWeight: FontWeight.w700,
                        color: item.isRead
                            ? Color(0xFF889899)
                            : Color(0xFF006B70),
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
  _TypeBadge({required this.type});

  final String type;

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final Color backgroundColor;
    final Color textColor;
    final String label;

    switch (type) {
      case 'Promotion':
        backgroundColor = Color(0xFFE5F7E9);
        textColor = Color(0xFF1B8A4B);
        label = l10n.notificationTypePromotion;
        break;
      case 'Warning':
        backgroundColor = Color(0xFFFFE6E0);
        textColor = Color(0xFFBE4A23);
        label = l10n.notificationTypeWarning;
        break;
      default:
        backgroundColor = Color(0xFFE4F2F3);
        textColor = Color(0xFF006B70);
        label = l10n.notificationTypeSystemUpdate;
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
  _LoadMoreCard({required this.isLoading, required this.onPressed});

  final bool isLoading;
  final VoidCallback onPressed;

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    return OutlinedButton(
      onPressed: isLoading ? null : onPressed,
      style: OutlinedButton.styleFrom(
        minimumSize: const Size.fromHeight(52),
        side: BorderSide(color: Color(0xFFBFD0D1)),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
      ),
      child: isLoading
          ? SizedBox(
              width: 20,
              height: 20,
              child: CircularProgressIndicator(
                strokeWidth: 2,
                color: Color(0xFF006B70),
              ),
            )
          : Text(
              l10n.loadMoreNotifications,
              style: TextStyle(
                color: Color(0xFF006B70),
                fontWeight: FontWeight.w700,
              ),
            ),
    );
  }
}

class _EmptyState extends StatelessWidget {
  _EmptyState({
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
            Icon(icon, size: 72, color: Color(0xFF90A4A5)),
            SizedBox(height: 16),
            Text(
              title,
              textAlign: TextAlign.center,
              style: TextStyle(
                fontSize: 20,
                fontWeight: FontWeight.bold,
                color: Color(0xFF1A1A1A),
              ),
            ),
            SizedBox(height: 10),
            Text(
              message,
              textAlign: TextAlign.center,
              style: TextStyle(
                fontSize: 14,
                height: 1.5,
                color: Color(0xFF6B7A7B),
              ),
            ),
            if (actionLabel != null && onAction != null) ...[
              SizedBox(height: 24),
              ElevatedButton(
                onPressed: onAction,
                style: ElevatedButton.styleFrom(
                  backgroundColor: Color(0xFF006B70),
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
