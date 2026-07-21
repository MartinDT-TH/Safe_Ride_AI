import 'package:safe_ride/core/constants/app_strings.dart';

class TripChatMessageModel {
  final String id;
  final int tripId;
  final String senderUserId;
  final String senderName;
  final String message;
  final DateTime sentAt;
  final bool isMine;

  const TripChatMessageModel({
    required this.id,
    required this.tripId,
    required this.senderUserId,
    required this.senderName,
    required this.message,
    required this.sentAt,
    this.isMine = false,
  });

  factory TripChatMessageModel.fromJson(Map<String, dynamic> json, String currentUserId) {
    final senderUserId = json['senderUserId']?.toString() ?? '';
    return TripChatMessageModel(
      id: json['id']?.toString() ?? DateTime.now().millisecondsSinceEpoch.toString(),
      tripId: (json[ApiKeys.tripId] as num?)?.toInt() ?? 0,
      senderUserId: senderUserId,
      senderName: json['senderName']?.toString() ?? '',
      message: json['message']?.toString() ?? '',
      sentAt: json['sentAt'] == null
          ? DateTime.now()
          : DateTime.tryParse(json['sentAt'].toString()) ?? DateTime.now(),
      isMine: senderUserId == currentUserId,
    );
  }

  factory TripChatMessageModel.fromSignalR(List<Object?>? arguments, String currentUserId) {
    if (arguments == null || arguments.isEmpty || arguments.first is! Map) {
      throw const FormatException('Invalid SignalR arguments for TripChatMessage');
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
      message: message,
      sentAt: sentAt,
      isMine: isMine ?? this.isMine,
    );
  }
}
