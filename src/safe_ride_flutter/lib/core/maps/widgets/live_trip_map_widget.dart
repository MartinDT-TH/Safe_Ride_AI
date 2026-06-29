import 'dart:math' as math;
import 'package:flutter/material.dart';

import '../models/map_models.dart';
import 'map_renderer_widget.dart';

enum LiveTripTrackingState { arriving, inProgress }

class LiveTripMapWidget extends StatefulWidget {
  final LiveTripTrackingState trackingState;
  final AppLatLng pickup;
  final AppLatLng? destination;
  final List<AppLatLng> arrivalRoutePoints;
  final List<AppLatLng> tripRoutePoints;
  final AppLatLng? driverPosition;
  final double driverHeading;
  final EdgeInsets padding;
  final void Function(AppMapController)? onMapCreated;

  const LiveTripMapWidget({
    super.key,
    required this.trackingState,
    required this.pickup,
    this.destination,
    this.arrivalRoutePoints = const [],
    this.tripRoutePoints = const [],
    this.driverPosition,
    this.driverHeading = 0,
    this.padding = const EdgeInsets.all(24),
    this.onMapCreated,
  });

  @override
  State<LiveTripMapWidget> createState() => _LiveTripMapWidgetState();
}

class _RouteProgress {
  const _RouteProgress({
    required this.point,
    required this.segmentIndex,
    required this.progress,
    this.distanceMeters = 0,
  });

  final AppLatLng point;
  final int segmentIndex;
  final double progress;
  final double distanceMeters;
}

class _LiveTripMapWidgetState extends State<LiveTripMapWidget> {
  double _arrivalRouteProgress = 0;
  double _tripRouteProgress = 0;
  AppMapController? _mapController;
  static const _tealColor = Color(0xFF006B70);
  static const double _snapToRouteThresholdMeters = 45;
  static const double _offRouteThresholdMeters = 90;

  @override
  void didUpdateWidget(covariant LiveTripMapWidget oldWidget) {
    super.didUpdateWidget(oldWidget);
    // Reset progress if routes change completely
    if (oldWidget.arrivalRoutePoints != widget.arrivalRoutePoints &&
        widget.arrivalRoutePoints.isNotEmpty) {
      _arrivalRouteProgress = 0;
    }
    if (oldWidget.tripRoutePoints != widget.tripRoutePoints &&
        widget.tripRoutePoints.isNotEmpty) {
      _tripRouteProgress = 0;
    }
  }

  @override
  Widget build(BuildContext context) {
    return MapRendererWidget(
      initialCameraPosition: AppCameraPosition(
        target: widget.pickup,
        zoom: 15,
      ),
      onMapCreated: (controller) {
        _mapController = controller;
        if (widget.onMapCreated != null) {
          widget.onMapCreated!(controller);
        }
        _fitBounds();
      },
      myLocationButtonEnabled: true,
      markers: _buildMarkers(),
      polylines: _buildPolylines(),
      padding: widget.padding,
    );
  }

  void _fitBounds() {
    if (_mapController == null) return;
    
    final points = <AppLatLng>[];
    if (widget.arrivalRoutePoints.isNotEmpty) {
      points.addAll(widget.arrivalRoutePoints);
    }
    if (widget.tripRoutePoints.isNotEmpty) {
      points.addAll(widget.tripRoutePoints);
    }
    points.add(widget.pickup);
    if (widget.destination != null) points.add(widget.destination!);
    if (widget.driverPosition != null) points.add(widget.driverPosition!);

    if (points.isEmpty) return;

    double minLat = points.first.latitude;
    double maxLat = points.first.latitude;
    double minLng = points.first.longitude;
    double maxLng = points.first.longitude;

    for (final p in points) {
      if (p.latitude < minLat) minLat = p.latitude;
      if (p.latitude > maxLat) maxLat = p.latitude;
      if (p.longitude < minLng) minLng = p.longitude;
      if (p.longitude > maxLng) maxLng = p.longitude;
    }

    if (maxLat - minLat < 0.001) {
      minLat -= 0.001;
      maxLat += 0.001;
    }
    if (maxLng - minLng < 0.001) {
      minLng -= 0.001;
      maxLng += 0.001;
    }

    _mapController!.animateCameraToBounds(
      AppLatLng(minLat, minLng),
      AppLatLng(maxLat, maxLng),
      40.0, // padding
    );
  }

