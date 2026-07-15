import 'package:flutter/foundation.dart';

import '../../data/datasources/trip_sharing_remote_datasource.dart';
import '../../data/models/trip_share_models.dart';

class TripSharingProvider extends ChangeNotifier {
  TripSharingProvider(this._datasource);
  final TripSharingRemoteDatasource _datasource;

  bool isLoading = false;
  String? errorMessage;
  List<TripShareListItem> shares = const [];

  Future<void> load(String token, int tripId) async {
    await _run(() async => shares = await _datasource.list(token, tripId));
  }

  Future<CreatedTripShare?> create(
    String token, {
    required int tripId,
    required String phoneNumber,
  }) async {
    CreatedTripShare? result;
    await _run(() async {
      result = await _datasource.create(
        token,
        tripId: tripId,
        recipientPhoneNumber: phoneNumber,
      );
      // The create response contains everything needed to render the new active
      // recipient. Do not keep the share dialog blocked on a second network call.
      shares = [
        TripShareListItem(
          tripShareId: result!.tripShareId,
          recipient: result!.recipient,
          expiresAt: result!.expiresAt,
          isActive: true,
        ),
        ...shares.where((share) => share.tripShareId != result!.tripShareId),
      ];
    });
    return result;
  }

  Future<bool> revoke(String token, int tripId, int tripShareId) async {
    var success = false;
    await _run(() async {
      await _datasource.revoke(token, tripId, tripShareId);
      shares = await _datasource.list(token, tripId);
      success = true;
    });
    return success;
  }

  Future<void> _run(Future<void> Function() action) async {
    isLoading = true;
    errorMessage = null;
    notifyListeners();
    try {
      await action();
    } catch (error) {
      errorMessage = error.toString();
    } finally {
      isLoading = false;
      notifyListeners();
    }
  }
}
