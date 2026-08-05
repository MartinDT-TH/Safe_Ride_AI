import 'package:flutter/material.dart';
import '../../../../../core/localization/localization_extensions.dart';
import 'package:flutter/services.dart';

import '../../data/models/vehicle_model.dart';

class VehicleFormSheet extends StatefulWidget {
  final VehicleModel? vehicle;
  final Future<bool> Function(VehicleModel) onSave;

  VehicleFormSheet({super.key, this.vehicle, required this.onSave});

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

  late TextEditingController _engineCapacityController;
  late TextEditingController _plateController;
  late TextEditingController _colorController;
  bool _isSaving = false;
  String? _nameError;
  String? _engineCapacityError;
  String? _plateError;
  String? _colorError;

  @override
  void initState() {
    super.initState();
    _selectedType = widget.vehicle?.type ?? VehicleType.motorbike;
    _nameController = TextEditingController(text: widget.vehicle?.name ?? '');
    _engineCapacityController = TextEditingController(
      text: widget.vehicle?.engineCapacityCc?.toString() ?? '',
    );
    _plateController = TextEditingController(
      text: widget.vehicle?.plateNumber ?? '',
    );
    _colorController = TextEditingController(text: widget.vehicle?.color ?? '');
  }

  @override
  void dispose() {
    _nameController.dispose();
    _engineCapacityController.dispose();
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
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.vertical(top: Radius.circular(28)),
      ),
      padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 20),
      child: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Text(
                  isEdit ? context.l10n.editVehicle : context.l10n.addVehicle,
                  style: TextStyle(
                    fontSize: 18,
                    fontWeight: FontWeight.bold,
                    color: textColor,
                  ),
                ),
                IconButton(
                  onPressed: () => Navigator.pop(context),
                  icon: Icon(Icons.close, color: textColor, size: 24),
                  splashRadius: 20,
                ),
              ],
            ),
            SizedBox(height: 8),
            Divider(height: 1, color: Color(0xFFF3F4F6)),
            SizedBox(height: 24),

            Text(
              context.l10n.vehicleType,
              style: TextStyle(
                fontSize: 14,
                fontWeight: FontWeight.bold,
                color: textColor,
              ),
            ),
            SizedBox(height: 12),
            Container(
              padding: const EdgeInsets.all(4),
              decoration: BoxDecoration(
                color: Color(0xFFF3F4F6),
                borderRadius: BorderRadius.circular(12),
              ),
              child: Row(
                children: [
                  _buildTypeButton(
                    context.l10n.motorbike,
                    Icons.directions_bike_rounded,
                    VehicleType.motorbike,
                  ),
                  _buildTypeButton(
                    context.l10n.car,
                    Icons.directions_car_rounded,
                    VehicleType.car,
                  ),
                ],
              ),
            ),
            SizedBox(height: 24),

            _buildInputField(
              label: context.l10n.vehicleName,
              controller: _nameController,
              hint: context.l10n.vehicleNameHint,
              errorText: _nameError,
            ),
            SizedBox(height: 20),
            if (_selectedType == VehicleType.motorbike) ...[
              _buildInputField(
                label: context.l10n.engineCapacity,
                controller: _engineCapacityController,
                hint: context.l10n.engineCapacityHint,
                errorText: _engineCapacityError,
                keyboardType: TextInputType.number,
                inputFormatters: [FilteringTextInputFormatter.digitsOnly],
              ),
              SizedBox(height: 20),
            ],
            _buildInputField(
              label: context.l10n.licensePlate,
              controller: _plateController,
              hint: _selectedType == VehicleType.motorbike
                  ? '74-F1 123.21'
                  : '74A 543.67',
              errorText: _plateError,
              textCapitalization: TextCapitalization.characters,
              inputFormatters: [
                _LicensePlateInputFormatter(_selectedType),
              ],
            ),
            SizedBox(height: 20),
            _buildInputField(
              label: context.l10n.color,
              controller: _colorController,
              hint: context.l10n.colorHint,
              errorText: _colorError,
            ),

            SizedBox(height: 32),

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
                    ? SizedBox(
                        width: 22,
                        height: 22,
                        child: CircularProgressIndicator(
                          strokeWidth: 2,
                          color: Colors.white,
                        ),
                      )
                    : Text(
                        isEdit
                            ? context.l10n.saveChanges
                            : context.l10n.saveVehicle,
                        style: TextStyle(
                          fontSize: 16,
                          fontWeight: FontWeight.bold,
                        ),
                      ),
              ),
            ),
            SizedBox(height: 8),
            Center(
              child: TextButton(
                onPressed: _isSaving ? null : () => Navigator.pop(context),
                child: Text(
                  context.l10n.cancel,
                  style: TextStyle(
                    color: tealColor,
                    fontWeight: FontWeight.bold,
                    fontSize: 16,
                  ),
                ),
              ),
            ),
            SizedBox(height: 12),
          ],
        ),
      ),
    );
  }

  Future<void> _saveVehicle() async {
    final name = _nameController.text.trim();
    final engineCapacityText = _engineCapacityController.text.trim();
    final plateNumber = _plateController.text.trim();
    final color = _colorController.text.trim();

    if (!_validateForm(
      name: name,
      engineCapacityText: engineCapacityText,
      plateNumber: plateNumber,
      color: color,
    )) {
      return;
    }

    setState(() => _isSaving = true);
    final vehicle = VehicleModel(
      id: widget.vehicle?.id ?? 0,
      name: name,
      plateNumber: _normalizePlateNumber(plateNumber, _selectedType)!,
      color: color,
      type: _selectedType,
      engineCapacityCc: _selectedType == VehicleType.motorbike
          ? int.parse(engineCapacityText)
          : null,
      requiredLicenseClass: widget.vehicle?.requiredLicenseClass ?? '',
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
        onTap: () => setState(() {
          _selectedType = type;
          _engineCapacityError = null;
          _plateError = null;
          final normalized = _formatPartialPlateNumber(
            _plateController.text,
            type,
          );
          _plateController.value = TextEditingValue(
            text: normalized,
            selection: TextSelection.collapsed(offset: normalized.length),
          );
        }),
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
                color: isSelected ? Colors.white : Color(0xFF6B7280),
              ),
              SizedBox(width: 8),
              Text(
                label,
                style: TextStyle(
                  fontSize: 14,
                  fontWeight: FontWeight.bold,
                  color: isSelected ? Colors.white : Color(0xFF6B7280),
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
    TextInputType? keyboardType,
    List<TextInputFormatter>? inputFormatters,
    TextCapitalization textCapitalization = TextCapitalization.none,
  }) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          label,
          style: TextStyle(
            fontSize: 14,
            fontWeight: FontWeight.w600,
            color: Color(0xFF374151),
          ),
        ),
        SizedBox(height: 8),
        TextField(
          controller: controller,
          keyboardType: keyboardType,
          inputFormatters: inputFormatters,
          textCapitalization: textCapitalization,
          onChanged: (_) => _clearErrorFor(controller),
          style: TextStyle(fontSize: 15, fontWeight: FontWeight.w500),
          decoration: InputDecoration(
            hintText: hint,
            errorText: errorText,
            hintStyle: TextStyle(color: Color(0xFF9CA3AF), fontSize: 15),
            filled: true,
            fillColor: Color(0xFFF9FAFB),
            contentPadding: const EdgeInsets.symmetric(
              horizontal: 16,
              vertical: 16,
            ),
            enabledBorder: OutlineInputBorder(
              borderRadius: BorderRadius.circular(12),
              borderSide: BorderSide(color: Color(0xFFE5E7EB)),
            ),
            focusedBorder: OutlineInputBorder(
              borderRadius: BorderRadius.circular(12),
              borderSide: BorderSide(color: Color(0xFF006B70), width: 1),
            ),
            errorBorder: OutlineInputBorder(
              borderRadius: BorderRadius.circular(12),
              borderSide: BorderSide(color: Colors.redAccent, width: 1),
            ),
            focusedErrorBorder: OutlineInputBorder(
              borderRadius: BorderRadius.circular(12),
              borderSide: BorderSide(color: Colors.redAccent, width: 1),
            ),
          ),
        ),
      ],
    );
  }

  void _clearErrorFor(TextEditingController controller) {
    if (_nameError == null &&
        _engineCapacityError == null &&
        _plateError == null &&
        _colorError == null) {
      return;
    }

    setState(() {
      if (controller == _nameController) _nameError = null;
      if (controller == _engineCapacityController) {
        _engineCapacityError = null;
      }
      if (controller == _plateController) _plateError = null;
      if (controller == _colorController) _colorError = null;
    });
  }

  bool _validateForm({
    required String name,
    required String engineCapacityText,
    required String plateNumber,
    required String color,
  }) {
    String? nameError;
    String? engineCapacityError;
    String? plateError;
    String? colorError;

    if (name.length < 2 || name.length > 100) {
      nameError = context.l10n.vehicleNameValidation;
    }

    if (_selectedType == VehicleType.motorbike) {
      final engineCapacity = int.tryParse(engineCapacityText);
      if (engineCapacity == null || engineCapacity <= 0) {
        engineCapacityError = context.l10n.engineCapacityValidation;
      }
    }

    if (_normalizePlateNumber(plateNumber, _selectedType) == null) {
      final example = _selectedType == VehicleType.motorbike
          ? '74-F1 123.21'
          : '74A 543.67';
      plateError = '${context.l10n.licensePlateFormatValidation} ($example)';
    }

    if (color.length > 30) {
      colorError = context.l10n.colorValidation;
    }

    setState(() {
      _nameError = nameError;
      _engineCapacityError = engineCapacityError;
      _plateError = plateError;
      _colorError = colorError;
    });

    return nameError == null &&
        engineCapacityError == null &&
        plateError == null &&
        colorError == null;
  }
}

