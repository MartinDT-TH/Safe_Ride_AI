import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:safe_ride/core/services/socket_service.dart';
import 'package:safe_ride/features/driver/dashboard/presentation/providers/driver_dashboard_provider.dart';

void main() {
  test(
    'completed booking event cannot recreate the cleared driver active trip',
    () async {
      final socket = _FakeSocketService();
      final dio = Dio()
        ..interceptors.add(
          InterceptorsWrapper(
            onRequest: (options, handler) {
              handler.resolve(
                Response<dynamic>(requestOptions: options, statusCode: 204),
              );
            },
          ),
        );
      final provider = DriverDashboardProvider(socketService: socket, dio: dio);
      await provider.initializeRealtime('header.payload.signature');

      socket.emitBooking(
        const BookingUpdate(
          bookingId: 101,
          status: 'DriverAssigned',
          tripId: 202,
          tripStatus: 'ACCEPTED',
        ),
      );
      expect(provider.activeTrip?.tripId, 202);

      socket.emitTripStatus(
        const TripStatusUpdate(
          tripId: 202,
          bookingId: 101,
          customerId: 'customer',
          driverId: 'driver',
          tripStatus: 'COMPLETED',
          updatedAt: null,
        ),
      );
      expect(provider.activeTrip, isNull);

      // Backend publishes BookingStatusChanged after TripStatusChanged. This
      // later event still carries tripId and previously recreated the popup.
      socket.emitBooking(
        const BookingUpdate(
          bookingId: 101,
          status: 'Completed',
          tripId: 202,
          tripStatus: 'COMPLETED',
        ),
      );

      expect(provider.activeTrip, isNull);
      expect(socket.leftTripIds, contains(202));
      provider.dispose();
    },
  );

  test('active trip reload cannot downgrade completed payment', () async {
    var returnActiveTrip = false;
    final socket = _FakeSocketService();
    final dio = Dio()
      ..interceptors.add(
        InterceptorsWrapper(
          onRequest: (options, handler) {
            if (returnActiveTrip &&
                options.path.endsWith('/drivers/trips/active')) {
              handler.resolve(
                Response<dynamic>(
                  requestOptions: options,
                  statusCode: 200,
                  data: const <String, dynamic>{
                    'bookingId': 101,
                    'tripId': 202,
                    'tripStatus': 'WAITING_RETURN_CONFIRM',
                    'paymentCompleted': false,
                  },
                ),
              );
              return;
            }

            handler.resolve(
              Response<dynamic>(requestOptions: options, statusCode: 204),
            );
          },
        ),
      );
    final provider = DriverDashboardProvider(socketService: socket, dio: dio);
    await provider.initializeRealtime('header.payload.signature');

    socket.emitBooking(
      const BookingUpdate(
        bookingId: 101,
        status: 'DriverAssigned',
        tripId: 202,
        tripStatus: 'WAITING_RETURN_CONFIRM',
      ),
    );
    provider.markTripPaymentCompleted(202);
    expect(provider.activeTrip?.paymentCompleted, isTrue);

    returnActiveTrip = true;
    await provider.loadActiveTrip();

    expect(provider.activeTrip?.paymentCompleted, isTrue);
    provider.dispose();
  });

  test(
    'successful payment advances the driver view to return confirmation',
    () async {
      final socket = _FakeSocketService();
      final dio = Dio()
        ..interceptors.add(
          InterceptorsWrapper(
            onRequest: (options, handler) {
              handler.resolve(
                Response<dynamic>(requestOptions: options, statusCode: 204),
              );
            },
          ),
        );
      final provider = DriverDashboardProvider(socketService: socket, dio: dio);
      await provider.initializeRealtime('header.payload.signature');

      socket.emitBooking(
        const BookingUpdate(
          bookingId: 101,
          status: 'DriverAssigned',
          tripId: 202,
          tripStatus: 'WAITING_PAYMENT',
        ),
      );
      socket.emitPayment(
        const TripPaymentUpdate(
          tripId: 202,
          bookingId: 101,
          customerId: 'customer',
          driverId: 'driver',
          paymentId: 303,
          paymentMethod: 'CASH',
          paymentStatus: 'Success',
          amount: 62000,
          currency: 'VND',
          tripStatus: 'WAITING_RETURN_CONFIRM',
          message: 'Paid',
          eventName: 'TripPaymentSucceeded',
        ),
      );

      expect(provider.activeTrip?.tripStatus, 'WAITING_RETURN_CONFIRM');
      expect(provider.activeTrip?.paymentCompleted, isTrue);
      provider.dispose();
    },
  );

  test(
    'return-confirmed recovery completes and clears the active trip',
    () async {
      String? requestedPath;
      final socket = _FakeSocketService();
      final dio = Dio()
        ..interceptors.add(
          InterceptorsWrapper(
            onRequest: (options, handler) {
              requestedPath = options.path;
              handler.resolve(
                Response<dynamic>(requestOptions: options, statusCode: 204),
              );
            },
          ),
        );
      final provider = DriverDashboardProvider(socketService: socket, dio: dio);
      await provider.initializeRealtime('header.payload.signature');
      socket.emitBooking(
        const BookingUpdate(
          bookingId: 101,
          status: 'DriverAssigned',
          tripId: 202,
          tripStatus: 'RETURN_CONFIRMED',
        ),
      );

      expect(await provider.completeActiveTrip(), isTrue);
      expect(requestedPath, endsWith('/trips/202/complete'));
      expect(provider.activeTrip, isNull);
      provider.dispose();
    },
  );
}

class _FakeSocketService extends SocketService {
  void Function(BookingUpdate update)? _bookingHandler;
  void Function(TripStatusUpdate update)? _tripStatusHandler;
  void Function(TripPaymentUpdate update)? _paymentHandler;
  final List<int> leftTripIds = [];

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
  }) {
    _tripStatusHandler = handler;
  }

  @override
  void onTripPaymentUpdated(
    void Function(TripPaymentUpdate update) handler, {
    String key = 'default',
  }) {
    _paymentHandler = handler;
  }

  @override
  void onBookingUpdated(
    void Function(BookingUpdate update) handler, {
    String key = 'default',
  }) {
    _bookingHandler = handler;
  }

  @override
  Future<void> joinTrip(int tripId) async {}

  @override
  Future<void> leaveTrip(int tripId) async {
    leftTripIds.add(tripId);
  }

  @override
  Future<void> leaveBooking(int bookingId) async {}

  void emitBooking(BookingUpdate update) => _bookingHandler?.call(update);

  void emitTripStatus(TripStatusUpdate update) =>
      _tripStatusHandler?.call(update);

  void emitPayment(TripPaymentUpdate update) => _paymentHandler?.call(update);
}
