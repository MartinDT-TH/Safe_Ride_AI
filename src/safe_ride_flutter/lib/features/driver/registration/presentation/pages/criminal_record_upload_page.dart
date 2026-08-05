import 'dart:io';
import 'dart:ui';
import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';
import 'package:provider/provider.dart';
import '../../application/services/identity_ocr_scanner.dart';
import '../../../../../core/constants/app_colors.dart';
import '../../../../../core/localization/localization_extensions.dart';
import '../../../../../core/storage/secure_storage_service.dart';
import '../../../../../core/widgets/custom_button.dart';
import '../../../../../dependency_injection/injection.dart';
import '../../../../auth/presentation/providers/auth_provider.dart';
import '../../../../customer/home/presentation/pages/customer_home_page.dart';
import '../../data/datasources/identity_verification_remote_datasource.dart';
import '../../data/models/identity_verification_submission.dart';

class CriminalRecordUploadPage extends StatefulWidget {
  CriminalRecordUploadPage({super.key, this.submission});

  final IdentityVerificationSubmission? submission;

  @override
  State<CriminalRecordUploadPage> createState() => _CriminalRecordUploadPageState();
}

class _CriminalRecordUploadPageState extends State<CriminalRecordUploadPage> {
  File? _selectedFile;
  String? _ocrRawText;
  bool _isScanning = false;
  bool _isSubmitting = false;
  final ImagePicker _picker = ImagePicker();
  final IdentityOcrScanner _ocrScanner = IdentityOcrScanner();
  late final IdentityVerificationSubmission _submission;
  late final IdentityVerificationRemoteDatasource _datasource;

  bool get _hasSelectedFile => _selectedFile != null;

  @override
  void initState() {
    super.initState();
    _submission = widget.submission ?? IdentityVerificationSubmission();
    _datasource = getIt<IdentityVerificationRemoteDatasource>();
    _selectedFile = _submission.criminalRecordFile;
  }

  Future<void> _pickFile() async {
    showModalBottomSheet(
      context: context,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
      ),
      builder: (context) => SafeArea(
        child: Wrap(
          children: [
            ListTile(
              leading: Icon(Icons.camera_alt),
              title: Text(context.l10n.takePhoto),
              onTap: () async {
                Navigator.pop(context);
                final XFile? image = await _picker.pickImage(source: ImageSource.camera);
                if (image != null) await _setSelectedFile(File(image.path));
              },
            ),
            ListTile(
              leading: Icon(Icons.photo_library),
              title: Text(context.l10n.chooseFromGallery),
              onTap: () async {
                Navigator.pop(context);
                final XFile? image = await _picker.pickImage(source: ImageSource.gallery);
                if (image != null) await _setSelectedFile(File(image.path));
              },
            ),
          ],
        ),
      ),
    );
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
                  _buildStepHeader(),
                  SizedBox(height: 32),
                  
                  Center(
                    child: Text(
                      context.l10n.criminalRecord,
                      style: TextStyle(
                        fontSize: 28,
                        fontWeight: FontWeight.w800,
                        color: Color(0xFF1F1F1F),
                        letterSpacing: -0.5,
                      ),
                    ),
                  ),
                  SizedBox(height: 12),
                  Text(
                    context.l10n.criminalRecordInstruction,
                    style: TextStyle(
                      fontSize: 16,
                      color: Color(0xFF607D8B),
                      height: 1.5,
                      fontWeight: FontWeight.w500,
                    ),
                    textAlign: TextAlign.center,
                  ),
                  SizedBox(height: 24),
                  
                  _buildRequirementsBox(),
                  SizedBox(height: 32),
                  
