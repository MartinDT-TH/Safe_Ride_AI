import 'dart:io';

import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';
import 'package:provider/provider.dart';

import '../../../../core/constants/app_strings.dart';
import '../../../../core/utils/validators.dart';
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
  String? _nameError;
  String? _phoneError;
  String? _emailError;
  String _selectedPhoneCountryCode = AppValues.vietnamCountryCode;

  bool get _hasExistingPhone => (widget.phoneNumber ?? '').trim().isNotEmpty;

  @override
  void initState() {
    super.initState();
    final auth = context.read<AuthProvider>();
    final currentName = auth.fullName?.trim() ?? '';
    _nameController = TextEditingController(
      text: currentName == HomeStrings.defaultUser ? '' : currentName,
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
                ? ProfileStrings.completeProfile
                : ProfileStrings.editProfile,
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
                          ProfileStrings.changeAvatar,
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
                                    ProfileStrings.verifiedPhone,
                                    style: TextStyle(
                                      fontWeight: FontWeight.bold,
                                      fontSize: 14,
                                      color: Color(0xFF2D3132),
                                    ),
                                  ),
                                  SizedBox(height: 2),
                                  Text(
                                    ProfileStrings.updateInformationHint,
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
                        label: ProfileStrings.fullName,
                        controller: _nameController,
                        errorText: _nameError,
                        suffixIcon: const Icon(
                          Icons.edit_outlined,
                          color: Color(0xFFBDBDBD),
                        ),
                      ),
                      const SizedBox(height: 24),
                      _buildInputField(
                        label: AuthStrings.phoneNumber,
                        controller: _phoneController,
                        isReadOnly: _hasExistingPhone,
                        keyboardType: TextInputType.phone,
                        errorText: _phoneError,
                        prefixIcon: _hasExistingPhone
                            ? null
                            : _CountryCodePicker(
                                value: _selectedPhoneCountryCode,
                                onChanged: (value) {
                                  if (value == null) return;
                                  setState(
                                    () => _selectedPhoneCountryCode = value,
                                  );
                                },
                              ),
                        suffixIcon: _hasExistingPhone
                            ? const Icon(
                                Icons.check_circle,
                                color: Color(0xFF006B70),
                              )
                            : null,
                      ),
                      const SizedBox(height: 24),
                      _buildInputField(
                        label: ProfileStrings.email,
                        controller: _emailController,
                        keyboardType: TextInputType.emailAddress,
                        errorText: _emailError,
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
                              ? ProfileStrings.saving
                              : ProfileStrings.saveAndContinue,
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
    final phoneNumber = _phoneController.text.trim();
    final email = _emailController.text.trim();
    if (!_validateForm(fullName, phoneNumber, email)) {
      return;
    }

    final normalizedPhone = phoneNumber.isEmpty
        ? null
        : PhoneNumberValidator.normalizePhone(
            phoneNumber,
            countryCode: _selectedPhoneCountryCode,
          );
    if (!_hasExistingPhone && normalizedPhone != null) {
      final phoneVerified = await _verifyPhoneBeforeSave(
        provider,
        normalizedPhone,
      );
      if (!mounted) return;
      if (!phoneVerified) {
        return;
      }
      _phoneController.text = normalizedPhone;
    }

    if (_selectedAvatar != null) {
      final uploaded = await provider.uploadAvatar(_selectedAvatar!.path);
      if (!mounted) return;
      if (!uploaded) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text(ProfileStrings.uploadAvatarFailed)),
        );
        return;
      }
    }

    final saved = await provider.updateProfile(
      fullName,
      normalizedPhone,
      email.isEmpty ? null : email,
    );
    if (!mounted) return;

    if (!saved) {
      if (_applyServerValidation(provider.lastErrorCode)) {
        return;
      }
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text(ProfileStrings.updateProfileFailed)),
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
    Widget? prefixIcon,
    String? errorText,
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
          onChanged: (_) {
            if (controller == _nameController) {
              setState(() {
                _nameError = null;
              });
              return;
            }
            if (controller == _phoneController) {
              setState(() {
                _phoneError = null;
              });
              return;
            }
            if (controller == _emailController) {
              setState(() {
                _emailError = null;
              });
            }
          },
          style: const TextStyle(
            fontSize: 15,
            color: Color(0xFF2D3132),
            fontWeight: FontWeight.w500,
          ),
          decoration: InputDecoration(
            filled: true,
            fillColor: const Color(0xFFF9FAFB),
            errorText: errorText,
            contentPadding: const EdgeInsets.symmetric(
              horizontal: 16,
              vertical: 16,
            ),
            prefixIcon: prefixIcon,
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
    if (value.isEmpty) return HomeStrings.defaultInitials;
    final words = value.split(RegExp(r'\s+'));
    return words.take(2).map((word) => word[0].toUpperCase()).join();
  }

  bool _validateForm(String fullName, String phoneNumber, String email) {
    String? nameError;
    String? phoneError;
    String? emailError;

    if (fullName.length < 2) {
      nameError = ProfileStrings.invalidFullName;
    }

    if (!_hasExistingPhone &&
        (widget.requiredCompletion || phoneNumber.isNotEmpty) &&
        !PhoneNumberValidator.isValidPhone(
          phoneNumber,
          countryCode: _selectedPhoneCountryCode,
        )) {
      phoneError = AuthStrings.invalidPhone;
    }

    if (email.isNotEmpty &&
        !RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$').hasMatch(email)) {
      emailError = ProfileStrings.invalidEmail;
    }

    setState(() {
      _nameError = nameError;
      _phoneError = phoneError;
      _emailError = emailError;
    });

    return nameError == null && phoneError == null && emailError == null;
  }

  bool _applyServerValidation(String? code) {
    final errorText = switch (code) {
      'auth.invalid_phone_number' => AuthStrings.invalidPhone,
      'auth.phone_number_conflict' => ProfileStrings.phoneNumberAlreadyUsed,
      'auth.phone_number_change_requires_verification' =>
        ProfileStrings.phoneNumberChangeRequiresVerification,
      'auth.phone_verification_required' =>
        ProfileStrings.phoneVerificationRequired,
      _ => null,
    };

    if (errorText == null) {
      return false;
    }

    setState(() {
      _phoneError = errorText;
    });
    return true;
  }

  Future<bool> _verifyPhoneBeforeSave(
    AuthProvider provider,
    String phoneNumber,
  ) async {
    final sent = await provider.sendProfilePhoneOtp(phoneNumber);
    if (!mounted) return false;
    if (!sent) {
      if (_applyServerValidation(provider.lastErrorCode)) {
        return false;
      }
      ScaffoldMessenger.of(
        context,
      ).showSnackBar(const SnackBar(content: Text(AuthStrings.sendOtpFailed)));
      return false;
    }

    final verified = await _showPhoneOtpDialog(provider, phoneNumber);
    return verified ?? false;
  }

  Future<bool?> _showPhoneOtpDialog(AuthProvider provider, String phoneNumber) {
    final otpController = TextEditingController();
    String? otpError;

    return showDialog<bool>(
      context: context,
      barrierDismissible: false,
      builder: (dialogContext) {
        return StatefulBuilder(
          builder: (context, setDialogState) {
            return AlertDialog(
              title: const Text(AuthStrings.otpTitle),
              content: TextField(
                controller: otpController,
                keyboardType: TextInputType.number,
                maxLength: 6,
                decoration: InputDecoration(
                  hintText: '123456',
                  errorText: otpError,
                  counterText: '',
                ),
              ),
              actions: [
                TextButton(
                  onPressed: provider.isLoading
                      ? null
                      : () => Navigator.of(dialogContext).pop(false),
                  child: const Text(AppStrings.cancel),
                ),
                TextButton(
                  onPressed: provider.isLoading
                      ? null
                      : () async {
                          final otp = otpController.text.trim();
                          if (!RegExp(r'^\d{6}$').hasMatch(otp)) {
                            setDialogState(() {
                              otpError = AuthStrings.otpRequired;
                            });
                            return;
                          }

                          final verified = await provider.verifyProfilePhoneOtp(
                            phoneNumber,
                            otp,
                          );
                          if (!context.mounted) return;
                          if (verified) {
                            Navigator.of(dialogContext).pop(true);
                            return;
                          }

                          setDialogState(() {
                            otpError = _otpErrorText(provider.lastErrorCode);
                          });
                        },
                  child: provider.isLoading
                      ? const SizedBox(
                          width: 18,
                          height: 18,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : const Text(AppStrings.confirm),
                ),
              ],
            );
          },
        );
      },
    ).whenComplete(otpController.dispose);
  }

  String _otpErrorText(String? code) {
    return switch (code) {
      'auth.otp_attempts_exceeded' => AuthStrings.otpAttemptsExceeded,
      'auth.otp_expired' => AuthStrings.invalidOtp,
      'auth.invalid_otp' => AuthStrings.invalidOtp,
      'auth.phone_number_conflict' => ProfileStrings.phoneNumberAlreadyUsed,
      _ => AuthStrings.invalidOtp,
    };
  }
}

class _CountryCodePicker extends StatelessWidget {
  final String value;
  final ValueChanged<String?> onChanged;

  const _CountryCodePicker({required this.value, required this.onChanged});

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(left: 12, right: 8),
      child: DropdownButtonHideUnderline(
        child: DropdownButton<String>(
          value: value,
          isDense: true,
          items: PhoneNumberValidator.supportedCountryCodes
              .map(
                (code) =>
                    DropdownMenuItem<String>(value: code, child: Text(code)),
              )
              .toList(),
          onChanged: onChanged,
        ),
      ),
    );
  }
}
