import 'dart:io';
import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';
import 'package:provider/provider.dart';
import 'package:intl/intl.dart';
import 'package:cached_network_image/cached_network_image.dart';
import '../../../../../core/constants/app_colors.dart';
import '../../../../../core/constants/app_strings.dart';
import '../../../../auth/presentation/providers/auth_provider.dart';
import '../providers/trip_chat_provider.dart';
import '../../data/models/trip_chat_message_model.dart';

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
  final ImagePicker _picker = ImagePicker();

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

  Future<void> _handlePickImage() async {
    if (!widget.canSendMessage) return;

    try {
      final XFile? image = await _picker.pickImage(
        source: ImageSource.gallery,
        imageQuality: 70,
      );

      if (image != null && mounted) {
        await context.read<TripChatProvider>().sendImage(File(image.path));
        _scrollToBottom();
      }
    } catch (e) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Không thể chọn ảnh.')),
      );
    }
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
        8,
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
          IconButton(
            onPressed: widget.canSendMessage ? _handlePickImage : null,
            icon: Icon(
              Icons.image_outlined,
              color: widget.canSendMessage ? const Color(0xFF667085) : Colors.grey,
            ),
          ),
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

  final TripChatMessageModel message;

  String _getFullImageUrl(String url) {
    if (url.startsWith('http')) return url;

    // Extract base host from AppConfig.apiBaseUrl
    // Typical apiBaseUrl: http://192.168.1.36:5026/api/
    final apiBase = AppConfig.apiBaseUrl;
    String root = apiBase;
    if (apiBase.endsWith('/api/')) {
      root = apiBase.substring(0, apiBase.length - 5);
    } else if (apiBase.endsWith('/api')) {
      root = apiBase.substring(0, apiBase.length - 4);
    }

    if (root.endsWith('/')) {
      root = root.substring(0, root.length - 1);
    }

    final normalizedPath = url.startsWith('/') ? url : '/$url';
    return '$root$normalizedPath';
  }

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
          if (message.isText)
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
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
            )
          else if (message.isImage && message.imageUrl != null)
            GestureDetector(
              onTap: () {
                // Potential full screen preview
              },
              child: Container(
                constraints: BoxConstraints(
                  maxWidth: MediaQuery.of(context).size.width * 0.7,
                ),
                child: ClipRRect(
                  borderRadius: BorderRadius.circular(12),
                  child: CachedNetworkImage(
                    imageUrl: _getFullImageUrl(message.imageUrl!),
                    placeholder: (context, url) => Container(
                      height: 200,
                      color: Colors.grey[200],
                      child: const Center(child: CircularProgressIndicator(strokeWidth: 2)),
                    ),
                    errorWidget: (context, url, error) => Container(
                      height: 100,
                      color: Colors.grey[200],
                      child: const Icon(Icons.error_outline),
                    ),
                    fit: BoxFit.cover,
                  ),
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
