import 'dart:io';

import 'package:camera/camera.dart';
import 'package:flutter/material.dart';
import 'package:google_mlkit_barcode_scanning/google_mlkit_barcode_scanning.dart';

import '../../../../../core/constants/app_colors.dart';
import '../../../../../core/localization/localization_extensions.dart';
import '../../application/services/document_image_cropper.dart';

class DocumentCaptureResult {
  const DocumentCaptureResult({
    required this.croppedImage,
    required this.originalImage,
    this.qrPayload,
  });

  final File croppedImage;
  final File originalImage;
  final String? qrPayload;
}

class DocumentCameraPage extends StatefulWidget {
  DocumentCameraPage({
    super.key,
    required this.title,
    required this.instruction,
    this.focusPoint,
    this.scanQrLive = false,
  });

  final String title;
  final String instruction;
  final Offset? focusPoint;
  final bool scanQrLive;

  @override
  State<DocumentCameraPage> createState() => _DocumentCameraPageState();
}

class _DocumentCameraPageState extends State<DocumentCameraPage> {
  final DocumentImageCropper _cropper = DocumentImageCropper();
  final BarcodeScanner _barcodeScanner = BarcodeScanner(
    formats: [BarcodeFormat.qrCode],
  );
  CameraController? _controller;
  Future<void>? _initializeCameraFuture;
  bool _isCapturing = false;
  bool _isProcessingQrFrame = false;
  String? _qrPayload;
  String? _errorMessage;

  @override
  void initState() {
    super.initState();
    _initializeCameraFuture = _initializeCamera();
  }

  @override
  void dispose() {
    _barcodeScanner.close();
    _controller?.dispose();
    super.dispose();
  }

  Future<void> _initializeCamera() async {
    try {
      final cameras = await availableCameras();
      final camera = cameras.firstWhere(
        (item) => item.lensDirection == CameraLensDirection.back,
        orElse: () => cameras.first,
      );
      final controller = CameraController(
        camera,
        ResolutionPreset.high,
        enableAudio: false,
        imageFormatGroup: widget.scanQrLive
            ? Platform.isAndroid
                  ? ImageFormatGroup.nv21
                  : ImageFormatGroup.bgra8888
            : ImageFormatGroup.jpeg,
      );
      await controller.initialize();
      await _focusCamera(controller);
      if (!mounted) {
        await controller.dispose();
        return;
      }
      setState(() => _controller = controller);
      if (widget.scanQrLive) await controller.startImageStream(_processQrFrame);
    } catch (_) {
      if (!mounted) return;
      setState(
        () => _errorMessage = context.l10n.cameraOpenFailed,
      );
    }
  }

