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

import '../../../../../core/constants/app_strings.dart';
import '../../../../../core/network/dio_client.dart';
import '../models/home_response.dart';

class HomeRemoteDatasource {
  Future<HomeResponse> getHomeData() async {
    try {
      final response = await DioClient().dio.get('/home');
      return HomeResponse.fromJson(response.data);
    } catch (e) {
      // For development, if /api/home doesn't exist yet, return a mock response
      // so the UI doesn't continuously show a server connection error widget.
      return HomeResponse(
        userName: HomeStrings.defaultUser,
        recentTrips: [],
      );
    }
  }
}

