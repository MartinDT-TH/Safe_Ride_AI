import 'dart:io';
import 'package:flutter/material.dart';
import '../../application/services/document_image_cropper.dart';
import '../../application/services/identity_ocr_scanner.dart';
import '../../../../../core/constants/app_colors.dart';
import '../../../../../core/widgets/custom_button.dart';
import '../../data/models/identity_verification_submission.dart';
import 'document_camera_page.dart';
import 'license_upload_page.dart';

class UploadCccdPage extends StatefulWidget {
  const UploadCccdPage({super.key, this.submission});

  final IdentityVerificationSubmission? submission;

  @override
  State<UploadCccdPage> createState() => _UploadCccdPageState();
}

class _UploadCccdPageState extends State<UploadCccdPage> {
  File? _frontImage;
  File? _backImage;
  final TextEditingController _fullNameController = TextEditingController();
  final TextEditingController _documentNumberController =
      TextEditingController();
  bool _isScanning = false;
  final IdentityOcrScanner _ocrScanner = IdentityOcrScanner();
  late final IdentityVerificationSubmission _submission;

  bool get _hasFrontImage => _frontImage != null;
  bool get _hasBackImage => _backImage != null;
  bool get _hasFullName => _fullNameController.text.trim().isNotEmpty;
  bool get _hasDocumentNumber =>
      _documentNumberController.text.trim().isNotEmpty;

  @override
  void initState() {
    super.initState();
    _submission = widget.submission ?? IdentityVerificationSubmission();
    _frontImage = _submission.cccdFrontImage;
    _backImage = _submission.cccdBackImage;
    _fullNameController.text = _submission.cccdFullName ?? '';
    _documentNumberController.text = _submission.cccdNumber ?? '';
  }

