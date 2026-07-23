import 'package:flutter/foundation.dart';

import '../../data/datasources/notification_remote_datasource.dart';
import '../../data/models/system_notification_item.dart';
import '../../domain/repositories/notification_repository.dart';
import '../../../../../core/services/socket_service.dart';

class NotificationProvider extends ChangeNotifier {
  NotificationProvider(
    this._repository,
    this._socketService,
  );

  static const int _pageSize = 20;
  static const String _socketHandlerKey = 'sharedNotifications';

  final NotificationRepository _repository;
  final SocketService _socketService;

  List<SystemNotificationItem> _notifications = [];
  bool _isLoading = false;
  bool _isLoadingMore = false;
  bool _isInitialized = false;
  String? _errorMessage;
  int _currentPage = 1;
  int _totalPages = 1;
  int _totalItems = 0;
  int _unreadCount = 0;
  String? _accessToken;

  List<SystemNotificationItem> get notifications => _notifications;
  bool get isLoading => _isLoading;
  bool get isLoadingMore => _isLoadingMore;
  String? get errorMessage => _errorMessage;
  int get unreadCount => _unreadCount;
  bool get hasMore => _currentPage < _totalPages;

  Future<void> initialize(String? accessToken) async {
    if (accessToken == null || accessToken.isEmpty) {
      return;
    }

    final shouldReload = !_isInitialized || _accessToken != accessToken;
    _accessToken = accessToken;
    _attachRealtime(accessToken);

    if (shouldReload) {
      await refresh(accessToken);
      _isInitialized = true;
    }
  }

  Future<void> refresh([String? accessToken]) async {
    final token = accessToken ?? _accessToken;
    if (token == null || token.isEmpty) {
      _notifications = [];
      _errorMessage = 'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.';
      notifyListeners();
      return;
    }

    _isLoading = true;
    _errorMessage = null;
    notifyListeners();

    try {
      final page = await _repository.getNotifications(
        token,
        page: 1,
        pageSize: _pageSize,
      );

      _notifications = page.items;
      _currentPage = page.page;
      _totalPages = page.totalPages;
      _totalItems = page.totalItems;
      _unreadCount = page.unreadCount;
    } on NotificationApiException catch (exception) {
      _errorMessage = exception.message;
    } catch (_) {
      _errorMessage = 'Đã xảy ra lỗi. Vui lòng thử lại.';
    } finally {
      _isLoading = false;
      notifyListeners();
    }
  }

  Future<void> loadMore() async {
    final token = _accessToken;
    if (_isLoadingMore || token == null || token.isEmpty || !hasMore) {
      return;
    }

    _isLoadingMore = true;
    notifyListeners();

    try {
      final nextPage = _currentPage + 1;
      final page = await _repository.getNotifications(
        token,
        page: nextPage,
        pageSize: _pageSize,
      );

      _notifications = [..._notifications, ...page.items];
      _currentPage = page.page;
      _totalPages = page.totalPages;
      _totalItems = page.totalItems;
      _unreadCount = page.unreadCount;
      _errorMessage = null;
    } on NotificationApiException catch (exception) {
      _errorMessage = exception.message;
    } catch (_) {
      _errorMessage = 'Đã xảy ra lỗi. Vui lòng thử lại.';
    } finally {
      _isLoadingMore = false;
      notifyListeners();
    }
  }

  Future<void> markAsRead(int notificationId) async {
    final token = _accessToken;
    if (token == null || token.isEmpty) {
      return;
    }

    final index = _notifications.indexWhere((item) => item.id == notificationId);
    if (index < 0 || _notifications[index].isRead) {
      return;
    }

    try {
      final updatedItem = await _repository.markAsRead(token, notificationId);
      final nextNotifications = [..._notifications];
      nextNotifications[index] = updatedItem;
      _notifications = nextNotifications;
      _unreadCount = (_unreadCount - 1).clamp(0, _totalItems);
      notifyListeners();
    } on NotificationApiException catch (exception) {
      _errorMessage = exception.message;
      notifyListeners();
    } catch (_) {
      _errorMessage = 'Đã xảy ra lỗi. Vui lòng thử lại.';
      notifyListeners();
    }
  }

  void clear() {
    _notifications = [];
    _errorMessage = null;
    _currentPage = 1;
    _totalPages = 1;
    _totalItems = 0;
    _unreadCount = 0;
    _isInitialized = false;
    _accessToken = null;
    _socketService.removeSystemNotificationReceivedHandler(_socketHandlerKey);
    notifyListeners();
  }

  void _attachRealtime(String accessToken) {
    _socketService.removeSystemNotificationReceivedHandler(_socketHandlerKey);
    _socketService.onSystemNotificationReceived(
      _handleRealtimeNotification,
      key: _socketHandlerKey,
    );

    _socketService.connect(accessToken).catchError((Object error) {
      debugPrint('NotificationProvider: SignalR connect failed: $error');
      return null;
    });
  }

  void _handleRealtimeNotification(SystemNotificationUpdate update) {
    if (_notifications.any((item) => item.id == update.id)) {
      return;
    }

    final item = SystemNotificationItem(
      id: update.id,
      title: update.title,
      content: update.content,
      notificationType: update.notificationType,
      isRead: false,
      sentAt: update.sentAt,
    );

    final nextNotifications = [item, ..._notifications];
    if (nextNotifications.length > _pageSize) {
      nextNotifications.removeLast();
    }

    _notifications = nextNotifications;
    _totalItems += 1;
    _unreadCount += 1;
    notifyListeners();
  }

  @override
  void dispose() {
    _socketService.removeSystemNotificationReceivedHandler(_socketHandlerKey);
    super.dispose();
  }
}
