import 'dart:math';
import 'dart:typed_data';
import 'dart:ui' as ui;
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
  final EdgeInsets padding;

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
    this.padding = EdgeInsets.zero,
  });

  @override
  State<MapRendererWidget> createState() => _MapRendererWidgetState();
}

class _MapRendererWidgetState extends State<MapRendererWidget> {
  vmap.VietmapController? _vmapController;
  final Map<String, vmap.Line> _vmapPolylineHandles = {};
  bool _vmapPolylineSyncInProgress = false;
  bool _vmapPolylineSyncPending = false;
  final Map<AppMarkerType, gmap.BitmapDescriptor> _cachedGoogleMarkerIcons = {};

  @override
  void initState() {
    super.initState();
    if (MapConfig.activeMapProvider == MapRenderProvider.googleMaps) {
      _initGoogleMarkerIcons();
    }
  }

  Future<void> _initGoogleMarkerIcons() async {
    try {
      final pickup = await _createMarkerImage(AppMarkerType.pickup);
      final dest = await _createMarkerImage(AppMarkerType.destination);
      final driver = await _createMarkerImage(AppMarkerType.driver);
      final custom = await _createMarkerImage(AppMarkerType.custom);

      if (mounted) {
        setState(() {
          _cachedGoogleMarkerIcons[AppMarkerType.pickup] = pickup;
          _cachedGoogleMarkerIcons[AppMarkerType.destination] = dest;
          _cachedGoogleMarkerIcons[AppMarkerType.driver] = driver;
          _cachedGoogleMarkerIcons[AppMarkerType.custom] = custom;
        });
      }
    } catch (e) {
      debugPrint('Error generating Google Maps custom markers: $e');
    }
  }

  Future<gmap.BitmapDescriptor> _createMarkerImage(AppMarkerType type) async {
    final double width = 72.0;
    final double height = type == AppMarkerType.driver ? 72.0 : 90.0;

    final ui.PictureRecorder recorder = ui.PictureRecorder();
    final Canvas canvas = Canvas(recorder);

    Color color;
    IconData iconData;
    double iconSize;

    switch (type) {
      case AppMarkerType.pickup:
        color = const Color(0xFF1565C0);
        iconData = Icons.person_pin_circle_rounded;
        iconSize = 36.0;
        break;
      case AppMarkerType.destination:
        color = const Color(0xFFC62828);
        iconData = Icons.flag_rounded;
        iconSize = 32.0;
        break;
      case AppMarkerType.driver:
        color = const Color(0xFF006B70);
        iconData = Icons.directions_car_filled_rounded;
        iconSize = 36.0;
        break;
      case AppMarkerType.custom:
        color = Colors.red;
        iconData = Icons.location_on;
        iconSize = 36.0;
        break;
    }

    final shadowPaint = Paint()
      ..color = Colors.black.withValues(alpha: 0.3)
      ..maskFilter = const MaskFilter.blur(BlurStyle.normal, 6);
    canvas.drawCircle(const Offset(36, 36), 26, shadowPaint);

    final mainPaint = Paint()..color = color;
    canvas.drawCircle(const Offset(36, 36), 26, mainPaint);

    if (type != AppMarkerType.driver) {
      final path = Path()
        ..moveTo(26, 52)
        ..lineTo(46, 52)
        ..lineTo(36, 76)
        ..close();
      canvas.drawPath(path, mainPaint);
    }

    final textPainter = TextPainter(textDirection: TextDirection.ltr);
    textPainter.text = TextSpan(
      text: String.fromCharCode(iconData.codePoint),
      style: TextStyle(
        fontSize: iconSize,
        fontFamily: 'MaterialIcons',
        color: Colors.white,
      ),
    );
    textPainter.layout();

    final double textX = 36 - (textPainter.width / 2);
    final double textY = 36 - (textPainter.height / 2);
    textPainter.paint(canvas, Offset(textX, textY));

    final ui.Picture picture = recorder.endRecording();
    final ui.Image image = await picture.toImage(width.toInt(), height.toInt());
    final ByteData? byteData = await image.toByteData(format: ui.ImageByteFormat.png);

    if (byteData == null) {
      return gmap.BitmapDescriptor.defaultMarker;
    }

    return gmap.BitmapDescriptor.bytes(byteData.buffer.asUint8List());
  }

  @override
  void didUpdateWidget(MapRendererWidget oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (MapConfig.activeMapProvider == MapRenderProvider.vietMap &&
        _vmapController != null) {
      _syncVietmapPolylines();
    }
  }