                  _buildUploadArea(),
                  if (_isScanning || _ocrRawText != null) ...[
                    SizedBox(height: 12),
                    _buildOcrStatus(),
                  ],
                  SizedBox(height: 24),
                  
                  Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Icon(Icons.access_time, color: Color(0xFF607D8B), size: 24),
                      SizedBox(width: 12),
                      Expanded(
                        child: Text(
                          context.l10n.reviewWithinHours,
                          style: TextStyle(
                            fontSize: 15,
                            color: Color(0xFF455A64),
                            height: 1.4,
                            fontWeight: FontWeight.w500,
                          ),
                        ),
                      ),
                    ],
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
              color: Colors.black.withOpacity(0.05),
              blurRadius: 10,
              offset: Offset(0, -5),
            ),
          ],
        ),
        child: CustomButton(
          text: _isSubmitting ? context.l10n.submittingApplication : context.l10n.completeAndSubmit,
          onPressed: _hasSelectedFile && !_isSubmitting ? _submitProfile : null,
        ),
      ),
    );
  }

  Widget _buildStepHeader() {
    return Column(
      children: [
        Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [
            Text(
              context.l10n.stepThreeOfThree,
              style: TextStyle(
                color: AppColors.primary,
                fontWeight: FontWeight.w800,
                fontSize: 15,
              ),
            ),
            Text(
              context.l10n.uploadCriminalRecord,
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
            value: 1.0,
            minHeight: 8,
            backgroundColor: Color(0xFFF0F0F0),
            valueColor: AlwaysStoppedAnimation<Color>(AppColors.primary),
          ),
        ),
      ],
    );
  }

  Widget _buildRequirementsBox() {
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
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  context.l10n.uploadRequirements,
                  style: TextStyle(
                    fontSize: 15,
                    fontWeight: FontWeight.w800,
                    color: Color(0xFF263238),
                  ),
                ),
                SizedBox(height: 4),
                _buildBulletPoint(context.l10n.clearNoGlare),
                _buildBulletPoint(context.l10n.allFourCorners),
                _buildBulletPoint(context.l10n.supportedDocumentFormats),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildBulletPoint(String text) {
    return Padding(
      padding: const EdgeInsets.only(top: 4),
      child: Text(
        '• $text',
        style: TextStyle(
          fontSize: 14,
          color: Color(0xFF455A64),
          height: 1.4,
          fontWeight: FontWeight.w500,
        ),
      ),
    );
  }

  Widget _buildUploadArea() {
    return GestureDetector(
      onTap: _pickFile,
      child: CustomPaint(
        painter: _DashedRectPainter(
          color: _hasSelectedFile ? AppColors.primary : Color(0xFFCFD8DC),
        ),
        child: Container(
          width: double.infinity,
          padding: const EdgeInsets.symmetric(vertical: 40, horizontal: 20),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(16),
          ),
          child: Column(
            children: [
              if (!_hasSelectedFile) ...[
                Container(
                  padding: const EdgeInsets.all(16),
                  decoration: BoxDecoration(
                    color: Color(0xFFF5F5F5),
                    shape: BoxShape.circle,
                  ),
                  child: Icon(
                    Icons.file_upload_outlined,
                    color: AppColors.primary,
                    size: 36,
                  ),
                ),
                SizedBox(height: 24),
                Text(
                  context.l10n.tapToUploadDocument,
                  style: TextStyle(
                    fontSize: 16,
                    fontWeight: FontWeight.w800,
                    color: Color(0xFF263238),
                  ),
                  textAlign: TextAlign.center,
                ),
                SizedBox(height: 8),
                Text(
                  context.l10n.photoOrPdfSupported,
                  style: TextStyle(
                    fontSize: 14,
                    color: Color(0xFF78909C),
                    fontWeight: FontWeight.w600,
                  ),
                ),
                SizedBox(height: 24),
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 10),
                  decoration: BoxDecoration(
                    color: Color(0xFFEEEEEE),
                    borderRadius: BorderRadius.circular(100),
                  ),
                  child: Text(
                    context.l10n.chooseDocument,
                    style: TextStyle(
                      fontSize: 14,
                      fontWeight: FontWeight.w800,
                      color: Color(0xFF455A64),
                    ),
                  ),
                ),
              ] else ...[
                ClipRRect(
                  borderRadius: BorderRadius.circular(12),
                  child: _selectedFile != null
                      ? Image.file(
                          _selectedFile!,
                          height: 200,
                          width: double.infinity,
                          fit: BoxFit.cover,
                        )
                      : const SizedBox.shrink(),
                ),
                SizedBox(height: 16),
                Row(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    Icon(Icons.check_circle, color: Colors.green, size: 20),
                    SizedBox(width: 8),
                    Text(
                      context.l10n.documentSelected,
                      style: TextStyle(color: Colors.green, fontWeight: FontWeight.bold),
                    ),
                    SizedBox(width: 16),
                    TextButton(
                      onPressed: _pickFile,
                      child: Text(context.l10n.change),
                    ),
                  ],
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }

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
                  : context.l10n.criminalRecordOcrRead,
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

  Future<void> _setSelectedFile(File file) async {
    setState(() => _selectedFile = file);
    await _scanFile(file);
  }

  Future<void> _scanFile(File file) async {
    setState(() => _isScanning = true);
    try {
      final result = await _ocrScanner.scanImage(
        image: file,
        documentType: IdentityOcrDocumentType.criminalRecord,
      );
      if (!mounted) return;
      setState(() => _ocrRawText = result.rawText);
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(context.l10n.criminalRecordScanned)),
      );
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(context.l10n.documentOcrFailed)),
      );
    } finally {
      if (mounted) setState(() => _isScanning = false);
    }
  }

  void _showSuccessDialog() {
    showDialog(
      context: context,
      barrierDismissible: false,
      builder: (dialogContext) => AlertDialog(
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            SizedBox(height: 20),
            Icon(Icons.check_circle_outline, color: AppColors.primary, size: 80),
            SizedBox(height: 24),
            Text(
              context.l10n.applicationSubmitted,
              style: TextStyle(fontSize: 22, fontWeight: FontWeight.w800),
              textAlign: TextAlign.center,
            ),
            SizedBox(height: 12),
            Text(
              context.l10n.applicationProcessing,
              textAlign: TextAlign.center,
              style: TextStyle(color: Color(0xFF78909C), height: 1.4),
            ),
            SizedBox(height: 32),
            CustomButton(
              text: context.l10n.backToHome,
              onPressed: () {
                Navigator.of(dialogContext).pop();
                if (!mounted) return;
                Navigator.of(context).pushAndRemoveUntil(
                  MaterialPageRoute(builder: (_) => CustomerHomePage()),
                  (route) => false,
                );
              },
            ),
          ],
        ),
      ),
    );
  }

  Future<void> _submitProfile() async {
    final providerToken = context.read<AuthProvider>().token;
    final token =
        providerToken ?? await getIt<SecureStorageService>().readAccessToken();
    if (!mounted) return;

    if (token == null || token.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(context.l10n.sessionExpired)),
      );
      return;
    }

    _submission.criminalRecordFile = _selectedFile;

    setState(() => _isSubmitting = true);
    try {
      await _datasource.submitAll(token, _submission);
      if (!mounted) return;
      _showSuccessDialog();
    } on DioException catch (error) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(_readErrorMessage(error))),
      );
    } catch (_) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(context.l10n.applicationSubmitFailed)),
      );
    } finally {
      if (mounted) setState(() => _isSubmitting = false);
    }
  }

  String _readErrorMessage(DioException error) {
    final data = error.response?.data;
    if (data is Map && data['message'] != null) {
      return data['message'].toString();
    }

    return context.l10n.applicationSubmitFailed;
  }
}

class _DashedRectPainter extends CustomPainter {
  final Color color;
  final double strokeWidth;
  final double gap;

  _DashedRectPainter({
    this.color = Colors.grey,
    this.strokeWidth = 1.5,
    this.gap = 5.0,
  });

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

    for (final PathMetric metric in path.computeMetrics()) {
      double distance = 0.0;
      while (distance < metric.length) {
        canvas.drawPath(
          metric.extractPath(distance, distance + gap),
          paint,
        );
        distance += gap * 2;
      }
    }
  }

  @override
  bool shouldRepaint(CustomPainter oldDelegate) => true;
}
