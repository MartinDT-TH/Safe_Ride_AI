import 'package:google_maps_flutter/google_maps_flutter.dart';

List<LatLng> decodePolyline(String encoded) {
  final points = <LatLng>[];
  var index = 0;
  var latitude = 0;
  var longitude = 0;

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

    points.add(LatLng(latitude / 1e5, longitude / 1e5));
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
