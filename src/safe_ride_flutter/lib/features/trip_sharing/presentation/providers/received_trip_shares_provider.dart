import 'dart:async';

import 'package:flutter/foundation.dart';

import '../../data/datasources/trip_sharing_remote_datasource.dart';
import '../../data/models/trip_share_models.dart';

class ReceivedTripSharesProvider extends ChangeNotifier {
  ReceivedTripSharesProvider(this._datasource);
  final TripSharingRemoteDatasource _datasource;

  List<ReceivedTripShare> shares = const [];
  bool isLoading = false;
  String? errorMessage;
  Timer? _refreshTimer;

  Future<void> load() async {
    _refreshTimer ??= Timer.periodic(
      const Duration(seconds: 15),
      (_) => unawaited(refresh()),
    );
    await refresh();
  }

  Future<void> refresh() async {
    _removeExpired();
    isLoading = true;
    errorMessage = null;
    notifyListeners();
    try {
      shares = (await _datasource.received(activeOnly: true))
          .where((share) => share.expiresAt.isAfter(DateTime.now().toUtc()))
          .toList(growable: false);
    } on TripSharingApiException catch (error) {
      if (error.statusCode == 401) {
        debugPrint('Received share refresh returned 401: ${error.message}');
      } else {
        debugPrint('Received share refresh failed: ${error.message}');
      }
      errorMessage = error.message;
    } catch (error) {
      debugPrint('Received share refresh failed: $error');
      errorMessage = 'Không thể tải danh sách chia sẻ. Vui lòng thử lại.';
    } finally {
      isLoading = false;
      notifyListeners();
    }
  }

  void _removeExpired() {
    final activeShares = shares
        .where((share) => share.expiresAt.isAfter(DateTime.now().toUtc()))
        .toList(growable: false);
    if (activeShares.length != shares.length) {
      shares = activeShares;
      notifyListeners();
    }
  }

  @override
  void dispose() {
    _refreshTimer?.cancel();
    super.dispose();
  }
}
