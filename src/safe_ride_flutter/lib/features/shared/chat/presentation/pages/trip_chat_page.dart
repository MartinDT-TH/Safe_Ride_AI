import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:intl/intl.dart';
import '../../../../../core/constants/app_colors.dart';
import '../../../../auth/presentation/providers/auth_provider.dart';
import '../providers/trip_chat_provider.dart';

class TripChatPage extends StatefulWidget {
  const TripChatPage({
    super.key,
    required this.tripId,
    required this.currentUserId,
    this.receiverName,
    this.canSendMessage = true,
  });

  final int tripId;
  final String currentUserId;
  final String? receiverName;
  final bool canSendMessage;

  @override
  State<TripChatPage> createState() => _TripChatPageState();
}

class _TripChatPageState extends State<TripChatPage> {
  final TextEditingController _messageController = TextEditingController();
  final ScrollController _scrollController = ScrollController();

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      final token = context.read<AuthProvider>().token;
      if (token != null) {
        context.read<TripChatProvider>().initialize(
              token: token,
              tripId: widget.tripId,
              currentUserId: widget.currentUserId,
            );
      }
    });
  }

  void _scrollToBottom() {
    if (_scrollController.hasClients) {
      _scrollController.animateTo(
        _scrollController.position.maxScrollExtent,
        duration: const Duration(milliseconds: 300),
        curve: Curves.easeOut,
      );
    }
  }

  Future<void> _handleSend() async {
    final text = _messageController.text;
    if (text.trim().isEmpty) return;

    _messageController.clear();
    await context.read<TripChatProvider>().sendMessage(text);
    _scrollToBottom();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFF9FAFB),
      appBar: AppBar(
        backgroundColor: Colors.white,
        elevation: 0.5,
        leading: IconButton(
          icon: const Icon(Icons.arrow_back, color: Color(0xFF1D2939)),
          onPressed: () => Navigator.pop(context),
        ),
        title: Column(
          crossAxisAlignment: CrossAxisAlignment.center,
          children: [
            const Text(
              'Nhắn tin',
              style: TextStyle(
                color: Color(0xFF1D2939),
                fontSize: 18,
                fontWeight: FontWeight.w700,
              ),
            ),
            if (widget.receiverName != null)
              Text(
                widget.receiverName!,
                style: const TextStyle(
                  color: Color(0xFF667085),
                  fontSize: 12,
                  fontWeight: FontWeight.w500,
                ),
              ),
          ],
        ),
        centerTitle: true,
      ),
      body: Column(
        children: [
          Expanded(
            child: Consumer<TripChatProvider>(
              builder: (context, provider, child) {
                if (provider.isLoading && provider.messages.isEmpty) {
                  return const Center(child: CircularProgressIndicator());
                }

                if (provider.messages.isEmpty) {
                  return const Center(
                    child: Text(
                      'Chưa có tin nhắn nào.',
                      style: TextStyle(color: Color(0xFF98A2B3)),
                    ),
                  );
                }

                WidgetsBinding.instance.addPostFrameCallback((_) => _scrollToBottom());

                return ListView.builder(
                  controller: _scrollController,
                  padding: const EdgeInsets.all(16),
                  itemCount: provider.messages.length,
                  itemBuilder: (context, index) {
                    final msg = provider.messages[index];
                    return _MessageBubble(message: msg);
                  },
                );
              },
            ),
          ),
          _buildInputArea(),
        ],
      ),
    );
  }

  Widget _buildInputArea() {
    return Container(
      padding: EdgeInsets.fromLTRB(
        16,
        12,
        16,
        MediaQuery.of(context).padding.bottom + 12,
      ),
      decoration: BoxDecoration(
        color: Colors.white,
        boxShadow: [
          BoxShadow(
            color: Colors.black.withOpacity(0.05),
            blurRadius: 10,
            offset: const Offset(0, -2),
          ),
        ],
      ),
      child: Row(
        children: [
          Expanded(
            child: Container(
              padding: const EdgeInsets.symmetric(horizontal: 16),
              decoration: BoxDecoration(
                color: const Color(0xFFF2F4F7),
                borderRadius: BorderRadius.circular(24),
              ),
              child: TextField(
                controller: _messageController,
                enabled: widget.canSendMessage,
                decoration: InputDecoration(
                  hintText: widget.canSendMessage
                      ? 'Nhập tin nhắn...'
                      : 'Chuyến đi đã kết thúc',
                  hintStyle: const TextStyle(fontSize: 14, color: Color(0xFF98A2B3)),
                  border: InputBorder.none,
                ),
                maxLines: null,
                textInputAction: TextInputAction.send,
                onSubmitted: (_) => _handleSend(),
              ),
            ),
          ),
          const SizedBox(width: 8),
          IconButton(
            onPressed: widget.canSendMessage ? _handleSend : null,
            icon: Icon(
              Icons.send_rounded,
              color: widget.canSendMessage ? AppColors.primary : Colors.grey,
            ),
          ),
        ],
      ),
    );
  }
}

class _MessageBubble extends StatelessWidget {
  const _MessageBubble({required this.message});

  final dynamic message;

  @override
  Widget build(BuildContext context) {
    final isMine = message.isMine;
    final timeStr = DateFormat('HH:mm').format(message.sentAt);

    return Align(
      alignment: isMine ? Alignment.centerRight : Alignment.centerLeft,
      child: Column(
        crossAxisAlignment:
            isMine ? CrossAxisAlignment.end : CrossAxisAlignment.start,
        children: [
          if (!isMine)
            Padding(
              padding: const EdgeInsets.only(left: 4, bottom: 4),
              child: Text(
                message.senderName,
                style: const TextStyle(fontSize: 10, color: Color(0xFF667085)),
              ),
            ),
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
            constraints: BoxConstraints(
              maxWidth: MediaQuery.of(context).size.width * 0.75,
            ),
            decoration: BoxDecoration(
              color: isMine ? AppColors.primary : Colors.white,
              borderRadius: BorderRadius.only(
                topLeft: const Radius.circular(16),
                topRight: const Radius.circular(16),
                bottomLeft: Radius.circular(isMine ? 16 : 4),
                bottomRight: Radius.circular(isMine ? 4 : 16),
              ),
              boxShadow: [
                if (!isMine)
                  BoxShadow(
                    color: Colors.black.withOpacity(0.03),
                    blurRadius: 4,
                    offset: const Offset(0, 2),
                  ),
              ],
            ),
            child: Text(
              message.message,
              style: TextStyle(
                color: isMine ? Colors.white : const Color(0xFF1D2939),
                fontSize: 15,
                fontWeight: FontWeight.w500,
              ),
            ),
          ),
          Padding(
            padding: const EdgeInsets.only(top: 4, bottom: 12),
            child: Text(
              timeStr,
              style: const TextStyle(fontSize: 10, color: Color(0xFF98A2B3)),
            ),
          ),
        ],
      ),
    );
  }
}
