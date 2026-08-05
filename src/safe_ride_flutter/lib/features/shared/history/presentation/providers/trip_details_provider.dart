import 'package:flutter/material.dart';
import '../../../../../core/localization/locale_provider.dart';
import '../../../../../core/localization/api_error_localizer.dart';

import '../../data/models/history_trip.dart';
import '../../data/models/trip_details_view_data.dart';
import '../../domain/repositories/trip_details_repository.dart';
import 'package:safe_ride/features/shared/feedback/domain/repositories/feedback_repository.dart';
import 'package:safe_ride/features/shared/feedback/data/models/driver_rating_item.dart';

class TripDetailsProvider extends ChangeNotifier {
  final TripDetailsRepository _repository;
  final FeedbackRepository? _feedbackRepository;
  final HistoryTrip _historyTrip;

  bool _isLoading = false;
  String? _errorMessage;
  bool _hasLoadedRemoteDetails = false;
  TripDetailsViewData _tripDetailsViewData;

  TripDetailsProvider._internal(
    this._repository,
    this._feedbackRepository,
    this._historyTrip,
    this._tripDetailsViewData,
  );

  factory TripDetailsProvider.create(
    TripDetailsRepository repository,
    HistoryTrip historyTrip, {
    FeedbackRepository? feedbackRepository,
  }) {
    return TripDetailsProvider._internal(
      repository,
      feedbackRepository,
      historyTrip,
      TripDetailsViewData(historyTrip: historyTrip),
    );
  }

  bool get isLoading => _isLoading;
  String? get errorMessage => _errorMessage;
  bool get hasLoadedRemoteDetails => _hasLoadedRemoteDetails;
  TripDetailsViewData get tripDetails => _tripDetailsViewData;

  Future<void> loadDetails(
    String? accessToken, {
    String? driverIdForFeedback,
  }) async {
    _isLoading = true;
    _errorMessage = null;
    notifyListeners();

    try {
      if (accessToken == null || accessToken.isEmpty) {
        throw TripDetailsRepositoryException(
          LocaleProvider.currentLocalizations.sessionExpired,
        );
      }

      final booking = await _repository.getTripDetails(
        accessToken,
        bookingId: _historyTrip.id,
      );

      DriverRatingItem? feedback;
      final tripId = booking.tripId ?? _historyTrip.tripId;

      if (driverIdForFeedback != null &&
          _feedbackRepository != null &&
          tripId != null) {
        try {
          final summary = await _feedbackRepository.getDriverRatings(
            accessToken,
            driverId: driverIdForFeedback,
          );
          feedback = summary.ratings.firstWhere(
            (r) => r.tripId == tripId,
            orElse: () => throw Exception('Not found'),
          );
        } catch (_) {
          // Feedback not found or error loading feedback, ignore and continue
        }
      }

      _tripDetailsViewData = TripDetailsViewData(
        historyTrip: _historyTrip,
        booking: booking,
        feedback: feedback,
      );
      _hasLoadedRemoteDetails = true;
    } on TripDetailsRepositoryException catch (exception) {
      _errorMessage = ApiErrorLocalizer.translate(
        LocaleProvider.currentLocalizations,
        fallback: exception.message,
      );
      if (!_hasLoadedRemoteDetails) {
        _tripDetailsViewData = TripDetailsViewData(historyTrip: _historyTrip);
      }
    } catch (_) {
      _errorMessage = LocaleProvider.currentLocalizations.tripDetailsLoadFailed;
      if (!_hasLoadedRemoteDetails) {
        _tripDetailsViewData = TripDetailsViewData(historyTrip: _historyTrip);
      }
    } finally {
      _isLoading = false;
      notifyListeners();
    }
  }
}
