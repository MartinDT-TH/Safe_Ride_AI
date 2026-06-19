import 'package:flutter/material.dart';
import 'package:google_maps_flutter/google_maps_flutter.dart';
import 'package:provider/provider.dart';

import '../../../../../core/config/api_keys_config.dart';
import '../../../../../core/constants/app_colors.dart';
import '../../data/models/booking_location.dart';
import '../providers/booking_provider.dart';

enum LocationPickerType { pickup, destination }

class LocationPickerPage extends StatefulWidget {
  const LocationPickerPage({
    super.key,
    required this.type,
    this.initialLocation,
    this.initialCameraLocation,
  });

  final LocationPickerType type;
  final BookingLocation? initialLocation;
  final BookingLocation? initialCameraLocation;

  @override
  State<LocationPickerPage> createState() => _LocationPickerPageState();
}

class _LocationPickerPageState extends State<LocationPickerPage> {
  static const _defaultPosition = LatLng(10.7769, 106.7009);

  final _searchController = TextEditingController();
  GoogleMapController? _mapController;
  BookingLocation? _selectedLocation;

  bool get _isPickup => widget.type == LocationPickerType.pickup;

  @override
  void initState() {
    super.initState();
    _selectedLocation = widget.initialLocation;
    _searchController.text = widget.initialLocation?.address ?? '';
  }

  @override
  void dispose() {
    _searchController.dispose();
    _mapController?.dispose();
    super.dispose();
  }

  Future<void> _search() async {
    FocusScope.of(context).unfocus();
    final location = await context.read<BookingProvider>().resolveAddress(
      _searchController.text,
    );
    if (!mounted || location == null) return;

    setState(() {
      _selectedLocation = location;
      _searchController.text = location.address;
    });
    await _moveCamera(location);
  }

  Future<void> _selectMapPosition(LatLng position) async {
    setState(() {
      _selectedLocation = BookingLocation(
        address: 'Đang xác định địa chỉ...',
        latitude: position.latitude,
        longitude: position.longitude,
      );
    });

    final location = await context.read<BookingProvider>().resolveCoordinates(
      position.latitude,
      position.longitude,
    );
    if (!mounted || location == null) return;

    setState(() {
      _selectedLocation = location;
      _searchController.text = location.address;
    });
  }

  Future<void> _useCurrentLocation() async {
    final location = await context.read<BookingProvider>().getCurrentLocation().timeout(
          const Duration(seconds: 12),
          onTimeout: () => null,
        );
    if (!mounted || location == null) return;

    setState(() {
      _selectedLocation = location;
      _searchController.text = location.address;
    });
    await _moveCamera(location);
  }

  Future<void> _moveCamera(BookingLocation location) async {
    await _mapController?.animateCamera(
      CameraUpdate.newLatLngZoom(
        LatLng(location.latitude, location.longitude),
        16,
      ),
    );
  }

  void _confirm() {
    final location = _selectedLocation;
    if (location == null) return;
    Navigator.of(context).pop(location);
  }

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<BookingProvider>();
    final selected = _selectedLocation;
    final cameraLocation = selected ?? widget.initialCameraLocation;
    final initial = cameraLocation == null
        ? _defaultPosition
        : LatLng(cameraLocation.latitude, cameraLocation.longitude);
    final color = _isPickup ? AppColors.primary : const Color(0xFFD71920);

