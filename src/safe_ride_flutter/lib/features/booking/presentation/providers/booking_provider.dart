import 'package:flutter/foundation.dart';

import '../../../../core/constants/app_strings.dart';
import '../../../../core/services/location_service.dart';
import '../../data/datasources/booking_remote_datasource.dart';
import '../../data/models/booking_catalog.dart';
import '../../data/models/booking_location.dart';
import '../../data/models/booking_response.dart';
import '../../data/models/create_booking_request.dart';
import '../../domain/repositories/booking_repository.dart';

class BookingProvider extends ChangeNotifier {
  BookingProvider(this._repository, this._locationService);

  final BookingRepository _repository;
  final LocationService _locationService;

  bool _isLoading = false;
  String? _errorMessage;
  BookingCatalog? _catalog;

  bool get isLoading => _isLoading;
  String? get errorMessage => _errorMessage;
  BookingCatalog? get catalog => _catalog;

  Future<void> loadCatalog() async {
    if (_catalog != null) return;
    _catalog = await _repository.getCatalog();
    notifyListeners();
  }

  Future<BookingLocation?> getCurrentLocation() async {
    return _run(() => _locationService.getCurrentLocation());
  }

  Future<BookingLocation?> resolveAddress(String address) async {
    return _run(() => _locationService.resolveAddress(address));
  }

  Future<BookingResponse?> createBooking(
    String accessToken,
    CreateBookingRequest request,
  ) {
    return _run(() => _repository.createBooking(accessToken, request));
  }

  Future<T?> _run<T>(Future<T> Function() action) async {
    _isLoading = true;
    _errorMessage = null;
    notifyListeners();

    try {
      return await action();
    } on LocationServiceException catch (exception) {
      _errorMessage = exception.message;
      return null;
    } on BookingApiException catch (exception) {
      _errorMessage = exception.message;
      return null;
    } catch (_) {
      _errorMessage = AppStrings.genericError;
      return null;
    } finally {
      _isLoading = false;
      notifyListeners();
    }
  }
}