  /// Only syncs polylines; markers are handled by the MarkerLayer widget overlay.
  void _syncVietmapPolylines() async {
    if (_vmapController == null) return;
    if (_vmapPolylineSyncInProgress) {
      _vmapPolylineSyncPending = true;
      return;
    }

    _vmapPolylineSyncInProgress = true;
    try {
      final nextPolylines = {
        for (final polyline in widget.polylines) polyline.id: polyline,
      };
      final removedIds = _vmapPolylineHandles.keys
          .where((id) => !nextPolylines.containsKey(id))
          .toList();

      for (final id in removedIds) {
        final handle = _vmapPolylineHandles.remove(id);
        if (handle != null) {
          await _vmapController!.removePolyline(handle);
        }
      }

      final sortedPolylines = widget.polylines.toList()
        ..sort((a, b) => a.zIndex.compareTo(b.zIndex));
      for (final polyline in sortedPolylines) {
        final options = _toVietmapPolylineOptions(polyline);
        final handle = _vmapPolylineHandles[polyline.id];
        if (handle == null) {
          _vmapPolylineHandles[polyline.id] = await _vmapController!
              .addPolyline(options);
        } else {
          await _vmapController!.updatePolyline(handle, options);
        }
      }
    } catch (_) {}
    _vmapPolylineSyncInProgress = false;
    if (_vmapPolylineSyncPending) {
      _vmapPolylineSyncPending = false;
      _syncVietmapPolylines();
    }
  }

  vmap.PolylineOptions _toVietmapPolylineOptions(AppPolyline polyline) {
    return vmap.PolylineOptions(
      geometry: polyline.points
          .map((p) => vmap.LatLng(p.latitude, p.longitude))
          .toList(),
      polylineColor: polyline.color,
      polylineWidth: polyline.width.toDouble(),
    );
  }

  @override
  Widget build(BuildContext context) {
    if (MapConfig.activeMapProvider == MapRenderProvider.googleMaps) {
      if (!ApiKeysConfig.hasGoogleMapsKey) {
        return const _MissingConfigWidget(
          message: 'Thiếu cấu hình Google Maps.',
        );
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
      gmap.BitmapDescriptor icon = gmap.BitmapDescriptor.defaultMarker;

      final cachedIcon = _cachedGoogleMarkerIcons[m.markerType];
      if (cachedIcon != null) {
        icon = cachedIcon;
      } else {
        switch (m.markerType) {
          case AppMarkerType.pickup:
            icon = gmap.BitmapDescriptor.defaultMarkerWithHue(
              gmap.BitmapDescriptor.hueAzure,
            );
            break;
          case AppMarkerType.destination:
            icon = gmap.BitmapDescriptor.defaultMarkerWithHue(
              gmap.BitmapDescriptor.hueRed,
            );
            break;
          case AppMarkerType.driver:
            icon = gmap.BitmapDescriptor.defaultMarkerWithHue(
              gmap.BitmapDescriptor.hueOrange,
            );
            break;
          case AppMarkerType.custom:
            icon = m.hue != null
                ? gmap.BitmapDescriptor.defaultMarkerWithHue(m.hue!)
                : gmap.BitmapDescriptor.defaultMarker;
            break;
        }
      }

      final anchor = m.markerType == AppMarkerType.driver
          ? const Offset(0.5, 0.5)
          : const Offset(0.5, 0.844);

      return gmap.Marker(
        markerId: gmap.MarkerId(m.id),
        position: gmap.LatLng(m.position.latitude, m.position.longitude),
        icon: icon,
        rotation: m.rotation,
        anchor: anchor,
        infoWindow: gmap.InfoWindow.noText,
      );
    }).toSet();

    final gPolylines = widget.polylines.map((p) {
      return gmap.Polyline(
        polylineId: gmap.PolylineId(p.id),
        points: p.points
            .map((pt) => gmap.LatLng(pt.latitude, pt.longitude))
            .toList(),
        color: p.color,
        width: p.width,
        zIndex: p.zIndex,
        patterns: p.isDashed
            ? [gmap.PatternItem.dash(12), gmap.PatternItem.gap(8)]
            : [],
        startCap: p.endCapRound ? gmap.Cap.roundCap : gmap.Cap.buttCap,
        endCap: p.endCapRound ? gmap.Cap.roundCap : gmap.Cap.buttCap,
        jointType: gmap.JointType.round,
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
      padding: widget.padding,
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
    return Stack(
      children: [
        vmap.VietmapGL(
          styleString:
              'https://maps.vietmap.vn/api/maps/light/styles.json?apikey=${ApiKeysConfig.vietMap}',
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
            setState(() {
              _vmapController = controller;
              _vmapPolylineHandles.clear();
            });
            _syncVietmapPolylines();
            if (widget.onMapCreated != null) {
              widget.onMapCreated!(_VietMapControllerWrapper(controller));
            }
          },
          myLocationEnabled: true,
          myLocationRenderMode: widget.myLocationButtonEnabled
              ? vmap.MyLocationRenderMode.compass
              : vmap.MyLocationRenderMode.normal,
          onCameraIdle: widget.onCameraIdle,
          trackCameraPosition: true,
        ),
        // Marker Widget Overlay — renders Flutter widgets on top of VietMap
        if (_vmapController != null)
          vmap.MarkerLayer(
            ignorePointer: true,
            mapController: _vmapController!,
            markers: widget.markers.map((m) {
              return vmap.Marker(
                latLng: vmap.LatLng(m.position.latitude, m.position.longitude),
                child: _buildMarkerWidget(m),
              );
            }).toList(),
          ),
      ],
    );
  }

  Widget _buildMarkerWidget(AppMarker marker) {
    switch (marker.markerType) {
      case AppMarkerType.pickup:
        return const _PickupMarkerWidget();
      case AppMarkerType.destination:
        return const _DestinationMarkerWidget();
      case AppMarkerType.driver:
        return _DriverMarkerWidget(heading: marker.rotation);
      case AppMarkerType.custom:
        return marker.iconWidget ?? _DefaultPinWidget(hue: marker.hue);
    }
  }
}

// ---------------------------------------------------------------------------
// Custom Marker Widgets
// ---------------------------------------------------------------------------

/// Blue pin for pickup location
class _PickupMarkerWidget extends StatelessWidget {
  const _PickupMarkerWidget();

  @override
  Widget build(BuildContext context) {
    return Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        Container(
          width: 36,
          height: 36,
          decoration: BoxDecoration(
            color: const Color(0xFF1565C0),
            shape: BoxShape.circle,
            boxShadow: [
              BoxShadow(
                color: Colors.black.withValues(alpha: 0.3),
                blurRadius: 6,
                offset: const Offset(0, 2),
              ),
            ],
          ),
          child: const Icon(
            Icons.person_pin_circle_rounded,
            color: Colors.white,
            size: 22,
          ),
        ),
        CustomPaint(
          size: const Size(12, 7),
          painter: _DownArrowPainter(const Color(0xFF1565C0)),
        ),
      ],
    );
  }
}

