import 'package:flutter/foundation.dart';

import '../../../../../core/constants/app_strings.dart';
import '../../../../../core/services/location_service.dart';
import '../../../../../core/services/socket_service.dart';
import '../../data/models/promo_model.dart';
import '../../data/datasources/booking_remote_datasource.dart';
import '../../../../../core/maps/models/map_api_models.dart';
import '../../data/models/booking_catalog.dart';
import '../../data/models/booking_fare_estimate.dart';
import '../../data/models/booking_location.dart';
import '../../data/models/booking_response.dart';
import '../../data/models/create_booking_request.dart';
import '../../data/models/nearby_driver.dart';
import '../../domain/repositories/booking_repository.dart';

class BookingProvider extends ChangeNotifier {
  BookingProvider(
    this._repository,
    this._locationService, [
    this._socketService,
  ]);

  final BookingRepository _repository;
  final LocationService _locationService;
  final SocketService? _socketService;

  bool _isLoading = false;
  bool _isEstimating = false;
  bool _isLoadingPromotions = false;
  String? _errorMessage;
  int? _errorStatusCode;
  BookingCatalog? _catalog;
  BookingFareEstimate? _fareEstimate;
  List<NearbyDriver> _nearbyDrivers = [];
  List<PromoModel> _availablePromotions = [];
  PromoModel? _selectedPromo;
  int _estimateRequestId = 0;
  String? _locationErrorMessage;

  BookingResponse? _activeBooking;
  BookingLocation? _activePickup;
  BookingLocation? _activeDestination;
  BookingVehicleOption? _activeVehicle;

  BookingResponse? _searchingBooking;

  bool get isLoading => _isLoading;
  bool get isEstimating => _isEstimating;
  bool get isLoadingPromotions => _isLoadingPromotions;
  String? get errorMessage => _errorMessage;
  int? get errorStatusCode => _errorStatusCode;
  String? get locationErrorMessage => _locationErrorMessage;
  BookingCatalog? get catalog => _catalog;
  BookingFareEstimate? get fareEstimate => _fareEstimate;
  List<NearbyDriver> get nearbyDrivers => _nearbyDrivers;
  List<PromoModel> get availablePromotions => _availablePromotions;
  PromoModel? get selectedPromo => _selectedPromo;

  BookingResponse? get activeBooking => _activeBooking;
  BookingLocation? get activePickup => _activePickup;
  BookingLocation? get activeDestination => _activeDestination;
  BookingVehicleOption? get activeVehicle => _activeVehicle;

  BookingResponse? get searchingBooking => _searchingBooking;

  bool get hasActiveNowBooking => _activeBooking != null;

  void setSearchingBooking(BookingResponse? booking) {
    debugPrint('PROVIDER: setSearchingBooking ID: ${booking?.bookingId}');
    _searchingBooking = booking;
    if (booking != null) {
      _setupBookingRealtime(booking.bookingId);
    } else {
      _socketService?.removeBookingUpdatedHandler('customer_booking');
    }
    notifyListeners();
  }

  void _setupBookingRealtime(int bookingId) {
    debugPrint('PROVIDER: Setting up SignalR for Booking $bookingId');
    _socketService?.onBookingUpdated((update) {
      debugPrint(
        'PROVIDER: SIGNALR RECEIVED for Booking ${update.bookingId}. Status: ${update.status}',
      );
      if (update.bookingId != bookingId) return;

      final current = _searchingBooking ?? _activeBooking;
      if (current == null) {
        debugPrint(
          'PROVIDER: Received update but no current booking in provider',
        );
        return;
      }

      final updatedBooking = current.copyWith(
        bookingStatus: update.status ?? current.bookingStatus,
        currentSearchRadiusKm:
            update.currentSearchRadiusKm ?? current.currentSearchRadiusKm,
        estimatedRemainingSeconds:
            update.estimatedRemainingSeconds ??
            current.estimatedRemainingSeconds,
        matchingMessage: update.matchingMessage ?? current.matchingMessage,
        driverOffer: update.driverOffer != null
            ? BookingDriverOffer.fromJson(update.driverOffer!)
            : current.driverOffer,
        tripId: update.tripId ?? current.tripId,
        tripStatus: update.tripStatus ?? current.tripStatus,
      );

      debugPrint(
        'PROVIDER: Updated status: ${updatedBooking.bookingStatus}, Offer: ${updatedBooking.driverOffer?.driverName}',
      );

      if (_isTerminalBooking(updatedBooking)) {
        debugPrint('PROVIDER: Booking $bookingId is terminal, clearing state');
        _searchingBooking = null;
        _activeBooking = null;
        _activePickup = null;
        _activeDestination = null;
        _activeVehicle = null;
        _socketService.removeBookingUpdatedHandler('customer_booking');
      } else if (_isActiveNowBooking(updatedBooking)) {
        _searchingBooking = updatedBooking;
        _setActiveBookingFromResponse(updatedBooking);
      } else {
        _searchingBooking = updatedBooking;
      }

      notifyListeners();
    }, key: 'customer_booking');
  }

