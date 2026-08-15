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
}

class _FakeSocketService extends SocketService {
  void Function(BookingUpdate update)? _bookingHandler;
  void Function(TripStatusUpdate update)? _tripStatusHandler;
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
  }) {}

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
}
