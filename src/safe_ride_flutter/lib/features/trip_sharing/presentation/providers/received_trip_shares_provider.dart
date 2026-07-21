import 'dart:async';

import 'package:flutter/foundation.dart';

import '../../data/datasources/trip_sharing_remote_datasource.dart';
import '../../data/models/trip_share_models.dart';

class ReceivedTripSharesProvider extends ChangeNotifier {
  ReceivedTripSharesProvider(this._datasource);
  final TripSharingRemoteDatasource _datasource;

  List<ReceivedTripShare> shares = const [];
  bool isLoading = false;
  Timer? _refreshTimer;
  String? _token;

  Future<void> load(String? token) async {
    if (token == null || token.isEmpty) return;
    if (_token != null && _token != token) {
      shares = const [];
    }
    _token = token;
    _refreshTimer ??= Timer.periodic(
      const Duration(seconds: 15),
      (_) => unawaited(refresh()),
    );
    await refresh();
  }

  Future<void> refresh() async {
    final token = _token;
    if (token == null || token.isEmpty) return;
    _removeExpired();
    isLoading = true;
    notifyListeners();
    try {
      shares = (await _datasource.received(token, activeOnly: true))
          .where((share) => share.expiresAt.isAfter(DateTime.now().toUtc()))
          .toList(growable: false);
    } catch (_) {
      // Preserve the last valid list during transient network failures.
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
