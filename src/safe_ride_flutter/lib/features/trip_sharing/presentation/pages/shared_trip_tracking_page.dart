import 'dart:async';

import 'package:flutter/material.dart';

import '../../../../core/maps/models/map_models.dart';
import '../../../../core/maps/polyline_decoder.dart';
import '../../../../core/maps/widgets/live_trip_map_widget.dart';
import '../../../../core/services/socket_service.dart';
import '../../../../core/session/session_manager.dart';
import '../../../../dependency_injection/injection.dart';
import '../../data/datasources/trip_sharing_remote_datasource.dart';
import '../../data/models/trip_share_models.dart';

class SharedTripTrackingPage extends StatefulWidget {
  const SharedTripTrackingPage({super.key, required this.tripShareId});
  final int tripShareId;

  @override
  State<SharedTripTrackingPage> createState() => _SharedTripTrackingPageState();
}

class _SharedTripTrackingPageState extends State<SharedTripTrackingPage> {
  final _datasource = getIt<TripSharingRemoteDatasource>();
  final _socket = getIt<SocketService>();
  Timer? _pollTimer;
  SharedTripTracking? _tracking;
  String? _terminalMessage;
  String? _error;
  bool _loading = true;
  bool _trackingStopped = false;
  bool _freezeLocation = false;

  String get _handlerKey => 'sharedTrip:${widget.tripShareId}';

  @override
  void initState() {
    super.initState();
    unawaited(_initialize());
  }

  Future<void> _initialize() async {
    await _refresh();
    if (!mounted || _terminalMessage != null || _trackingStopped) return;
    _socket.onSharedTripLocationUpdated(_onLocation, key: _handlerKey);
    _socket.onSharedTripStatusUpdated(_onStatus, key: _handlerKey);
    try {
      await _socket.connect();
      await _socket.subscribeSharedTrip(widget.tripShareId);
    } catch (_) {
      // Polling below remains the durable fallback when realtime is unavailable.
    }
    _pollTimer = Timer.periodic(
      const Duration(seconds: 15),
      (_) => unawaited(_refresh(silent: true)),
    );
  }

  Future<void> _refresh({bool silent = false}) async {
    final token = await getIt<SessionManager>().getValidAccessToken();
    if (token == null) {
      if (mounted) setState(() => _error = 'Phiên đăng nhập đã hết hạn.');
      return;
    }
    try {
      final tracking = await _datasource.tracking(token, widget.tripShareId);
      if (!mounted) return;
      if (const {
        'COMPLETED',
        'CANCELLED',
      }.contains(tracking.tripStatus.toUpperCase())) {
        _freezeLocation = true;
      }
      setState(() {
        _tracking = _freezeLocation && _tracking != null
            ? _tracking!.copyWith(tripStatus: tracking.tripStatus)
            : tracking;
        _error = null;
        _loading = false;
      });
    } on TripSharingApiException catch (error) {
      if (!mounted) return;
      if (error.statusCode == 410) {
        await _stopTracking('Liên kết chia sẻ đã hết hạn hoặc bị thu hồi.');
      } else if (!silent || _tracking == null) {
        setState(() {
          _error = error.message;
          _loading = false;
        });
      }
    }
  }

  void _onLocation(Map<String, dynamic> payload) {
    final id = (payload['tripShareId'] as num?)?.toInt();
    if (!mounted ||
        id != widget.tripShareId ||
        _terminalMessage != null ||
        _freezeLocation) {
      return;
    }
    final latitude = (payload['latitude'] as num?)?.toDouble();
    final longitude = (payload['longitude'] as num?)?.toDouble();
    if (latitude == null || longitude == null || _tracking == null) return;
    setState(() {
      _tracking = _tracking!.copyWith(
        currentDriverLocation: SharedTripPoint(
          latitude: latitude,
          longitude: longitude,
        ),
        lastLocationUpdate: DateTime.tryParse(
          payload['updatedAt']?.toString() ?? '',
        )?.toUtc(),
      );
    });
  }

  void _onStatus(String event, Map<String, dynamic> payload) {
    final id = (payload['tripShareId'] as num?)?.toInt();
    if (!mounted || id != widget.tripShareId) return;
    if (event == 'TripShareRevoked') {
      unawaited(_stopTracking('Chủ chuyến đi đã thu hồi quyền theo dõi.'));
      return;
    }
    if (event == 'TripShareExpired') {
      unawaited(_stopTracking('Liên kết chia sẻ đã hết hạn.'));
      return;
    }
    final status = payload['tripStatus']?.toString();
    if (status != null && _tracking != null) {
      if (const {'COMPLETED', 'CANCELLED'}.contains(status.toUpperCase())) {
        _freezeLocation = true;
      }
      setState(() => _tracking = _tracking!.copyWith(tripStatus: status));
    }
  }

