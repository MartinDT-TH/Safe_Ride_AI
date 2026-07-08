import 'dart:async';
import 'dart:io';
import 'dart:typed_data';
import 'dart:ui' as ui;

class DocumentImageCropper {
  static const double documentAspectRatio = 85.6 / 54.0;
  static const double cropWidthFraction = 0.92;
  static const double cropMaxHeightFraction = 0.72;

  Future<File> cropToDocument(File image) async {
    final bytes = await image.readAsBytes();
    final decoded = await _decodeImage(bytes);
    final crop = _centerDocumentRect(decoded.width, decoded.height);

    final recorder = ui.PictureRecorder();
    final canvas = ui.Canvas(recorder);
    final outputSize = ui.Size(crop.width, crop.height);

    canvas.drawImageRect(
      decoded,
      crop,
      ui.Offset.zero & outputSize,
      ui.Paint()..filterQuality = ui.FilterQuality.high,
    );

    final picture = recorder.endRecording();
    final cropped = await picture.toImage(
      crop.width.round(),
      crop.height.round(),
    );
    final pngBytes = await cropped.toByteData(format: ui.ImageByteFormat.png);
    decoded.dispose();
    cropped.dispose();

    if (pngBytes == null) return image;

    final output = File(_croppedPath(image.path));
    await output.writeAsBytes(pngBytes.buffer.asUint8List(), flush: true);
    return output;
  }

  ui.Rect _centerDocumentRect(int imageWidth, int imageHeight) {
    final width = imageWidth.toDouble();
    final height = imageHeight.toDouble();

    var cropWidth = width * cropWidthFraction;
    var cropHeight = cropWidth / documentAspectRatio;

    final maxHeight = height * cropMaxHeightFraction;
    if (cropHeight > maxHeight) {
      cropHeight = maxHeight;
      cropWidth = cropHeight * documentAspectRatio;
    }

    final left = (width - cropWidth) / 2;
    final top = (height - cropHeight) / 2;
    return ui.Rect.fromLTWH(left, top, cropWidth, cropHeight);
  }

  Future<ui.Image> _decodeImage(Uint8List bytes) {
    final completer = Completer<ui.Image>();
    ui.decodeImageFromList(bytes, completer.complete);
    return completer.future;
  }

  String _croppedPath(String sourcePath) {
    final dotIndex = sourcePath.lastIndexOf('.');
    if (dotIndex <= 0) {
      return '${sourcePath}_document.png';
    }
    return '${sourcePath.substring(0, dotIndex)}_document.png';
  }
}