  Future<void> _capture() async {
    final controller = _controller;
    if (controller == null || _isCapturing) return;

    setState(() => _isCapturing = true);
    try {
      if (controller.value.isStreamingImages) {
        await controller.stopImageStream();
        while (_isProcessingQrFrame) {
          await Future<void>.delayed(const Duration(milliseconds: 20));
        }
      }
      await _focusCamera(controller);
      if (widget.focusPoint != null) {
        await Future<void>.delayed(const Duration(milliseconds: 450));
      }
      final image = await controller.takePicture();
      final original = File(image.path);
      final cropped = await _cropper.cropToDocument(original);
      if (!mounted) return;
      Navigator.of(context).pop(
        DocumentCaptureResult(
          croppedImage: cropped,
          originalImage: original,
          qrPayload: _qrPayload,
        ),
      );
    } catch (_) {
      if (!mounted) return;
      setState(() => _isCapturing = false);
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(context.l10n.photoCaptureFailed)),
      );
    }
  }

  Future<void> _processQrFrame(CameraImage frame) async {
    final controller = _controller;
    if (controller == null ||
        _isCapturing ||
        _isProcessingQrFrame ||
        _qrPayload != null ||
        frame.planes.length != 1) {
      return;
    }

    final rotation = InputImageRotationValue.fromRawValue(
      controller.description.sensorOrientation,
    );
    final format = InputImageFormatValue.fromRawValue(frame.format.raw);
    if (rotation == null || format == null) return;

    _isProcessingQrFrame = true;
    try {
      final input = InputImage.fromBytes(
        bytes: frame.planes.first.bytes,
        metadata: InputImageMetadata(
          size: Size(frame.width.toDouble(), frame.height.toDouble()),
          rotation: rotation,
          format: format,
          bytesPerRow: frame.planes.first.bytesPerRow,
        ),
      );
      final barcodes = await _barcodeScanner.processImage(input);
      for (final barcode in barcodes) {
        final payload = barcode.rawValue?.trim();
        if (payload == null || payload.isEmpty) continue;
        _qrPayload = payload;
        if (!mounted) return;
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Đã nhận diện QR GPLX.')),
        );
        break;
      }
    } finally {
      _isProcessingQrFrame = false;
    }
  }

  Future<void> _focusCamera(CameraController controller) async {
    final focusPoint = widget.focusPoint;
    if (focusPoint == null) return;
    try {
      await controller.setFocusMode(FocusMode.auto);
      await controller.setExposureMode(ExposureMode.auto);
      await controller.setFocusPoint(focusPoint);
      await controller.setExposurePoint(focusPoint);
    } catch (_) {
      // Some camera devices do not support metering points. Their default
      // continuous autofocus remains available as a fallback.
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: Colors.black,
      body: FutureBuilder<void>(
        future: _initializeCameraFuture,
        builder: (context, snapshot) {
          final controller = _controller;
          if (_errorMessage != null) {
            return _CameraError(
              message: _errorMessage!,
              onBack: () => Navigator.of(context).pop(),
            );
          }
          if (controller == null || !controller.value.isInitialized) {
            return Center(
              child: CircularProgressIndicator(color: Colors.white),
            );
          }

          return Stack(
            fit: StackFit.expand,
            children: [
              _CameraPreviewCover(controller: controller),
              _DocumentFrameOverlay(
                title: widget.title,
                instruction: widget.instruction,
              ),
              Positioned(
                left: 0,
                right: 0,
                bottom: 0,
                child: SafeArea(
                  top: false,
                  child: Padding(
                    padding: const EdgeInsets.fromLTRB(24, 0, 24, 28),
                    child: Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        IconButton.filled(
                          onPressed: _isCapturing
                              ? null
                              : () => Navigator.of(context).pop(),
                          icon: Icon(Icons.close),
                          style: IconButton.styleFrom(
                            backgroundColor: Colors.white,
                            foregroundColor: Colors.black87,
                          ),
                        ),
                        GestureDetector(
                          onTap: _isCapturing ? null : _capture,
                          child: Container(
                            width: 76,
                            height: 76,
                            padding: const EdgeInsets.all(6),
                            decoration: BoxDecoration(
                              shape: BoxShape.circle,
                              border: Border.all(color: Colors.white, width: 4),
                            ),
                            child: DecoratedBox(
                              decoration: BoxDecoration(
                                color: Colors.white,
                                shape: BoxShape.circle,
                              ),
                              child: _isCapturing
                                  ? Padding(
                                      padding: EdgeInsets.all(18),
                                      child: CircularProgressIndicator(
                                        strokeWidth: 3,
                                        color: AppColors.primary,
                                      ),
                                    )
                                  : null,
                            ),
                          ),
                        ),
                        SizedBox(width: 48),
                      ],
                    ),
                  ),
                ),
              ),
            ],
          );
        },
      ),
    );
  }
}

class _CameraPreviewCover extends StatelessWidget {
  _CameraPreviewCover({required this.controller});

  final CameraController controller;

  @override
  Widget build(BuildContext context) {
    final size = MediaQuery.of(context).size;
    final previewSize = controller.value.previewSize;
    if (previewSize == null) return const SizedBox.shrink();

    final previewAspectRatio = previewSize.height / previewSize.width;
    final screenAspectRatio = size.width / size.height;
    final scale = previewAspectRatio / screenAspectRatio;

    return Transform.scale(
      scale: scale < 1 ? 1 / scale : scale,
      child: Center(child: CameraPreview(controller)),
    );
  }
}

class _DocumentFrameOverlay extends StatelessWidget {
  _DocumentFrameOverlay({required this.title, required this.instruction});

