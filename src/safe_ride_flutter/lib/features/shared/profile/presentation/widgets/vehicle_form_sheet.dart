import 'package:flutter/material.dart';
import '../../data/models/vehicle_model.dart';

class VehicleFormSheet extends StatefulWidget {
  final VehicleModel? vehicle;
  final Future<bool> Function(VehicleModel) onSave;

  const VehicleFormSheet({super.key, this.vehicle, required this.onSave});

  static Future<void> show(
    BuildContext context, {
    VehicleModel? vehicle,
    required Future<bool> Function(VehicleModel) onSave,
  }) {
    return showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.transparent,
      builder: (context) => Padding(
        padding: EdgeInsets.only(
          bottom: MediaQuery.of(context).viewInsets.bottom,
        ),
        child: VehicleFormSheet(vehicle: vehicle, onSave: onSave),
      ),
    );
  }

  @override
  State<VehicleFormSheet> createState() => _VehicleFormSheetState();
}

class _VehicleFormSheetState extends State<VehicleFormSheet> {
  late VehicleType _selectedType;
  late TextEditingController _nameController;
  late TextEditingController _licenseController;
  late TextEditingController _plateController;
  late TextEditingController _colorController;
  bool _isSaving = false;
  String? _nameError;
  String? _plateError;
  String? _colorError;

  @override
  void initState() {
    super.initState();
    _selectedType = widget.vehicle?.type ?? VehicleType.motorbike;
    _nameController = TextEditingController(text: widget.vehicle?.name ?? '');
    _licenseController = TextEditingController(
      text: widget.vehicle?.licenseType ?? '',
    );
    _plateController = TextEditingController(
      text: widget.vehicle?.plateNumber ?? '',
    );
    _colorController = TextEditingController(text: widget.vehicle?.color ?? '');
  }