  Set<AppMarker> _buildMarkers() {
    final markers = <AppMarker>{
      AppMarker(
        id: 'pickup',
        position: widget.pickup,
        markerType: AppMarkerType.pickup,
      ),
    };

    final driverPos = _resolveDriverDisplayPosition(widget.driverPosition);
    if (driverPos != null) {
      markers.add(
        AppMarker(
          id: 'driver',
          position: driverPos,
          markerType: AppMarkerType.driver,
          rotation: widget.driverHeading,
        ),
      );
    }

    final destination = widget.destination;
    if (destination != null) {
      markers.add(
        AppMarker(
          id: 'destination',
          position: destination,
          markerType: AppMarkerType.destination,
        ),
      );
    }

    return markers;
  }

  Set<AppPolyline> _buildPolylines() {
    final polylines = <AppPolyline>{};
    final bool isArriving =
        widget.trackingState == LiveTripTrackingState.arriving;

    // Very faded full-route background (whole route outline) — zIndex: 1
    if (widget.tripRoutePoints.length >= 2) {
      polylines.add(
        AppPolyline(
          id: 'trip-route-static',
          points: widget.tripRoutePoints,
          color: _tealColor.withValues(alpha: 0.15),
          width: 4,
          zIndex: 1,
        ),
      );
    }

    if (widget.arrivalRoutePoints.length >= 2 && isArriving) {
      polylines.add(
        AppPolyline(
          id: 'arrival-route-static',
          points: widget.arrivalRoutePoints,
          color: const Color(0xFF2F80ED).withValues(alpha: 0.15),
          width: 4,
          zIndex: 1,
        ),
      );
    }

    // Passed (already driven) portion — extra faded grey, zIndex: 2
    if (!isArriving) {
      final passedPoints = _getPassedTripPoints();
      if (passedPoints.length >= 2) {
        polylines.add(
          AppPolyline(
            id: 'trip-route-passed',
            points: passedPoints,
            color: Colors.grey.withValues(alpha: 0.35),
            width: 5,
            zIndex: 2,
            endCapRound: true,
          ),
        );
      }
    } else {
      final passedArrival = _getPassedArrivalPoints();
      if (passedArrival.length >= 2) {
        polylines.add(
          AppPolyline(
            id: 'arrival-route-passed',
            points: passedArrival,
            color: Colors.grey.withValues(alpha: 0.35),
            width: 5,
            zIndex: 2,
            endCapRound: true,
          ),
        );
      }
    }

    // Active (remaining) routes on top — zIndex: 3/4
    if (widget.tripRoutePoints.isNotEmpty && !isArriving) {
      final tripPoints = _getDynamicTripPoints();
      if (tripPoints.length >= 2) {
        polylines.add(
          AppPolyline(
            id: 'trip-route-active',
            points: tripPoints,
            color: _tealColor,
            width: 6,
            zIndex: 3,
            endCapRound: true,
          ),
        );
      }
    }

    if (isArriving) {
      final arrivalPoints = _getDynamicArrivalPoints();
      if (arrivalPoints.length >= 2) {
        polylines.add(
          AppPolyline(
            id: 'arrival-route-active',
            points: arrivalPoints,
            color: const Color(0xFF2F80ED),
            width: 5,
            zIndex: 4,
            isDashed: true,
            endCapRound: true,
          ),
        );
      }
    }

    return polylines;
  }

  List<AppLatLng> _getDynamicTripPoints() {
    if (widget.tripRoutePoints.isEmpty) return const [];
    final progress = _routePositionAtProgress(
      widget.tripRoutePoints,
      _tripRouteProgress,
    );
    if (progress == null) return widget.tripRoutePoints;

    final points = [
      progress.point,
      ...widget.tripRoutePoints.skip(progress.segmentIndex + 1),
    ];
    return points.length < 2
        ? [progress.point, widget.tripRoutePoints.last]
        : points;
  }

  List<AppLatLng> _getDynamicArrivalPoints() {
    final driverPos = _resolveDriverDisplayPosition(widget.driverPosition);
    if (driverPos == null) return const [];
    if (widget.arrivalRoutePoints.isEmpty) {
      return [driverPos, widget.pickup];
    }

    final progress = _routePositionAtProgress(
      widget.arrivalRoutePoints,
      _arrivalRouteProgress,
    );
    if (progress == null) return widget.arrivalRoutePoints;

    final points = [
      progress.point,
      ...widget.arrivalRoutePoints.skip(progress.segmentIndex + 1),
    ];
    return points.length < 2
        ? [progress.point, widget.arrivalRoutePoints.last]
        : points;
  }

  List<AppLatLng> _getPassedTripPoints() {
    if (widget.tripRoutePoints.isEmpty || _tripRouteProgress <= 0) {
      return const [];
    }
    final progress = _routePositionAtProgress(
      widget.tripRoutePoints,
      _tripRouteProgress,
    );
    if (progress == null) return const [];
    return [
      ...widget.tripRoutePoints.take(progress.segmentIndex + 1),
      progress.point,
    ];
  }