    return Scaffold(
      body: Column(
        children: [
          Expanded(
            child: Stack(
              children: [
                Positioned.fill(
                  child: ApiKeysConfig.hasGoogleMapsKey
                      ? GoogleMap(
                          initialCameraPosition: CameraPosition(
                            target: initial,
                            zoom: cameraLocation == null ? 12 : 16,
                          ),
                          markers: selected == null
                              ? {}
                              : {
                                  Marker(
                                    markerId: const MarkerId(
                                      'selected-location',
                                    ),
                                    position: initial,
                                    icon: BitmapDescriptor.defaultMarkerWithHue(
                                      _isPickup
                                          ? BitmapDescriptor.hueAzure
                                          : BitmapDescriptor.hueRed,
                                    ),
                                  ),
                                },
                          onTap: provider.isLoading ? null : _selectMapPosition,
                          onMapCreated: (controller) {
                            _mapController = controller;
                          },
                          myLocationButtonEnabled: false,
                          zoomControlsEnabled: false,
                          mapToolbarEnabled: false,
                        )
                      : const ColoredBox(
                          color: Color(0xFFE9EEEE),
                          child: Center(
                            child: Text(
                              'Thiếu cấu hình Google Maps.',
                              textAlign: TextAlign.center,
                            ),
                          ),
                        ),
                ),
                SafeArea(
                  child: Padding(
                    padding: const EdgeInsets.fromLTRB(16, 12, 16, 0),
                    child: Row(
                      children: [
                        Material(
                          color: Colors.white,
                          shape: const CircleBorder(),
                          elevation: 3,
                          child: IconButton(
                            onPressed: () => Navigator.pop(context),
                            icon: const Icon(Icons.arrow_back),
                          ),
                        ),
                        const SizedBox(width: 10),
                        Expanded(
                          child: Material(
                            color: Colors.white,
                            elevation: 3,
                            borderRadius: BorderRadius.circular(12),
                            child: TextField(
                              controller: _searchController,
                              textInputAction: TextInputAction.search,
                              onSubmitted: (_) => _search(),
                              decoration: InputDecoration(
                                hintText: _isPickup
                                    ? 'Tìm điểm đón'
                                    : 'Tìm điểm đến',
                                prefixIcon: Icon(Icons.search, color: color),
                                suffixIcon: IconButton(
                                  onPressed: provider.isLoading
                                      ? null
                                      : _search,
                                  icon: const Icon(Icons.arrow_forward),
                                ),
                                border: InputBorder.none,
                                contentPadding: const EdgeInsets.symmetric(
                                  vertical: 16,
                                ),
                              ),
                            ),
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
                Positioned(
                  right: 16,
                  bottom: 16,
                  child: FloatingActionButton.small(
                    heroTag: 'current-location',
                    onPressed: provider.isLoading ? null : _useCurrentLocation,
                    backgroundColor: Colors.white,
                    foregroundColor: AppColors.primary,
                    child: const Icon(Icons.my_location),
                  ),
                ),
              ],
            ),
          ),
          SafeArea(
            top: false,
            child: Container(
              width: double.infinity,
              padding: const EdgeInsets.fromLTRB(20, 14, 20, 16),
              decoration: const BoxDecoration(
                color: Colors.white,
                boxShadow: [
                  BoxShadow(
                    color: Color(0x26000000),
                    blurRadius: 16,
                    offset: Offset(0, -4),
                  ),
                ],
              ),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    _isPickup ? 'Điểm đón đã chọn' : 'Điểm đến đã chọn',
                    style: const TextStyle(
                      fontSize: 13,
                      fontWeight: FontWeight.w700,
                      color: Color(0xFF667174),
                    ),
                  ),
                  const SizedBox(height: 8),
                  Row(
                    children: [
                      Icon(
                        _isPickup ? Icons.my_location : Icons.location_on,
                        color: color,
                      ),
                      const SizedBox(width: 10),
                      Expanded(
                        child: Text(
                          selected?.address ??
                              'Tìm kiếm hoặc chạm vào bản đồ để chọn.',
                          maxLines: 2,
                          overflow: TextOverflow.ellipsis,
                          style: const TextStyle(
                            fontSize: 16,
                            fontWeight: FontWeight.w600,
                          ),
                        ),
                      ),
                    ],
                  ),
                  if (provider.locationErrorMessage != null) ...[
                    const SizedBox(height: 8),
                    Text(
                      provider.locationErrorMessage!,
                      style: const TextStyle(color: Colors.red),
                    ),
                  ],
                  const SizedBox(height: 14),
                  SizedBox(
                    width: double.infinity,
                    height: 52,
                    child: FilledButton(
                      onPressed: selected == null || provider.isLoading
                          ? null
                          : _confirm,
                      style: FilledButton.styleFrom(backgroundColor: color),
                      child: provider.isLoading
                          ? const SizedBox.square(
                              dimension: 22,
                              child: CircularProgressIndicator(
                                color: Colors.white,
                                strokeWidth: 2,
                              ),
                            )
                          : Text(
                              _isPickup
                                  ? 'Xác nhận điểm đón'
                                  : 'Xác nhận điểm đến',
                            ),
                    ),
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}
