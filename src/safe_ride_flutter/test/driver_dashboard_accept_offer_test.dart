import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:safe_ride/core/constants/app_strings.dart';
import 'package:safe_ride/core/services/socket_service.dart';
import 'package:safe_ride/features/driver/dashboard/presentation/providers/driver_dashboard_provider.dart';
import 'package:safe_ride/features/driver/trip_requests/data/datasources/driver_trip_request_remote_datasource.dart';
import 'package:safe_ride/features/driver/trip_requests/data/models/driver_trip_request_model.dart';
import 'package:safe_ride/features/driver/trip_requests/domain/repositories/driver_trip_request_repository.dart';

void main() {
  group('DriverDashboardProvider.acceptRequest', () {
    test(
      'accepts numeric response and enters customer confirmation wait',
      () async {
        final repository = _FakeTripRequestRepository([
          [_request(status: 'Sent')],
          [_request(status: 'DriverAccepted')],
        ]);
        String? acceptedPath;
        Object? authorization;
        final dio = _dio((options, handler) {
          if (options.path.contains('/offers/')) {
            acceptedPath = options.path;
            authorization = options.headers[ApiKeys.authorization];
            handler.resolve(
              Response<dynamic>(
                requestOptions: options,
                statusCode: 200,
                data: {
                  ApiKeys.bookingStatus: 1,
                  ApiKeys.tripId: null,
                  ApiKeys.driverOffer: {ApiKeys.offerStatus: '1'},
                },
              ),
            );
            return;
          }
          handler.resolve(
            Response<dynamic>(requestOptions: options, statusCode: 204),
          );
        });
        final provider = DriverDashboardProvider(
          socketService: _FakeSocketService(),
          dio: dio,
          tripRequestRepository: repository,
        );

        await provider.initializeRealtime('header.payload.signature');
        await provider.acceptRequest();
        await Future<void>.delayed(Duration.zero);

        expect(acceptedPath, '/drivers/offers/41/accept');
        expect(authorization, 'Bearer header.payload.signature');
        expect(provider.hasNewRequest, isFalse);
        expect(provider.isWaitingForCustomerConfirmation, isTrue);
        expect(provider.currentRequest?.offerId, 41);
        provider.dispose();
      },
    );

    test(
      '409 ProblemDetails keeps active offer and exposes error code',
      () async {
        final repository = _FakeTripRequestRepository([
          [_request(status: 'Sent')],
          [_request(status: 'Sent')],
        ]);
        final dio = _dio((options, handler) {
          if (options.path.contains('/offers/')) {
            handler.reject(
              DioException.badResponse(
                statusCode: 409,
                requestOptions: options,
                response: Response<dynamic>(
                  requestOptions: options,
                  statusCode: 409,
                  data: const {
                    ApiKeys.code: 'driver_offer.driver_unavailable',
                    ApiKeys.detail:
                        'Bạn không còn ở trạng thái sẵn sàng nhận chuyến.',
                  },
                ),
              ),
            );
            return;
          }
          handler.resolve(
            Response<dynamic>(requestOptions: options, statusCode: 204),
          );
        });
        final provider = DriverDashboardProvider(
          socketService: _FakeSocketService(),
          dio: dio,
          tripRequestRepository: repository,
        );

        await provider.initializeRealtime('header.payload.signature');
        await provider.acceptRequest();

        expect(provider.currentRequest?.offerId, 41);
        expect(provider.hasNewRequest, isTrue);
        expect(provider.isResponding, isFalse);
        expect(
          provider.tripRequestActionErrorCode,
          'driver_offer.driver_unavailable',
        );
        expect(
          provider.snackbarMessage,
          'Bạn không còn ở trạng thái sẵn sàng nhận chuyến.',
        );
        expect(repository.calls, 2);
        provider.dispose();
      },
    );

    test(
      'refresh failure after accept error preserves offer for retry',
      () async {
        final repository = _FakeTripRequestRepository([
          [_request(status: 'Sent')],
        ], errorAfterResponse: const DriverTripRequestApiException('offline'));
        final dio = _dio((options, handler) {
          if (options.path.contains('/offers/')) {
            handler.reject(
              DioException(
                requestOptions: options,
                type: DioExceptionType.connectionError,
              ),
            );
            return;
          }
          handler.resolve(
            Response<dynamic>(requestOptions: options, statusCode: 204),
          );
        });
        final provider = DriverDashboardProvider(
          socketService: _FakeSocketService(),
          dio: dio,
          tripRequestRepository: repository,
        );

        await provider.initializeRealtime('header.payload.signature');
        await provider.acceptRequest();

        expect(provider.currentRequest?.offerId, 41);
        expect(provider.hasNewRequest, isTrue);
        expect(provider.tripRequestsErrorMessage, isNotNull);
        provider.dispose();
      },
    );
  });

  group('DriverTripRequestModel', () {
    test('accepts positive numeric-string ids and mixed-case status', () {
      final model = DriverTripRequestModel.fromJson({
        'offerId': '41',
        'bookingId': '73',
        'offerStatus': 'dRiVeRaCcEpTeD',
        'expectedIncome': 100000,
        'pickupAddress': 'A',
        'destinationAddress': 'B',
      });

      expect(model.offerId, 41);
      expect(model.bookingId, 73);
      expect(model.offerStatus, 'DriverAccepted');
    });

    test('rejects non-actionable ids and unknown status', () {
      expect(
        () => DriverTripRequestModel.fromJson({
          'offerId': 0,
          'bookingId': 73,
          'offerStatus': 'Sent',
        }),
        throwsFormatException,
      );
      expect(
        () => DriverTripRequestModel.fromJson({
          'offerId': 41,
          'bookingId': 73,
          'offerStatus': 'Unknown',
        }),
        throwsFormatException,
      );
    });
  });
}