  List<AppLatLng> _getPassedArrivalPoints() {
    if (widget.arrivalRoutePoints.isEmpty || _arrivalRouteProgress <= 0) {
      return const [];
    }
    final progress = _routePositionAtProgress(
      widget.arrivalRoutePoints,
      _arrivalRouteProgress,
    );
    if (progress == null) return const [];
    return [
      ...widget.arrivalRoutePoints.take(progress.segmentIndex + 1),
      progress.point,
    ];
  }

  AppLatLng? _resolveDriverDisplayPosition(AppLatLng? rawPosition) {
    if (rawPosition == null) return null;
    final isArriving = widget.trackingState == LiveTripTrackingState.arriving;
    final route =
        isArriving ? widget.arrivalRoutePoints : widget.tripRoutePoints;
    if (route.length < 2) return rawPosition;

    final snap = _findClosestRouteSnap(rawPosition, route);
    if (snap == null || snap.distanceMeters > _offRouteThresholdMeters) {
      return rawPosition;
    }

    if (isArriving) {
      _arrivalRouteProgress = math.max(_arrivalRouteProgress, snap.progress);
    } else {
      _tripRouteProgress = math.max(_tripRouteProgress, snap.progress);
    }

    if (snap.distanceMeters <= _snapToRouteThresholdMeters) {
      return _routePositionAtProgress(
            route,
            isArriving ? _arrivalRouteProgress : _tripRouteProgress,
          )?.point ??
          snap.point;
    }

    return rawPosition;
  }

  _RouteProgress? _routePositionAtProgress(
    List<AppLatLng> route,
    double progress,
  ) {
    if (route.length < 2) return null;
    final clampedProgress = progress.clamp(0, route.length - 1).toDouble();
    final segmentIndex = math.min(clampedProgress.floor(), route.length - 2);
    final fraction = (clampedProgress - segmentIndex).clamp(0, 1).toDouble();
    return _RouteProgress(
      point: _interpolate(
        route[segmentIndex],
        route[segmentIndex + 1],
        fraction,
      ),
      segmentIndex: segmentIndex,
      progress: segmentIndex + fraction,
    );
  }

  _RouteProgress? _findClosestRouteSnap(
    AppLatLng target,
    List<AppLatLng> route,
  ) {
    if (route.length < 2) return null;

    _RouteProgress? closest;
    for (int i = 0; i < route.length - 1; i++) {
      final snap = _projectPointOnSegment(target, route[i], route[i + 1], i);
      if (closest == null || snap.distanceMeters < closest.distanceMeters) {
        closest = snap;
      }
    }
    return closest;
  }

  _RouteProgress _projectPointOnSegment(
    AppLatLng target,
    AppLatLng start,
    AppLatLng end,
    int segmentIndex,
  ) {
    final metersPerLat = 111320.0;
    final metersPerLng = 111320.0 * math.cos(target.latitude * math.pi / 180);
    final ax = (start.longitude - target.longitude) * metersPerLng;
    final ay = (start.latitude - target.latitude) * metersPerLat;
    final bx = (end.longitude - target.longitude) * metersPerLng;
    final by = (end.latitude - target.latitude) * metersPerLat;
    final abx = bx - ax;
    final aby = by - ay;
    final abLengthSquared = abx * abx + aby * aby;
    final fraction = abLengthSquared == 0
        ? 0.0
        : ((-ax * abx - ay * aby) / abLengthSquared).clamp(0, 1).toDouble();
    final point = _interpolate(start, end, fraction);
    final distanceMeters = _calculateDirectDistance(target, point) * 1000;

    return _RouteProgress(
      point: point,
      segmentIndex: segmentIndex,
      progress: segmentIndex + fraction,
      distanceMeters: distanceMeters,
    );
  }

  AppLatLng _interpolate(AppLatLng start, AppLatLng end, double fraction) {
    return AppLatLng(
      start.latitude + (end.latitude - start.latitude) * fraction,
      start.longitude + (end.longitude - start.longitude) * fraction,
    );
  }

  double _calculateDirectDistance(AppLatLng start, AppLatLng end) {
    const double earthRadiusKm = 6371.0;
    final lat1 = start.latitude * (math.pi / 180);
    final lon1 = start.longitude * (math.pi / 180);
    final lat2 = end.latitude * (math.pi / 180);
    final lon2 = end.longitude * (math.pi / 180);

    final dLat = lat2 - lat1;
    final dLon = lon2 - lon1;

    final a =
        math.sin(dLat / 2) * math.sin(dLat / 2) +
        math.cos(lat1) *
            math.cos(lat2) *
            math.sin(dLon / 2) *
            math.sin(dLon / 2);
    final c = 2 * math.atan2(math.sqrt(a), math.sqrt(1 - a));

    return earthRadiusKm * c;
  }
}
