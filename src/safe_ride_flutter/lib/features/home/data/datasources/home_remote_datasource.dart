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

import '../../../../core/constants/app_strings.dart';
import '../models/home_response.dart';

class HomeRemoteDatasource {
  Future<HomeResponse> getHomeData() async {
    await Future.delayed(const Duration(seconds: 1));

    return HomeResponse.fromJson({
      ApiKeys.userName: 'Alex',
      ApiKeys.recentTrips: [
        {
          ApiKeys.pickup: 'AEON Mall',
          ApiKeys.destination: 'Quận 1',
          ApiKeys.time: '10:20',
        },
      ],
    });
  }
}