  final String title;
  final String instruction;

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, constraints) {
        final frame = _frameRect(constraints.biggest);
        return Stack(
          children: [
            CustomPaint(
              size: constraints.biggest,
              painter: _DocumentFramePainter(frame),
            ),
            Positioned(
              left: frame.left,
              right: constraints.maxWidth - frame.right,
              top: frame.top - 96,
              child: SafeArea(
                bottom: false,
                child: Column(
                  children: [
                    Text(
                      title,
                      textAlign: TextAlign.center,
                      style: TextStyle(
                        color: Colors.white,
                        fontSize: 22,
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                    SizedBox(height: 8),
                    Text(
                      instruction,
                      textAlign: TextAlign.center,
                      style: TextStyle(
                        color: Colors.white70,
                        fontSize: 14,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                  ],
                ),
              ),
            ),
            Positioned(
              left: frame.left,
              right: constraints.maxWidth - frame.right,
              top: frame.bottom + 18,
              child: Text(
                context.l10n.alignDocumentCorners,
                textAlign: TextAlign.center,
                style: TextStyle(
                  color: Colors.white,
                  fontSize: 15,
                  fontWeight: FontWeight.w800,
                ),
              ),
            ),
          ],
        );
      },
    );
  }

  Rect _frameRect(Size size) {
    var width = size.width * DocumentImageCropper.cropWidthFraction;
    var height = width / DocumentImageCropper.documentAspectRatio;
    final maxHeight = size.height * DocumentImageCropper.cropMaxHeightFraction;
    if (height > maxHeight) {
      height = maxHeight;
      width = height * DocumentImageCropper.documentAspectRatio;
    }
    return Rect.fromCenter(
      center: Offset(size.width / 2, size.height / 2),
      width: width,
      height: height,
    );
  }
}

class _DocumentFramePainter extends CustomPainter {
  _DocumentFramePainter(this.frame);

  final Rect frame;

  @override
  void paint(Canvas canvas, Size size) {
    final overlayPath = Path()
      ..addRect(Offset.zero & size)
      ..addRRect(RRect.fromRectAndRadius(frame, const Radius.circular(18)))
      ..fillType = PathFillType.evenOdd;

    canvas.drawPath(
      overlayPath,
      Paint()..color = Colors.black.withValues(alpha: 0.58),
    );

    final borderPaint = Paint()
      ..color = Colors.white
      ..strokeWidth = 3
      ..style = PaintingStyle.stroke;
    canvas.drawRRect(
      RRect.fromRectAndRadius(frame, const Radius.circular(18)),
      borderPaint,
    );

    final cornerPaint = Paint()
      ..color = AppColors.primary
      ..strokeWidth = 7
      ..strokeCap = StrokeCap.round
      ..style = PaintingStyle.stroke;
    const cornerLength = 34.0;
    final radius = frame.deflate(4);
    for (final corner in [
      radius.topLeft,
      radius.topRight,
      radius.bottomLeft,
      radius.bottomRight,
    ]) {
      final isLeft = corner.dx == radius.left;
      final isTop = corner.dy == radius.top;
      final horizontalEnd = Offset(
        corner.dx + (isLeft ? cornerLength : -cornerLength),
        corner.dy,
      );
      final verticalEnd = Offset(
        corner.dx,
        corner.dy + (isTop ? cornerLength : -cornerLength),
      );
      canvas
        ..drawLine(corner, horizontalEnd, cornerPaint)
        ..drawLine(corner, verticalEnd, cornerPaint);
    }
  }

  @override
  bool shouldRepaint(_DocumentFramePainter oldDelegate) {
    return oldDelegate.frame != frame;
  }
}

class _CameraError extends StatelessWidget {
  _CameraError({required this.message, required this.onBack});

  final String message;
  final VoidCallback onBack;

  @override
  Widget build(BuildContext context) {
    return SafeArea(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Icon(Icons.no_photography, color: Colors.white, size: 54),
            SizedBox(height: 18),
            Text(
              message,
              textAlign: TextAlign.center,
              style: TextStyle(color: Colors.white, fontSize: 16),
            ),
            SizedBox(height: 24),
            FilledButton(
              onPressed: onBack,
              style: FilledButton.styleFrom(
                backgroundColor: Colors.white,
                foregroundColor: Colors.black87,
              ),
              child: Text(context.l10n.goBack),
            ),
          ],
        ),
      ),
    );
  }
}
