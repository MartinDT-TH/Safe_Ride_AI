import 'dart:io';
import 'package:flutter/material.dart';
import '../../../../../core/localization/localization_extensions.dart';
import 'package:image_picker/image_picker.dart';
import 'package:provider/provider.dart';
import 'package:intl/intl.dart';
import 'package:cached_network_image/cached_network_image.dart';
import '../../../../../core/constants/app_colors.dart';
import '../../../../../core/constants/app_strings.dart';
import '../../../../../core/localization/locale_provider.dart';
import '../../../../auth/presentation/providers/auth_provider.dart';
import '../providers/trip_chat_provider.dart';
import '../../data/models/trip_chat_message_model.dart';

class TripChatPage extends StatefulWidget {
  TripChatPage({
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
        duration: Duration(milliseconds: 300),
        curve: Curves.easeOut,
      );
    }
  }

  Future<void> _handleSend() async {
    if (!widget.canSendMessage) return;
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
        if (!widget.canSendMessage) return;
        await context.read<TripChatProvider>().sendImage(File(image.path));
        _scrollToBottom();
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(context.l10n.imageSelectionFailed)),
        );
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: Color(0xFFF9FAFB),
      appBar: AppBar(
        backgroundColor: Colors.white,
        elevation: 0.5,
        leading: IconButton(
          icon: Icon(Icons.arrow_back, color: Color(0xFF1D2939)),
          onPressed: () => Navigator.pop(context),
        ),
        title: Column(
          crossAxisAlignment: CrossAxisAlignment.center,
          children: [
            Text(
              context.l10n.chatTitle,
              style: TextStyle(
                color: Color(0xFF1D2939),
                fontSize: 18,
                fontWeight: FontWeight.w700,
              ),
            ),
            if (widget.receiverName != null)
              Text(
                widget.receiverName!,
                style: TextStyle(
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
          if (!widget.canSendMessage)
            Container(
              width: double.infinity,
              padding: const EdgeInsets.symmetric(vertical: 8, horizontal: 16),
              color: Colors.amber.shade50,
              child: Row(
                children: [
                  Icon(
                    Icons.info_outline,
                    size: 18,
                    color: Colors.amber.shade800,
                  ),
                  SizedBox(width: 8),
                  Expanded(
                    child: Text(
                      context.l10n.chatReadOnly,
                      style: TextStyle(
                        fontSize: 12,
                        color: Colors.amber.shade900,
                        fontWeight: FontWeight.w500,
                      ),
                    ),
                  ),
                ],
              ),
            ),
          Expanded(
            child: Consumer<TripChatProvider>(
              builder: (context, provider, child) {
                if (provider.isLoading && provider.messages.isEmpty) {
                  return Center(child: CircularProgressIndicator());
                }

                if (provider.messages.isEmpty) {
                  return Center(
                    child: Text(
                      context.l10n.noMessages,
                      style: TextStyle(color: Color(0xFF98A2B3)),
                    ),
                  );
                }

                WidgetsBinding.instance.addPostFrameCallback(
                  (_) => _scrollToBottom(),
                );

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
            offset: Offset(0, -2),
          ),
        ],
      ),
      child: Row(
        children: [
          IconButton(
            onPressed: widget.canSendMessage ? _handlePickImage : null,
            icon: Icon(
              Icons.image_outlined,
              color: widget.canSendMessage
                  ? Color(0xFF667085)
                  : Colors.grey,
            ),
          ),
          Expanded(
            child: Container(
              padding: const EdgeInsets.symmetric(horizontal: 16),
              decoration: BoxDecoration(
                color: Color(0xFFF2F4F7),
                borderRadius: BorderRadius.circular(24),
              ),
              child: TextField(
                controller: _messageController,
                enabled: widget.canSendMessage,
                decoration: InputDecoration(
                  hintText: widget.canSendMessage
                      ? context.l10n.messageHint
                      : context.l10n.tripEnded,
                  hintStyle: TextStyle(
                    fontSize: 14,
                    color: Color(0xFF98A2B3),
                  ),
                  border: InputBorder.none,
                ),
                maxLines: null,
                textInputAction: TextInputAction.send,
                onSubmitted: (_) => _handleSend(),
              ),
            ),
          ),
          SizedBox(width: 8),
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

class _MessageBubble extends StatefulWidget {
  _MessageBubble({required this.message});

  final TripChatMessageModel message;

  @override
  State<_MessageBubble> createState() => _MessageBubbleState();
}

class _MessageBubbleState extends State<_MessageBubble> {
  bool _showOriginal = false;

  TripChatMessageModel get message => widget.message;

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
    final locale = LocaleProvider.currentLocale.languageCode.toLowerCase();
    final translatedText = message.translations[locale];
    final hasTranslation = !isMine &&
        translatedText != null &&
        translatedText.trim().isNotEmpty &&
        translatedText.trim() != message.message.trim();
    final displayedText = hasTranslation && !_showOriginal
        ? translatedText
        : message.message;

    return Align(
      alignment: isMine ? Alignment.centerRight : Alignment.centerLeft,
      child: Column(
        crossAxisAlignment: isMine
            ? CrossAxisAlignment.end
            : CrossAxisAlignment.start,
        children: [
          if (!isMine)
            Padding(
              padding: const EdgeInsets.only(left: 4, bottom: 4),
              child: Text(
                message.senderName,
                style: TextStyle(fontSize: 10, color: Color(0xFF667085)),
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
                      offset: Offset(0, 2),
                    ),
                ],
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    displayedText!,
                    style: TextStyle(
                      color: isMine ? Colors.white : Color(0xFF1D2939),
                      fontSize: 15,
                      fontWeight: FontWeight.w500,
                    ),
                  ),
                  if (hasTranslation) ...[
                    const SizedBox(height: 6),
                    InkWell(
                      onTap: () => setState(() {
                        _showOriginal = !_showOriginal;
                      }),
                      child: Text(
                        _translationLabel(locale, _showOriginal),
                        style: TextStyle(
                          color: AppColors.primary,
                          fontSize: 11,
                          fontWeight: FontWeight.w600,
                        ),
                      ),
                    ),
                  ],
                ],
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
                      child: Center(
                        child: CircularProgressIndicator(strokeWidth: 2),
                      ),
                    ),
                    errorWidget: (context, url, error) => Container(
                      height: 100,
                      color: Colors.grey[200],
                      child: Icon(Icons.error_outline),
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
              style: TextStyle(fontSize: 10, color: Color(0xFF98A2B3)),
            ),
          ),
        ],
      ),
    );
  }

  String _translationLabel(String locale, bool showingOriginal) {
    const labels = <String, List<String>>{
      'vi': ['AI dịch tự động · Xem bản gốc', 'AI dịch tự động · Xem bản dịch'],
      'en': ['AI translation · View original', 'AI translation · View translation'],
      'ko': ['AI 자동 번역 · 원문 보기', 'AI 자동 번역 · 번역 보기'],
      'ja': ['AI自動翻訳 · 原文を見る', 'AI自動翻訳 · 翻訳を見る'],
      'zh': ['AI 自动翻译 · 查看原文', 'AI 自动翻译 · 查看译文'],
    };
    final localizedLabels = labels[locale] ?? labels['en']!;
    return localizedLabels[showingOriginal ? 1 : 0];
  }
}
