import 'package:flutter/foundation.dart';
import 'package:signalr_netcore/signalr_client.dart';

import '../constants/app_strings.dart';
import 'mobile_config_service.dart';

class DriverLocationUpdate {
  const DriverLocationUpdate({
    required this.driverId,
    required this.customerId,
    required this.tripId,
    required this.latitude,
    required this.longitude,
    required this.updatedAt,
  });

  final String driverId;
  final String customerId;
  final int tripId;
  final double latitude;
  final double longitude;
  final DateTime? updatedAt;

  static DriverLocationUpdate? fromSignalRArguments(List<Object?>? arguments) {
    if (arguments == null || arguments.isEmpty || arguments.first is! Map) {
      return null;
    }

    final data = Map<String, dynamic>.from(arguments.first as Map);
    final tripId = (_value(data, ApiKeys.tripId) as num?)?.toInt();
    final latitude = (_value(data, ApiKeys.latitude) as num?)?.toDouble();
    final longitude = (_value(data, ApiKeys.longitude) as num?)?.toDouble();
    if (tripId == null || latitude == null || longitude == null) {
      return null;
    }

    return DriverLocationUpdate(
      driverId: _value(data, ApiKeys.driverId)?.toString() ?? '',
      customerId: _value(data, 'customerId')?.toString() ?? '',
      tripId: tripId,
      latitude: latitude,
      longitude: longitude,
      updatedAt: _value(data, 'updatedAt') == null
          ? null
          : DateTime.tryParse(_value(data, 'updatedAt').toString()),
    );
  }

  static Object? _value(Map<String, dynamic> data, String key) {
    final pascalKey = key.isEmpty
        ? key
        : '${key[0].toUpperCase()}${key.substring(1)}';
    return data[key] ?? data[pascalKey];
  }
}

class DriverOfferUpdate {
  const DriverOfferUpdate({
    required this.bookingId,
    required this.offerId,
    required this.driverId,
    required this.message,
    required this.expiresAt,
  });

  final int bookingId;
  final int offerId;
  final String driverId;
  final String message;
  final DateTime? expiresAt;

  static DriverOfferUpdate? fromSignalRArguments(List<Object?>? arguments) {
    if (arguments == null || arguments.isEmpty || arguments.first is! Map) {
      return null;
    }

    final data = Map<String, dynamic>.from(arguments.first as Map);
    final driverOfferRaw = _value(data, 'driverOffer');
    if (driverOfferRaw is! Map) {
      return null;
    }

    final driverOffer = Map<String, dynamic>.from(driverOfferRaw);
    final bookingId = (_value(data, ApiKeys.bookingId) as num?)?.toInt();
    final offerId = (_value(driverOffer, ApiKeys.offerId) as num?)?.toInt();
    if (bookingId == null || offerId == null) {
      return null;
    }

    final expiresAtRaw = _value(driverOffer, ApiKeys.expiresAt);
    return DriverOfferUpdate(
      bookingId: bookingId,
      offerId: offerId,
      driverId: _value(data, ApiKeys.driverId)?.toString() ?? '',
      message:
          _value(data, ApiKeys.message)?.toString() ?? 'Bạn có chuyến mới.',
      expiresAt: expiresAtRaw == null
          ? null
          : DateTime.tryParse(expiresAtRaw.toString()),
    );
  }

  static Object? _value(Map<String, dynamic> data, String key) {
    final pascalKey = key.isEmpty
        ? key
        : '${key[0].toUpperCase()}${key.substring(1)}';
    return data[key] ?? data[pascalKey];
  }
}

class TripStatusUpdate {
  const TripStatusUpdate({
    required this.tripId,
    required this.bookingId,
    required this.customerId,
    required this.driverId,
    required this.tripStatus,
    required this.updatedAt,
  });

  final int tripId;
  final int bookingId;
  final String customerId;
  final String driverId;
  final String tripStatus;
  final DateTime? updatedAt;

