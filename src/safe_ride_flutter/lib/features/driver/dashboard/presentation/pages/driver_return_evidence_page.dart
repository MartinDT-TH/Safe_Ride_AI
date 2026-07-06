import 'dart:io';

import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';
import 'package:provider/provider.dart';

import '../../../../../core/constants/app_colors.dart';
import '../../../../../core/constants/app_strings.dart';
import '../providers/driver_dashboard_provider.dart';

/// Driver submits 1–3 evidence photos to confirm vehicle return on behalf of
/// the customer when the customer is unavailable to self-confirm.
///
/// Flow:  WAITING_RETURN_CONFIRM → (this page) → RETURN_CONFIRMED
class DriverReturnEvidencePage extends StatefulWidget {
  const DriverReturnEvidencePage({
    super.key,
    required this.tripId,
  });

  final int tripId;

  @override
  State<DriverReturnEvidencePage> createState() =>
      _DriverReturnEvidencePageState();
}

class _DriverReturnEvidencePageState extends State<DriverReturnEvidencePage>
    with SingleTickerProviderStateMixin {
  final _noteController = TextEditingController();
  final _picker = ImagePicker();
  final List<File> _evidenceFiles = [];
  bool _isSubmitting = false;
  String? _errorMessage;
  late AnimationController _pulseController;
  late Animation<double> _pulseAnimation;

  static const int _maxPhotos = 3;
  static const Color _primary = Color(0xFF006B70);
  static const Color _primaryLight = Color(0xFFE8F2F2);
  static const Color _errorColor = Color(0xFFE53935);

  @override
  void initState() {
    super.initState();
    _pulseController = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 1200),
    )..repeat(reverse: true);
    _pulseAnimation = Tween<double>(begin: 0.95, end: 1.0).animate(
      CurvedAnimation(parent: _pulseController, curve: Curves.easeInOut),
    );
  }

  @override
  void dispose() {
    _noteController.dispose();
    _pulseController.dispose();
    super.dispose();
  }

  // ───────────────────────────── Actions ──────────────────────────────

  Future<void> _pickImage(ImageSource source) async {
    if (_evidenceFiles.length >= _maxPhotos) return;
    try {
      final xFile = await _picker.pickImage(
        source: source,
        imageQuality: 85,
        maxWidth: 1920,
      );
      if (xFile == null) return;
      if (!mounted) return;
      setState(() {
        _evidenceFiles.add(File(xFile.path));
        _errorMessage = null;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() => _errorMessage = 'Không thể truy cập ${source == ImageSource.camera ? "camera" : "thư viện"}.');
    }
  }

  void _showImageSourceSheet() {
    if (_evidenceFiles.length >= _maxPhotos) {
      setState(() => _errorMessage = DriverReturnEvidenceStrings.errorMaxPhoto);
      return;
    }
    showModalBottomSheet(
      context: context,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(24)),
      ),
      backgroundColor: Colors.white,
      builder: (ctx) => SafeArea(
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Container(
                width: 40,
                height: 4,
                margin: const EdgeInsets.only(bottom: 16),
                decoration: BoxDecoration(
                  color: Colors.grey[300],
                  borderRadius: BorderRadius.circular(4),
                ),
              ),
              ListTile(
                leading: Container(
                  padding: const EdgeInsets.all(8),
                  decoration: const BoxDecoration(
                    color: _primaryLight,
                    shape: BoxShape.circle,
                  ),
                  child: const Icon(Icons.camera_alt_rounded, color: _primary),
                ),
                title: const Text(
                  DriverReturnEvidenceStrings.camera,
                  style: TextStyle(fontWeight: FontWeight.w600),
                ),
                onTap: () {
                  Navigator.pop(ctx);
                  _pickImage(ImageSource.camera);
                },
              ),
              ListTile(
                leading: Container(
                  padding: const EdgeInsets.all(8),
                  decoration: const BoxDecoration(
                    color: _primaryLight,
                    shape: BoxShape.circle,
                  ),
                  child: const Icon(Icons.photo_library_rounded, color: _primary),
                ),
                title: const Text(
                  DriverReturnEvidenceStrings.gallery,
                  style: TextStyle(fontWeight: FontWeight.w600),
                ),
                onTap: () {
                  Navigator.pop(ctx);
                  _pickImage(ImageSource.gallery);
                },
              ),
              const SizedBox(height: 8),
            ],
          ),
        ),
      ),
    );
  }

  void _removeImage(int index) {
    showDialog(
      context: context,
      builder: (ctx) => AlertDialog(
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
        title: const Text(
          DriverReturnEvidenceStrings.removePhoto,
          style: TextStyle(fontWeight: FontWeight.bold),
        ),
        content: const Text(DriverReturnEvidenceStrings.confirmRemove),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx),
            child: Text(
              AppStrings.cancel,
              style: TextStyle(color: Colors.grey[600]),
            ),
          ),
          ElevatedButton(
            style: ElevatedButton.styleFrom(
              backgroundColor: _errorColor,
              foregroundColor: Colors.white,
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(10),
              ),
            ),
            onPressed: () {
              Navigator.pop(ctx);
              setState(() => _evidenceFiles.removeAt(index));
            },
            child: const Text('Xóa'),
          ),
        ],
      ),
    );
  }

  Future<void> _submit() async {
    if (_evidenceFiles.isEmpty) {
      setState(
          () => _errorMessage = DriverReturnEvidenceStrings.errorMinPhoto);
      return;
    }
    if (_evidenceFiles.length > _maxPhotos) {
      setState(
          () => _errorMessage = DriverReturnEvidenceStrings.errorMaxPhoto);
      return;
    }

    setState(() {
      _isSubmitting = true;
      _errorMessage = null;
    });

    try {
      final provider =
          context.read<DriverDashboardProvider>();
      await provider.confirmReturnByDriver(
        tripId: widget.tripId,
        evidenceFiles: List.unmodifiable(_evidenceFiles),
        note: _noteController.text.trim().isEmpty
            ? null
            : _noteController.text.trim(),
      );
      if (!mounted) return;
      _showSuccessDialog();
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _errorMessage = DriverReturnEvidenceStrings.errorUploadFailed;
        _isSubmitting = false;
      });
    }
  }

  void _showSuccessDialog() {
    showDialog(
      context: context,
      barrierDismissible: false,
      builder: (ctx) => AlertDialog(
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(24)),
        contentPadding:
            const EdgeInsets.symmetric(horizontal: 28, vertical: 24),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Container(
              padding: const EdgeInsets.all(20),
              decoration: const BoxDecoration(
                color: Color(0xFFE8F7F0),
                shape: BoxShape.circle,
              ),
              child: const Icon(
                Icons.check_circle_rounded,
                color: Color(0xFF0A8F62),
                size: 52,
              ),
            ),
            const SizedBox(height: 20),
            const Text(
              DriverReturnEvidenceStrings.successTitle,
              textAlign: TextAlign.center,
              style: TextStyle(
                fontSize: 20,
                fontWeight: FontWeight.bold,
                color: Color(0xFF1F1F1F),
              ),
            ),
            const SizedBox(height: 10),
            const Text(
              DriverReturnEvidenceStrings.successMessage,
              textAlign: TextAlign.center,
              style: TextStyle(
                fontSize: 14,
                color: Color(0xFF6B6B6B),
                height: 1.5,
              ),
            ),
            const SizedBox(height: 24),
            SizedBox(
              width: double.infinity,
              child: ElevatedButton(
                style: ElevatedButton.styleFrom(
                  backgroundColor: _primary,
                  foregroundColor: Colors.white,
                  padding: const EdgeInsets.symmetric(vertical: 14),
                  shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(14)),
                ),
                onPressed: () {
                  Navigator.pop(ctx); // close dialog
                  Navigator.pop(context); // close evidence page
                },
                child: const Text(
                  DriverReturnEvidenceStrings.done,
                  style: TextStyle(
                    fontSize: 16,
                    fontWeight: FontWeight.w700,
                  ),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }

  // ────────────────────────────── Build ───────────────────────────────

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        backgroundColor: _primary,
        foregroundColor: Colors.white,
        elevation: 0,
        title: const Text(
          DriverReturnEvidenceStrings.pageTitle,
          style: TextStyle(
            fontWeight: FontWeight.w700,
            fontSize: 18,
          ),
        ),
        centerTitle: true,
        leading: IconButton(
          icon: const Icon(Icons.arrow_back_ios_new_rounded),
          onPressed: _isSubmitting ? null : () => Navigator.pop(context),
        ),
      ),
      body: Column(
        children: [
          // Gradient header bar
          Container(
            decoration: const BoxDecoration(
              gradient: LinearGradient(
                colors: [_primary, Color(0xFF005A64)],
                begin: Alignment.topLeft,
                end: Alignment.bottomRight,
              ),
            ),
            padding: const EdgeInsets.fromLTRB(20, 0, 20, 20),
            child: Row(
              children: [
                Container(
                  padding: const EdgeInsets.all(10),
                  decoration: BoxDecoration(
                    color: Colors.white.withOpacity(0.18),
                    borderRadius: BorderRadius.circular(12),
                  ),
                  child: const Icon(
                    Icons.photo_camera_rounded,
                    color: Colors.white,
                    size: 22,
                  ),
                ),
                const SizedBox(width: 14),
                const Expanded(
                  child: Text(
                    DriverReturnEvidenceStrings.instruction,
                    style: TextStyle(
                      color: Colors.white,
                      fontSize: 13,
                      height: 1.5,
                    ),
                  ),
                ),
              ],
            ),
          ),

          // Scrollable body
          Expanded(
            child: SingleChildScrollView(
              padding: const EdgeInsets.all(20),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  // Photo count indicator
                  _buildPhotoCountBadge(),
                  const SizedBox(height: 16),

                  // Photo grid
                  _buildPhotoGrid(),
                  const SizedBox(height: 24),

                  // Note field
                  _buildNoteField(),
                  const SizedBox(height: 16),

                  // Error message
                  if (_errorMessage != null) _buildErrorBanner(),
                  const SizedBox(height: 24),
                ],
              ),
            ),
          ),

          // Bottom submit button
          _buildSubmitButton(),
        ],
      ),
    );
  }

  Widget _buildPhotoCountBadge() {
    final count = _evidenceFiles.length;
    final color = count == 0
        ? Colors.orange
        : count < _maxPhotos
            ? _primary
            : const Color(0xFF0A8F62);
    return Row(
      children: [
        Container(
          padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 6),
          decoration: BoxDecoration(
            color: color.withOpacity(0.12),
            borderRadius: BorderRadius.circular(20),
            border: Border.all(color: color.withOpacity(0.3)),
          ),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(
                count == 0
                    ? Icons.add_photo_alternate_rounded
                    : Icons.photo_library_rounded,
                size: 16,
                color: color,
              ),
              const SizedBox(width: 6),
              Text(
                '$count / $_maxPhotos ảnh',
                style: TextStyle(
                  color: color,
                  fontSize: 13,
                  fontWeight: FontWeight.w700,
                ),
              ),
            ],
          ),
        ),
        const Spacer(),
        if (count < _maxPhotos)
          Text(
            'Còn ${_maxPhotos - count} ảnh',
            style: const TextStyle(
              fontSize: 12,
              color: Color(0xFF6B6B6B),
            ),
          ),
      ],
    );
  }

  Widget _buildPhotoGrid() {
    return GridView.builder(
      shrinkWrap: true,
      physics: const NeverScrollableScrollPhysics(),
      gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
        crossAxisCount: 3,
        crossAxisSpacing: 10,
        mainAxisSpacing: 10,
        childAspectRatio: 1,
      ),
      itemCount: _evidenceFiles.length < _maxPhotos
          ? _evidenceFiles.length + 1
          : _evidenceFiles.length,
      itemBuilder: (context, index) {
        if (index == _evidenceFiles.length) {
          // Add button cell
          return _buildAddPhotoCell();
        }
        return _buildPhotoCell(index);
      },
    );
  }

  Widget _buildAddPhotoCell() {
    return ScaleTransition(
      scale: _pulseAnimation,
      child: GestureDetector(
        onTap: _showImageSourceSheet,
        child: Container(
          decoration: BoxDecoration(
            color: Colors.white,
            borderRadius: BorderRadius.circular(16),
            border: Border.all(
              color: _primary.withOpacity(0.4),
              width: 2,
              style: BorderStyle.solid,
            ),
            boxShadow: [
              BoxShadow(
                color: _primary.withOpacity(0.08),
                blurRadius: 8,
                offset: const Offset(0, 4),
              ),
            ],
          ),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Container(
                padding: const EdgeInsets.all(10),
                decoration: const BoxDecoration(
                  color: _primaryLight,
                  shape: BoxShape.circle,
                ),
                child: const Icon(
                  Icons.add_photo_alternate_rounded,
                  color: _primary,
                  size: 26,
                ),
              ),
              const SizedBox(height: 6),
              const Text(
                DriverReturnEvidenceStrings.tapToAdd,
                textAlign: TextAlign.center,
                style: TextStyle(
                  fontSize: 10,
                  color: _primary,
                  fontWeight: FontWeight.w600,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildPhotoCell(int index) {
    return Stack(
      fit: StackFit.expand,
      children: [
        // Photo
        ClipRRect(
          borderRadius: BorderRadius.circular(16),
          child: Image.file(
            _evidenceFiles[index],
            fit: BoxFit.cover,
          ),
        ),
        // Gradient overlay
        ClipRRect(
          borderRadius: BorderRadius.circular(16),
          child: Container(
            decoration: BoxDecoration(
              gradient: LinearGradient(
                begin: Alignment.topCenter,
                end: Alignment.bottomCenter,
                colors: [
                  Colors.transparent,
                  Colors.black.withOpacity(0.4),
                ],
              ),
            ),
          ),
        ),
        // Photo label
        Positioned(
          bottom: 6,
          left: 6,
          child: Container(
            padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
            decoration: BoxDecoration(
              color: Colors.black.withOpacity(0.5),
              borderRadius: BorderRadius.circular(6),
            ),
            child: Text(
              '${DriverReturnEvidenceStrings.photoOf} ${index + 1}',
              style: const TextStyle(
                color: Colors.white,
                fontSize: 10,
                fontWeight: FontWeight.w600,
              ),
            ),
          ),
        ),
        // Delete button
        Positioned(
          top: 6,
          right: 6,
          child: GestureDetector(
            onTap: () => _removeImage(index),
            child: Container(
              padding: const EdgeInsets.all(4),
              decoration: BoxDecoration(
                color: Colors.black.withOpacity(0.6),
                shape: BoxShape.circle,
              ),
              child: const Icon(
                Icons.close_rounded,
                color: Colors.white,
                size: 14,
              ),
            ),
          ),
        ),
      ],
    );
  }

  Widget _buildNoteField() {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const Text(
          DriverReturnEvidenceStrings.noteLabel,
          style: TextStyle(
            fontSize: 14,
            fontWeight: FontWeight.w700,
            color: Color(0xFF1F1F1F),
          ),
        ),
        const SizedBox(height: 8),
        Container(
          decoration: BoxDecoration(
            color: Colors.white,
            borderRadius: BorderRadius.circular(16),
            boxShadow: [
              BoxShadow(
                color: Colors.black.withOpacity(0.05),
                blurRadius: 8,
                offset: const Offset(0, 2),
              ),
            ],
          ),
          child: TextField(
            controller: _noteController,
            maxLines: 3,
            maxLength: 300,
            decoration: InputDecoration(
              hintText: DriverReturnEvidenceStrings.noteHint,
              hintStyle: const TextStyle(
                color: Color(0xFFBBBBBB),
                fontSize: 14,
              ),
              border: OutlineInputBorder(
                borderRadius: BorderRadius.circular(16),
                borderSide: const BorderSide(color: AppColors.border),
              ),
              enabledBorder: OutlineInputBorder(
                borderRadius: BorderRadius.circular(16),
                borderSide: const BorderSide(color: AppColors.border),
              ),
              focusedBorder: OutlineInputBorder(
                borderRadius: BorderRadius.circular(16),
                borderSide: const BorderSide(color: _primary, width: 2),
              ),
              contentPadding: const EdgeInsets.all(16),
              counterStyle: const TextStyle(color: Color(0xFFBBBBBB)),
            ),
          ),
        ),
      ],
    );
  }

  Widget _buildErrorBanner() {
    return AnimatedContainer(
      duration: const Duration(milliseconds: 250),
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
      decoration: BoxDecoration(
        color: _errorColor.withOpacity(0.08),
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: _errorColor.withOpacity(0.3)),
      ),
      child: Row(
        children: [
          const Icon(Icons.error_outline_rounded, color: _errorColor, size: 20),
          const SizedBox(width: 10),
          Expanded(
            child: Text(
              _errorMessage!,
              style: const TextStyle(
                color: _errorColor,
                fontSize: 13,
                fontWeight: FontWeight.w600,
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildSubmitButton() {
    final hasPhotos = _evidenceFiles.isNotEmpty;
    return Container(
      decoration: BoxDecoration(
        color: Colors.white,
        boxShadow: [
          BoxShadow(
            color: Colors.black.withOpacity(0.08),
            blurRadius: 16,
            offset: const Offset(0, -4),
          ),
        ],
      ),
      padding: EdgeInsets.fromLTRB(
        20,
        16,
        20,
        16 + MediaQuery.of(context).padding.bottom,
      ),
      child: SizedBox(
        width: double.infinity,
        height: 56,
        child: ElevatedButton(
          style: ElevatedButton.styleFrom(
            backgroundColor: hasPhotos && !_isSubmitting
                ? _primary
                : const Color(0xFFB0C4C5),
            foregroundColor: Colors.white,
            elevation: hasPhotos && !_isSubmitting ? 2 : 0,
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(16),
            ),
          ),
          onPressed: (hasPhotos && !_isSubmitting) ? _submit : null,
          child: _isSubmitting
              ? const Row(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    SizedBox(
                      width: 20,
                      height: 20,
                      child: CircularProgressIndicator(
                        strokeWidth: 2.5,
                        valueColor: AlwaysStoppedAnimation(Colors.white),
                      ),
                    ),
                    SizedBox(width: 12),
                    Text(
                      DriverReturnEvidenceStrings.submitting,
                      style: TextStyle(
                        fontSize: 16,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                  ],
                )
              : Row(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    const Icon(Icons.verified_rounded, size: 22),
                    const SizedBox(width: 10),
                    Text(
                      hasPhotos
                          ? '${DriverReturnEvidenceStrings.submitButton} (${_evidenceFiles.length} ảnh)'
                          : DriverReturnEvidenceStrings.submitButton,
                      style: const TextStyle(
                        fontSize: 16,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                  ],
                ),
        ),
      ),
    );
  }
}
