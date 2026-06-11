import 'trip_model.dart';

class HomeResponse {

  final String userName;

  final List<TripModel> recentTrips;

  HomeResponse({
    required this.userName,
    required this.recentTrips,
  });

  factory HomeResponse.fromJson(
      Map<String, dynamic> json,
      ) {

    return HomeResponse(

      userName: json['userName'],

      recentTrips:
      (json['recentTrips'] as List)
          .map(
            (e) => TripModel.fromJson(
          e,
        ),
      )
          .toList(),
    );
  }
}