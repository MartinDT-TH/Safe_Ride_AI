import '../../../booking/data/models/booking_location.dart';

class AiChatMessage {
  const AiChatMessage({
    required this.id,
    required this.role,
    required this.content,
    required this.createdAt,
    this.bookingDraft,
  });

  final String id;
  final String role;
  final String content;
  final DateTime createdAt;
  final AiBookingDraft? bookingDraft;

  bool get isUser => role == 'user';

  factory AiChatMessage.fromJson(Map<String, dynamic> json) => AiChatMessage(
        id: json['id']?.toString() ?? '',
        role: json['role']?.toString() ?? 'assistant',
        content: json['content']?.toString() ?? '',
        createdAt: DateTime.tryParse(json['createdAt']?.toString() ?? '') ??
            DateTime.now(),
        bookingDraft: json['bookingDraft'] is Map<String, dynamic>
            ? AiBookingDraft.fromJson(
                json['bookingDraft'] as Map<String, dynamic>,
              )
            : null,
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
        title: json['title']?.toString() ?? 'Cuộc trò chuyện',
        updatedAt: DateTime.tryParse(json['updatedAt']?.toString() ?? '') ??
            DateTime.now(),
      );
}

class AiBookingDraft {
  const AiBookingDraft({required this.pickup, required this.destination});

  final BookingLocation pickup;
  final BookingLocation destination;

  factory AiBookingDraft.fromJson(Map<String, dynamic> json) {
    BookingLocation location(Map<String, dynamic> value) => BookingLocation(
          address: value['address']?.toString() ?? '',
          latitude: (value['latitude'] as num).toDouble(),
          longitude: (value['longitude'] as num).toDouble(),
        );

    return AiBookingDraft(
      pickup: location(json['pickup'] as Map<String, dynamic>),
      destination: location(json['destination'] as Map<String, dynamic>),
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
            ? AiBookingDraft.fromJson(
                json['bookingDraft'] as Map<String, dynamic>,
              )
            : null,
      );
}
