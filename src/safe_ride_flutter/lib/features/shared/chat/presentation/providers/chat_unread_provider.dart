import 'dart:async';
import 'dart:collection';

import 'package:flutter/material.dart';

import '../../../../../core/constants/app_strings.dart';
import '../../../../../core/services/socket_service.dart';
import '../../../../../core/session/session_coordinator.dart';
import '../../../../auth/presentation/providers/auth_provider.dart';
import '../../../history/data/models/history_trip.dart';
import '../../../history/domain/repositories/history_repository.dart';
import '../../data/datasources/trip_chat_remote_datasource.dart';
import '../../data/models/trip_chat_message_model.dart';
import '../../data/models/trip_chat_unread_model.dart';
import '../../data/services/trip_chat_socket_service.dart';
import '../pages/trip_chat_page.dart';

class ChatUnreadProvider extends ChangeNotifier {
  ChatUnreadProvider(
    this._remoteDatasource,
    this._historyRepository,
    this._appSocketService,
  );

  static const _socketHandlerKey = 'chat-unread';
  static const _terminalChatRetention = Duration(days: 7);

  final TripChatRemoteDatasource _remoteDatasource;
  final HistoryRepository _historyRepository;
  final SocketService _appSocketService;
  final TripChatSocketService _chatSocketService = TripChatSocketService();
  final Map<int, TripChatUnreadItemModel> _items = {};
  final Queue<_PendingChatNotification> _notificationQueue = Queue();

  String? _token;
  String? _currentUserId;
  Set<String> _roles = {};
  int? _activeChatTripId;
  Timer? _overlayTimer;
  Timer? _refreshDebounce;
  OverlayEntry? _overlayEntry;
  bool _isConfiguring = false;
  bool _appSocketHandlersAttached = false;

  int get totalUnread =>
      _items.values.fold(0, (total, item) => total + item.unreadCount);

  int unreadCountForTrip(int? tripId) =>
      tripId == null ? 0 : _items[tripId]?.unreadCount ?? 0;

  Future<void> updateSession(AuthProvider auth) async {
    final token = auth.token;
    final userId = auth.userId;
    final roles = auth.roles.map((role) => role.toLowerCase()).toSet();
    if (token == null || token.isEmpty || userId == null || userId.isEmpty) {
      await clearSession();
      return;
    }

    final sessionChanged = token != _token || userId != _currentUserId;
    _token = token;
    _currentUserId = userId;
    _roles = roles;
    if (_isConfiguring) return;

    _isConfiguring = true;
    try {
      if (sessionChanged) {
        await _chatSocketService.disconnect();
        _items.clear();
      }
      await refresh();
      await _attachAppSocketRefreshHandlers();
    } catch (error) {
      debugPrint('CHAT_UNREAD: Initialization failed: $error');
    } finally {
      _isConfiguring = false;
    }
  }

  Future<void> refresh() async {
    final token = _token;
    if (token == null || token.isEmpty) return;
    try {
      await _loadUnread();
    } catch (error) {
      debugPrint('CHAT_UNREAD: Cannot load unread summary: $error');
    }
    try {
      await _chatSocketService.connect(
        token,
        onMessageReceived: _handleIncomingMessage,
      );
      await refreshSubscriptions();
    } catch (error) {
      debugPrint('CHAT_UNREAD: Refresh failed: $error');
    }
  }

  Future<void> refreshSubscriptions() async {
    final token = _token;
    if (token == null || token.isEmpty) return;

    for (final tripId in _items.keys) {
      await _chatSocketService.joinTripChat(tripId);
    }

    try {
      final requestedRoles = <String>{};
      if (_roles.contains(AppValues.roleDriver)) {
        requestedRoles.add(AppValues.roleDriver);
      }
      if (_roles.contains(AppValues.roleCustomer) || requestedRoles.isEmpty) {
        requestedRoles.add(AppValues.roleCustomer);
      }

      final histories = await Future.wait(
        requestedRoles.map(
          (role) => _historyRepository.getBookingHistory(token, role: role),
        ),
      );
      final now = DateTime.now();
      final tripIds = <int>{};
      for (final history in histories.expand((items) => items)) {
        final tripId = history.tripId;
        if (tripId == null) continue;
        final isActive = history.status == HistoryTripStatus.booked;
        final isRecent =
            now.difference(history.time.toLocal()).abs() <=
            _terminalChatRetention;
        if (isActive || isRecent) {
          tripIds.add(tripId);
        }
      }
      for (final tripId in tripIds) {
        await _chatSocketService.joinTripChat(tripId);
      }
    } catch (error) {
      debugPrint('CHAT_UNREAD: Cannot refresh trip subscriptions: $error');
    }
  }

