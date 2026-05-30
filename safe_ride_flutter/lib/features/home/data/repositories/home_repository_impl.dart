import '../../domain/repositories/home_repository.dart';

import '../datasources/home_remote_datasource.dart';

import '../models/home_response.dart';

class HomeRepositoryImpl
    implements HomeRepository {

  final HomeRemoteDatasource
  datasource;

  HomeRepositoryImpl(
      this.datasource,
      );

  @override
  Future<HomeResponse>
  getHomeData() {

    return datasource
        .getHomeData();
  }
}