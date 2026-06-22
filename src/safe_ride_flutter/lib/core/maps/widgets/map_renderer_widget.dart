import 'dart:math';
import 'package:flutter/material.dart';
import 'package:google_maps_flutter/google_maps_flutter.dart' as gmap;
import 'package:vietmap_flutter_gl/vietmap_flutter_gl.dart' as vmap;

import '../map_config.dart';
import '../models/map_models.dart';
import '../../config/api_keys_config.dart';

class MapRendererWidget extends StatefulWidget {
  final AppCameraPosition initialCameraPosition;
  final Set<AppMarker> markers;
  final Set<AppPolyline> polylines;
  final Function(AppMapController)? onMapCreated;
  final Function(AppLatLng)? onTap;
  final VoidCallback? onCameraMove;
  final VoidCallback? onCameraIdle;
  final bool myLocationButtonEnabled;

  const MapRendererWidget({
    super.key,
    required this.initialCameraPosition,
    this.markers = const {},
    this.polylines = const {},
    this.onMapCreated,
    this.onTap,
    this.onCameraMove,
    this.onCameraIdle,
    this.myLocationButtonEnabled = false,
  });

  @override
  State<MapRendererWidget> createState() => _MapRendererWidgetState();
}

class _MapRendererWidgetState extends State<MapRendererWidget> {
  vmap.VietmapController? _vmapController;

  @override
  void didUpdateWidget(MapRendererWidget oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (MapConfig.activeMapProvider == MapRenderProvider.vietMap && _vmapController != null) {
      _syncVietmap();
    }
  }

  void _syncVietmap() async {
    if (_vmapController == null) return;
    try {
      await _vmapController!.clearSymbols();
      await _vmapController!.clearCircles();
      await _vmapController!.clearPolylines();

      for (var marker in widget.markers) {
        final isRed = marker.hue == null || marker.hue! < 100;
        final color = isRed ? const Color(0xFFD71920) : const Color(0xFF007BFF);
        await _vmapController!.addCircle(
          vmap.CircleOptions(
            geometry: vmap.LatLng(marker.position.latitude, marker.position.longitude),
            circleColor: color,
            circleRadius: 8,
            circleStrokeWidth: 2,
            circleStrokeColor: const Color(0xFFFFFFFF),
          ),
        );
      }

      for (var polyline in widget.polylines) {
        await _vmapController!.addPolyline(
          vmap.PolylineOptions(
            geometry: polyline.points.map((p) => vmap.LatLng(p.latitude, p.longitude)).toList(),
            polylineColor: polyline.color,
            polylineWidth: polyline.width.toDouble(),
          ),
        );
      }
    } catch (_) {}
  }

  @override
  Widget build(BuildContext context) {
    if (MapConfig.activeMapProvider == MapRenderProvider.googleMaps) {
      if (!ApiKeysConfig.hasGoogleMapsKey) {
        return const _MissingConfigWidget(message: 'Thiếu cấu hình Google Maps.');
      }
      return _buildGoogleMap();
    } else {
      if (!ApiKeysConfig.hasVietMapKey) {
        return const _MissingConfigWidget(message: 'Thiếu cấu hình VietMap.');
      }
      return _buildVietMap();
    }
  }

  Widget _buildGoogleMap() {
    final gMarkers = widget.markers.map((m) {
      return gmap.Marker(
        markerId: gmap.MarkerId(m.id),
        position: gmap.LatLng(m.position.latitude, m.position.longitude),
        icon: m.hue != null 
          ? gmap.BitmapDescriptor.defaultMarkerWithHue(m.hue!)
          : gmap.BitmapDescriptor.defaultMarker,
      );
    }).toSet();

    final gPolylines = widget.polylines.map((p) {
      return gmap.Polyline(
        polylineId: gmap.PolylineId(p.id),
        points: p.points.map((pt) => gmap.LatLng(pt.latitude, pt.longitude)).toList(),
        color: p.color,
        width: p.width,
      );
    }).toSet();

    return gmap.GoogleMap(
      initialCameraPosition: gmap.CameraPosition(
        target: gmap.LatLng(
          widget.initialCameraPosition.target.latitude,
          widget.initialCameraPosition.target.longitude,
        ),
        zoom: widget.initialCameraPosition.zoom,
      ),
      markers: gMarkers,
      polylines: gPolylines,
      onTap: widget.onTap != null
          ? (pos) => widget.onTap!(AppLatLng(pos.latitude, pos.longitude))
          : null,
      myLocationEnabled: true,
      myLocationButtonEnabled: widget.myLocationButtonEnabled,
      zoomControlsEnabled: false,
      mapToolbarEnabled: false,
      onCameraMove: (_) => widget.onCameraMove?.call(),
      onCameraIdle: widget.onCameraIdle,
      onMapCreated: (controller) {
        if (widget.onMapCreated != null) {
          widget.onMapCreated!(_GoogleMapControllerWrapper(controller));
        }
      },
    );
  }

