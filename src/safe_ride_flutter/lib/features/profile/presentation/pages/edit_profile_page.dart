import 'dart:io';

import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';
import 'package:provider/provider.dart';

import '../../../auth/presentation/providers/auth_provider.dart';
import '../../../home/presentation/pages/customer_home_page.dart';

class EditProfilePage extends StatefulWidget {
  final bool requiredCompletion;
  final String? phoneNumber;

  const EditProfilePage({
    super.key,
    this.requiredCompletion = false,
    this.phoneNumber,
  });

  @override
  State<EditProfilePage> createState() => _EditProfilePageState();
}

class _EditProfilePageState extends State<EditProfilePage> {
  late TextEditingController _nameController;
  late TextEditingController _phoneController;
  late TextEditingController _emailController;
  final ImagePicker _imagePicker = ImagePicker();
  XFile? _selectedAvatar;

  @override
  void initState() {
    super.initState();
    final auth = context.read<AuthProvider>();
    _nameController = TextEditingController(
      text: widget.requiredCompletion ? '' : auth.fullName,
    );
    _phoneController = TextEditingController(
      text: widget.phoneNumber ?? auth.phoneNumber ?? '',
    );
    _emailController = TextEditingController(text: auth.email ?? '');
  }

  @override
  void dispose() {
    _nameController.dispose();
    _phoneController.dispose();
    _emailController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return PopScope(
      canPop: !widget.requiredCompletion,
      child: Scaffold(
        backgroundColor: Colors.white,
        appBar: AppBar(
          backgroundColor: Colors.white,
          elevation: 0,
          automaticallyImplyLeading: !widget.requiredCompletion,
          leading: widget.requiredCompletion
              ? null
              : IconButton(
                  icon: const Icon(Icons.arrow_back, color: Color(0xFF006B70)),
                  onPressed: () => Navigator.pop(context),
                ),
          title: Text(
            widget.requiredCompletion
                ? 'Hoàn thiện thông tin'
                : 'Chỉnh sửa hồ sơ',
            style: const TextStyle(
              color: Color(0xFF006B70),
              fontSize: 18,
              fontWeight: FontWeight.bold,
            ),
          ),
          centerTitle: false,
        ),
        body: SafeArea(
          child: Column(
            children: [
              Expanded(
                child: SingleChildScrollView(
                  padding: const EdgeInsets.symmetric(horizontal: 24),
                  child: Column(
                    children: [
                      const SizedBox(height: 20),
                      // Profile Image Section
                      Consumer<AuthProvider>(
                        builder: (_, auth, child) {
                          final avatarUrl = auth.avatarUrl?.trim() ?? '';
                          return GestureDetector(
                            onTap: auth.isLoading ? null : _pickAvatar,
                            child: Stack(
                              children: [
                                Container(
                                  padding: const EdgeInsets.all(4),
                                  decoration: BoxDecoration(
                                    shape: BoxShape.circle,
                                    border: Border.all(
                                      color: const Color(0xFFE0F2F1),
                                      width: 2,
                                    ),
                                  ),
                                  child: CircleAvatar(
                                    radius: 55,
                                    backgroundColor: const Color(0xFFF5F5F5),
                                    backgroundImage: _selectedAvatar != null
                                        ? FileImage(File(_selectedAvatar!.path))
                                        : avatarUrl.isNotEmpty
                                        ? NetworkImage(avatarUrl)
                                        : null,
                                    child:
                                        _selectedAvatar == null &&
                                            avatarUrl.isEmpty
                                        ? Text(
                                            _initials(_nameController.text),
                                            style: const TextStyle(
                                              color: Color(0xFF006B70),
                                              fontSize: 30,
                                              fontWeight: FontWeight.bold,
                                            ),
                                          )
                                        : null,
                                  ),
                                ),
                                Positioned(
                                  bottom: 5,
                                  right: 5,
                                  child: Container(
                                    padding: const EdgeInsets.all(6),
                                    decoration: const BoxDecoration(
                                      color: Color(0xFF006B70),
                                      shape: BoxShape.circle,
                                    ),
                                    child: const Icon(
                                      Icons.camera_alt,
                                      color: Colors.white,
                                      size: 18,
                                    ),
                                  ),
                                ),
                              ],
                            ),
                          );
                        },
                      ),
                      const SizedBox(height: 16),
                      TextButton(
                        onPressed: _pickAvatar,
                        child: const Text(
                          'Thay đổi ảnh đại diện',
                          style: TextStyle(
                            color: Color(0xFF006B70),
                            fontWeight: FontWeight.bold,
                            fontSize: 14,
                          ),
                        ),
                      ),
                      const SizedBox(height: 32),

                      Container(
                        padding: const EdgeInsets.all(16),
                        decoration: BoxDecoration(
                          color: const Color(0xFFF2F7F7),
                          borderRadius: BorderRadius.circular(12),
                        ),
                        child: Row(
                          children: [
                            Container(
                              padding: const EdgeInsets.all(8),
                              decoration: BoxDecoration(
                                color: const Color(0xFFD1E8E8),
                                borderRadius: BorderRadius.circular(10),
                              ),
                              child: const Icon(
                                Icons.verified_user,
                                color: Color(0xFF006B70),
                                size: 20,
                              ),
                            ),
                            const SizedBox(width: 16),
                            const Expanded(
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Text(
                                    'Số điện thoại đã xác minh',
                                    style: TextStyle(
                                      fontWeight: FontWeight.bold,
                                      fontSize: 14,
                                      color: Color(0xFF2D3132),
                                    ),
                                  ),
                                  SizedBox(height: 2),
                                  Text(
                                    'Vui lòng cập nhật thông tin cá nhân để tiếp tục.',
                                    style: TextStyle(
                                      color: Color(0xFF6B7280),
                                      fontSize: 12,
                                    ),
                                  ),
                                ],
                              ),
                            ),
                          ],
                        ),
                      ),
                      const SizedBox(height: 32),

                      // Input Fields
                      _buildInputField(
                        label: 'Họ và tên',
                        controller: _nameController,
                        suffixIcon: const Icon(
                          Icons.edit_outlined,
                          color: Color(0xFFBDBDBD),
                        ),
                      ),
                      const SizedBox(height: 24),
                      _buildInputField(
                        label: 'Số điện thoại',
                        controller: _phoneController,
                        isReadOnly: true,
                        suffixIcon: const Icon(
                          Icons.check_circle,
                          color: Color(0xFF006B70),
                        ),
                      ),
                      const SizedBox(height: 24),
                      _buildInputField(
                        label: 'Email',
                        controller: _emailController,
                        keyboardType: TextInputType.emailAddress,
                      ),
                      const SizedBox(height: 40),
                    ],
                  ),
                ),
              ),

              Consumer<AuthProvider>(
                builder: (_, provider, child) {
                  return Padding(
                    padding: const EdgeInsets.all(24),
                    child: SizedBox(
                      width: double.infinity,
                      height: 56,
                      child: ElevatedButton.icon(
                        onPressed: provider.isLoading
                            ? null
                            : () => _saveProfile(provider),
                        icon: provider.isLoading
                            ? const SizedBox(
                                width: 20,
                                height: 20,
                                child: CircularProgressIndicator(
                                  strokeWidth: 2,
                                  color: Colors.white,
                                ),
                              )
                            : const Icon(Icons.save, size: 20),
                        label: Text(
                          provider.isLoading
                              ? 'Đang lưu...'
                              : 'Lưu và tiếp tục',
                          style: const TextStyle(
                            fontSize: 16,
                            fontWeight: FontWeight.bold,
                          ),
                        ),
                        style: ElevatedButton.styleFrom(
                          backgroundColor: const Color(0xFF006B70),
                          foregroundColor: Colors.white,
                          elevation: 0,
                          shape: RoundedRectangleBorder(
                            borderRadius: BorderRadius.circular(12),
                          ),
                        ),
                      ),
                    ),
                  );
                },
              ),
            ],
          ),
        ),
      ),
    );
  }

  Future<void> _saveProfile(AuthProvider provider) async {
    final fullName = _nameController.text.trim();
    final email = _emailController.text.trim();
    if (fullName.length < 2) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Vui lòng nhập họ và tên hợp lệ.')),
      );
      return;
    }

    if (email.isNotEmpty &&
        !RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$').hasMatch(email)) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Địa chỉ email không hợp lệ.')),
      );
      return;
    }

    if (_selectedAvatar != null) {
      final uploaded = await provider.uploadAvatar(_selectedAvatar!.path);
      if (!mounted) return;
      if (!uploaded) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Không thể tải ảnh đại diện lên.')),
        );
        return;
      }
    }

    final saved = await provider.updateProfile(
      fullName,
      email.isEmpty ? null : email,
    );
    if (!mounted) return;

    if (!saved) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Không thể cập nhật thông tin.')),
      );
      return;
    }

    if (widget.requiredCompletion) {
      Navigator.of(context).pushAndRemoveUntil(
        MaterialPageRoute(builder: (_) => const CustomerHomePage()),
        (_) => false,
      );
    } else {
      Navigator.pop(context);
    }
  }

  Future<void> _pickAvatar() async {
    final image = await _imagePicker.pickImage(
      source: ImageSource.gallery,
      maxWidth: 1600,
      maxHeight: 1600,
      imageQuality: 88,
    );
    if (image == null || !mounted) return;
    setState(() => _selectedAvatar = image);
  }

  Widget _buildInputField({
    required String label,
    required TextEditingController controller,
    Widget? suffixIcon,
    bool isReadOnly = false,
    TextInputType? keyboardType,
  }) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          label,
          style: const TextStyle(
            fontSize: 13,
            fontWeight: FontWeight.w600,
            color: Color(0xFF6B7280),
          ),
        ),
        const SizedBox(height: 8),
        TextField(
          controller: controller,
          readOnly: isReadOnly,
          keyboardType: keyboardType,
          onChanged: controller == _nameController
              ? (_) => setState(() {})
              : null,
          style: const TextStyle(
            fontSize: 15,
            color: Color(0xFF2D3132),
            fontWeight: FontWeight.w500,
          ),
          decoration: InputDecoration(
            filled: true,
            fillColor: const Color(0xFFF9FAFB),
            contentPadding: const EdgeInsets.symmetric(
              horizontal: 16,
              vertical: 16,
            ),
            suffixIcon: suffixIcon,
            enabledBorder: OutlineInputBorder(
              borderRadius: BorderRadius.circular(12),
              borderSide: const BorderSide(color: Color(0xFFE5E7EB)),
            ),
            focusedBorder: OutlineInputBorder(
              borderRadius: BorderRadius.circular(12),
              borderSide: const BorderSide(color: Color(0xFF006B70), width: 1),
            ),
            border: OutlineInputBorder(borderRadius: BorderRadius.circular(12)),
          ),
        ),
      ],
    );
  }

  String _initials(String fullName) {
    final value = fullName.trim();
    if (value.isEmpty) return 'SR';
    final words = value.split(RegExp(r'\s+'));
    return words.take(2).map((word) => word[0].toUpperCase()).join();
  }
}