  @override
  void dispose() {
    _nameController.dispose();
    _licenseController.dispose();
    _plateController.dispose();
    _colorController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    const tealColor = Color(0xFF006B70);
    const textColor = Color(0xFF1F2937);
    final isEdit = widget.vehicle != null;

    return Container(
      decoration: const BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.vertical(top: Radius.circular(28)),
      ),
      padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 20),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text(
                isEdit ? 'Chỉnh sửa phương tiện' : 'Thêm phương tiện mới',
                style: const TextStyle(
                  fontSize: 18,
                  fontWeight: FontWeight.bold,
                  color: textColor,
                ),
              ),
              IconButton(
                onPressed: () => Navigator.pop(context),
                icon: const Icon(Icons.close, color: textColor, size: 24),
                splashRadius: 20,
              ),
            ],
          ),
          const SizedBox(height: 8),
          const Divider(height: 1, color: Color(0xFFF3F4F6)),
          const SizedBox(height: 24),

          const Text(
            'Loại phương tiện',
            style: TextStyle(
              fontSize: 14,
              fontWeight: FontWeight.bold,
              color: textColor,
            ),
          ),
          const SizedBox(height: 12),
          Container(
            padding: const EdgeInsets.all(4),
            decoration: BoxDecoration(
              color: const Color(0xFFF3F4F6),
              borderRadius: BorderRadius.circular(12),
            ),
            child: Row(
              children: [
                _buildTypeButton(
                  'Xe máy',
                  Icons.directions_bike_rounded,
                  VehicleType.motorbike,
                ),
                _buildTypeButton(
                  'Ô tô',
                  Icons.directions_car_rounded,
                  VehicleType.car,
                ),
              ],
            ),
          ),
          const SizedBox(height: 24),

          _buildInputField(
            label: 'Tên phương tiện',
            controller: _nameController,
            hint: 'Ví dụ: Honda Vision',
            errorText: _nameError,
          ),
          const SizedBox(height: 20),
          _buildInputField(
            label: 'Loại bằng lái',
            controller: _licenseController,
            hint: 'Ví dụ: A1, B2...',
          ),
          const SizedBox(height: 20),
          _buildInputField(
            label: 'Biển số xe',
            controller: _plateController,
            hint: 'Ví dụ: 29A1 - 123.45',
            errorText: _plateError,
          ),
          const SizedBox(height: 20),
          _buildInputField(
            label: 'Màu sắc',
            controller: _colorController,
            hint: 'Ví dụ: Xanh dương',
            errorText: _colorError,
          ),
          const SizedBox(height: 32),

          SizedBox(
            width: double.infinity,
            height: 56,
            child: ElevatedButton(
              onPressed: _isSaving ? null : _saveVehicle,
              style: ElevatedButton.styleFrom(
                backgroundColor: tealColor,
                foregroundColor: Colors.white,
                shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(16),
                ),
                elevation: 0,
              ),
              child: _isSaving
                  ? const SizedBox(
                      width: 22,
                      height: 22,
                      child: CircularProgressIndicator(
                        strokeWidth: 2,
                        color: Colors.white,
                      ),
                    )
                  : Text(
                      isEdit ? 'Lưu thay đổi' : 'Lưu phương tiện',
                      style: const TextStyle(
                        fontSize: 16,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
            ),
          ),
          const SizedBox(height: 8),
          Center(
            child: TextButton(
              onPressed: _isSaving ? null : () => Navigator.pop(context),
              child: const Text(
                'Hủy',
                style: TextStyle(
                  color: tealColor,
                  fontWeight: FontWeight.bold,
                  fontSize: 16,
                ),
              ),
            ),
          ),
          const SizedBox(height: 12),
        ],
      ),
    );
  }

  Future<void> _saveVehicle() async {
    final name = _nameController.text.trim();
    final license = _licenseController.text.trim();
    final plateNumber = _plateController.text.trim();
    final color = _colorController.text.trim();

    if (!_validateForm(name: name, plateNumber: plateNumber, color: color)) {
      return;
    }

    setState(() => _isSaving = true);
    final vehicle = VehicleModel(
      id: widget.vehicle?.id ?? 0,
      name: name,
      licenseType: license,
      plateNumber: plateNumber,
      color: color,
      type: _selectedType,
    );
    final saved = await widget.onSave(vehicle);
    if (!mounted) return;
    if (saved) {
      Navigator.pop(context);
    } else {
      setState(() => _isSaving = false);
    }
  }

  Widget _buildTypeButton(String label, IconData icon, VehicleType type) {
    final isSelected = _selectedType == type;
    const tealColor = Color(0xFF006B70);

    return Expanded(
      child: GestureDetector(
        onTap: () => setState(() => _selectedType = type),
        child: Container(
          padding: const EdgeInsets.symmetric(vertical: 12),
          decoration: BoxDecoration(
            color: isSelected ? tealColor : Colors.transparent,
            borderRadius: BorderRadius.circular(10),
          ),
          child: Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Icon(
                icon,
                size: 20,
                color: isSelected ? Colors.white : const Color(0xFF6B7280),
              ),
              const SizedBox(width: 8),
              Text(
                label,
                style: TextStyle(
                  fontSize: 14,
                  fontWeight: FontWeight.bold,
                  color: isSelected ? Colors.white : const Color(0xFF6B7280),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildInputField({
    required String label,
    required TextEditingController controller,
    required String hint,
    String? errorText,
  }) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          label,
          style: const TextStyle(
            fontSize: 14,
            fontWeight: FontWeight.w600,
            color: Color(0xFF374151),
          ),
        ),
        const SizedBox(height: 8),
        TextField(
          controller: controller,
          onChanged: (_) => _clearErrorFor(controller),
          style: const TextStyle(fontSize: 15, fontWeight: FontWeight.w500),
          decoration: InputDecoration(
            hintText: hint,
            errorText: errorText,
            hintStyle: const TextStyle(color: Color(0xFF9CA3AF), fontSize: 15),
            filled: true,
            fillColor: const Color(0xFFF9FAFB),
            contentPadding: const EdgeInsets.symmetric(
              horizontal: 16,
              vertical: 16,
            ),
            enabledBorder: OutlineInputBorder(
              borderRadius: BorderRadius.circular(12),
              borderSide: const BorderSide(color: Color(0xFFE5E7EB)),
            ),
            focusedBorder: OutlineInputBorder(
              borderRadius: BorderRadius.circular(12),
              borderSide: const BorderSide(color: Color(0xFF006B70), width: 1),
            ),
            errorBorder: OutlineInputBorder(
              borderRadius: BorderRadius.circular(12),
              borderSide: const BorderSide(color: Colors.redAccent, width: 1),
            ),
            focusedErrorBorder: OutlineInputBorder(
              borderRadius: BorderRadius.circular(12),
              borderSide: const BorderSide(color: Colors.redAccent, width: 1),
            ),
          ),
        ),
      ],
    );
  }

  void _clearErrorFor(TextEditingController controller) {
    if (_nameError == null && _plateError == null && _colorError == null) {
      return;
    }

    setState(() {
      if (controller == _nameController) _nameError = null;
      if (controller == _plateController) _plateError = null;
      if (controller == _colorController) _colorError = null;
    });
  }

  bool _validateForm({
    required String name,
    required String plateNumber,
    required String color,
  }) {
    String? nameError;
    String? plateError;
    String? colorError;

    if (name.length < 2 || name.length > 100) {
      nameError = 'Tên phương tiện phải từ 2 đến 100 ký tự.';
    }

    if (plateNumber.length < 4 || plateNumber.length > 20) {
      plateError = 'Biển số xe phải từ 4 đến 20 ký tự.';
    } else if (!RegExp(r'^[A-Za-z0-9 .-]+$').hasMatch(plateNumber)) {
      plateError =
          'Biển số chỉ được chứa chữ cái, chữ số, dấu chấm, khoảng trắng và gạch ngang.';
    }

    if (color.length > 30) {
      colorError = 'Màu sắc không được vượt quá 30 ký tự.';
    }

    setState(() {
      _nameError = nameError;
      _plateError = plateError;
      _colorError = colorError;
    });

    return nameError == null && plateError == null && colorError == null;
  }
}

