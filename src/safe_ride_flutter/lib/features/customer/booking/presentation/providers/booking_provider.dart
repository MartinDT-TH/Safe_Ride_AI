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
  String? _locationErrorMessage;

  BookingResponse? _activeBooking;
  BookingLocation? _activePickup;
  BookingLocation? _activeDestination;
  BookingVehicleOption? _activeVehicle;

  BookingResponse? _searchingBooking;

  bool get isLoading => _isLoading;
  bool get isEstimating => _isEstimating;
  String? get errorMessage => _errorMessage;
  String? get locationErrorMessage => _locationErrorMessage;
  BookingCatalog? get catalog => _catalog;
  BookingFareEstimate? get fareEstimate => _fareEstimate;
  List<NearbyDriver> get nearbyDrivers => _nearbyDrivers;

  BookingResponse? get activeBooking => _activeBooking;
  BookingLocation? get activePickup => _activePickup;
  BookingLocation? get activeDestination => _activeDestination;
  BookingVehicleOption? get activeVehicle => _activeVehicle;

  BookingResponse? get searchingBooking => _searchingBooking;

  bool get hasActiveNowBooking => _activeBooking != null;

  void setSearchingBooking(BookingResponse? booking) {
    _searchingBooking = booking;
    notifyListeners();
  }

  Future<BookingResponse?> refreshSearchingBooking(
    String accessToken, {
    required int bookingId,
  }) async {
    final booking = await _repository.getBookingDetails(
      accessToken,
      bookingId: bookingId,
    );
    _searchingBooking = booking;
    if (_isActiveNowBooking(booking)) {
      _setActiveBookingFromResponse(booking);
    } else if (booking.bookingStatus == 'Cancelled' ||
        booking.bookingStatus == 'Expired' ||
        booking.bookingStatus == 'Completed') {
      _activeBooking = null;
      _activePickup = null;
      _activeDestination = null;
      _activeVehicle = null;
    }
    notifyListeners();
    return booking;
  }

  Future<BookingResponse?> loadActiveBooking(String accessToken) async {
    _isLoading = true;
    _errorMessage = null;
    notifyListeners();

    try {
      final booking = await _repository.getActiveBooking(accessToken);
      if (_isActiveNowBooking(booking)) {
        _setActiveBookingFromResponse(booking!);
      } else {
        _activeBooking = null;
        _activePickup = null;
        _activeDestination = null;
        _activeVehicle = null;
      }
      return booking;
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

  void setActiveBooking({
    BookingResponse? booking,
    BookingLocation? pickup,
    BookingLocation? destination,
    BookingVehicleOption? vehicle,
  }) {
    _activeBooking = booking;
    _activePickup = pickup ?? booking?.pickup;
    _activeDestination = destination ?? booking?.destination;
    _activeVehicle = vehicle ?? booking?.vehicle;
    notifyListeners();
  }

  void clearActiveBooking() {
    _activeBooking = null;
    _activePickup = null;
    _activeDestination = null;
    _activeVehicle = null;
    notifyListeners();
  }

  Future<void> loadCatalog(
    String accessToken, {
    bool forceRefresh = false,
  }) async {
    if (_catalog != null && !forceRefresh) return;
    await _run(() async {
      _catalog = await _repository.getCatalog(accessToken);
    });
  }

  Future<BookingLocation?> getCurrentLocation() async {
    _locationErrorMessage = null;
    notifyListeners();

    try {
      return await _locationService.getCurrentLocation();
    } on LocationServiceException catch (exception) {
      _locationErrorMessage = exception.message;
      notifyListeners();
      return null;
    } catch (_) {
      _locationErrorMessage = AppStrings.genericError;
      notifyListeners();
      return null;
    }
  }

  Future<BookingLocation?> resolveAddress(String address) async {
    return _runLocation(() => _locationService.resolveAddress(address));
  }

  Future<BookingLocation?> resolveCoordinates(
    double latitude,
    double longitude,
  ) async {
    return _runLocation(
      () => _locationService.resolveCoordinates(latitude, longitude),
    );
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

  Future<BookingResponse?> rejectDriver(
    String accessToken, {
    required int bookingId,
  }) {
    return _run(
      () => _repository.rejectDriver(accessToken, bookingId: bookingId),
    );
  }

  Future<BookingResponse?> cancelBooking(
    String accessToken, {
    required int bookingId,
    required String reason,
  }) {
    return _run(() async {
      final booking = await _repository.cancelBooking(
        accessToken,
        bookingId: bookingId,
        reason: reason,
      );
      if (_isTerminalBooking(booking)) {
        _searchingBooking = null;
        _activeBooking = null;
        _activePickup = null;
        _activeDestination = null;
        _activeVehicle = null;
      }
      return booking;
    });
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

  Future<T?> _runLocation<T>(Future<T> Function() action) async {
    _locationErrorMessage = null;
    notifyListeners();

    try {
      return await action();
    } on LocationServiceException catch (exception) {
      _locationErrorMessage = exception.message;
      notifyListeners();
      return null;
    } catch (_) {
      _locationErrorMessage = AppStrings.genericError;
      notifyListeners();
      return null;
    }
  }

  void _setActiveBookingFromResponse(BookingResponse booking) {
    _activeBooking = booking;
    _activePickup = booking.pickup;
    _activeDestination = booking.destination;
    _activeVehicle = booking.vehicle;
  }

  bool _isActiveNowBooking(BookingResponse? booking) {
    return booking != null &&
        booking.bookingType == AppValues.bookingNow &&
        (booking.bookingStatus == 'Searching' ||
            booking.bookingStatus == 'DriverAssigned');
  }

  bool _isTerminalBooking(BookingResponse booking) {
    return booking.bookingStatus == 'Cancelled' ||
        booking.bookingStatus == 'Expired' ||
        booking.bookingStatus == 'Completed';
  }
}
