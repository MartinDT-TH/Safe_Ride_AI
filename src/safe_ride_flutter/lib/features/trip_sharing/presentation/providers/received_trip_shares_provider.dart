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
  bool _isRefreshing = false;
  bool _isDisposed = false;
  int _consecutiveFailures = 0;

  static const _pollInterval = Duration(seconds: 15);
  static const _maxBackoff = Duration(minutes: 2);

  Future<void> load() async {
    await refresh(silent: true);
    if (_isDisposed) return;
    _scheduleBackgroundRefresh();
  }

  Future<void> refresh({bool silent = false}) async {
    if (_isDisposed || _isRefreshing) return;
    _isRefreshing = true;
    _removeExpired();
    if (!silent) {
      isLoading = true;
      errorMessage = null;
      notifyListeners();
    }
    try {
      final updatedShares =
          (await _datasource.received(
                activeOnly: true,
                suppressGlobalErrorSnackBar: silent,
              ))
              .where((share) => share.expiresAt.isAfter(DateTime.now().toUtc()))
              .toList(growable: false);
      final sharesChanged = !_sameShares(shares, updatedShares);
      if (sharesChanged) {
        shares = updatedShares;
      }
      _consecutiveFailures = 0;
      if (silent && sharesChanged && !_isDisposed) notifyListeners();
    } on TripSharingApiException catch (error) {
      _consecutiveFailures++;
      if (error.statusCode == 401) {
        debugPrint('Received share refresh returned 401: ${error.message}');
      } else {
        debugPrint('Received share refresh failed: ${error.message}');
      }
      errorMessage = error.message;
    } catch (error) {
      _consecutiveFailures++;
      debugPrint('Received share refresh failed: $error');
      errorMessage = 'Không thể tải danh sách chia sẻ. Vui lòng thử lại.';
    } finally {
      _isRefreshing = false;
      if (!silent && !_isDisposed) {
        isLoading = false;
        notifyListeners();
      }
    }
  }

  void _scheduleBackgroundRefresh() {
    if (_isDisposed) return;
    _refreshTimer?.cancel();
    _refreshTimer = Timer(_nextRefreshDelay, () async {
      if (_isDisposed) return;
      await refresh(silent: true);
      _scheduleBackgroundRefresh();
    });
  }

  Duration get _nextRefreshDelay {
    if (_consecutiveFailures == 0) return _pollInterval;
    final multiplier = 1 << (_consecutiveFailures - 1).clamp(0, 3);
    final delay = Duration(seconds: _pollInterval.inSeconds * multiplier);
    return delay > _maxBackoff ? _maxBackoff : delay;
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

  static bool _sameShares(
    List<ReceivedTripShare> current,
    List<ReceivedTripShare> updated,
  ) {
    if (current.length != updated.length) return false;
    for (var index = 0; index < current.length; index++) {
      final left = current[index];
      final right = updated[index];
      if (left.tripShareId != right.tripShareId ||
          left.tripStatus != right.tripStatus ||
          left.sharedByName != right.sharedByName ||
          left.sharedByAvatarUrl != right.sharedByAvatarUrl ||
          left.openedAt != right.openedAt ||
          left.expiresAt != right.expiresAt ||
          left.isActive != right.isActive) {
        return false;
      }
    }
    return true;
  }

  @override
  void dispose() {
    _isDisposed = true;
    _refreshTimer?.cancel();
    super.dispose();
  }
}
