import 'dart:io';
import 'package:flutter/material.dart';
import '../../application/services/document_image_cropper.dart';
import '../../application/services/identity_ocr_scanner.dart';
import '../../../../../core/constants/app_colors.dart';
import '../../../../../core/widgets/custom_button.dart';
import '../../data/models/identity_verification_submission.dart';
import 'criminal_record_upload_page.dart';
import 'document_camera_page.dart';

enum LicenseType { motorbike, car }

class LicenseUploadPage extends StatefulWidget {
  const LicenseUploadPage({super.key, this.submission});

  final IdentityVerificationSubmission? submission;

  @override
  State<LicenseUploadPage> createState() => _LicenseUploadPageState();
}

class _LicenseUploadPageState extends State<LicenseUploadPage> {
  LicenseType _selectedType = LicenseType.motorbike;
  final TextEditingController _fullNameController = TextEditingController();
  final TextEditingController _licenseNumberController =
      TextEditingController();
  String? _selectedGrade;
  DateTime? _issuedDate;
  DateTime? _expiryDate;
  bool _hasNoExpiryDate = false;
  bool _isScanning = false;
  String? _ocrRawText;
  late final IdentityVerificationSubmission _submission;

  File? _frontImage;
  File? _backImage;
  final IdentityOcrScanner _ocrScanner = IdentityOcrScanner();

  final List<String> _grades = ['A1', 'A2', 'A', 'B1', 'B2', 'B'];

  bool get _hasFrontImage => _frontImage != null;
  bool get _hasBackImage => _backImage != null;
  bool get _hasFullName => _fullNameController.text.trim().isNotEmpty;
  bool get _hasLicenseNumber => _licenseNumberController.text.trim().isNotEmpty;

  @override
  void initState() {
    super.initState();
    _submission = widget.submission ?? IdentityVerificationSubmission();
    _frontImage = _submission.licenseFrontImage;
    _backImage = _submission.licenseBackImage;
    _fullNameController.text = _submission.licenseFullName ?? '';
    _licenseNumberController.text = _submission.licenseNumber ?? '';
    _selectedGrade = _submission.licenseClass;
    _issuedDate = _submission.licenseIssueDate;
    _expiryDate = _submission.licenseExpiryDate;
    _hasNoExpiryDate = _submission.licenseHasNoExpiryDate;
  }