DriverTripRequestModel _request({required String status}) =>
    DriverTripRequestModel(
      offerId: 41,
      bookingId: 73,
      offerStatus: status,
      expiresAt: DateTime.utc(2026, 8, 21, 6),
      pickupAddress: 'Điểm đón',
      destinationAddress: 'Điểm đến',
    );

Dio _dio(void Function(RequestOptions, RequestInterceptorHandler) onRequest) =>
    Dio(BaseOptions(baseUrl: 'https://example.test/api/'))
      ..interceptors.add(InterceptorsWrapper(onRequest: onRequest));

class _FakeTripRequestRepository implements DriverTripRequestRepository {
  _FakeTripRequestRepository(this.responses, {this.errorAfterResponse});

  final List<List<DriverTripRequestModel>> responses;
  final Object? errorAfterResponse;
  int calls = 0;

  @override
  Future<List<DriverTripRequestModel>> getOpenTripRequests(
    String accessToken,
  ) async {
    final index = calls++;
    if (index < responses.length) {
      return responses[index];
    }
    if (errorAfterResponse != null) {
      throw errorAfterResponse!;
    }
    return responses.isEmpty ? const [] : responses.last;
  }
}

class _FakeSocketService extends SocketService {
  @override
  Future<void> connect([String? legacyAccessToken]) async {}

  @override
  void onDriverOfferReceived(
    void Function(DriverOfferUpdate update) handler, {
    String key = 'default',
  }) {}

  @override
  void onDriverOfferClosed(
    void Function({int? offerId, int? bookingId}) handler, {
    String key = 'default',
  }) {}

  @override
  void onTripStatusChanged(
    void Function(TripStatusUpdate update) handler, {
    String key = 'default',
  }) {}

  @override
  void onTripPaymentUpdated(
    void Function(TripPaymentUpdate update) handler, {
    String key = 'default',
  }) {}

  @override
  void onBookingUpdated(
    void Function(BookingUpdate update) handler, {
    String key = 'default',
  }) {}

  @override
  Future<void> joinBooking(int bookingId) async {}
}
