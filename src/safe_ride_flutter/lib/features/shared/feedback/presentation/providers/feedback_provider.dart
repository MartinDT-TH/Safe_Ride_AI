import 'package:flutter/material.dart';
import '../../../../../core/localization/locale_provider.dart';
import '../../domain/repositories/feedback_repository.dart';
import '../../data/models/driver_rating_summary.dart';

class FeedbackProvider extends ChangeNotifier {
  final FeedbackRepository _repository;

  FeedbackProvider(this._repository);

  DriverRatingSummary? _driverRatingSummary;
  bool _isLoading = false;
  String? _errorMessage;

  DriverRatingSummary? get driverRatingSummary => _driverRatingSummary;
  bool get isLoading => _isLoading;
  String? get errorMessage => _errorMessage;

  Future<void> loadDriverRatings(String? accessToken, String driverId) async {
    if (accessToken == null) {
      _errorMessage = LocaleProvider.currentLocalizations.sessionExpired;
      notifyListeners();
      return;
    }

    _isLoading = true;
    _errorMessage = null;
    notifyListeners();

    try {
      _driverRatingSummary = await _repository.getDriverRatings(
        accessToken,
        driverId: driverId,
      );
    } catch (e) {
      _errorMessage = LocaleProvider.currentLocalizations.genericError;
    } finally {
      _isLoading = false;
      notifyListeners();
    }
  }

  void clearSummary() {
    _driverRatingSummary = null;
    _errorMessage = null;
    notifyListeners();
  }
}
