import 'dart:io';
import 'package:flutter/material.dart';
import '../../application/services/document_image_cropper.dart';
import '../../application/services/identity_ocr_scanner.dart';
import '../../../../../core/constants/app_colors.dart';
import '../../../../../core/localization/localization_extensions.dart';
import '../../../../../core/widgets/custom_button.dart';
import '../../data/models/identity_verification_submission.dart';
import 'document_camera_page.dart';
import 'license_upload_page.dart';

class UploadCccdPage extends StatefulWidget {
  UploadCccdPage({super.key, this.submission});

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
  final TextEditingController _dateOfBirthController = TextEditingController();
  final TextEditingController _addressController = TextEditingController();
  String? _gender;
  bool _isScanning = false;
  final IdentityOcrScanner _ocrScanner = IdentityOcrScanner();
  late final IdentityVerificationSubmission _submission;

  bool get _hasFrontImage => _frontImage != null;
  bool get _hasBackImage => _backImage != null;
  bool get _hasFullName => _fullNameController.text.trim().isNotEmpty;
  bool get _hasDocumentNumber =>
      _documentNumberController.text.trim().isNotEmpty;
  bool get _hasIdentityDetails =>
      _parseDate(_dateOfBirthController.text) != null &&
      _gender != null &&
      _addressController.text.trim().isNotEmpty;

  @override
  void initState() {
    super.initState();
    _submission = widget.submission ?? IdentityVerificationSubmission();
    _frontImage = _submission.cccdFrontImage;
    _backImage = _submission.cccdBackImage;
    _fullNameController.text = _submission.cccdFullName ?? '';
    _documentNumberController.text = _submission.cccdNumber ?? '';
    _dateOfBirthController.text = _submission.cccdDateOfBirth == null
        ? ''
        : _formatDisplayDate(_submission.cccdDateOfBirth!);
    _gender = _submission.cccdGender;
    _addressController.text = _submission.cccdAddress ?? '';
  }