String? _normalizePlateNumber(String value, VehicleType type) {
  final compact = value.toUpperCase().replaceAll(RegExp(r'[\s.\-]'), '');
  final pattern = type == VehicleType.motorbike
      ? RegExp(r'^(\d{2})([A-Z](?:[A-Z]|\d))(\d{5})$')
      : RegExp(r'^(\d{2})([A-Z])(\d{5})$');
  final match = pattern.firstMatch(compact);
  if (match == null) return null;
  final province = match.group(1)!;
  final series = match.group(2)!;
  final sequence = match.group(3)!;
  return type == VehicleType.motorbike
      ? '$province-$series ${sequence.substring(0, 3)}.${sequence.substring(3)}'
      : '$province$series ${sequence.substring(0, 3)}.${sequence.substring(3)}';
}

String _formatPartialPlateNumber(String value, VehicleType type) {
  final maxLength = type == VehicleType.motorbike ? 9 : 8;
  var compact = value
      .toUpperCase()
      .replaceAll(RegExp(r'[^A-Z0-9]'), '');
  if (compact.length > maxLength) compact = compact.substring(0, maxLength);
  if (compact.length <= 2) return compact;

  final seriesLength = type == VehicleType.motorbike ? 2 : 1;
  final seriesEnd = 2 + seriesLength;
  final availableSeriesEnd = compact.length < seriesEnd
      ? compact.length
      : seriesEnd;
  final prefix = type == VehicleType.motorbike
      ? '${compact.substring(0, 2)}-${compact.substring(2, availableSeriesEnd)}'
      : compact.substring(0, availableSeriesEnd);
  if (compact.length <= seriesEnd) return prefix;

  final sequence = compact.substring(seriesEnd);
  final firstEnd = sequence.length < 3 ? sequence.length : 3;
  final first = sequence.substring(0, firstEnd);
  final last = sequence.length > 3 ? '.${sequence.substring(3)}' : '';
  return '$prefix $first$last';
}

class _LicensePlateInputFormatter extends TextInputFormatter {
  _LicensePlateInputFormatter(this.type);

  final VehicleType type;

  @override
  TextEditingValue formatEditUpdate(
    TextEditingValue oldValue,
    TextEditingValue newValue,
  ) {
    final formatted = _formatPartialPlateNumber(newValue.text, type);
    return TextEditingValue(
      text: formatted,
      selection: TextSelection.collapsed(offset: formatted.length),
    );
  }
}
