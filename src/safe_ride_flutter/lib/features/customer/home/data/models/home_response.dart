import 'trip_model.dart';
import '../../../../../core/constants/app_strings.dart';

class HomeResponse {
  final String userName;

  final List<TripModel> recentTrips;

  HomeResponse({required this.userName, required this.recentTrips});

  factory HomeResponse.fromJson(Map<String, dynamic> json) {
    return HomeResponse(
      userName: json[ApiKeys.userName],

      recentTrips: (json[ApiKeys.recentTrips] as List)
          .map((e) => TripModel.fromJson(e))
          .toList(),
    );
  }
}

