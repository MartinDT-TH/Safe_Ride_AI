import 'package:flutter/material.dart';

enum AppMarkerType { pickup, destination, driver, custom }

class AppLatLng {
  final double latitude;
  final double longitude;

  const AppLatLng(this.latitude, this.longitude);
}

class AppCameraPosition {
  final AppLatLng target;
  final double zoom;

  const AppCameraPosition({required this.target, this.zoom = 14.0});
}

class AppMarker {
  final String id;
  final AppLatLng position;

  /// The semantic type of the marker — determines which icon widget is used.
  final AppMarkerType markerType;

  /// For the driver marker, the bearing/heading in degrees (0 = North).
  final double rotation;

  /// Custom widget override (if null, renderer uses [markerType] to pick icon).
  final Widget? iconWidget;

  /// Legacy hue fallback (Google Maps only, used when markerType == custom).
  final double? hue;

  const AppMarker({
    required this.id,
    required this.position,
    this.markerType = AppMarkerType.custom,
    this.rotation = 0.0,
    this.iconWidget,
    this.hue,
  });
}

class AppPolyline {
  final String id;
  final List<AppLatLng> points;
  final Color color;
  final int width;

  /// Render order — higher zIndex is drawn on top.
  final int zIndex;

  /// Whether to draw as a dashed line.
  final bool isDashed;

  /// Whether to use round caps at both ends of the line.
  final bool endCapRound;

  const AppPolyline({
    required this.id,
    required this.points,
    this.color = Colors.blue,
    this.width = 5,
    this.zIndex = 1,
    this.isDashed = false,
    this.endCapRound = false,
  });
}

class AppScreenCoordinate {
  final int x;
  final int y;
  const AppScreenCoordinate({required this.x, required this.y});
}

abstract class AppMapController {
  Future<void> animateCamera(AppCameraPosition position);
  Future<void> moveCamera(AppCameraPosition position);
  Future<void> animateCameraToBounds(
    AppLatLng southwest,
    AppLatLng northeast,
    double padding, {
    double top = 0,
    double bottom = 0,
    double left = 0,
    double right = 0,
  });
  Future<AppScreenCoordinate> getScreenCoordinate(AppLatLng position);
  void dispose();
}

