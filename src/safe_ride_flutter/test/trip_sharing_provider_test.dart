import 'dart:async';

import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:safe_ride/features/trip_sharing/data/datasources/trip_sharing_remote_datasource.dart';
import 'package:safe_ride/features/trip_sharing/data/models/trip_share_models.dart';
import 'package:safe_ride/features/trip_sharing/presentation/providers/trip_sharing_provider.dart';
import 'package:safe_ride/features/trip_sharing/presentation/providers/received_trip_shares_provider.dart';

void main() {
  test(
    'create completes from POST response without waiting for a list refresh',
    () async {
      final datasource = _FakeTripSharingDatasource();
      final provider = TripSharingProvider(datasource);

      final created = await provider.create(
        tripId: 12,
        phoneNumber: '+84901234567',
      );

      expect(created?.tripShareId, 42);
      expect(datasource.listCalls, 0);
      expect(provider.isLoading, isFalse);
      expect(provider.shares.single.tripShareId, 42);
      expect(provider.shares.single.isActive, isTrue);
    },
  );

  test('load does not require explicit access token', () async {
    final datasource = _FakeTripSharingDatasource();
    final provider = TripSharingProvider(datasource);

    await provider.load(12);

    expect(datasource.listCalls, 1);
    expect(provider.shares, isEmpty);
  });

  test('received provider refresh preserves cache on failure', () async {
    final datasource = _FakeTripSharingDatasource();
    final provider = ReceivedTripSharesProvider(datasource);

    datasource.stubReceivedFailure = true;
    await provider.refresh();

    expect(provider.shares, isEmpty);
    expect(provider.errorMessage, isNotNull);
  });

  test('received provider does not overlap background refreshes', () async {
    final datasource = _FakeTripSharingDatasource()..holdReceivedRequest = true;
    final provider = ReceivedTripSharesProvider(datasource);

    final firstRefresh = provider.refresh(silent: true);
    await Future<void>.delayed(Duration.zero);
    final secondRefresh = provider.refresh(silent: true);

    expect(datasource.receivedCalls, 1);
    datasource.completeReceivedRequest();
    await Future.wait([firstRefresh, secondRefresh]);
  });
}

class _FakeTripSharingDatasource extends TripSharingRemoteDatasource {
  _FakeTripSharingDatasource() : super(dio: Dio());

  int listCalls = 0;
  int receivedCalls = 0;
  bool stubReceivedFailure = false;
  bool holdReceivedRequest = false;
  Completer<void>? _receivedCompleter;

  @override
  Future<CreatedTripShare> create({
    required int tripId,
    required String recipientPhoneNumber,
  }) async => CreatedTripShare(
    tripShareId: 42,
    recipient: const TripShareRecipient(
      userId: 'recipient-id',
      fullName: 'Người nhận',
      maskedPhoneNumber: '0901***567',
    ),
    shareUrl: 'https://app.saferide.vn/trip-share?t=test',
    expiresAt: DateTime.utc(2026, 7, 16),
  );

  @override
  Future<List<TripShareListItem>> list(int tripId) async {
    listCalls++;
    return const [];
  }

  @override
  Future<List<ReceivedTripShare>> received({
    bool activeOnly = true,
    bool suppressGlobalErrorSnackBar = false,
  }) async {
    receivedCalls++;
    if (holdReceivedRequest) {
      _receivedCompleter ??= Completer<void>();
      await _receivedCompleter!.future;
    }
    if (stubReceivedFailure) {
      throw TripSharingApiException('Network error', statusCode: 500);
    }
    return const [];
  }

  void completeReceivedRequest() => _receivedCompleter?.complete();
}