  Future<void> _pickImage(bool isFront) async {
    try {
      final image = await Navigator.of(context).push<File>(
        MaterialPageRoute(
          builder: (_) => DocumentCameraPage(
            title: isFront ? 'Mặt trước CCCD' : 'Mặt sau CCCD',
            instruction: 'Đặt CCCD nằm gọn trong khung, đủ sáng và rõ nét.',
          ),
        ),
      );

      if (image != null) {
        setState(() {
          if (isFront) {
            _frontImage = image;
          } else {
            _backImage = image;
          }
        });
        await _scanImage(image);
      }
    } catch (e) {
      debugPrint('Error picking image: $e');
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text(
              'Không thể mở camera. Vui lòng kiểm tra quyền truy cập.',
            ),
          ),
        );
      }
    }
  }

  Future<void> _scanImage(File image) async {
    setState(() => _isScanning = true);
    try {
      final result = await _ocrScanner.scanImage(
        image: image,
        documentType: IdentityOcrDocumentType.idCard,
      );
      if (!mounted) return;
      setState(() {
        if (result.fullName != null) {
          _fullNameController.text = result.fullName!;
        }
        if (result.documentNumber != null) {
          _documentNumberController.text = result.documentNumber!;
        }
      });
      if (result.documentNumber != null || result.fullName != null) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Đã quét thông tin CCCD.')),
        );
      }
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Không thể quét OCR từ ảnh này.')),
      );
    } finally {
      if (mounted) setState(() => _isScanning = false);
    }
  }

  @override
  void dispose() {
    _fullNameController.dispose();
    _documentNumberController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: Colors.white,
      appBar: AppBar(
        backgroundColor: Colors.white,
        elevation: 0,
        leading: IconButton(
          icon: const Icon(Icons.arrow_back, color: Color(0xFF263238)),
          onPressed: () => Navigator.pop(context),
        ),
        title: const Text(
          'Xác minh danh tính',
          style: TextStyle(
            color: AppColors.primary,
            fontSize: 18,
            fontWeight: FontWeight.w700,
          ),
        ),
        centerTitle: true,
      ),
      body: Column(
        children: [
          const Divider(height: 1, color: Color(0xFFF0F0F0)),
          Expanded(
            child: SingleChildScrollView(
              physics: const BouncingScrollPhysics(),
              padding: const EdgeInsets.symmetric(horizontal: 20),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const SizedBox(height: 20),
                  // Step Indicator
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: const [
                      Text(
                        'Bước 1/3',
                        style: TextStyle(
                          color: AppColors.primary,
                          fontWeight: FontWeight.w800,
                          fontSize: 15,
                        ),
                      ),
                      Text(
                        'Tải lên CCCD',
                        style: TextStyle(
                          color: Color(0xFF78909C),
                          fontWeight: FontWeight.w600,
                          fontSize: 15,
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 12),
                  ClipRRect(
                    borderRadius: BorderRadius.circular(10),
                    child: const LinearProgressIndicator(
                      value: 0.33,
                      minHeight: 8,
                      backgroundColor: Color(0xFFF0F0F0),
                      valueColor: AlwaysStoppedAnimation<Color>(
                        AppColors.primary,
                      ),
                    ),
                  ),
                  const SizedBox(height: 32),
                  const Text(
                    'Chụp ảnh CCCD',
                    style: TextStyle(
                      fontSize: 26,
                      fontWeight: FontWeight.w800,
                      color: Color(0xFF1F1F1F),
                      letterSpacing: -0.5,
                    ),
                  ),
                  const SizedBox(height: 12),
                  const Text(
                    'Vui lòng cung cấp hình ảnh mặt trước và mặt sau của Căn cước công dân. Đảm bảo ảnh rõ nét, không bị lóa sáng hay mất góc.',
                    style: TextStyle(
                      fontSize: 16,
                      color: Color(0xFF607D8B),
                      height: 1.5,
                      fontWeight: FontWeight.w500,
                    ),
                  ),
                  const SizedBox(height: 24),
                  // Tip Box
                  _buildTipBox(),
                  if (_isScanning || _hasDocumentNumber || _hasFullName) ...[
                    const SizedBox(height: 12),
                    _buildOcrStatus(),
                  ],
                  const SizedBox(height: 32),

                  // Front Photo Box
                  _PhotoUploadBox(
                    label: 'Mặt trước CCCD',
                    image: _frontImage,
                    onTap: () => _pickImage(true),
                  ),
                  const SizedBox(height: 20),

                  // Back Photo Box
                  _PhotoUploadBox(
                    label: 'Mặt sau CCCD',
                    image: _backImage,
                    onTap: () => _pickImage(false),
                  ),
                  const SizedBox(height: 24),
                  _buildInputField(
                    label: 'Họ và Tên',
                    child: TextField(
                      controller: _fullNameController,
                      textCapitalization: TextCapitalization.words,
                      onChanged: (_) => setState(() {}),
                      decoration: const InputDecoration(
                        hintText: 'Nhập họ và tên trên CCCD',
                        hintStyle: TextStyle(
                          color: Color(0xFF919191),
                          fontSize: 15,
                        ),
                        border: OutlineInputBorder(
                          borderSide: BorderSide(color: Color(0xFFCFD8DC)),
                        ),
                        enabledBorder: OutlineInputBorder(
                          borderSide: BorderSide(color: Color(0xFFCFD8DC)),
                        ),
                        focusedBorder: OutlineInputBorder(
                          borderSide: BorderSide(color: AppColors.primary),
                        ),
                        contentPadding: EdgeInsets.symmetric(
                          horizontal: 12,
                          vertical: 14,
                        ),
                      ),
                    ),
                  ),
                  const SizedBox(height: 20),
                  _buildInputField(
                    label: 'Số CCCD',
                    child: TextField(
                      controller: _documentNumberController,
                      keyboardType: TextInputType.number,
                      onChanged: (_) => setState(() {}),
                      decoration: const InputDecoration(
                        hintText: 'Nhập số CCCD',
                        hintStyle: TextStyle(
                          color: Color(0xFF919191),
                          fontSize: 15,
                        ),
                        border: OutlineInputBorder(
                          borderSide: BorderSide(color: Color(0xFFCFD8DC)),
                        ),
                        enabledBorder: OutlineInputBorder(
                          borderSide: BorderSide(color: Color(0xFFCFD8DC)),
                        ),
                        focusedBorder: OutlineInputBorder(
                          borderSide: BorderSide(color: AppColors.primary),
                        ),
                        contentPadding: EdgeInsets.symmetric(
                          horizontal: 12,
                          vertical: 14,
                        ),
                      ),
                    ),
                  ),
                  const SizedBox(height: 40),
                ],
              ),
            ),
          ),
        ],
      ),
      bottomNavigationBar: Container(
        padding: const EdgeInsets.fromLTRB(20, 12, 20, 32),
        decoration: BoxDecoration(
          color: Colors.white,
          boxShadow: [
            BoxShadow(
              color: Colors.black.withValues(alpha: 0.05),
              blurRadius: 10,
              offset: const Offset(0, -5),
            ),
          ],
        ),
        child: CustomButton(
          text: 'Tiếp tục',
          onPressed: () {
            if (_hasFrontImage &&
                _hasBackImage &&
                _hasFullName &&
                _hasDocumentNumber) {
              _submission
                ..cccdFrontImage = _frontImage
                ..cccdBackImage = _backImage
                ..cccdFullName = _fullNameController.text.trim()
                ..cccdNumber = _documentNumberController.text.trim();
              Navigator.push(
                context,
                MaterialPageRoute(
                  builder: (_) => LicenseUploadPage(submission: _submission),
                ),
              );
            } else {
              ScaffoldMessenger.of(context).showSnackBar(
                const SnackBar(
                  content: Text(
                    'Vui lòng chụp đủ ảnh và kiểm tra Họ và Tên, Số CCCD.',
                  ),
                ),
              );
            }
          },
        ),
      ),
    );
  }

  Widget _buildTipBox() {
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: const Color(0xFFE1EAEB),
        borderRadius: BorderRadius.circular(12),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Icon(Icons.info_outline, color: AppColors.primary, size: 24),
          const SizedBox(width: 12),
          const Expanded(
            child: Text(
              'Mẹo: Đặt CCCD trên mặt phẳng tối màu, đủ ánh sáng tự nhiên để đạt kết quả tốt nhất.',
              style: TextStyle(
                fontSize: 14,
                color: Color(0xFF455A64),
                height: 1.4,
                fontWeight: FontWeight.w500,
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildInputField({required String label, required Widget child}) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          label,
          style: const TextStyle(
            fontSize: 15,
            fontWeight: FontWeight.w600,
            color: Color(0xFF455A64),
          ),
        ),
        const SizedBox(height: 8),
        child,
      ],
    );
  }

  Widget _buildOcrStatus() {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: const Color(0xFFFFF8E1),
        borderRadius: BorderRadius.circular(10),
        border: Border.all(color: const Color(0xFFFFE082)),
      ),
      child: Row(
        children: [
          Icon(
            _isScanning ? Icons.document_scanner_outlined : Icons.check_circle,
            color: AppColors.primary,
            size: 20,
          ),
          const SizedBox(width: 8),
          Expanded(
            child: Text(
              _isScanning
                  ? 'Đang quét OCR trên thiết bị...'
                  : 'OCR đã tự điền thông tin CCCD',
              style: const TextStyle(
                color: Color(0xFF455A64),
                fontSize: 13,
                fontWeight: FontWeight.w700,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _PhotoUploadBox extends StatelessWidget {
  final String label;
  final File? image;
  final VoidCallback onTap;

  const _PhotoUploadBox({required this.label, this.image, required this.onTap});

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: onTap,
      child: Stack(
        children: [
          CustomPaint(
            painter: _DashedRectPainter(
              color: image != null
                  ? AppColors.primary
                  : const Color(0xFFCFD8DC),
            ),
            child: AspectRatio(
              aspectRatio: DocumentImageCropper.documentAspectRatio,
              child: Container(
                width: double.infinity,
                decoration: BoxDecoration(
                  borderRadius: BorderRadius.circular(16),
                ),
                child: image == null
                    ? Column(
                        mainAxisAlignment: MainAxisAlignment.center,
                        children: [
                          Container(
                            padding: const EdgeInsets.all(16),
                            decoration: const BoxDecoration(
                              color: Color(0xFFF5F5F5),
                              shape: BoxShape.circle,
                            ),
                            child: const Icon(
                              Icons.add_photo_alternate_outlined,
                              color: Color(0xFF607D8B),
                              size: 32,
                            ),
                          ),
                          const SizedBox(height: 16),
                          Text(
                            label,
                            style: const TextStyle(
                              fontSize: 16,
                              fontWeight: FontWeight.w800,
                              color: Color(0xFF263238),
                            ),
                          ),
                          const SizedBox(height: 4),
                          const Text(
                            'Chạm để chụp hoặc tải lên',
                            style: TextStyle(
                              fontSize: 14,
                              color: Color(0xFF78909C),
                              fontWeight: FontWeight.w600,
                            ),
                          ),
                        ],
                      )
                    : ClipRRect(
                        borderRadius: BorderRadius.circular(16),
                        child: Image.file(
                          image!,
                          fit: BoxFit.cover,
                          width: double.infinity,
                          height: double.infinity,
                        ),
                      ),
              ),
            ),
          ),
          if (image != null)
            Positioned(
              right: 12,
              top: 12,
              child: Container(
                padding: const EdgeInsets.all(4),
                decoration: const BoxDecoration(
                  color: AppColors.primary,
                  shape: BoxShape.circle,
                ),
                child: const Icon(Icons.check, color: Colors.white, size: 16),
              ),
            ),
        ],
      ),
    );
  }
}

class _DashedRectPainter extends CustomPainter {
  final Color color;
  static const double strokeWidth = 1.5;
  static const double gap = 5.0;

  _DashedRectPainter({this.color = Colors.grey});

  @override
  void paint(Canvas canvas, Size size) {
    final Paint paint = Paint()
      ..color = color
      ..strokeWidth = strokeWidth
      ..style = PaintingStyle.stroke;

    final double x = size.width;
    final double y = size.height;
    final double radius = 16.0;

    final Path path = Path()
      ..moveTo(radius, 0)
      ..lineTo(x - radius, 0)
      ..arcToPoint(Offset(x, radius), radius: Radius.circular(radius))
      ..lineTo(x, y - radius)
      ..arcToPoint(Offset(x - radius, y), radius: Radius.circular(radius))
      ..lineTo(radius, y)
      ..arcToPoint(Offset(0, y - radius), radius: Radius.circular(radius))
      ..lineTo(0, radius)
      ..arcToPoint(Offset(radius, 0), radius: Radius.circular(radius));

    for (final pathMetric in path.computeMetrics()) {
      double distance = 0.0;
      while (distance < pathMetric.length) {
        canvas.drawPath(
          pathMetric.extractPath(distance, distance + gap),
          paint,
        );
        distance += gap * 2;
      }
    }
  }

  @override
  bool shouldRepaint(CustomPainter oldDelegate) => true;
}
