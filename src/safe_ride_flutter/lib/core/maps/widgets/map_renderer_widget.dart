import 'dart:async';
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

class _VietmapPolylineEntry {
  _VietmapPolylineEntry({required this.handle, required this.signature});

  final vmap.Line handle;
  String signature;
}

class _MapRendererWidgetState extends State<MapRendererWidget> {
  vmap.VietmapController? _vmapController;
  final Map<String, _VietmapPolylineEntry> _vmapPolylines = {};
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
    final double width = type == AppMarkerType.driver ? 56.0 : 56.0;
    final double height = type == AppMarkerType.driver ? 56.0 : 72.0;

    final ui.PictureRecorder recorder = ui.PictureRecorder();
    final Canvas canvas = Canvas(recorder);

    Color color;
    IconData iconData;
    double iconSize;

    switch (type) {
      case AppMarkerType.pickup:
        color = const Color(0xFF1565C0);
        iconData = Icons.person_pin_circle_rounded;
        iconSize = 28.0;
        break;
      case AppMarkerType.destination:
        color = const Color(0xFFC62828);
        iconData = Icons.flag_rounded;
        iconSize = 24.0;
        break;
      case AppMarkerType.driver:
        color = const Color(0xFF006B70);
        iconData = Icons.directions_car_filled_rounded;
        iconSize = 28.0;
        break;
      case AppMarkerType.custom:
        color = Colors.red;
        iconData = Icons.location_on;
        iconSize = 28.0;
        break;
    }

    final shadowPaint = Paint()
      ..color = Colors.black.withValues(alpha: 0.3)
      ..maskFilter = const MaskFilter.blur(BlurStyle.normal, 6);

    final mainPaint = Paint()..color = color;