  Future<void> _pickImage(bool isFront) async {
    try {
      final file = await Navigator.of(context).push<File>(
        MaterialPageRoute(
          builder: (_) => DocumentCameraPage(
            title: isFront ? 'Mặt trước GPLX' : 'Mặt sau GPLX',
            instruction: 'Đặt bằng lái nằm gọn trong khung, đủ sáng và rõ nét.',
          ),
        ),
      );

      if (file != null) {
        setState(() {
          if (isFront) {
            _frontImage = file;
          } else {
            _backImage = file;
          }
        });
        await _scanLicenseImage(file);
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('Không thể mở camera. Vui lòng kiểm tra quyền.'),
          ),
        );
      }
    }
  }

  Future<void> _scanLicenseImage(File image) async {
    setState(() => _isScanning = true);
    try {
      final result = await _ocrScanner.scanImage(
        image: image,
        documentType: IdentityOcrDocumentType.drivingLicense,
      );
      if (!mounted) return;
      setState(() {
        _ocrRawText = result.rawText;
        if (result.fullName != null) {
          _fullNameController.text = result.fullName!;
        }
        if (result.documentNumber != null) {
          _licenseNumberController.text = result.documentNumber!;
        }
        if (result.licenseClass != null &&
            _grades.contains(result.licenseClass)) {
          _selectedGrade = result.licenseClass;
        }
        _issuedDate = result.issueDate ?? _issuedDate;
        _expiryDate = result.expiryDate ?? _expiryDate;
      });
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Đã quét OCR bằng Google ML Kit.')),
      );
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Không thể quét OCR từ ảnh GPLX này.')),
      );
    } finally {
      if (mounted) setState(() => _isScanning = false);
    }
  }

  Future<void> _selectDate(BuildContext context, bool isIssued) async {
    final DateTime? picked = await showDatePicker(
      context: context,
      initialDate: DateTime.now(),
      firstDate: DateTime(1990),
      lastDate: DateTime(2100),
      builder: (context, child) {
        return Theme(
          data: Theme.of(context).copyWith(
            colorScheme: const ColorScheme.light(primary: AppColors.primary),
          ),
          child: child!,
        );
      },
    );
    if (picked != null) {
      setState(() {
        if (isIssued) {
          _issuedDate = picked;
        } else {
          _expiryDate = picked;
          _hasNoExpiryDate = false;
        }
      });
    }
  }

  @override
  void dispose() {
    _fullNameController.dispose();
    _licenseNumberController.dispose();
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
                  _buildStepIndicator(),
                  const SizedBox(height: 32),
                  const Text(
                    'Loại bằng lái',
                    style: TextStyle(
                      fontSize: 16,
                      fontWeight: FontWeight.w700,
                      color: Color(0xFF263238),
                    ),
                  ),
                  const SizedBox(height: 12),
                  _buildTypeSelector(),
                  const SizedBox(height: 24),
                  const Text(
                    'Ảnh chụp bằng lái xe',
                    style: TextStyle(
                      fontSize: 16,
                      fontWeight: FontWeight.w700,
                      color: Color(0xFF263238),
                    ),
                  ),
                  const SizedBox(height: 12),
                  Row(
                    children: [
                      Expanded(
                        child: _PhotoUploadBoxSmall(
                          label: 'Mặt trước',
                          image: _frontImage,
                          onTap: () => _pickImage(true),
                        ),
                      ),
                      const SizedBox(width: 16),
                      Expanded(
                        child: _PhotoUploadBoxSmall(
                          label: 'Mặt sau',
                          image: _backImage,
                          onTap: () => _pickImage(false),
                        ),
                      ),
                    ],
                  ),
                  if (_isScanning || _ocrRawText != null) ...[
                    const SizedBox(height: 12),
                    _buildOcrStatus(),
                  ],
                  const SizedBox(height: 24),
                  _buildInputField(
                    label: 'Họ và Tên',
                    child: TextField(
                      controller: _fullNameController,
                      textCapitalization: TextCapitalization.words,
                      onChanged: (_) => setState(() {}),
                      decoration: const InputDecoration(
                        hintText: 'Nhập họ và tên trên GPLX',
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
                    label: 'Số bằng lái (GPLX)',
                    child: TextField(
                      controller: _licenseNumberController,
                      onChanged: (_) => setState(() {}),
                      decoration: const InputDecoration(
                        hintText: 'Nhập số trên bằng lái',
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
                    label: 'Hạng bằng',
                    child: DropdownButtonFormField<String>(
                      initialValue: _selectedGrade,
                      decoration: const InputDecoration(
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
                      hint: const Text(
                        'Chọn hạng bằng',
                        style: TextStyle(
                          color: Color(0xFF919191),
                          fontSize: 15,
                        ),
                      ),
                      items: _grades
                          .map(
                            (grade) => DropdownMenuItem(
                              value: grade,
                              child: Text(grade),
                            ),
                          )
                          .toList(),
                      onChanged: (val) => setState(() => _selectedGrade = val),
                    ),
                  ),
                  const SizedBox(height: 20),
                  Row(
                    children: [
                      Expanded(
                        child: _buildInputField(
                          label: 'Ngày cấp',
                          child: _DateSelector(
                            value: _issuedDate,
                            onTap: () => _selectDate(context, true),
                          ),
                        ),
                      ),
                      const SizedBox(width: 16),
                      Expanded(
                        child: _buildInputField(
                          label: 'Ngày hết hạn',
                          child: _DateSelector(
                            value: _expiryDate,
                            placeholder: _hasNoExpiryDate
                                ? 'Không giới hạn'
                                : 'mm/dd/yyyy',
                            enabled: !_hasNoExpiryDate,
                            onTap: () => _selectDate(context, false),
                          ),
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 12),
                  CheckboxListTile(
                    value: _hasNoExpiryDate,
                    onChanged: (value) {
                      setState(() {
                        _hasNoExpiryDate = value ?? false;
                        if (_hasNoExpiryDate) {
                          _expiryDate = null;
                        }
                      });
                    },
                    contentPadding: EdgeInsets.zero,
                    controlAffinity: ListTileControlAffinity.leading,
                    activeColor: AppColors.primary,
                    title: const Text(
                      'Bằng lái không có ngày hết hạn',
                      style: TextStyle(
                        color: Color(0xFF455A64),
                        fontSize: 15,
                        fontWeight: FontWeight.w600,
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
          onPressed:
              (_hasFrontImage &&
                  _hasBackImage &&
                  _hasFullName &&
                  _hasLicenseNumber &&
                  _selectedGrade != null &&
                  _issuedDate != null &&
                  (_hasNoExpiryDate || _expiryDate != null))
              ? () {
                  if (!_isSamePersonName(
                    _submission.cccdFullName,
                    _fullNameController.text,
                  )) {
                    ScaffoldMessenger.of(context).showSnackBar(
                      const SnackBar(
                        content: Text(
                          'Họ và Tên trên CCCD và GPLX không trùng khớp.',
                        ),
                      ),
                    );
                    return;
                  }

                  _submission
                    ..licenseFrontImage = _frontImage
                    ..licenseBackImage = _backImage
                    ..licenseFullName = _fullNameController.text.trim()
                    ..licenseNumber = _licenseNumberController.text.trim()
                    ..licenseClass = _selectedGrade
                    ..licenseIssueDate = _issuedDate
                    ..licenseExpiryDate = _hasNoExpiryDate ? null : _expiryDate
                    ..licenseHasNoExpiryDate = _hasNoExpiryDate;
                  Navigator.push(
                    context,
                    MaterialPageRoute(
                      builder: (_) =>
                          CriminalRecordUploadPage(submission: _submission),
                    ),
                  );
                }
              : null,
        ),
      ),
    );
  }

  Widget _buildStepIndicator() {
    return Column(
      children: [
        Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: const [
            Text(
              'Bước 2/3',
              style: TextStyle(
                color: AppColors.primary,
                fontWeight: FontWeight.w800,
                fontSize: 15,
              ),
            ),
            Text(
              'Tải lên GPLX',
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
            value: 0.66,
            minHeight: 8,
            backgroundColor: Color(0xFFF0F0F0),
            valueColor: AlwaysStoppedAnimation<Color>(AppColors.primary),
          ),
        ),
      ],
    );
  }

  Widget _buildTypeSelector() {
    return Container(
      height: 50,
      decoration: BoxDecoration(
        color: const Color(0xFFF0F0F0),
        borderRadius: BorderRadius.circular(10),
      ),
      child: Row(
        children: [
          Expanded(
            child: _TypeToggleItem(
              label: 'Xe máy',
              isSelected: _selectedType == LicenseType.motorbike,
              onTap: () =>
                  setState(() => _selectedType = LicenseType.motorbike),
            ),
          ),
          Expanded(
            child: _TypeToggleItem(
              label: 'Ô tô',
              isSelected: _selectedType == LicenseType.car,
              onTap: () => setState(() => _selectedType = LicenseType.car),
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
                  : 'OCR đã tự điền thông tin GPLX',
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

  bool _isSamePersonName(String? cccdName, String licenseName) {
    final normalizedCccdName = _normalizePersonName(cccdName);
    final normalizedLicenseName = _normalizePersonName(licenseName);
    return normalizedCccdName.isNotEmpty &&
        normalizedLicenseName.isNotEmpty &&
        normalizedCccdName == normalizedLicenseName;
  }

  String _normalizePersonName(String? value) {
    final source = (value ?? '').trim().toLowerCase();
    if (source.isEmpty) return '';
    return _stripVietnameseMarks(source)
        .replaceAll(RegExp(r'[^a-z\s]'), ' ')
        .replaceAll(RegExp(r'\s+'), ' ')
        .trim();
  }

  String _stripVietnameseMarks(String value) {
    const replacements = {
      'à': 'a',
      'á': 'a',
      'ạ': 'a',
      'ả': 'a',
      'ã': 'a',
      'â': 'a',
      'ầ': 'a',
      'ấ': 'a',
      'ậ': 'a',
      'ẩ': 'a',
      'ẫ': 'a',
      'ă': 'a',
      'ằ': 'a',
      'ắ': 'a',
      'ặ': 'a',
      'ẳ': 'a',
      'ẵ': 'a',
      'è': 'e',
      'é': 'e',
      'ẹ': 'e',
      'ẻ': 'e',
      'ẽ': 'e',
      'ê': 'e',
      'ề': 'e',
      'ế': 'e',
      'ệ': 'e',
      'ể': 'e',
      'ễ': 'e',
      'ì': 'i',
      'í': 'i',
      'ị': 'i',
      'ỉ': 'i',
      'ĩ': 'i',
      'ò': 'o',
      'ó': 'o',
      'ọ': 'o',
      'ỏ': 'o',
      'õ': 'o',
      'ô': 'o',
      'ồ': 'o',
      'ố': 'o',
      'ộ': 'o',
      'ổ': 'o',
      'ỗ': 'o',
      'ơ': 'o',
      'ờ': 'o',
      'ớ': 'o',
      'ợ': 'o',
      'ở': 'o',
      'ỡ': 'o',
      'ù': 'u',
      'ú': 'u',
      'ụ': 'u',
      'ủ': 'u',
      'ũ': 'u',
      'ư': 'u',
      'ừ': 'u',
      'ứ': 'u',
      'ự': 'u',
      'ử': 'u',
      'ữ': 'u',
      'ỳ': 'y',
      'ý': 'y',
      'ỵ': 'y',
      'ỷ': 'y',
      'ỹ': 'y',
      'đ': 'd',
    };

    final buffer = StringBuffer();
    for (final rune in value.runes) {
      final character = String.fromCharCode(rune);
      buffer.write(replacements[character] ?? character);
    }
    return buffer.toString();
  }
}

class _TypeToggleItem extends StatelessWidget {
  final String label;
  final bool isSelected;
  final VoidCallback onTap;

  const _TypeToggleItem({
    required this.label,
    required this.isSelected,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: onTap,
      child: Container(
        margin: const EdgeInsets.all(4),
        decoration: BoxDecoration(
          color: isSelected ? AppColors.primary : Colors.transparent,
          borderRadius: BorderRadius.circular(8),
        ),
        alignment: Alignment.center,
        child: Text(
          label,
          style: TextStyle(
            color: isSelected ? Colors.white : const Color(0xFF607D8B),
            fontWeight: FontWeight.w700,
            fontSize: 15,
          ),
        ),
      ),
    );
  }
}

class _PhotoUploadBoxSmall extends StatelessWidget {
  final String label;
  final File? image;
  final VoidCallback onTap;

  const _PhotoUploadBoxSmall({
    required this.label,
    this.image,
    required this.onTap,
  });

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
                  borderRadius: BorderRadius.circular(12),
                ),
                child: image == null
                    ? Column(
                        mainAxisAlignment: MainAxisAlignment.center,
                        children: [
                          const Icon(
                            Icons.add_a_photo_outlined,
                            color: Color(0xFF607D8B),
                            size: 28,
                          ),
                          const SizedBox(height: 8),
                          Text(
                            label,
                            style: const TextStyle(
                              fontSize: 13,
                              fontWeight: FontWeight.w700,
                              color: Color(0xFF455A64),
                            ),
                          ),
                        ],
                      )
                    : ClipRRect(
                        borderRadius: BorderRadius.circular(12),
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
              right: 8,
              top: 8,
              child: Container(
                padding: const EdgeInsets.all(2),
                decoration: const BoxDecoration(
                  color: AppColors.primary,
                  shape: BoxShape.circle,
                ),
                child: const Icon(Icons.check, color: Colors.white, size: 14),
              ),
            ),
        ],
      ),
    );
  }
}

class _DateSelector extends StatelessWidget {
  final DateTime? value;
  final String placeholder;
  final bool enabled;
  final VoidCallback onTap;

  const _DateSelector({
    this.value,
    this.placeholder = 'mm/dd/yyyy',
    this.enabled = true,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: enabled ? onTap : null,
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 14),
        decoration: BoxDecoration(
          border: Border.all(color: const Color(0xFFCFD8DC)),
          borderRadius: BorderRadius.circular(4),
        ),
        child: Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [
            Text(
              value == null
                  ? placeholder
                  : '${value!.day}/${value!.month}/${value!.year}',
              style: TextStyle(
                color: value == null
                    ? const Color(0xFF919191)
                    : enabled
                    ? Colors.black
                    : const Color(0xFF607D8B),
                fontSize: 15,
              ),
            ),
          ],
        ),
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
    final double radius = 12.0;

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

    for (final metric in path.computeMetrics()) {
      double distance = 0.0;
      while (distance < metric.length) {
        canvas.drawPath(metric.extractPath(distance, distance + gap), paint);
        distance += gap * 2;
      }
    }
  }

  @override
  bool shouldRepaint(CustomPainter oldDelegate) => true;
}