  static TripStatusUpdate? fromSignalRArguments(List<Object?>? arguments) {
    if (arguments == null || arguments.isEmpty || arguments.first is! Map) {
      return null;
    }

    final data = Map<String, dynamic>.from(arguments.first as Map);
    final tripId = (_value(data, ApiKeys.tripId) as num?)?.toInt();
    final bookingId = (_value(data, ApiKeys.bookingId) as num?)?.toInt();
    final tripStatus = _normalizeTripStatus(_value(data, ApiKeys.tripStatus));
    if (tripId == null || bookingId == null || tripStatus == null) {
      return null;
    }

    return TripStatusUpdate(
      tripId: tripId,
      bookingId: bookingId,
      customerId: _value(data, 'customerId')?.toString() ?? '',
      driverId: _value(data, ApiKeys.driverId)?.toString() ?? '',
      tripStatus: tripStatus,
      updatedAt: _value(data, 'updatedAt') == null
          ? null
          : DateTime.tryParse(_value(data, 'updatedAt').toString()),
    );
  }

  static Object? _value(Map<String, dynamic> data, String key) {
    final pascalKey = key.isEmpty
        ? key
        : '${key[0].toUpperCase()}${key.substring(1)}';
    return data[key] ?? data[pascalKey];
  }

  static String? _normalizeTripStatus(Object? value) {
    if (value == null) {
      return null;
    }

    if (value is num) {
      return switch (value.toInt()) {
        0 => 'ACCEPTED',
        1 => 'DRIVER_ARRIVING',
        2 => 'ARRIVED',
        3 => 'IN_PROGRESS',
        4 => 'WAITING_RETURN_CONFIRM',
        5 => 'RETURN_CONFIRMED',
        6 => 'WAITING_PAYMENT',
        7 => 'COMPLETED',
        8 => 'CANCELLED',
        _ => value.toString(),
      };
    }

    final text = value.toString();
    return switch (text) {
      '0' => 'ACCEPTED',
      '1' => 'DRIVER_ARRIVING',
      '2' => 'ARRIVED',
      '3' => 'IN_PROGRESS',
      '4' => 'WAITING_RETURN_CONFIRM',
      '5' => 'RETURN_CONFIRMED',
      '6' => 'WAITING_PAYMENT',
      '7' => 'COMPLETED',
      '8' => 'CANCELLED',
      _ => text,
    };
  }
}

class TripPaymentUpdate {
  const TripPaymentUpdate({
    required this.tripId,
    required this.bookingId,
    required this.customerId,
    required this.driverId,
    required this.paymentStatus,
    required this.amount,
    required this.currency,
    required this.tripStatus,
    required this.message,
    required this.eventName,
    this.paymentId,
    this.paymentMethod,
    this.occurredAt,
  });

  final int tripId;
  final int bookingId;
  final String customerId;
  final String driverId;
  final int? paymentId;
  final String? paymentMethod;
  final String paymentStatus;
  final double amount;
  final String currency;
  final String tripStatus;
  final String message;
  final String eventName;
  final DateTime? occurredAt;

  bool get isSuccess => paymentStatus.toLowerCase() == 'success';

  static TripPaymentUpdate? fromSignalRArgumentsForEvent(
    List<Object?>? arguments,
    String eventName,
  ) {
    if (arguments == null || arguments.isEmpty || arguments.first is! Map) {
      return null;
    }

    final data = Map<String, dynamic>.from(arguments.first as Map);
    final tripId = (_value(data, ApiKeys.tripId) as num?)?.toInt();
    final bookingId = (_value(data, ApiKeys.bookingId) as num?)?.toInt();
    final amount = (_value(data, ApiKeys.amount) as num?)?.toDouble();
    final tripStatus = TripStatusUpdate._normalizeTripStatus(
      _value(data, ApiKeys.tripStatus),
    );
    if (tripId == null ||
        bookingId == null ||
        amount == null ||
        tripStatus == null) {
      return null;
    }

    final occurredAtRaw =
        _value(data, 'paidAt') ??
        _value(data, 'createdAt') ??
        _value(data, 'updatedAt');

    return TripPaymentUpdate(
      tripId: tripId,
      bookingId: bookingId,
      customerId: _value(data, 'customerId')?.toString() ?? '',
      driverId: _value(data, ApiKeys.driverId)?.toString() ?? '',
      paymentId: (_value(data, ApiKeys.paymentId) as num?)?.toInt(),
      paymentMethod: _value(data, ApiKeys.paymentMethod)?.toString(),
      paymentStatus:
          _value(data, ApiKeys.paymentStatus)?.toString() ?? 'Pending',
      amount: amount,
      currency: _value(data, ApiKeys.currency)?.toString() ?? 'VND',
      tripStatus: tripStatus,
      message: _value(data, ApiKeys.message)?.toString() ?? '',
      eventName: eventName,
      occurredAt: occurredAtRaw == null
          ? null
          : DateTime.tryParse(occurredAtRaw.toString()),
    );
  }

