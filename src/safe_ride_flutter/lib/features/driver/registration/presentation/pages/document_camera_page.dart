import 'dart:io';

import 'package:camera/camera.dart';
import 'package:flutter/material.dart';

import '../../../../../core/constants/app_colors.dart';
import '../../application/services/document_image_cropper.dart';

class DocumentCameraPage extends StatefulWidget {
  const DocumentCameraPage({
    super.key,
    required this.title,
    required this.instruction,
  });

  final String title;
  final String instruction;

  @override
  State<DocumentCameraPage> createState() => _DocumentCameraPageState();
}

class _DocumentCameraPageState extends State<DocumentCameraPage> {
  final DocumentImageCropper _cropper = DocumentImageCropper();
  CameraController? _controller;
  Future<void>? _initializeCameraFuture;
  bool _isCapturing = false;
  String? _errorMessage;

  @override
  void initState() {
    super.initState();
    _initializeCameraFuture = _initializeCamera();
  }

  @override
  void dispose() {
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
        imageFormatGroup: ImageFormatGroup.jpeg,
      );
      await controller.initialize();
      if (!mounted) {
        await controller.dispose();
        return;
      }
      setState(() => _controller = controller);
    } catch (_) {
      if (!mounted) return;
      setState(
        () => _errorMessage = 'Không thể mở camera. Vui lòng kiểm tra quyền.',
      );
    }
  }

  Future<void> _capture() async {
    final controller = _controller;
    if (controller == null || _isCapturing) return;

    setState(() => _isCapturing = true);
    try {
      final image = await controller.takePicture();
      final cropped = await _cropper.cropToDocument(File(image.path));
      if (!mounted) return;
      Navigator.of(context).pop(cropped);
    } catch (_) {
      if (!mounted) return;
      setState(() => _isCapturing = false);
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Không thể chụp ảnh. Vui lòng thử lại.')),
      );
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
            return const Center(
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
                          icon: const Icon(Icons.close),
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
                              decoration: const BoxDecoration(
                                color: Colors.white,
                                shape: BoxShape.circle,
                              ),
                              child: _isCapturing
                                  ? const Padding(
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
                        const SizedBox(width: 48),
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
  const _CameraPreviewCover({required this.controller});

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
  const _DocumentFrameOverlay({required this.title, required this.instruction});

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
                      style: const TextStyle(
                        color: Colors.white,
                        fontSize: 22,
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                    const SizedBox(height: 8),
                    Text(
                      instruction,
                      textAlign: TextAlign.center,
                      style: const TextStyle(
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
              child: const Text(
                'Canh 4 góc giấy tờ sát trong khung',
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
  const _DocumentFramePainter(this.frame);

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
  const _CameraError({required this.message, required this.onBack});

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
            const Icon(Icons.no_photography, color: Colors.white, size: 54),
            const SizedBox(height: 18),
            Text(
              message,
              textAlign: TextAlign.center,
              style: const TextStyle(color: Colors.white, fontSize: 16),
            ),
            const SizedBox(height: 24),
            FilledButton(
              onPressed: onBack,
              style: FilledButton.styleFrom(
                backgroundColor: Colors.white,
                foregroundColor: Colors.black87,
              ),
              child: const Text('Quay lại'),
            ),
          ],
        ),
      ),
    );
  }
}
