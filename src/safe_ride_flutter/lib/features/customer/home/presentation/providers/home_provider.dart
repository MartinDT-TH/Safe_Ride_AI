import 'package:flutter/material.dart';

import '../../domain/repositories/home_repository.dart';

import '../../data/models/trip_model.dart';

class HomeProvider extends ChangeNotifier {
  final HomeRepository repository;

  HomeProvider(this.repository);

  bool _isLoading = false;

  bool get isLoading => _isLoading;

  String _userName = '';

  String get userName => _userName;

  List<TripModel> _recentTrips = [];

  List<TripModel> get recentTrips => _recentTrips;

  int _selectedIndex = 0;
  int get selectedIndex => _selectedIndex;

  void setSelectedIndex(int index) {
    if (_selectedIndex == index) return;
    _selectedIndex = index;
    notifyListeners();
  }

  Future<void> loadHomeData() async {
    _isLoading = true;

    notifyListeners();

    try {
      final data = await repository.getHomeData();

      _userName = data.userName;

      _recentTrips = data.recentTrips;
    } catch (e) {
      debugPrint(e.toString());
    } finally {
      _isLoading = false;

      notifyListeners();
    }
  }
}