  Future<void> openChat(int tripId) async {
    _activeChatTripId = tripId;
    _clearFloatingNotifications();
    await _chatSocketService.joinTripChat(tripId);
    await markRead(tripId);
  }

  void closeChat(int tripId) {
    if (_activeChatTripId == tripId) {
      _activeChatTripId = null;
    }
  }

  Future<void> markRead(int tripId) async {
    final token = _token;
    if (token == null || token.isEmpty) return;
    try {
      await _remoteDatasource.markRead(token: token, tripId: tripId);
      if (_items.remove(tripId) != null) {
        notifyListeners();
      }
    } catch (error) {
      debugPrint('CHAT_UNREAD: Mark read failed for trip $tripId: $error');
    }
  }

  Future<void> clearSession() async {
    if (_token == null && _currentUserId == null && _items.isEmpty) return;
    _token = null;
    _currentUserId = null;
    _roles = {};
    _activeChatTripId = null;
    _items.clear();
    _clearFloatingNotifications();
    _detachAppSocketRefreshHandlers();
    await _chatSocketService.disconnect();
    notifyListeners();
  }

  Future<void> _loadUnread() async {
    final token = _token;
    if (token == null) return;
    final summary = await _remoteDatasource.getUnreadSummary(token: token);
    _items
      ..clear()
      ..addEntries(summary.items.map((item) => MapEntry(item.tripId, item)));
    notifyListeners();
  }

  void _handleIncomingMessage(List<Object?>? arguments) {
    final currentUserId = _currentUserId;
    if (currentUserId == null) return;
    try {
      final message = TripChatMessageModel.fromSignalR(
        arguments,
        currentUserId,
      );
      if (message.senderUserId.toLowerCase() == currentUserId.toLowerCase()) {
        return;
      }
      if (_activeChatTripId == message.tripId) {
        unawaited(markRead(message.tripId));
        return;
      }

      final previous = _items[message.tripId];
      final preview = message.isImage
          ? 'Đã gửi một hình ảnh'
          : message.message.trim().isEmpty
          ? 'Tin nhắn mới'
          : message.message.trim();
      _items[message.tripId] = TripChatUnreadItemModel(
        tripId: message.tripId,
        bookingId: message.bookingId,
        unreadCount: (previous?.unreadCount ?? 0) + 1,
        lastMessagePreview: preview,
        lastMessageAt: message.sentAt,
      );
      notifyListeners();
      _enqueueFloatingNotification(message, preview);
    } catch (error) {
      debugPrint('CHAT_UNREAD: Invalid realtime message: $error');
    }
  }

  Future<void> _attachAppSocketRefreshHandlers() async {
    if (_appSocketHandlersAttached) return;
    _appSocketHandlersAttached = true;
    _appSocketService.onBookingUpdated(
      (_) => _scheduleSubscriptionRefresh(),
      key: _socketHandlerKey,
    );
    _appSocketService.onTripStatusChanged(
      (_) => _scheduleSubscriptionRefresh(),
      key: _socketHandlerKey,
    );
    try {
      await _appSocketService.connect(_token);
    } catch (error) {
      debugPrint('CHAT_UNREAD: App socket unavailable: $error');
    }
  }

  void _scheduleSubscriptionRefresh() {
    _refreshDebounce?.cancel();
    _refreshDebounce = Timer(
      const Duration(milliseconds: 500),
      () => unawaited(refreshSubscriptions()),
    );
  }

  void _detachAppSocketRefreshHandlers() {
    if (!_appSocketHandlersAttached) return;
    _appSocketHandlersAttached = false;
    _appSocketService.removeBookingUpdatedHandler(_socketHandlerKey);
    _appSocketService.removeTripStatusChangedHandler(_socketHandlerKey);
  }

