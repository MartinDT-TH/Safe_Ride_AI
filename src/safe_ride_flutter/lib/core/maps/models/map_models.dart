import 'package:flutter/material.dart';

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
  final Widget? iconWidget;
  final double? hue;

  const AppMarker({
    required this.id,
    required this.position,
    this.iconWidget,
    this.hue,
  });
}

class AppPolyline {
  final String id;
  final List<AppLatLng> points;
  final Color color;
  final int width;

  const AppPolyline({
    required this.id,
    required this.points,
    this.color = Colors.blue,
    this.width = 5,
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
  Future<void> animateCameraToBounds(AppLatLng southwest, AppLatLng northeast, double padding);
  Future<AppScreenCoordinate> getScreenCoordinate(AppLatLng position);
  void dispose();
}