  Future<void> _stopTracking(String message) async {
    await _cleanupTracking();
    if (!mounted) return;
    setState(() {
      _terminalMessage = message;
      _loading = false;
    });
  }

  Future<void> _cleanupTracking() async {
    if (_trackingStopped) return;
    _trackingStopped = true;
    _pollTimer?.cancel();
    _socket.removeSharedTripHandlers(_handlerKey);
    try {
      await _socket.unsubscribeSharedTrip(widget.tripShareId);
    } catch (_) {
      // The desired shared-trip group has already been removed locally.
    }
  }

  Future<void> _closeTracking() async {
    await _cleanupTracking();
    if (mounted) Navigator.of(context).maybePop();
  }

  @override
  void dispose() {
    unawaited(_cleanupTracking());
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) {
      return const Scaffold(body: Center(child: CircularProgressIndicator()));
    }
    if (_terminalMessage != null || _error != null || _tracking == null) {
      return Scaffold(
        appBar: AppBar(
          title: const Text('Chuyến đi được chia sẻ'),
          leading: IconButton(
            icon: const Icon(Icons.close),
            onPressed: _closeTracking,
          ),
          actions: [
            TextButton(
              onPressed: _closeTracking,
              child: const Text('Về trang chủ'),
            ),
          ],
        ),
        body: Center(
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: Text(
              _terminalMessage ?? _error ?? 'Không thể tải chuyến đi.',
              textAlign: TextAlign.center,
              style: const TextStyle(fontSize: 17),
            ),
          ),
        ),
      );
    }

    final tracking = _tracking!;
    final route = tracking.routePolyline == null
        ? const <AppLatLng>[]
        : _decodeRoute(tracking.routePolyline!);
    final isArriving = const {
      'ACCEPTED',
      'DRIVER_ARRIVING',
      'ARRIVED',
    }.contains(tracking.tripStatus.toUpperCase());

    return Scaffold(
      appBar: AppBar(
        title: const Text('Chuyến đi được chia sẻ'),
        leading: IconButton(
          icon: const Icon(Icons.close),
          onPressed: _closeTracking,
        ),
        actions: [
          TextButton(onPressed: _closeTracking, child: const Text('Đóng')),
        ],
      ),
      body: Column(
        children: [
          Expanded(
            child: LiveTripMapWidget(
              trackingState: isArriving
                  ? LiveTripTrackingState.arriving
                  : LiveTripTrackingState.inProgress,
              pickup: AppLatLng(
                tracking.pickup.latitude,
                tracking.pickup.longitude,
              ),
              destination: tracking.destination == null
                  ? null
                  : AppLatLng(
                      tracking.destination!.latitude,
                      tracking.destination!.longitude,
                    ),
              tripRoutePoints: route,
              driverPosition: tracking.currentDriverLocation == null
                  ? null
                  : AppLatLng(
                      tracking.currentDriverLocation!.latitude,
                      tracking.currentDriverLocation!.longitude,
                    ),
            ),
          ),
          SafeArea(
            top: false,
            child: Padding(
              padding: const EdgeInsets.all(20),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    _statusLabel(tracking.tripStatus),
                    style: const TextStyle(
                      color: Color(0xFF006B70),
                      fontWeight: FontWeight.w800,
                      fontSize: 17,
                    ),
                  ),
                  const SizedBox(height: 12),
                  Text(
                    tracking.driverName,
                    style: const TextStyle(
                      fontSize: 18,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                  Text(
                    '${tracking.vehicleBrandModel} • ${tracking.vehicleColor ?? ''} • ${tracking.maskedPlateNumber}',
                  ),
                  if (tracking.lastLocationUpdate != null) ...[
                    const SizedBox(height: 8),
                    Text(
                      'Cập nhật vị trí: ${tracking.lastLocationUpdate!.toLocal()}',
                      style: const TextStyle(color: Colors.black54),
                    ),
                  ],
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }

  List<AppLatLng> _decodeRoute(String encoded) {
    try {
      return decodePolyline(encoded);
    } on FormatException {
      return const [];
    }
  }

  String _statusLabel(String status) => switch (status.toUpperCase()) {
    'COMPLETED' => 'Chuyến đi đã hoàn thành',
    'CANCELLED' => 'Chuyến đi đã bị hủy',
    'IN_PROGRESS' => 'Chuyến đi đang diễn ra',
    'ARRIVED' => 'Tài xế đã đến điểm đón',
    _ => 'Tài xế đang đến điểm đón',
  };
}
