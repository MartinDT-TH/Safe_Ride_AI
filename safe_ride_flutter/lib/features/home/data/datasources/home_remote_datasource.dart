import 'package:dio/dio.dart';

// import '../models/home_response.dart';
//
// class HomeRemoteDatasource {
//   final Dio dio;
//
//   HomeRemoteDatasource(this.dio);
//
//   Future<HomeResponse> getHomeData() async {
//
//     final response =
//         await dio.get(
//           '/api/home',
//         );
//
//     return HomeResponse.fromJson(
//       response.data,
//     );
//   }
// }

import '../models/home_response.dart';

class HomeRemoteDatasource {

  Future<HomeResponse> getHomeData() async {

    await Future.delayed(
      const Duration(seconds: 1),
    );

    return HomeResponse.fromJson(
      {
        "userName": "Alex",
        "recentTrips": [
          {
            "pickup": "AEON Mall",
            "destination": "Quận 1",
            "time": "10:20"
          }
        ]
      },
    );
  }
}