  Future<void> _pickImage(bool isFront) async {
    try {
      final image = await Navigator.of(context).push<File>(
        MaterialPageRoute(
          builder: (_) => DocumentCameraPage(
            title: isFront ? context.l10n.idCardFront : context.l10n.idCardBack,
            instruction: context.l10n.idCardCameraInstruction,
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
          SnackBar(
            content: Text(
              context.l10n.cameraOpenFailed,
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
        if (result.dateOfBirth != null) {
          _dateOfBirthController.text = _formatDisplayDate(result.dateOfBirth!);
        }
        if (result.gender != null) _gender = result.gender;
        if (result.address != null) _addressController.text = result.address!;
      });
      if (result.documentNumber != null || result.fullName != null) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(context.l10n.idCardScanned)),
        );
      }
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(context.l10n.ocrScanFailed)),
      );
    } finally {
      if (mounted) setState(() => _isScanning = false);
    }
  }

  @override
  void dispose() {
    _fullNameController.dispose();
    _documentNumberController.dispose();
    _dateOfBirthController.dispose();
    _addressController.dispose();
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
          icon: Icon(Icons.arrow_back, color: Color(0xFF263238)),
          onPressed: () => Navigator.pop(context),
        ),
        title: Text(
          context.l10n.identityVerification,
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
          Divider(height: 1, color: Color(0xFFF0F0F0)),
          Expanded(
            child: SingleChildScrollView(
              physics: BouncingScrollPhysics(),
              padding: const EdgeInsets.symmetric(horizontal: 20),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  SizedBox(height: 20),
                  // Step Indicator
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Text(
                        context.l10n.stepOneOfThree,
                        style: TextStyle(
                          color: AppColors.primary,
                          fontWeight: FontWeight.w800,
                          fontSize: 15,
                        ),
                      ),
                      Text(
                        context.l10n.uploadIdCard,
                        style: TextStyle(
                          color: Color(0xFF78909C),
                          fontWeight: FontWeight.w600,
                          fontSize: 15,
                        ),
                      ),
                    ],
                  ),
                  SizedBox(height: 12),
                  ClipRRect(
                    borderRadius: BorderRadius.circular(10),
                    child: LinearProgressIndicator(
                      value: 0.33,
                      minHeight: 8,
                      backgroundColor: Color(0xFFF0F0F0),
                      valueColor: AlwaysStoppedAnimation<Color>(
                        AppColors.primary,
                      ),
                    ),
                  ),
                  SizedBox(height: 32),
                  Text(
                    context.l10n.captureIdCard,
                    style: TextStyle(
                      fontSize: 26,
                      fontWeight: FontWeight.w800,
                      color: Color(0xFF1F1F1F),
                      letterSpacing: -0.5,
                    ),
                  ),
                  SizedBox(height: 12),
                  Text(
                    context.l10n.idCardUploadInstruction,
                    style: TextStyle(
                      fontSize: 16,
                      color: Color(0xFF607D8B),
                      height: 1.5,
                      fontWeight: FontWeight.w500,
                    ),
                  ),
                  SizedBox(height: 24),
                  // Tip Box
                  _buildTipBox(),
                  if (_isScanning || _hasDocumentNumber || _hasFullName) ...[
                    SizedBox(height: 12),
                    _buildOcrStatus(),
                  ],
                  SizedBox(height: 32),

                  // Front Photo Box
                  _PhotoUploadBox(
                    label: context.l10n.idCardFront,
                    image: _frontImage,
                    onTap: () => _pickImage(true),
                  ),
                  SizedBox(height: 20),

                  // Back Photo Box
                  _PhotoUploadBox(
                    label: context.l10n.idCardBack,
                    image: _backImage,
                    onTap: () => _pickImage(false),
                  ),
                  SizedBox(height: 24),
                  _buildInputField(
                    label: context.l10n.fullName,
                    child: TextField(
                      controller: _fullNameController,
                      readOnly: true,
                      decoration: InputDecoration(
                        hintText: context.l10n.idCardNameHint,
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
                  SizedBox(height: 20),
                  _buildInputField(
                    label: context.l10n.idCardNumber,
                    child: TextField(
                      controller: _documentNumberController,
                      keyboardType: TextInputType.number,
                      readOnly: true,
                      decoration: InputDecoration(
                        hintText: context.l10n.idCardNumberHint,
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
                  SizedBox(height: 20),
                  _buildInputField(
                    label: 'Ngày sinh',
                    child: TextField(
                      controller: _dateOfBirthController,
                      keyboardType: TextInputType.datetime,
                      readOnly: true,
                      decoration: _fieldDecoration('dd/MM/yyyy'),
                    ),
                  ),
                  SizedBox(height: 20),
                  _buildInputField(
                    label: 'Giới tính',
                    child: DropdownButtonFormField<String>(
                      initialValue: _gender,
                      decoration: _fieldDecoration('Chọn giới tính'),
                      items: const [
                        DropdownMenuItem(value: 'Male', child: Text('Nam')),
                        DropdownMenuItem(value: 'Female', child: Text('Nữ')),
                        DropdownMenuItem(value: 'Other', child: Text('Khác')),
                      ],
                      onChanged: null,
                    ),
                  ),
                  SizedBox(height: 20),
                  _buildInputField(
                    label: 'Địa chỉ thường trú',
                    child: TextField(
                      controller: _addressController,
                      readOnly: true,
                      minLines: 2,
                      maxLines: 3,
                      decoration: _fieldDecoration(
                        'Nhập địa chỉ thường trú trên CCCD',
                      ),
                    ),
                  ),
                  SizedBox(height: 40),
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
              offset: Offset(0, -5),
            ),
          ],
        ),
        child: CustomButton(
          text: context.l10n.continueAction,
          onPressed: () {
            if (_hasFrontImage &&
                _hasBackImage &&
                _hasFullName &&
                _hasDocumentNumber &&
                _hasIdentityDetails) {
              _submission
                ..cccdFrontImage = _frontImage
                ..cccdBackImage = _backImage
                ..cccdFullName = _fullNameController.text.trim()
                ..cccdNumber = _documentNumberController.text.trim()
                ..cccdDateOfBirth = _parseDate(_dateOfBirthController.text)
                ..cccdGender = _gender
                ..cccdAddress = _addressController.text.trim();
              Navigator.push(
                context,
                MaterialPageRoute(
                  builder: (_) => LicenseUploadPage(submission: _submission),
                ),
              );
            } else {
              ScaffoldMessenger.of(context).showSnackBar(
                SnackBar(
                  content: Text(
                    context.l10n.idCardFieldsRequired,
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
        color: Color(0xFFE1EAEB),
        borderRadius: BorderRadius.circular(12),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(Icons.info_outline, color: AppColors.primary, size: 24),
          SizedBox(width: 12),
          Expanded(
            child: Text(
              context.l10n.idCardPhotoTip,
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
          style: TextStyle(
            fontSize: 15,
            fontWeight: FontWeight.w600,
            color: Color(0xFF455A64),
          ),
        ),
        SizedBox(height: 8),
        child,
      ],
    );
  }

  InputDecoration _fieldDecoration(String hint) => InputDecoration(
    hintText: hint,
    hintStyle: TextStyle(color: Color(0xFF919191), fontSize: 15),
    border: OutlineInputBorder(
      borderSide: BorderSide(color: Color(0xFFCFD8DC)),
    ),
    enabledBorder: OutlineInputBorder(
      borderSide: BorderSide(color: Color(0xFFCFD8DC)),
    ),
    focusedBorder: OutlineInputBorder(
      borderSide: BorderSide(color: AppColors.primary),
    ),
    contentPadding: EdgeInsets.symmetric(horizontal: 12, vertical: 14),
  );

  Widget _buildOcrStatus() {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: Color(0xFFFFF8E1),
        borderRadius: BorderRadius.circular(10),
        border: Border.all(color: Color(0xFFFFE082)),
      ),
      child: Row(
        children: [
          Icon(
            _isScanning ? Icons.document_scanner_outlined : Icons.check_circle,
            color: AppColors.primary,
            size: 20,
          ),
          SizedBox(width: 8),
          Expanded(
            child: Text(
              _isScanning
                  ? context.l10n.ocrScanningOnDevice
                  : context.l10n.idCardOcrFilled,
              style: TextStyle(
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

DateTime? _parseDate(String value) {
  final match = RegExp(r'^(\d{1,2})[\/\-.](\d{1,2})[\/\-.](\d{4})$')
      .firstMatch(value.trim());
  if (match == null) return null;
  final day = int.parse(match.group(1)!);
  final month = int.parse(match.group(2)!);
  final year = int.parse(match.group(3)!);
  final date = DateTime(year, month, day);
  return date.year == year && date.month == month && date.day == day
      ? date
      : null;
}

String _formatDisplayDate(DateTime value) =>
    '${value.day.toString().padLeft(2, '0')}/'
    '${value.month.toString().padLeft(2, '0')}/${value.year}';

class _PhotoUploadBox extends StatelessWidget {
  final String label;
  final File? image;
  final VoidCallback onTap;

  _PhotoUploadBox({required this.label, this.image, required this.onTap});

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
                  : Color(0xFFCFD8DC),
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
                            decoration: BoxDecoration(
                              color: Color(0xFFF5F5F5),
                              shape: BoxShape.circle,
                            ),
                            child: Icon(
                              Icons.add_photo_alternate_outlined,
                              color: Color(0xFF607D8B),
                              size: 32,
                            ),
                          ),
                          SizedBox(height: 16),
                          Text(
                            label,
                            style: TextStyle(
                              fontSize: 16,
                              fontWeight: FontWeight.w800,
                              color: Color(0xFF263238),
                            ),
                          ),
                          SizedBox(height: 4),
                          Text(
                            context.l10n.tapToCaptureOrUpload,
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
                decoration: BoxDecoration(
                  color: AppColors.primary,
                  shape: BoxShape.circle,
                ),
                child: Icon(Icons.check, color: Colors.white, size: 16),
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