    if (type != AppMarkerType.driver) {
      final double radius = (width - 12.0) / 2; // leave margin for shadow
      final double xc = width / 2;
      final double yc = radius + 6.0; // margin at top
      final double yp = height - 8.0; // margin at bottom

      final double d = yp - yc;
      if (d > radius) {
        final double cosAlpha = radius / d;
        final double sinAlpha = sqrt(1.0 - cosAlpha * cosAlpha);

        final double xtLeft = xc - radius * sinAlpha;
        final double xtRight = xc + radius * sinAlpha;
        final double yt = yc + radius * cosAlpha;

        final path = Path();
        path.moveTo(xtLeft, yt);
        path.arcToPoint(
          Offset(xtRight, yt),
          radius: Radius.circular(radius),
          largeArc: true,
          clockwise: true,
        );
        path.lineTo(xc, yp);
        path.lineTo(xtLeft, yt);
        path.close();

        canvas.drawPath(path, shadowPaint);
        canvas.drawPath(path, mainPaint);
      } else {
        // Fallback
        final path = Path();
        path.moveTo(6.0, yc);
        path.arcToPoint(
          Offset(width - 6.0, yc),
          radius: Radius.circular(radius),
          clockwise: true,
        );
        path.quadraticBezierTo(
          width - 6.0,
          height * 0.7,
          width / 2,
          height - 8.0,
        );
        path.quadraticBezierTo(6.0, height * 0.7, 6.0, yc);
        path.close();

        canvas.drawPath(path, shadowPaint);
        canvas.drawPath(path, mainPaint);
      }
    } else {
      // Driver circular pin
      canvas.drawCircle(Offset(width / 2, height / 2), 20, shadowPaint);
      canvas.drawCircle(Offset(width / 2, height / 2), 20, mainPaint);
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

    final double centerX = 28.0;
    final double centerY = 28.0;
    final double textX = centerX - (textPainter.width / 2);
    final double textY = centerY - (textPainter.height / 2);
    textPainter.paint(canvas, Offset(textX, textY));

    final ui.Picture picture = recorder.endRecording();
    final ui.Image image = await picture.toImage(width.toInt(), height.toInt());
    final ByteData? byteData = await image.toByteData(
      format: ui.ImageByteFormat.png,
    );

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
  ///
  /// VietMap keeps native polyline handles outside Flutter's widget diff, so
  /// each route is keyed by id and a geometry/style signature. Matching
  /// signatures are left untouched to avoid flicker, changed signatures update
  /// the existing handle, and stale handles are removed.
  Future<void> _syncVietmapPolylines() async {
    final controller = _vmapController;
    if (controller == null) return;
    if (_vmapPolylineSyncInProgress) {
      _vmapPolylineSyncPending = true;
      return;
    }

    _vmapPolylineSyncInProgress = true;
    try {
      final nextPolylines = {
        for (final polyline in widget.polylines) polyline.id: polyline,
      };
      final removedIds = _vmapPolylines.keys
          .where((id) => !nextPolylines.containsKey(id))
          .toList();

      for (final id in removedIds) {
        final entry = _vmapPolylines.remove(id);
        if (entry != null) {
          await controller.removePolyline(entry.handle);
        }
      }

      final sortedPolylines = widget.polylines.toList()
        ..sort((a, b) => a.zIndex.compareTo(b.zIndex));
      for (final polyline in sortedPolylines) {
        if (!mounted || !identical(_vmapController, controller)) {
          return;
        }
        await _upsertVietmapPolyline(controller, polyline);
      }
    } catch (error) {
      debugPrint('VietMap polyline sync failed: $error');
    } finally {
      _vmapPolylineSyncInProgress = false;
      if (_vmapPolylineSyncPending) {
        _vmapPolylineSyncPending = false;
        unawaited(_syncVietmapPolylines());
      }
    }
  }

  Future<void> _upsertVietmapPolyline(
    vmap.VietmapController controller,
    AppPolyline polyline,
  ) async {
    final signature = _vietmapPolylineSignature(polyline);
    final entry = _vmapPolylines[polyline.id];
    if (entry?.signature == signature) {
      return;
    }

    final options = _toVietmapPolylineOptions(polyline);
    if (entry == null) {
      final handle = await controller.addPolyline(options);
      if (!mounted || !identical(_vmapController, controller)) {
        try {
          await controller.removePolyline(handle);
        } catch (_) {}
        return;
      }
      _vmapPolylines[polyline.id] = _VietmapPolylineEntry(
        handle: handle,
        signature: signature,
      );
      return;
    }

    try {
      await controller.updatePolyline(entry.handle, options);
      entry.signature = signature;
    } catch (error) {
      debugPrint('VietMap polyline update failed; recreating handle: $error');
      try {
        await controller.removePolyline(entry.handle);
      } catch (_) {}
      if (!mounted || !identical(_vmapController, controller)) {
        return;
      }
      final handle = await controller.addPolyline(options);
      if (!mounted || !identical(_vmapController, controller)) {
        try {
          await controller.removePolyline(handle);
        } catch (_) {}
        return;
      }
      _vmapPolylines[polyline.id] = _VietmapPolylineEntry(
        handle: handle,
        signature: signature,
      );
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

  String _vietmapPolylineSignature(AppPolyline polyline) {
    final buffer = StringBuffer()
      ..write(polyline.color)
      ..write('|')
      ..write(polyline.width)
      ..write('|')
      ..write(polyline.zIndex)
      ..write('|')
      ..write(polyline.isDashed)
      ..write('|')
      ..write(polyline.endCapRound);

    for (final point in polyline.points) {
      buffer
        ..write('|')
        ..write(point.latitude.toStringAsFixed(6))
        ..write(',')
        ..write(point.longitude.toStringAsFixed(6));
    }

    return buffer.toString();
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
          : const Offset(0.5, 0.889);

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
            ? <gmap.PatternItem>[gmap.PatternItem.dash(12), gmap.PatternItem.gap(8)]
            : const <gmap.PatternItem>[],
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
              _vmapPolylines.clear();
            });
            _syncVietmapPolylines();
            if (widget.onMapCreated != null) {
              widget.onMapCreated!(_VietMapControllerWrapper(controller));
            }
          },
          myLocationEnabled: widget.myLocationButtonEnabled,
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
              final alignment = m.markerType == AppMarkerType.driver
                  ? Alignment.center
                  : Alignment.bottomCenter;
                  
              final double width = m.markerType == AppMarkerType.driver ? 40.0 : 32.0;
              final double height = m.markerType == AppMarkerType.driver ? 40.0 : 44.0;

              return vmap.Marker(
                width: width,
                height: height,
                latLng: vmap.LatLng(m.position.latitude, m.position.longitude),
                alignment: alignment,
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

class _TeardropPinWidget extends StatelessWidget {
  final Color color;
  final IconData icon;

  const _TeardropPinWidget({required this.color, required this.icon});

  @override
  Widget build(BuildContext context) {
    const double width = 32.0;
    const double height = 44.0;

    return CustomPaint(
      size: const Size(width, height),
      painter: _PinPainter(color),
      child: SizedBox(
        width: width,
        height: height,
        child: Align(
          alignment: Alignment.topCenter,
          child: SizedBox(
            width: width,
            height: width, // 32x32 area for the circle
            child: Center(child: Icon(icon, color: Colors.white, size: 18)),
          ),
        ),
      ),
    );
  }
}

/// Blue pin for pickup location
class _PickupMarkerWidget extends StatelessWidget {
  const _PickupMarkerWidget();

  @override
  Widget build(BuildContext context) {
    return const _TeardropPinWidget(
      color: Color(0xFF1565C0),
      icon: Icons.person_pin_circle_rounded,
    );
  }
}

/// Red pin for destination
class _DestinationMarkerWidget extends StatelessWidget {
  const _DestinationMarkerWidget();

  @override
  Widget build(BuildContext context) {
    return const _TeardropPinWidget(
      color: Color(0xFFC62828),
      icon: Icons.flag_rounded,
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

class _PinPainter extends CustomPainter {
  final Color color;
  const _PinPainter(this.color);

  @override
  void paint(Canvas canvas, Size size) {
    final paint = Paint()..color = color;
    final shadowPaint = Paint()
      ..color = Colors.black.withValues(alpha: 0.3)
      ..maskFilter = const MaskFilter.blur(BlurStyle.normal, 4);

    final double width = size.width;
    final double height = size.height;
    final double radius = width / 2;
    final double xc = radius;
    final double yc = radius;
    final double yp = height;

    final double d = yp - yc;
    if (d > radius) {
      final double cosAlpha = radius / d;
      final double sinAlpha = sqrt(1.0 - cosAlpha * cosAlpha);

      final double xtLeft = xc - radius * sinAlpha;
      final double xtRight = xc + radius * sinAlpha;
      final double yt = yc + radius * cosAlpha;

      final path = Path();
      path.moveTo(xtLeft, yt);
      path.arcToPoint(
        Offset(xtRight, yt),
        radius: Radius.circular(radius),
        largeArc: true,
        clockwise: true,
      );
      path.lineTo(xc, yp);
      path.lineTo(xtLeft, yt);
      path.close();

      canvas.drawPath(path, shadowPaint);
      canvas.drawPath(path, paint);
    } else {
      final path = Path();
      path.moveTo(0, radius);
      path.arcToPoint(
        Offset(width, radius),
        radius: Radius.circular(radius),
        clockwise: true,
      );
      path.quadraticBezierTo(width, height * 0.7, width / 2, height);
      path.quadraticBezierTo(0, height * 0.7, 0, radius);
      path.close();

      canvas.drawPath(path, shadowPaint);
      canvas.drawPath(path, paint);
    }
  }

  @override
  bool shouldRepaint(_PinPainter old) => old.color != color;
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