  Widget _buildVietMap() {
    return vmap.VietmapGL(
      styleString: 'https://maps.vietmap.vn/api/maps/light/styles.json?apikey=${ApiKeysConfig.vietMap}',
      initialCameraPosition: vmap.CameraPosition(
        target: vmap.LatLng(
          widget.initialCameraPosition.target.latitude,
          widget.initialCameraPosition.target.longitude,
        ),
        zoom: widget.initialCameraPosition.zoom,
      ),
      onMapClick: (point, latlng) {
        if (widget.onTap != null) {
          widget.onTap!(AppLatLng(latlng.latitude, latlng.longitude));
        }
      },
      onMapCreated: (controller) {
        _vmapController = controller;
        _syncVietmap();
        if (widget.onMapCreated != null) {
          widget.onMapCreated!(_VietMapControllerWrapper(controller));
        }
      },
      myLocationEnabled: true,
      myLocationRenderMode: widget.myLocationButtonEnabled 
          ? vmap.MyLocationRenderMode.compass 
          : vmap.MyLocationRenderMode.normal,
      onCameraIdle: widget.onCameraIdle,
    );
  }
}

class _GoogleMapControllerWrapper implements AppMapController {
  final gmap.GoogleMapController _controller;

  _GoogleMapControllerWrapper(this._controller);

  @override
  Future<void> animateCamera(AppCameraPosition position) async {
    await _controller.animateCamera(
      gmap.CameraUpdate.newLatLngZoom(
        gmap.LatLng(position.target.latitude, position.target.longitude),
        position.zoom,
      ),
    );
  }

  @override
  Future<void> moveCamera(AppCameraPosition position) async {
    await _controller.moveCamera(
      gmap.CameraUpdate.newLatLngZoom(
        gmap.LatLng(position.target.latitude, position.target.longitude),
        position.zoom,
      ),
    );
  }

  @override
  Future<void> animateCameraToBounds(AppLatLng southwest, AppLatLng northeast, double padding) async {
    await _controller.animateCamera(
      gmap.CameraUpdate.newLatLngBounds(
        gmap.LatLngBounds(
          southwest: gmap.LatLng(southwest.latitude, southwest.longitude),
          northeast: gmap.LatLng(northeast.latitude, northeast.longitude),
        ),
        padding,
      ),
    );
  }

  @override
  Future<AppScreenCoordinate> getScreenCoordinate(AppLatLng position) async {
    final gmap.ScreenCoordinate coord = await _controller.getScreenCoordinate(
      gmap.LatLng(position.latitude, position.longitude),
    );
    return AppScreenCoordinate(x: coord.x, y: coord.y);
  }

  @override
  void dispose() {
    _controller.dispose();
  }
}

class _VietMapControllerWrapper implements AppMapController {
  final vmap.VietmapController _controller;

  _VietMapControllerWrapper(this._controller);

  @override
  Future<void> animateCamera(AppCameraPosition position) async {
    await _controller.animateCamera(
      vmap.CameraUpdate.newLatLngZoom(
        vmap.LatLng(position.target.latitude, position.target.longitude),
        position.zoom,
      ),
    );
  }

  @override
  Future<void> moveCamera(AppCameraPosition position) async {
    await _controller.moveCamera(
      vmap.CameraUpdate.newLatLngZoom(
        vmap.LatLng(position.target.latitude, position.target.longitude),
        position.zoom,
      ),
    );
  }

  @override
  Future<void> animateCameraToBounds(AppLatLng southwest, AppLatLng northeast, double padding) async {
    await _controller.animateCamera(
      vmap.CameraUpdate.newLatLngBounds(
        vmap.LatLngBounds(
          southwest: vmap.LatLng(southwest.latitude, southwest.longitude),
          northeast: vmap.LatLng(northeast.latitude, northeast.longitude),
        ),
        left: padding,
        top: padding,
        right: padding,
        bottom: padding,
      ),
    );
  }

  @override
  Future<AppScreenCoordinate> getScreenCoordinate(AppLatLng position) async {
    final Point<num> point = await _controller.toScreenLocation(
      vmap.LatLng(position.latitude, position.longitude),
    );
    return AppScreenCoordinate(x: point.x.toInt(), y: point.y.toInt());
  }

  @override
  void dispose() {
    _controller.dispose();
  }
}

class _MissingConfigWidget extends StatelessWidget {
  final String message;

  const _MissingConfigWidget({required this.message});

  @override
  Widget build(BuildContext context) {
    return ColoredBox(
      color: const Color(0xFFE9EEEE),
      child: Center(
        child: Text(
          message,
          textAlign: TextAlign.center,
        ),
      ),
    );
  }
}
