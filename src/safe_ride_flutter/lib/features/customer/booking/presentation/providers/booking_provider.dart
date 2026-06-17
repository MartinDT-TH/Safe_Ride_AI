import 'package:flutter/foundation.dart';

import '../../../../../core/constants/app_strings.dart';
import '../../../../../core/services/location_service.dart';
import '../../data/datasources/booking_remote_datasource.dart';
import '../../data/models/booking_catalog.dart';
import '../../data/models/booking_fare_estimate.dart';
import '../../data/models/booking_location.dart';
import '../../data/models/booking_response.dart';
import '../../data/models/create_booking_request.dart';
import '../../data/models/nearby_driver.dart';
import '../../domain/repositories/booking_repository.dart';

class BookingProvider extends ChangeNotifier {
  BookingProvider(this._repository, this._locationService);

  final BookingRepository _repository;
  final LocationService _locationService;

  bool _isLoading = false;
  bool _isEstimating = false;
  String? _errorMessage;
  BookingCatalog? _catalog;
  BookingFareEstimate? _fareEstimate;
  List<NearbyDriver> _nearbyDrivers = [];
  int _estimateRequestId = 0;

  bool get isLoading => _isLoading;
  bool get isEstimating => _isEstimating;
  String? get errorMessage => _errorMessage;
  BookingCatalog? get catalog => _catalog;
  BookingFareEstimate? get fareEstimate => _fareEstimate;
  List<NearbyDriver> get nearbyDrivers => _nearbyDrivers;

  Future<void> loadCatalog(String accessToken) async {
    if (_catalog != null) return;
    await _run(() async {
      _catalog = await _repository.getCatalog(accessToken);
    });
  }

  Future<BookingLocation?> getCurrentLocation() async {
    return _run(() => _locationService.getCurrentLocation());
  }

  Future<BookingLocation?> resolveAddress(String address) async {
    return _run(() => _locationService.resolveAddress(address));
  }

  Future<BookingLocation?> resolveCoordinates(
    double latitude,
    double longitude,
  ) async {
    return _run(() => _locationService.resolveCoordinates(latitude, longitude));
  }

  Future<BookingFareEstimate?> estimateFare(
    String accessToken, {
    required int vehicleId,
    required int serviceTypeId,
    required BookingLocation pickup,
    BookingLocation? destination,
    int? estimatedHours,
  }) async {
    final requestId = ++_estimateRequestId;
    _isEstimating = true;
    _fareEstimate = null;
    _errorMessage = null;
    notifyListeners();

    try {
      final estimate = await _repository.estimateFare(
        accessToken,
        vehicleId: vehicleId,
        serviceTypeId: serviceTypeId,
        pickup: pickup,
        destination: destination,
        estimatedHours: estimatedHours,
      );
      if (requestId != _estimateRequestId) return null;
      _fareEstimate = estimate;
      return estimate;
    } on BookingApiException catch (exception) {
      if (requestId == _estimateRequestId) {
        _errorMessage = exception.message;
      }
      return null;
    } catch (_) {
      if (requestId == _estimateRequestId) {
        _errorMessage = AppStrings.genericError;
      }
      return null;
    } finally {
      if (requestId == _estimateRequestId) {
        _isEstimating = false;
        notifyListeners();
      }
    }
  }

  void clearFareEstimate() {
    _estimateRequestId++;
    _fareEstimate = null;
    _isEstimating = false;
    notifyListeners();
  }

  Future<BookingResponse?> createBooking(
    String accessToken,
    CreateBookingRequest request,
  ) {
    return _run(() => _repository.createBooking(accessToken, request));
  }

  Future<BookingResponse?> confirmDriver(
    String accessToken, {
    required int bookingId,
  }) {
    return _run(
      () => _repository.confirmDriver(accessToken, bookingId: bookingId),
    );
  }

  Future<BookingResponse?> cancelBooking(
    String accessToken, {
    required int bookingId,
    required String reason,
  }) {
    return _run(
      () => _repository.cancelBooking(
        accessToken,
        bookingId: bookingId,
        reason: reason,
      ),
    );
  }

  Future<void> fetchNearbyDrivers(
    String accessToken, {
    required double latitude,
    required double longitude,
  }) async {
    try {
      _nearbyDrivers = await _repository.getNearbyDrivers(
        accessToken,
        latitude: latitude,
        longitude: longitude,
      );
      debugPrint('Found ${_nearbyDrivers.length} nearby drivers');
      notifyListeners();
    } catch (e) {
      debugPrint('Error fetching nearby drivers: $e');
    }
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