  static Object? _value(Map<String, dynamic> data, String key) {
    final pascalKey = key.isEmpty
        ? key
        : '${key[0].toUpperCase()}${key.substring(1)}';
    return data[key] ?? data[pascalKey];
  }
}

class BookingUpdate {
  const BookingUpdate({
    required this.bookingId,
    this.status,
    this.currentSearchRadiusKm,
    this.estimatedRemainingSeconds,
    this.matchingMessage,
    this.driverOffer,
    this.tripId,
    this.tripStatus,
  });

  final int bookingId;
  final String? status;
  final double? currentSearchRadiusKm;
  final int? estimatedRemainingSeconds;
  final String? matchingMessage;
  final Map<String, dynamic>? driverOffer;
  final int? tripId;
  final String? tripStatus;

  static BookingUpdate? fromSignalRArguments(List<Object?>? arguments) {
    return fromSignalRArgumentsForEvent(arguments, null);
  }

  static BookingUpdate? fromSignalRArgumentsForEvent(
    List<Object?>? arguments,
    String? event,
  ) {
    if (arguments == null || arguments.isEmpty || arguments.first is! Map) {
      return null;
    }

    final data = Map<String, dynamic>.from(arguments.first as Map);
    final bookingId = (_value(data, ApiKeys.bookingId) as num?)?.toInt();
    if (bookingId == null) return null;

    final tripStatus = _normalizeTripStatus(_value(data, ApiKeys.tripStatus));
    final status = _normalizeBookingStatus(
      _value(data, ApiKeys.bookingStatus),
      event,
      tripStatus,
    );
    final driverOfferRaw = _value(data, ApiKeys.driverOffer);

    return BookingUpdate(
      bookingId: bookingId,
      status: status,
      currentSearchRadiusKm:
          (_value(data, ApiKeys.currentSearchRadiusKm) as num?)?.toDouble(),
      estimatedRemainingSeconds:
          (_value(data, ApiKeys.estimatedRemainingSeconds) as num?)?.toInt(),
      matchingMessage:
          (_value(data, ApiKeys.matchingMessage) ??
                  _value(data, ApiKeys.message))
              ?.toString(),
      driverOffer: driverOfferRaw is Map
          ? Map<String, dynamic>.from(driverOfferRaw)
          : null,
      tripId: (_value(data, ApiKeys.tripId) as num?)?.toInt(),
      tripStatus: tripStatus,
    );
  }

  static Object? _value(Map<String, dynamic> data, String key) {
    final pascalKey = key.isEmpty
        ? key
        : '${key[0].toUpperCase()}${key.substring(1)}';
    return data[key] ?? data[pascalKey];
  }

  static String? _normalizeBookingStatus(
    Object? value,
    String? event,
    String? tripStatus,
  ) {
    if (value != null) {
      if (value is num) {
        return switch (value.toInt()) {
          0 => 'PendingSchedule',
          1 => 'Searching',
          2 => 'DriverAssigned',
          3 => 'Cancelled',
          4 => 'Expired',
          5 => 'Completed',
          _ => value.toString(),
        };
      }

      final text = value.toString();
      return switch (text) {
        '0' => 'PendingSchedule',
        '1' => 'Searching',
        '2' => 'DriverAssigned',
        '3' => 'Cancelled',
        '4' => 'Expired',
        '5' => 'Completed',
        _ => text,
      };
    }

    if (tripStatus == 'COMPLETED') return 'Completed';
    if (tripStatus == 'CANCELLED') return 'Cancelled';

    return switch (event) {
      'DriverOfferAccepted' => 'Searching',
      'BookingDriverAssigned' ||
      'TripCreated' ||
      'CustomerConfirmedDriverOffer' => 'DriverAssigned',
      'BookingExpired' => 'Expired',
      'BookingCancelled' => 'Cancelled',
      _ => null,
    };
  }

