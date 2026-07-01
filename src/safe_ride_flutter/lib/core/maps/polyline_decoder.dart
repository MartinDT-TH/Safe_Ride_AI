import 'models/map_models.dart';

List<AppLatLng> decodePolyline(String encoded) {
  final points = <AppLatLng>[];
  var index = 0;
  var latitude = 0;
  var longitude = 0;

  final rawPoints = <({int lat, int lng})>[];

  while (index < encoded.length) {
    final latitudeChunk = _decodeChunk(encoded, index);
    index = latitudeChunk.nextIndex;
    latitude += latitudeChunk.delta;

    if (index >= encoded.length) {
      throw const FormatException('Encoded polyline is incomplete.');
    }

    final longitudeChunk = _decodeChunk(encoded, index);
    index = longitudeChunk.nextIndex;
    longitude += longitudeChunk.delta;

    rawPoints.add((lat: latitude, lng: longitude));
  }

  if (rawPoints.isEmpty) return points;

  // Auto-detect precision (1e5 or 1e6) and coordinate order
  // Vietnam coordinates: lat ~ 8-24, lng ~ 102-110
  double bestDistance = double.infinity;
  double bestPrecision = 1e5;
  bool bestSwap = false;
  
  final firstLatRaw = rawPoints.first.lat.toDouble();
  final firstLngRaw = rawPoints.first.lng.toDouble();

  final options = [
    (1e5, false),
    (1e5, true),
    (1e6, false),
    (1e6, true)
  ];

  for (final opt in options) {
    final prec = opt.$1;
    final swap = opt.$2;
    final lat = (swap ? firstLngRaw : firstLatRaw) / prec;
    final lng = (swap ? firstLatRaw : firstLngRaw) / prec;

    // Check if valid coordinate (latitude must be [-90, 90])
    if (lat.abs() <= 90 && lng.abs() <= 180) {
      // Check distance to center of Vietnam (lat 16, lng 106)
      final dLat = lat - 16;
      final dLng = lng - 106;
      final distSq = dLat * dLat + dLng * dLng;
      if (distSq < bestDistance) {
        bestDistance = distSq;
        bestPrecision = prec;
        bestSwap = swap;
      }
    }
  }

  for (final p in rawPoints) {
    final lat = (bestSwap ? p.lng : p.lat) / bestPrecision;
    final lng = (bestSwap ? p.lat : p.lng) / bestPrecision;
    points.add(AppLatLng(lat, lng));
  }

  return points;
}

({int delta, int nextIndex}) _decodeChunk(String encoded, int startIndex) {
  var index = startIndex;
  var shift = 0;
  var result = 0;
  var byte = 0;

  do {
    if (index >= encoded.length) {
      throw const FormatException('Encoded polyline is incomplete.');
    }

    byte = encoded.codeUnitAt(index++) - 63;
    result |= (byte & 0x1f) << shift;
    shift += 5;
  } while (byte >= 0x20);

  final delta = (result & 1) != 0 ? ~(result >> 1) : result >> 1;
  return (delta: delta, nextIndex: index);
}

