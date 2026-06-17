import '../../../../../core/constants/app_strings.dart';

class TripModel {
  final String pickup;

  final String destination;

  final String time;

  TripModel({
    required this.pickup,
    required this.destination,
    required this.time,
  });

  factory TripModel.fromJson(Map<String, dynamic> json) {
    return TripModel(
      pickup: json[ApiKeys.pickup],
      destination: json[ApiKeys.destination],
      time: json[ApiKeys.time],
    );
  }
}

