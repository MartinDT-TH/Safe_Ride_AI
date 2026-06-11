import '../../data/models/home_response.dart';

abstract class HomeRepository {

  Future<HomeResponse>
  getHomeData();
}