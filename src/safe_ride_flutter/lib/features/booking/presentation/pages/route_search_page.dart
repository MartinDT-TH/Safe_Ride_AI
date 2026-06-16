import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../../core/constants/app_colors.dart';
import '../../../../core/constants/app_strings.dart';
import '../../data/models/booking_location.dart';
import '../../data/models/create_booking_request.dart';
import '../providers/booking_provider.dart';
import 'booking_options_page.dart';
import 'location_picker_page.dart';

class RouteSearchPage extends StatefulWidget {
  const RouteSearchPage({super.key, required this.bookingType});

  final BookingType bookingType;

  @override
  State<RouteSearchPage> createState() => _RouteSearchPageState();
}

class _RouteSearchPageState extends State<RouteSearchPage> {
  BookingLocation? _pickup;
  BookingLocation? _destination;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _useCurrentLocation());
  }

  Future<void> _useCurrentLocation() async {
    final location = await context.read<BookingProvider>().getCurrentLocation();
    if (!mounted || location == null) return;
    setState(() => _pickup = location);
  }

  Future<void> _pickLocation(LocationPickerType type) async {
    final selected = type == LocationPickerType.pickup ? _pickup : _destination;
    final cameraLocation = selected ?? _pickup;
    final location = await Navigator.of(context).push<BookingLocation>(
      MaterialPageRoute(
        builder: (_) => LocationPickerPage(
          type: type,
          initialLocation: selected,
          initialCameraLocation: cameraLocation,
        ),
      ),
    );
    if (!mounted || location == null) return;

    setState(() {
      if (type == LocationPickerType.pickup) {
        _pickup = location;
      } else {
        _destination = location;
      }
    });
  }

  void _continue() {
    final pickup = _pickup;
    final destination = _destination;
    if (pickup == null || destination == null) return;

    Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => BookingOptionsPage(
          bookingType: widget.bookingType,
          pickup: pickup,
          destination: destination,
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<BookingProvider>();

    return Scaffold(
      backgroundColor: const Color(0xFFFCFAFA),
      appBar: AppBar(
        title: const Text(
          BookingStrings.routeSearch,
          style: TextStyle(
            color: Color(0xFF252525),
            fontWeight: FontWeight.w700,
          ),
        ),
        leading: IconButton(
          onPressed: () => Navigator.pop(context),
          icon: const Icon(Icons.arrow_back, color: Color(0xFF252525)),
        ),
      ),
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.fromLTRB(20, 18, 20, 24),
          child: Column(
            children: [
              _LocationPickerField(
                color: AppColors.primary,
                icon: Icons.my_location,
                label: 'Điểm đón',
                value: _pickup?.address,
                placeholder: BookingStrings.locatingCurrentPosition,
                onTap: () => _pickLocation(LocationPickerType.pickup),
              ),
              const SizedBox(height: 14),
              _LocationPickerField(
                color: const Color(0xFFD71920),
                icon: Icons.location_on,
                label: 'Điểm đến',
                value: _destination?.address,
                placeholder: 'Chọn điểm đến trên bản đồ',
                onTap: () => _pickLocation(LocationPickerType.destination),
              ),
              if (provider.errorMessage != null) ...[
                const SizedBox(height: 12),
                Text(
                  provider.errorMessage!,
                  style: const TextStyle(color: Colors.red),
                ),
              ],
              const SizedBox(height: 22),
              const _FlowHint(),
              const Spacer(),
              SizedBox(
                width: double.infinity,
                height: 54,
                child: FilledButton.icon(
                  onPressed:
                      _pickup != null &&
                          _destination != null &&
                          !provider.isLoading
                      ? _continue
                      : null,
                  style: FilledButton.styleFrom(
                    backgroundColor: AppColors.primary,
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(14),
                    ),
                  ),
                  icon: const Icon(Icons.route),
                  label: const Text(
                    'Tiếp tục chọn dịch vụ',
                    style: TextStyle(fontWeight: FontWeight.w700),
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _LocationPickerField extends StatelessWidget {
  const _LocationPickerField({
    required this.color,
    required this.icon,
    required this.label,
    required this.value,
    required this.placeholder,
    required this.onTap,
  });

  final Color color;
  final IconData icon;
  final String label;
  final String? value;
  final String placeholder;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(14),
      child: Container(
        padding: const EdgeInsets.all(14),
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(14),
          border: Border.all(color: const Color(0xFFCAD6D8)),
        ),
        child: Row(
          children: [
            Container(
              width: 42,
              height: 42,
              decoration: BoxDecoration(
                shape: BoxShape.circle,
                border: Border.all(color: color, width: 2),
              ),
              child: Icon(icon, color: color),
            ),
            const SizedBox(width: 14),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    label.toUpperCase(),
                    style: const TextStyle(
                      color: Color(0xFF667174),
                      fontSize: 12,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    value ?? placeholder,
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                    style: TextStyle(
                      color: value == null
                          ? const Color(0xFF8B9496)
                          : const Color(0xFF252525),
                      fontSize: 15,
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                ],
              ),
            ),
            const Icon(Icons.chevron_right),
          ],
        ),
      ),
    );
  }
}

class _FlowHint extends StatelessWidget {
  const _FlowHint();

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: const Color(0xFFEAF4F4),
        borderRadius: BorderRadius.circular(14),
      ),
      child: const Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(Icons.map_outlined, color: AppColors.primary),
          SizedBox(width: 12),
          Expanded(
            child: Text(
              'Chọn từng điểm bằng cách tìm địa chỉ hoặc chạm trực tiếp '
              'lên bản đồ, sau đó xác nhận vị trí.',
              style: TextStyle(height: 1.4),
            ),
          ),
        ],
      ),
    );
  }
}
