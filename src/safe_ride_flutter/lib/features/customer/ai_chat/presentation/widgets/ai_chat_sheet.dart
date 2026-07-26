import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../booking/presentation/pages/booking_options_page.dart';
import '../../../booking/presentation/providers/booking_provider.dart';
import '../../data/models/ai_chat_models.dart';
import '../../data/services/ai_chat_service.dart';

class AiChatSheet extends StatefulWidget {
  const AiChatSheet({super.key});

  static Future<void> show(BuildContext context) => showModalBottomSheet<void>(
        context: context,
        isScrollControlled: true,
        useSafeArea: true,
        backgroundColor: Colors.transparent,
        builder: (_) => const AiChatSheet(),
      );

  @override
  State<AiChatSheet> createState() => _AiChatSheetState();
}

class _AiChatSheetState extends State<AiChatSheet> {
  final _controller = TextEditingController();
  final _scrollController = ScrollController();
  final _service = AiChatService();
  final List<AiChatMessage> _messages = [];
  String? _conversationId;
  AiBookingDraft? _draft;
  bool _sending = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    _restoreLatestConversation();
  }

  @override
  void dispose() {
    _controller.dispose();
    _scrollController.dispose();
    super.dispose();
  }

  Future<void> _send() async {
    final text = _controller.text.trim();
    if (text.isEmpty || _sending) return;
    _controller.clear();
    setState(() {
      _sending = true;
      _error = null;
      _messages.add(AiChatMessage(
        id: 'pending-${DateTime.now().microsecondsSinceEpoch}',
        role: 'user',
        content: text,
        createdAt: DateTime.now(),
      ));
    });
    _scrollToBottom();

    try {
      final currentLocation =
          await context.read<BookingProvider>().getCurrentLocation();
      final reply = await _service.sendMessage(
        message: text,
        conversationId: _conversationId,
        currentLocation: currentLocation,
      );
      if (!mounted) return;
      setState(() {
        _conversationId = reply.conversationId;
        _messages
          ..removeWhere((message) => message.id.startsWith('pending-'))
          ..add(reply.userMessage)
          ..add(reply.assistantMessage);
        _draft = reply.bookingDraft;
      });
    } on DioException catch (exception) {
      if (!mounted) return;
      final data = exception.response?.data;
      final isServerError = (exception.response?.statusCode ?? 0) >= 500;
      setState(() {
        _error = isServerError
            ? 'Trợ lý AI đang gặp sự cố. Vui lòng thử lại sau.'
            : data is Map<String, dynamic>
            ? data['detail']?.toString()
            : 'Không thể kết nối với trợ lý AI. Vui lòng thử lại.';
      });
    } finally {
      if (mounted) setState(() => _sending = false);
      _scrollToBottom();
    }
  }

  void _scrollToBottom() {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (_scrollController.hasClients) {
        _scrollController.animateTo(
          _scrollController.position.maxScrollExtent,
          duration: const Duration(milliseconds: 220),
          curve: Curves.easeOut,
        );
      }
    });
  }

  void _openBooking() {
    final draft = _draft;
    if (draft == null) return;
    final navigator = Navigator.of(context);
    navigator.pop();
    navigator.push(
      MaterialPageRoute(
        builder: (_) => BookingOptionsPage(
          initialPickup: draft.pickup,
          initialDestination: draft.destination,
        ),
      ),
    );
  }

  Future<void> _restoreLatestConversation() async {
    try {
      final conversations = await _service.getConversations();
      if (!mounted || conversations.isEmpty) return;

      for (final conversation in conversations) {
        final messages = await _service.getMessages(conversation.id);
        if (!mounted) return;
        if (messages.isEmpty) continue;

        setState(() {
          _conversationId = conversation.id;
          _messages
            ..clear()
            ..addAll(messages);
          _draft = messages
              .where((message) => message.bookingDraft != null)
              .lastOrNull
              ?.bookingDraft;
        });
        _scrollToBottom();
        return;
      }
    } catch (_) {
      // The composer remains usable; send will surface a localized API error.
    }
  }

  void _newConversation() {
    setState(() {
      _conversationId = null;
      _messages.clear();
      _draft = null;
      _error = null;
    });
  }

  @override
  Widget build(BuildContext context) {
    final height = MediaQuery.sizeOf(context).height * .82;
    return Container(
      height: height,
      decoration: const BoxDecoration(
        color: Color(0xFFFCF9F9),
        borderRadius: BorderRadius.vertical(top: Radius.circular(24)),
      ),
      child: Column(
        children: [
          _Header(onNewConversation: _newConversation),
          Expanded(
            child: _messages.isEmpty
                ? const _Welcome()
                : ListView.builder(
                    controller: _scrollController,
                    padding: const EdgeInsets.all(16),
                    itemCount: _messages.length,
                    itemBuilder: (_, index) =>
                        _Bubble(message: _messages[index]),
                  ),
          ),
          if (_draft != null)
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 4, 16, 8),
              child: FilledButton.icon(
                onPressed: _openBooking,
                icon: const Icon(Icons.directions_car_rounded),
                label: const Text('Kiểm tra và tiếp tục đặt chuyến'),
                style: FilledButton.styleFrom(
                  minimumSize: const Size.fromHeight(48),
                  backgroundColor: const Color(0xFF006B70),
                ),
              ),
            ),
          if (_error != null)
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 16),
              child: Text(_error!, style: const TextStyle(color: Colors.red)),
            ),
          Padding(
            padding: EdgeInsets.fromLTRB(
              12,
              8,
              12,
              12 + MediaQuery.viewInsetsOf(context).bottom,
            ),
            child: Row(
              children: [
                Expanded(
                  child: TextField(
                    controller: _controller,
                    minLines: 1,
                    maxLines: 4,
                    maxLength: 1000,
                    textInputAction: TextInputAction.send,
                    onSubmitted: (_) => _send(),
                    decoration: InputDecoration(
                      counterText: '',
                      hintText: 'Nhắn cho trợ lý SafeRide...',
                      filled: true,
                      fillColor: Colors.white,
                      border: OutlineInputBorder(
                        borderRadius: BorderRadius.circular(24),
                        borderSide: BorderSide.none,
                      ),
                    ),
                  ),
                ),
                const SizedBox(width: 8),
                IconButton.filled(
                  onPressed: _sending ? null : _send,
                  icon: _sending
                      ? const SizedBox.square(
                          dimension: 18,
                          child: CircularProgressIndicator(
                            strokeWidth: 2,
                            color: Colors.white,
                          ),
                        )
                      : const Icon(Icons.send_rounded),
                  style: IconButton.styleFrom(
                    backgroundColor: const Color(0xFF006B70),
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _Header extends StatelessWidget {
  const _Header({required this.onNewConversation});

  final VoidCallback onNewConversation;

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.fromLTRB(16, 12, 8, 8),
        child: Row(
          children: [
            const CircleAvatar(
              backgroundColor: Color(0xFFE0F2F1),
              child: Icon(Icons.auto_awesome, color: Color(0xFF006B70)),
            ),
            const SizedBox(width: 12),
            const Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('Trợ lý SafeRide',
                      style: TextStyle(fontWeight: FontWeight.bold)),
                  Text('AI có thể mắc lỗi • Kiểm tra trước khi đặt',
                      style: TextStyle(fontSize: 12, color: Colors.grey)),
                ],
              ),
            ),
            IconButton(
              tooltip: 'Đoạn chat mới',
              onPressed: onNewConversation,
              icon: const Icon(Icons.add_comment_outlined),
            ),
            IconButton(
              onPressed: () => Navigator.pop(context),
              icon: const Icon(Icons.close),
            ),
          ],
        ),
      );
}

class _Welcome extends StatelessWidget {
  const _Welcome();

  @override
  Widget build(BuildContext context) => const Center(
        child: Padding(
          padding: EdgeInsets.all(32),
          child: Text(
            'Xin chào! Mình có thể hỗ trợ bạn sử dụng SafeRide hoặc chuẩn bị một chuyến đi.\n\nVí dụ: “Đặt xe từ Đại học FPT đến sân bay Tân Sơn Nhất”.',
            textAlign: TextAlign.center,
            style: TextStyle(height: 1.5, color: Color(0xFF555555)),
          ),
        ),
      );
}

class _Bubble extends StatelessWidget {
  const _Bubble({required this.message});

  final AiChatMessage message;

  @override
  Widget build(BuildContext context) => Align(
        alignment:
            message.isUser ? Alignment.centerRight : Alignment.centerLeft,
        child: Container(
          constraints: const BoxConstraints(maxWidth: 300),
          margin: const EdgeInsets.only(bottom: 10),
          padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
          decoration: BoxDecoration(
            color: message.isUser ? const Color(0xFF006B70) : Colors.white,
            borderRadius: BorderRadius.circular(16),
          ),
          child: Text(
            message.content,
            style: TextStyle(
              color: message.isUser ? Colors.white : const Color(0xFF222222),
              height: 1.4,
            ),
          ),
        ),
      );
}
