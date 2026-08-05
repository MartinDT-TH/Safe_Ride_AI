import '../../../booking/data/models/booking_location.dart';
import '../../../../../core/localization/locale_provider.dart';

class AiChatMessage {
  const AiChatMessage({
    required this.id,
    required this.role,
    required this.content,
    required this.createdAt,
    this.bookingDraft,
    this.localAudioPath,
    this.isAudio = false,
    this.audioUrl,
  });

  final String id;
  final String role;
  final String content;
  final DateTime createdAt;
  final AiBookingDraft? bookingDraft;
  final String? localAudioPath;
  final bool isAudio;
  final String? audioUrl;

  bool get isUser => role == 'user';

  factory AiChatMessage.fromJson(Map<String, dynamic> json) => AiChatMessage(
    id: json['id']?.toString() ?? '',
    role: json['role']?.toString() ?? 'assistant',
    content: json['content']?.toString() ?? '',
    createdAt:
        DateTime.tryParse(json['createdAt']?.toString() ?? '') ??
        DateTime.now(),
    bookingDraft: json['bookingDraft'] is Map<String, dynamic>
        ? AiBookingDraft.fromJson(json['bookingDraft'] as Map<String, dynamic>)
        : null,
    isAudio: json['isAudio'] as bool? ?? false,
    audioUrl: json['audioUrl']?.toString(),
  );
}

class AiConversation {
  const AiConversation({
    required this.id,
    required this.title,
    required this.updatedAt,
  });

  final String id;
  final String title;
  final DateTime updatedAt;

  factory AiConversation.fromJson(Map<String, dynamic> json) => AiConversation(
    id: json['id']?.toString() ?? '',
    title:
        json['title']?.toString() ??
        LocaleProvider.currentLocalizations.aiConversationFallback,
    updatedAt:
        DateTime.tryParse(json['updatedAt']?.toString() ?? '') ??
        DateTime.now(),
  );
}

class AiBookingDraft {
  const AiBookingDraft({
    required this.pickup,
    required this.destination,
    this.vehicleQuery,
    this.promotionCode,
  });

  final BookingLocation pickup;
  final BookingLocation destination;
  final String? vehicleQuery;
  final String? promotionCode;

  factory AiBookingDraft.fromJson(Map<String, dynamic> json) {
    BookingLocation location(Map<String, dynamic> value) => BookingLocation(
      address: value['address']?.toString() ?? '',
      latitude: (value['latitude'] as num).toDouble(),
      longitude: (value['longitude'] as num).toDouble(),
    );

    return AiBookingDraft(
      pickup: location(json['pickup'] as Map<String, dynamic>),
      destination: location(json['destination'] as Map<String, dynamic>),
      vehicleQuery: json['vehicleQuery']?.toString(),
      promotionCode: json['promotionCode']?.toString(),
    );
  }
}

class AiChatReply {
  const AiChatReply({
    required this.conversationId,
    required this.userMessage,
    required this.assistantMessage,
    this.bookingDraft,
  });

  final String conversationId;
  final AiChatMessage userMessage;
  final AiChatMessage assistantMessage;
  final AiBookingDraft? bookingDraft;

  factory AiChatReply.fromJson(Map<String, dynamic> json) => AiChatReply(
    conversationId: json['conversationId']?.toString() ?? '',
    userMessage: AiChatMessage.fromJson(
      json['userMessage'] as Map<String, dynamic>,
    ),
    assistantMessage: AiChatMessage.fromJson(
      json['assistantMessage'] as Map<String, dynamic>,
    ),
    bookingDraft: json['bookingDraft'] is Map<String, dynamic>
        ? AiBookingDraft.fromJson(json['bookingDraft'] as Map<String, dynamic>)
        : null,
  );
}