  void _enqueueFloatingNotification(
    TripChatMessageModel message,
    String preview,
  ) {
    _notificationQueue.add(
      _PendingChatNotification(message: message, preview: preview),
    );
    _showNextFloatingNotification();
  }

  void _showNextFloatingNotification() {
    if (_overlayEntry != null || _notificationQueue.isEmpty) return;

    final overlay = SessionCoordinator.navigatorKey.currentState?.overlay;
    if (overlay == null) {
      _overlayTimer?.cancel();
      _overlayTimer = Timer(
        const Duration(milliseconds: 500),
        _showNextFloatingNotification,
      );
      return;
    }

    final pending = _notificationQueue.removeFirst();
    final message = pending.message;
    final preview = pending.preview;
    _overlayEntry = OverlayEntry(
      builder: (context) => Positioned(
        top: 0,
        left: 12,
        right: 12,
        child: SafeArea(
          bottom: false,
          minimum: const EdgeInsets.only(top: 8),
          child: Material(
            color: Colors.transparent,
            child: InkWell(
              borderRadius: BorderRadius.circular(12),
              onTap: () => _openNotificationChat(message),
              child: Container(
                padding: const EdgeInsets.symmetric(
                  horizontal: 14,
                  vertical: 12,
                ),
                decoration: BoxDecoration(
                  color: const Color(0xFF123C3E),
                  borderRadius: BorderRadius.circular(12),
                  boxShadow: const [
                    BoxShadow(
                      color: Color(0x33000000),
                      blurRadius: 16,
                      offset: Offset(0, 6),
                    ),
                  ],
                ),
                child: Row(
                  children: [
                    const Icon(
                      Icons.chat_bubble_rounded,
                      color: Colors.white,
                      size: 22,
                    ),
                    const SizedBox(width: 12),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Text(
                            message.senderName.trim().isEmpty
                                ? 'Tin nhắn mới'
                                : message.senderName,
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                            style: const TextStyle(
                              color: Colors.white,
                              fontSize: 14,
                              fontWeight: FontWeight.w700,
                            ),
                          ),
                          const SizedBox(height: 3),
                          Text(
                            preview,
                            maxLines: 2,
                            overflow: TextOverflow.ellipsis,
                            style: const TextStyle(
                              color: Color(0xFFDCEBEC),
                              fontSize: 13,
                            ),
                          ),
                        ],
                      ),
                    ),
                    const SizedBox(width: 8),
                    const Icon(
                      Icons.chevron_right_rounded,
                      color: Colors.white,
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
    overlay.insert(_overlayEntry!);
    _overlayTimer = Timer(
      const Duration(seconds: 5),
      () => _dismissCurrentNotification(showNext: true),
    );
  }

  void _openNotificationChat(TripChatMessageModel message) {
    _clearFloatingNotifications();
    final navigator = SessionCoordinator.navigatorKey.currentState;
    final currentUserId = _currentUserId;
    if (navigator == null || currentUserId == null) return;
    navigator.push(
      MaterialPageRoute(
        builder: (_) => TripChatPage(
          tripId: message.tripId,
          currentUserId: currentUserId,
          receiverName: message.senderName,
        ),
      ),
    );
  }

  void _dismissCurrentNotification({required bool showNext}) {
    _overlayTimer?.cancel();
    _overlayTimer = null;
    _overlayEntry?.remove();
    _overlayEntry = null;
    if (showNext) {
      _showNextFloatingNotification();
    }
  }

  void _clearFloatingNotifications() {
    _notificationQueue.clear();
    _dismissCurrentNotification(showNext: false);
  }

  @override
  void dispose() {
    _refreshDebounce?.cancel();
    _clearFloatingNotifications();
    _detachAppSocketRefreshHandlers();
    unawaited(_chatSocketService.disconnect());
    super.dispose();
  }
}

class _PendingChatNotification {
  const _PendingChatNotification({
    required this.message,
    required this.preview,
  });

  final TripChatMessageModel message;
  final String preview;
}