  static String? _normalizeTripStatus(Object? value) {
    if (value == null) return null;
    if (value is num) {
      return switch (value.toInt()) {
        0 => 'ACCEPTED',
        1 => 'DRIVER_ARRIVING',
        2 => 'ARRIVED',
        3 => 'IN_PROGRESS',
        4 => 'WAITING_RETURN_CONFIRM',
        5 => 'RETURN_CONFIRMED',
        6 => 'WAITING_PAYMENT',
        7 => 'COMPLETED',
        8 => 'CANCELLED',
        _ => value.toString(),
      };
    }

    final text = value.toString();
    return switch (text) {
      '0' => 'ACCEPTED',
      '1' => 'DRIVER_ARRIVING',
      '2' => 'ARRIVED',
      '3' => 'IN_PROGRESS',
      '4' => 'WAITING_RETURN_CONFIRM',
      '5' => 'RETURN_CONFIRMED',
      '6' => 'WAITING_PAYMENT',
      '7' => 'COMPLETED',
      '8' => 'CANCELLED',
      _ => text,
    };
  }
}

class SocketService {
  SocketService({MobileConfigService? mobileConfigService})
    : _mobileConfigService = mobileConfigService ?? MobileConfigService();

  final MobileConfigService _mobileConfigService;
  HubConnection? _connection;
  String? _accessToken;
  bool _driverLocationListenerAttached = false;
  bool _tripStatusListenerAttached = false;
  bool _tripPaymentListenerAttached = false;
  bool _driverOfferReceivedListenerAttached = false;
  bool _driverOfferClosedListenerAttached = false;
  bool _bookingListenerAttached = false;
  final List<void Function()> _connectionLostHandlers = [];
  final Map<String, void Function(DriverLocationUpdate update)>
  _driverLocationHandlers = {};
  final Map<String, void Function(TripStatusUpdate update)>
  _tripStatusHandlers = {};
  final Map<String, void Function(TripPaymentUpdate update)>
  _tripPaymentHandlers = {};
  final Map<String, void Function(DriverOfferUpdate update)>
  _driverOfferReceivedHandlers = {};
  final Map<String, void Function(int offerId)> _driverOfferClosedHandlers = {};

  final Map<String, void Function(BookingUpdate update)> _bookingHandlers = {};

  final Set<int> _desiredTripGroups = {};
  final Set<int> _joinedTripGroups = {};
  final Set<int> _desiredBookingGroups = {};
  final Set<int> _joinedBookingGroups = {};

  bool get isConnected => _connection?.state == HubConnectionState.Connected;

  MobileConfigService get _configService => _mobileConfigService;

  /// Register a callback that fires when SignalR connection is permanently lost
  /// (i.e. automatic reconnect exhausted all retries and gave up).
  void addConnectionLostHandler(void Function() handler) {
    _connectionLostHandlers.add(handler);
  }

  void removeConnectionLostHandler(void Function() handler) {
    _connectionLostHandlers.remove(handler);
  }