/// Red pin for destination
class _DestinationMarkerWidget extends StatelessWidget {
  const _DestinationMarkerWidget();

  @override
  Widget build(BuildContext context) {
    return Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        Container(
          width: 36,
          height: 36,
          decoration: BoxDecoration(
            color: const Color(0xFFC62828),
            shape: BoxShape.circle,
            boxShadow: [
              BoxShadow(
                color: Colors.black.withValues(alpha: 0.3),
                blurRadius: 6,
                offset: const Offset(0, 2),
              ),
            ],
          ),
          child: const Icon(Icons.flag_rounded, color: Colors.white, size: 20),
        ),
        CustomPaint(
          size: const Size(12, 7),
          painter: _DownArrowPainter(const Color(0xFFC62828)),
        ),
      ],
    );
  }
}

/// Animated car icon for driver, rotates based on heading
class _DriverMarkerWidget extends StatelessWidget {
  final double heading;
  const _DriverMarkerWidget({this.heading = 0});

  @override
  Widget build(BuildContext context) {
    return Transform.rotate(
      angle: heading * (3.14159265358979 / 180),
      child: Container(
        width: 40,
        height: 40,
        decoration: BoxDecoration(
          color: const Color(0xFF006B70),
          shape: BoxShape.circle,
          boxShadow: [
            BoxShadow(
              color: Colors.black.withValues(alpha: 0.35),
              blurRadius: 8,
              offset: const Offset(0, 3),
            ),
          ],
        ),
        child: const Icon(
          Icons.directions_car_filled_rounded,
          color: Colors.white,
          size: 22,
        ),
      ),
    );
  }
}

/// Fallback pin using hue
class _DefaultPinWidget extends StatelessWidget {
  final double? hue;
  const _DefaultPinWidget({this.hue});

  @override
  Widget build(BuildContext context) {
    final color = hue != null
        ? HSVColor.fromAHSV(1, hue!, 0.8, 0.85).toColor()
        : Colors.red;
    return Icon(Icons.location_on, color: color, size: 36);
  }
}

/// Triangle "tip" at the bottom of circular pins
class _DownArrowPainter extends CustomPainter {
  final Color color;
  const _DownArrowPainter(this.color);

  @override
  void paint(Canvas canvas, Size size) {
    final paint = Paint()..color = color;
    final path = Path()
      ..moveTo(0, 0)
      ..lineTo(size.width, 0)
      ..lineTo(size.width / 2, size.height)
      ..close();
    canvas.drawPath(path, paint);
  }

  @override
  bool shouldRepaint(_DownArrowPainter old) => old.color != color;
}

// ---------------------------------------------------------------------------
// Controller Wrappers
// ---------------------------------------------------------------------------

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
  Future<void> animateCameraToBounds(
    AppLatLng southwest,
    AppLatLng northeast,
    double padding, {
    double top = 0,
    double bottom = 0,
    double left = 0,
    double right = 0,
  }) async {
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
  Future<void> animateCameraToBounds(
    AppLatLng southwest,
    AppLatLng northeast,
    double padding, {
    double top = 0,
    double bottom = 0,
    double left = 0,
    double right = 0,
  }) async {
    await _controller.animateCamera(
      vmap.CameraUpdate.newLatLngBounds(
        vmap.LatLngBounds(
          southwest: vmap.LatLng(southwest.latitude, southwest.longitude),
          northeast: vmap.LatLng(northeast.latitude, northeast.longitude),
        ),
        left: left != 0 ? left : padding,
        top: top != 0 ? top : padding,
        right: right != 0 ? right : padding,
        bottom: bottom != 0 ? bottom : padding,
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
      child: Center(child: Text(message, textAlign: TextAlign.center)),
    );
  }
}