  Future<BookingResponse?> refreshSearchingBooking(
    String accessToken, {
    required int bookingId,
  }) async {
    try {
      final newBooking = await _repository.getBookingDetails(
        accessToken,
        bookingId: bookingId,
      );

      // Preserve promotion info if the polling response is missing it
      final current = _searchingBooking ?? _activeBooking;

      final booking =
          (current != null && current.bookingId == newBooking.bookingId)
          ? current.mergeWithPreservedPromotion(newBooking)
          : newBooking;

      debugPrint(
        'PROVIDER: polling booking ${booking.bookingId} status=${booking.bookingStatus} trip=${booking.tripStatus} offer=${booking.driverOffer?.offerStatus}/${booking.driverOffer?.driverName}',
      );

      _searchingBooking = booking;
      if (_isActiveNowBooking(booking)) {
        _setActiveBookingFromResponse(booking);
      } else if (_isTerminalBooking(booking)) {
        _activeBooking = null;
        _activePickup = null;
        _activeDestination = null;
        _activeVehicle = null;
        _searchingBooking = null;
      }
      notifyListeners();
      return booking;
    } catch (e) {
      debugPrint('PROVIDER: Refresh searching booking failed: $e');
      return null;
    }
  }

  Future<BookingResponse?> loadActiveBooking(String accessToken) async {
    _isLoading = true;
    _errorMessage = null;
    notifyListeners();

    try {
      final newBooking = await _repository.getActiveBooking(accessToken);
      if (newBooking == null) {
        _activeBooking = null;
        _activePickup = null;
        _activeDestination = null;
        _activeVehicle = null;
        return null;
      }

      // Preserve promotion info if we already have it
      final booking =
          (_activeBooking != null &&
              _activeBooking!.bookingId == newBooking.bookingId)
          ? _activeBooking!.mergeWithPreservedPromotion(newBooking)
          : newBooking;

      if (_isActiveNowBooking(booking) || booking.tripStatus != null) {
        _setActiveBookingFromResponse(booking);
        // If it's searching, also set searchingBooking
        if (booking.bookingStatus == 'Searching' ||
            booking.bookingStatus == 'DriverAssigned') {
          _searchingBooking = booking;
          _setupBookingRealtime(booking.bookingId);
        }
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

  Future<BookingResponse?> refreshActiveBookingDetails(
    String accessToken, {
    required int bookingId,
  }) async {
    try {
      final newBooking = await _repository.getBookingDetails(
        accessToken,
        bookingId: bookingId,
      );
      final current = _activeBooking ?? _searchingBooking;
      final booking =
          (current != null && current.bookingId == newBooking.bookingId)
          ? current.mergeWithPreservedPromotion(newBooking)
          : newBooking;

      if (_isTerminalBooking(booking)) {
        _activeBooking = null;
        _activePickup = null;
        _activeDestination = null;
        _activeVehicle = null;
        _searchingBooking = null;
      } else {
        _setActiveBookingFromResponse(booking);
      }

      notifyListeners();
      return booking;
    } on BookingApiException catch (exception) {
      _errorMessage = exception.message;
      notifyListeners();
      return null;
    } catch (_) {
      _errorMessage = AppStrings.genericError;
      notifyListeners();
      return null;
    }
  }

  Future<BookingResponse?> getPastBookingDetails(
    String accessToken, {
    required int bookingId,
  }) {
    return _run(
      () => _repository.getBookingDetails(accessToken, bookingId: bookingId),
    );
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
    _searchingBooking = null;
    _socketService?.removeBookingUpdatedHandler('customer_booking');
    notifyListeners();
  }

  void updateActiveTripStatus({
    required int bookingId,
    required String tripStatus,
  }) {
    final activeBooking = _activeBooking;
    if (activeBooking == null || activeBooking.bookingId != bookingId) {
      return;
    }

    _activeBooking = activeBooking.copyWith(
      tripStatus: tripStatus,
      bookingStatus: tripStatus == 'COMPLETED'
          ? 'Completed'
          : tripStatus == 'CANCELLED'
          ? 'Cancelled'
          : activeBooking.bookingStatus,
    );

    if (tripStatus == 'CANCELLED' || tripStatus == 'COMPLETED') {
      // We keep activeBooking for the summary page, but effectively it's finishing
    }

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

  Future<void> loadAvailablePromotions(String accessToken) async {
    _isLoadingPromotions = true;
    _errorMessage = null;
    notifyListeners();

    try {
      _availablePromotions = await _repository.getAvailablePromotions(
        accessToken,
      );
    } on BookingApiException catch (exception) {
      _errorMessage = exception.message;
    } catch (_) {
      _errorMessage = AppStrings.genericError;
    } finally {
      _isLoadingPromotions = false;
      notifyListeners();
    }
  }

  void selectPromo(PromoModel promo) {
    _selectedPromo = promo;
    notifyListeners();
  }

  void clearSelectedPromo() {
    _selectedPromo = null;
    notifyListeners();
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

  Future<BookingLocation?> resolvePlaceId(String placeId) async {
    return _runLocation(() => _locationService.resolvePlaceId(placeId));
  }

  Future<List<PlaceAutocompleteResult>> autocompleteAddress(
    String query, {
    double? lat,
    double? lng,
  }) async {
    return await _locationService.autocompleteAddress(
      query,
      lat: lat,
      lng: lng,
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
    final finalRequest = request.promotionCode == null
        ? request.copyWith(promotionCode: _selectedPromo?.promotionCode)
        : request;
    return _run(() => _repository.createBooking(accessToken, finalRequest));
  }

  Future<BookingResponse?> confirmDriver(
    String accessToken, {
    required int bookingId,
  }) {
    return _run(
      () => _repository.confirmDriver(accessToken, bookingId: bookingId),
    );
  }

  Future<BookingResponse?> confirmDriverOffer(
    String accessToken, {
    required int bookingId,
    required int offerId,
  }) {
    return _run(() async {
      final booking = await _repository.confirmDriverOffer(
        accessToken,
        bookingId: bookingId,
        offerId: offerId,
      );
      _searchingBooking = booking;
      if (_isActiveNowBooking(booking)) {
        _setActiveBookingFromResponse(booking);
      }
      return booking;
    });
  }

  Future<bool> completeTrip(String accessToken, {required int tripId}) async {
    final ok = await _run(() async {
      await _repository.completeTrip(accessToken, tripId: tripId);
      return true;
    });
    return ok == true;
  }

  Future<bool> submitTripRating(
    String accessToken, {
    required int tripId,
    required int ratingScore,
    String? comment,
  }) async {
    final ok = await _run(() async {
      await _repository.submitTripRating(
        accessToken,
        tripId: tripId,
        ratingScore: ratingScore,
        comment: comment,
      );
      return true;
    });
    return ok == true;
  }

  Future<bool> submitTripReport(
    String accessToken, {
    required int bookingId,
    required String subject,
    required String description,
  }) async {
    final ok = await _run(() async {
      await _repository.submitTripReport(
        accessToken,
        bookingId: bookingId,
        subject: subject,
        description: description,
      );
      return true;
    });
    return ok == true;
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
    debugPrint('PROVIDER: cancelBooking for ID $bookingId');
    return _run(() async {
      final booking = await _repository.cancelBooking(
        accessToken,
        bookingId: bookingId,
        reason: reason,
      );

      debugPrint(
        'PROVIDER: cancelBooking success, new status: ${booking.bookingStatus}',
      );

      if (_isTerminalBooking(booking)) {
        debugPrint('PROVIDER: Booking is terminal, clearing state');
        _searchingBooking = null;
        _activeBooking = null;
        _activePickup = null;
        _activeDestination = null;
        _activeVehicle = null;
        _socketService?.removeBookingUpdatedHandler('customer_booking');
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
    _errorStatusCode = null;
    notifyListeners();

    try {
      return await action();
    } on LocationServiceException catch (exception) {
      _errorMessage = exception.message;
      return null;
    } on BookingApiException catch (exception) {
      _errorMessage = exception.message;
      _errorStatusCode = exception.statusCode;
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
    // Preserve existing location/vehicle info if the new response is missing it
    _activePickup = booking.pickup ?? _activePickup;
    _activeDestination = booking.destination ?? _activeDestination;
    _activeVehicle = booking.vehicle ?? _activeVehicle;
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