  Future<void> connect(String accessToken) async {
    if (accessToken.isEmpty) {
      return;
    }

    final currentState = _connection?.state;

    if (_connection != null && _accessToken == accessToken) {
      if (currentState == HubConnectionState.Connected) return;
      if (currentState == HubConnectionState.Connecting ||
          currentState == HubConnectionState.Reconnecting) {
        debugPrint('SOCKET: Already connecting/reconnecting, waiting...');
        while (_connection?.state == HubConnectionState.Connecting ||
            _connection?.state == HubConnectionState.Reconnecting) {
          await Future.delayed(const Duration(milliseconds: 100));
        }
        return;
      }
    }

    if (_connection != null && _accessToken != accessToken) {
      debugPrint('SOCKET: New token, disconnecting old session');
      await disconnect();
    }

    _accessToken = accessToken;

    final options = HttpConnectionOptions(
      accessTokenFactory: () async => accessToken,
      requestTimeout: 30000,
      skipNegotiation: AppConfig.forceWebSockets,
      transport: AppConfig.forceWebSockets
          ? HttpTransportType.WebSockets
          : null,
    );

    _connection ??= HubConnectionBuilder()
        .withUrl(_hubUrl, options: options)
        .withAutomaticReconnect()
        .build();

    _connection!.serverTimeoutInMilliseconds = 60000;
    _connection!.keepAliveIntervalInMilliseconds = 30000;

    _connection!.onreconnected(({connectionId}) {
      debugPrint(
        'SOCKET: Reconnected ($connectionId). Re-joining groups: Trips=$_desiredTripGroups, Bookings=$_desiredBookingGroups',
      );
      _rejoinGroups();
    });

    // When automatic reconnect is exhausted, SignalR fires onclose. We null out
    // the connection object so the next connect() call rebuilds it from scratch.
    _connection!.onclose(({error}) {
      debugPrint('SOCKET: Connection permanently closed. error=$error');
      _connection = null;
      _driverLocationListenerAttached = false;
      _tripStatusListenerAttached = false;
      _tripPaymentListenerAttached = false;
      _driverOfferReceivedListenerAttached = false;
      _driverOfferClosedListenerAttached = false;
      _bookingListenerAttached = false;
      _joinedTripGroups.clear();
      _joinedBookingGroups.clear();
      for (final h in List.of(_connectionLostHandlers)) {
        h();
      }
    });

    if (_connection!.state == HubConnectionState.Disconnected) {
      try {
        debugPrint('SOCKET: Starting connection to $_hubUrl ...');
        await _connection!.start();
        debugPrint('SOCKET: Connected successfully');
      } catch (e) {
        debugPrint('SOCKET: Connection failed: $e');
        _connection = null;
        rethrow;
      }
    }

    if (_driverLocationHandlers.isNotEmpty &&
        !_driverLocationListenerAttached) {
      _attachDriverLocationListener();
    }
    if (_tripStatusHandlers.isNotEmpty && !_tripStatusListenerAttached) {
      _attachTripStatusListener();
    }
    if (_tripPaymentHandlers.isNotEmpty && !_tripPaymentListenerAttached) {
      _attachTripPaymentListeners();
    }
    if (_driverOfferReceivedHandlers.isNotEmpty &&
        !_driverOfferReceivedListenerAttached) {
      _attachDriverOfferReceivedListener();
    }
    if (_driverOfferClosedHandlers.isNotEmpty &&
        !_driverOfferClosedListenerAttached) {
      _attachDriverOfferClosedListeners();
    }
    if (_bookingHandlers.isNotEmpty && !_bookingListenerAttached) {
      _attachBookingListeners();
    }
    if (_desiredTripGroups.isNotEmpty || _desiredBookingGroups.isNotEmpty) {
      _rejoinGroups();
    }
  }

  void onDriverLocationUpdated(
    void Function(DriverLocationUpdate update) handler, {
    String key = 'default',
  }) {
    _driverLocationHandlers[key] = handler;
    _attachDriverLocationListener();
  }

  void _attachDriverLocationListener() {
    if (_connection == null || _driverLocationListenerAttached) {
      return;
    }

    _driverLocationListenerAttached = true;
    _connection!.on(
      _configService.config.realtime.events.driverLocationUpdated,
      (arguments) {
        final update = DriverLocationUpdate.fromSignalRArguments(arguments);
        if (update != null) {
          for (final handler in List.of(_driverLocationHandlers.values)) {
            handler(update);
          }
        }
      },
    );
  }

  void removeDriverLocationUpdatedHandler(String key) {
    _driverLocationHandlers.remove(key);
  }

  void onDriverOfferReceived(
    void Function(DriverOfferUpdate update) handler, {
    String key = 'default',
  }) {
    _driverOfferReceivedHandlers[key] = handler;
    _attachDriverOfferReceivedListener();
  }

  void _attachDriverOfferReceivedListener() {
    if (_connection == null || _driverOfferReceivedListenerAttached) {
      return;
    }

    _driverOfferReceivedListenerAttached = true;
    final event = _configService.config.realtime.events.driverOfferReceived;
    _connection!.on(event, (arguments) {
      final update = DriverOfferUpdate.fromSignalRArguments(arguments);
      if (update != null) {
        for (final handler in List.of(_driverOfferReceivedHandlers.values)) {
          handler(update);
        }
      }
    });
  }

  void removeDriverOfferReceivedHandler(String key) {
    _driverOfferReceivedHandlers.remove(key);
  }

