import 'package:safe_ride/core/constants/app_strings.dart';

class TripChatMessageModel {
  final String id;
  final int tripId;
  final String senderUserId;
  final String senderName;
  final String messageType;
  final String message;
  final String? imageUrl;
  final DateTime sentAt;
  final Map<String, String> translations;
  final String? sourceLanguage;
  final bool isMine;

  const TripChatMessageModel({
    required this.id,
    required this.tripId,
    required this.senderUserId,
    required this.senderName,
    required this.messageType,
    required this.message,
    this.imageUrl,
    required this.sentAt,
    this.translations = const {},
    this.sourceLanguage,
    this.isMine = false,
  });

  bool get isText => messageType.toLowerCase() == 'text';
  bool get isImage => messageType.toLowerCase() == 'image';

  factory TripChatMessageModel.fromJson(
    Map<String, dynamic> json,
    String currentUserId,
  ) {
    final senderUserId = json['senderUserId']?.toString() ?? '';
    return TripChatMessageModel(
      id:
          json['id']?.toString() ??
          DateTime.now().millisecondsSinceEpoch.toString(),
      tripId: (json[ApiKeys.tripId] as num?)?.toInt() ?? 0,
      senderUserId: senderUserId,
      senderName: json['senderName']?.toString() ?? '',
      messageType: json['messageType']?.toString() ?? 'Text',
      message: json['message']?.toString() ?? '',
      imageUrl: json['imageUrl']?.toString(),
      sentAt: json['sentAt'] == null
          ? DateTime.now()
          : DateTime.tryParse(json['sentAt'].toString()) ?? DateTime.now(),
      translations: _parseTranslations(json['translations']),
      sourceLanguage: json['sourceLanguage']?.toString(),
      isMine: senderUserId == currentUserId,
    );
  }

  factory TripChatMessageModel.fromSignalR(
    List<Object?>? arguments,
    String currentUserId,
  ) {
    if (arguments == null || arguments.isEmpty || arguments.first is! Map) {
      throw const FormatException(
        'Invalid SignalR arguments for TripChatMessage',
      );
    }
    final data = Map<String, dynamic>.from(arguments.first as Map);
    return TripChatMessageModel.fromJson(data, currentUserId);
  }

  TripChatMessageModel copyWith({bool? isMine}) {
    return TripChatMessageModel(
      id: id,
      tripId: tripId,
      senderUserId: senderUserId,
      senderName: senderName,
      messageType: messageType,
      message: message,
      imageUrl: imageUrl,
      sentAt: sentAt,
      translations: translations,
      sourceLanguage: sourceLanguage,
      isMine: isMine ?? this.isMine,
    );
  }

  static Map<String, String> _parseTranslations(dynamic value) {
    if (value is! Map) return const {};
    return value.map(
      (key, translation) => MapEntry(
        key.toString().toLowerCase(),
        translation?.toString() ?? '',
      ),
    )..removeWhere((_, translation) => translation.trim().isEmpty);
  }
}
