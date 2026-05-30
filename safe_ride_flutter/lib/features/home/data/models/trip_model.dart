class TripModel {

  final String pickup;

  final String destination;

  final String time;

  TripModel({
    required this.pickup,
    required this.destination,
    required this.time,
  });

  factory TripModel.fromJson(
      Map<String, dynamic> json,
      ) {
    return TripModel(
      pickup: json['pickup'],
      destination: json['destination'],
      time: json['time'],
    );
  }
}