  void onDriverOfferClosed(
    void Function(int offerId) handler, {
    String key = 'default',
  }) {
    _driverOfferClosedHandlers[key] = handler;
    _attachDriverOfferClosedListeners();
  }

  void _attachDriverOfferClosedListeners() {
    if (_connection == null || _driverOfferClosedListenerAttached) {
      return;
    }

    _driverOfferClosedListenerAttached = true;
    void handle(List<Object?>? arguments) {
      if (arguments == null || arguments.isEmpty || arguments.first is! Map) {
        return;
      }

      final data = Map<String, dynamic>.from(arguments.first as Map);
      final offerId = (data[ApiKeys.offerId] ?? data['OfferId']) as num?;
      if (offerId != null) {
        for (final handler in List.of(_driverOfferClosedHandlers.values)) {
          handler(offerId.toInt());
        }
      }
    }

    final events = _configService.config.realtime.events;
    _connection?.on(events.driverOfferExpired, handle);
    _connection?.on(events.driverOfferCancelled, handle);
  }

  void removeDriverOfferClosedHandler(String key) {
    _driverOfferClosedHandlers.remove(key);
  }

  void onTripStatusChanged(
    void Function(TripStatusUpdate update) handler, {
    String key = 'default',
  }) {
    _tripStatusHandlers[key] = handler;
    _attachTripStatusListener();
  }

  void _attachTripStatusListener() {
    if (_connection == null || _tripStatusListenerAttached) {
      return;
    }

    _tripStatusListenerAttached = true;
    _connection!.on(_configService.config.realtime.events.tripStatusChanged, (
      arguments,
    ) {
      final update = TripStatusUpdate.fromSignalRArguments(arguments);
      if (update != null) {
        for (final handler in List.of(_tripStatusHandlers.values)) {
          handler(update);
        }
      }
    });
  }

  void removeTripStatusChangedHandler(String key) {
    _tripStatusHandlers.remove(key);
  }

  void onTripPaymentUpdated(
    void Function(TripPaymentUpdate update) handler, {
    String key = 'default',
  }) {
    _tripPaymentHandlers[key] = handler;
    _attachTripPaymentListeners();
  }

  void _attachTripPaymentListeners() {
    if (_connection == null || _tripPaymentListenerAttached) {
      return;
    }

    _tripPaymentListenerAttached = true;
    final events = _configService.config.realtime.events;
    void handle(String eventName, List<Object?>? arguments) {
      final update = TripPaymentUpdate.fromSignalRArgumentsForEvent(
        arguments,
        eventName,
      );
      if (update != null) {
        for (final handler in List.of(_tripPaymentHandlers.values)) {
          handler(update);
        }
      }
    }

    _connection!.on(
      events.tripPaymentPending,
      (arguments) => handle(events.tripPaymentPending, arguments),
    );
    _connection!.on(
      events.tripPaymentSucceeded,
      (arguments) => handle(events.tripPaymentSucceeded, arguments),
    );
  }

  void removeTripPaymentUpdatedHandler(String key) {
    _tripPaymentHandlers.remove(key);
  }

  void onBookingUpdated(
    void Function(BookingUpdate update) handler, {
    String key = 'default',
  }) {
    _bookingHandlers[key] = handler;
    if (_connection == null) {
      return;
    }

    _attachBookingListeners();
  }

  void _attachBookingListeners() {
    if (_connection == null || _bookingListenerAttached) {
      return;
    }

    _bookingListenerAttached = true;
    final events = _configService.config.realtime.events.bookingUpdateEvents;

    for (final event in events) {
      _connection?.on(event, (arguments) {
        final update = BookingUpdate.fromSignalRArgumentsForEvent(
          arguments,
          event,
        );
        if (update != null) {
          for (final handler in List.of(_bookingHandlers.values)) {
            handler(update);
          }
        }
      });
    }
  }

  void removeBookingUpdatedHandler(String key) {
    _bookingHandlers.remove(key);
  }

  Future<void> joinTrip(int tripId) async {
    final firstRequest = _desiredTripGroups.add(tripId);
    if (!firstRequest && _joinedTripGroups.contains(tripId)) {
      return;
    }

    await _joinTripGroup(tripId);
  }

  Future<void> leaveTrip(int tripId) async {
    _desiredTripGroups.remove(tripId);
    _joinedTripGroups.remove(tripId);
    await _invokeSafely('LeaveTrip', [tripId]);
  }

