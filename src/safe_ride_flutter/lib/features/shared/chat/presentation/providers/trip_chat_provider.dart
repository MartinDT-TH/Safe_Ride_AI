import 'package:flutter/material.dart';
import '../../data/datasources/trip_chat_remote_datasource.dart';
import '../../data/models/trip_chat_message_model.dart';
import '../../data/services/trip_chat_socket_service.dart';

class TripChatProvider extends ChangeNotifier {
  final TripChatRemoteDatasource _remoteDatasource = TripChatRemoteDatasource();
  final TripChatSocketService _socketService = TripChatSocketService();

  List<TripChatMessageModel> _messages = [];
  bool _isLoading = false;
  bool _isSending = false;
  String? _errorMessage;
  int? _activeTripId;
  String? _currentUserId;

  List<TripChatMessageModel> get messages => _messages;
  bool get isLoading => _isLoading;
  bool get isSending => _isSending;
  String? get errorMessage => _errorMessage;
  bool get isConnected => _socketService.isConnected;

  Future<void> initialize({
    required String token,
    required int tripId,
    required String currentUserId,
  }) async {
    _activeTripId = tripId;
    _currentUserId = currentUserId;
    _isLoading = true;
    _errorMessage = null;
    notifyListeners();

    try {
      // 1. Load History
      final history = await _remoteDatasource.getTripChatMessages(
        token: token,
        tripId: tripId,
        currentUserId: currentUserId,
      );
      _messages = history;
      _sortMessages();

      // 2. Connect Socket
      await _socketService.connect(token, onMessageReceived: (args) {
        _handleIncomingMessage(args);
      });

      // 3. Join Group
      await _socketService.joinTripChat(tripId);
    } catch (e) {
      _errorMessage = 'Không thể kết nối trò chuyện.';
    } finally {
      _isLoading = false;
      notifyListeners();
    }
  }

  void _handleIncomingMessage(List<Object?>? args) {
    if (_currentUserId == null) return;
    try {
      final msg = TripChatMessageModel.fromSignalR(args, _currentUserId!);

      // Prevent duplicate
      if (_messages.any((m) => m.id == msg.id)) return;

      _messages.add(msg);
      _sortMessages();
      notifyListeners();
    } catch (e) {
      debugPrint('CHAT_PROVIDER: Error parsing message: $e');
    }
  }

  void _sortMessages() {
    _messages.sort((a, b) => a.sentAt.compareTo(b.sentAt));
  }

  Future<void> sendMessage(String text) async {
    final cleanText = text.trim();
    if (cleanText.isEmpty || _activeTripId == null) return;

    _isSending = true;
    notifyListeners();

    try {
      await _socketService.sendTripMessage(_activeTripId!, cleanText);
    } catch (e) {
      _errorMessage = 'Không thể gửi tin nhắn.';
    } finally {
      _isSending = false;
      notifyListeners();
    }
  }

  Future<void> disposeChat() async {
    if (_activeTripId != null) {
      await _socketService.leaveTripChat(_activeTripId!);
    }
    await _socketService.disconnect();
    _messages = [];
    _activeTripId = null;
    _currentUserId = null;
  }

  @override
  void dispose() {
    disposeChat();
    super.dispose();
  }
}