  Future<void> joinBooking(int bookingId) async {
    final firstRequest = _desiredBookingGroups.add(bookingId);
    if (!firstRequest && _joinedBookingGroups.contains(bookingId)) {
      return;
    }

    await _joinBookingGroup(bookingId);
  }

  Future<void> leaveBooking(int bookingId) async {
    _desiredBookingGroups.remove(bookingId);
    _joinedBookingGroups.remove(bookingId);
    await _invokeSafely('LeaveBooking', [bookingId]);
  }

  Future<void> setDriverOnline(double latitude, double longitude) async {
    await _invokeSafely('SetDriverOnline', [latitude, longitude]);
  }

  Future<void> updateDriverLocation(double latitude, double longitude) async {
    await _invokeSafely('UpdateDriverLocation', [latitude, longitude]);
  }

  Future<void> setDriverOffline() async {
    await _invokeSafely('SetDriverOffline', []);
  }

  void _rejoinGroups() {
    _joinedTripGroups.clear();
    _joinedBookingGroups.clear();
    for (final tripId in List.of(_desiredTripGroups)) {
      _joinTripGroup(tripId, force: true);
    }
    for (final bookingId in List.of(_desiredBookingGroups)) {
      _joinBookingGroup(bookingId, force: true);
    }
  }

  Future<void> _joinTripGroup(int tripId, {bool force = false}) async {
    if (!force && _joinedTripGroups.contains(tripId)) {
      return;
    }

    final joined = await _invokeSafely('JoinTrip', [tripId]);
    if (joined && _desiredTripGroups.contains(tripId)) {
      _joinedTripGroups.add(tripId);
    }
  }

  Future<void> _joinBookingGroup(int bookingId, {bool force = false}) async {
    if (!force && _joinedBookingGroups.contains(bookingId)) {
      return;
    }

    final joined = await _invokeSafely('JoinBooking', [bookingId]);
    if (joined && _desiredBookingGroups.contains(bookingId)) {
      _joinedBookingGroups.add(bookingId);
    }
  }

  Future<bool> _invokeSafely(String methodName, List<Object> args) async {
    if (_connection?.state != HubConnectionState.Connected) {
      debugPrint(
        'SOCKET: Cannot invoke $methodName - Not connected (${_connection?.state})',
      );
      return false;
    }
    try {
      await _connection?.invoke(methodName, args: args);
      return true;
    } catch (e) {
      debugPrint('SOCKET: Invoke $methodName failed: $e');
      return false;
    }
  }

  Future<void> disconnect() async {
    if (_connection != null) {
      await _connection!.stop();
    }
    _connection = null;
    _accessToken = null;
    _driverLocationListenerAttached = false;
    _tripStatusListenerAttached = false;
    _tripPaymentListenerAttached = false;
    _driverOfferReceivedListenerAttached = false;
    _driverOfferClosedListenerAttached = false;
    _bookingListenerAttached = false;
    _driverLocationHandlers.clear();
    _tripStatusHandlers.clear();
    _tripPaymentHandlers.clear();
    _driverOfferReceivedHandlers.clear();
    _driverOfferClosedHandlers.clear();
    _bookingHandlers.clear();
    _desiredTripGroups.clear();
    _joinedTripGroups.clear();
    _desiredBookingGroups.clear();
    _joinedBookingGroups.clear();
  }

  String get _hubUrl {
    final apiBase = AppConfig.apiBaseUrl.endsWith('/')
        ? AppConfig.apiBaseUrl.substring(0, AppConfig.apiBaseUrl.length - 1)
        : AppConfig.apiBaseUrl;
    final root = apiBase.endsWith('/api')
        ? apiBase.substring(0, apiBase.length - 4)
        : apiBase;
    final hubPath = _configService.config.realtime.hubPath.startsWith('/')
        ? _configService.config.realtime.hubPath
        : '/${_configService.config.realtime.hubPath}';
    var url = '$root$hubPath';
    if (AppConfig.forceWebSockets) {
      if (url.startsWith('https://')) {
        url = url.replaceFirst('https://', 'wss://');
      } else if (url.startsWith('http://')) {
        url = url.replaceFirst('http://', 'ws://');
      }
    }
    return url;
  }
}
