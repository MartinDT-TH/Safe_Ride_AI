import 'dart:async';
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../../../core/constants/app_colors.dart';
import '../../../../../core/maps/models/map_models.dart';
import '../../../../../core/maps/models/map_api_models.dart';
import '../../../../../core/maps/widgets/map_renderer_widget.dart';
import '../../../../../core/widgets/current_location_button.dart';
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
  static const _defaultPosition = AppLatLng(10.7769, 106.7009);

  final _searchController = TextEditingController();
  AppMapController? _mapController;
  BookingLocation? _selectedLocation;

  List<PlaceAutocompleteResult> _suggestions = [];
  Timer? _debounceTimer;
  bool _isSearching = false;
  bool _ignoreSearchChanges = false;

  bool get _isPickup => widget.type == LocationPickerType.pickup;

  @override
  void initState() {
    super.initState();
    _selectedLocation = widget.initialLocation;
    _searchController.text = widget.initialLocation?.address ?? '';
    _searchController.addListener(_onSearchChanged);
  }

  @override
  void dispose() {
    _debounceTimer?.cancel();
    _searchController.removeListener(_onSearchChanged);
    _searchController.dispose();
    _mapController?.dispose();
    super.dispose();
  }

  void _onSearchChanged() {
    if (_ignoreSearchChanges) return;

    final query = _searchController.text;
    if (query.isEmpty) {
      setState(() {
        _suggestions = [];
      });
      return;
    }

    if (_debounceTimer?.isActive ?? false) _debounceTimer!.cancel();
    _debounceTimer = Timer(const Duration(milliseconds: 500), () async {
      setState(() => _isSearching = true);
      final focus = _autocompleteFocus;
      final results = await context.read<BookingProvider>().autocompleteAddress(
        query,
        lat: focus.latitude,
        lng: focus.longitude,
      );
      if (mounted && _searchController.text == query) {
        setState(() {
          _suggestions = results;
          _isSearching = false;
        });
      }
    });
  }

  AppLatLng get _autocompleteFocus {
    final selected = _selectedLocation;
    if (selected != null) {
      return AppLatLng(selected.latitude, selected.longitude);
    }

    final cameraLocation =
        widget.initialCameraLocation ?? widget.initialLocation;
    if (cameraLocation != null) {
      return AppLatLng(cameraLocation.latitude, cameraLocation.longitude);
    }

    return _defaultPosition;
  }

  Future<void> _selectSuggestion(PlaceAutocompleteResult suggestion) async {
    FocusScope.of(context).unfocus();
    setState(() {
      _suggestions = [];
      _ignoreSearchChanges = true;
      _searchController.text = suggestion.primaryText;
      _ignoreSearchChanges = false;
    });

    final location = await context.read<BookingProvider>().resolvePlaceId(
      suggestion.providerPlaceId,
    );
    if (!mounted || location == null) return;

    setState(() {
      _selectedLocation = location;
      _ignoreSearchChanges = true;
      _searchController.text = location.address;
      _ignoreSearchChanges = false;
    });
    await _moveCamera(location);
  }

  Future<void> _search() async {
    FocusScope.of(context).unfocus();
    setState(() => _suggestions = []);
    final location = await context.read<BookingProvider>().resolveAddress(
      _searchController.text,
    );
    if (!mounted || location == null) return;

    setState(() {
      _selectedLocation = location;
      _ignoreSearchChanges = true;
      _searchController.text = location.address;
      _ignoreSearchChanges = false;
    });
    await _moveCamera(location);
  }

  Future<void> _selectMapPosition(AppLatLng position) async {
    setState(() {
      _suggestions = [];
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
      _ignoreSearchChanges = true;
      _searchController.text = location.address;
      _ignoreSearchChanges = false;
    });
  }

  Future<void> _useCurrentLocation() async {
    setState(() => _suggestions = []);
    final location = await context
        .read<BookingProvider>()
        .getCurrentLocation();
    if (!mounted) return;
    
    if (location == null) {
      final error = context.read<BookingProvider>().locationErrorMessage;
      if (error != null) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(error)),
        );
      }
      return;
    }

    setState(() {
      _selectedLocation = location;
      _ignoreSearchChanges = true;
      _searchController.text = location.address;
      _ignoreSearchChanges = false;
    });
    await _moveCamera(location);
  }

  Future<void> _moveCamera(BookingLocation location) async {
    await _mapController?.animateCamera(
      AppCameraPosition(
        target: AppLatLng(location.latitude, location.longitude),
        zoom: 16,
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
        : AppLatLng(cameraLocation.latitude, cameraLocation.longitude);
    final color = _isPickup ? AppColors.primary : const Color(0xFFD71920);

    return Scaffold(
      body: Column(
        children: [
          Expanded(
            child: Stack(
              children: [
                Positioned.fill(
                  child: MapRendererWidget(
                    initialCameraPosition: AppCameraPosition(
                      target: initial,
                      zoom: cameraLocation == null ? 12 : 16,
                    ),
                    markers: selected == null
                        ? {}
                        : {
                            AppMarker(
                              id: 'selected-location',
                              position: initial,
                              hue: _isPickup
                                  ? 210.0
                                  : 0.0, // azure = ~210, red = 0
                            ),
                          },
                    onTap: provider.isLoading ? null : _selectMapPosition,
                    onMapCreated: (controller) {
                      _mapController = controller;
                    },
                    myLocationButtonEnabled: false,
                  ),
                ),
                SafeArea(
                  child: Padding(
                    padding: const EdgeInsets.fromLTRB(16, 12, 16, 0),
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Row(
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
                                  // enableSuggestions: false, // <-- Tắt gợi ý
                                  // autocorrect: false,        // <-- Tắt tự động sửa lỗi
                                  // autofillHints: const [],   // <-- Tắt lưu trữ tự động điền của OS
                                  decoration: InputDecoration(
                                    hintText: _isPickup
                                        ? 'Tìm điểm đón'
                                        : 'Tìm điểm đến',
                                    prefixIcon: Icon(
                                      Icons.search,
                                      color: color,
                                    ),
                                    suffixIcon: _isSearching
                                        ? const Padding(
                                            padding: EdgeInsets.all(12.0),
                                            child: SizedBox.square(
                                              dimension: 20,
                                              child: CircularProgressIndicator(
                                                strokeWidth: 2,
                                              ),
                                            ),
                                          )
                                        : IconButton(
                                            onPressed: provider.isLoading
                                                ? null
                                                : _search,
                                            icon: const Icon(
                                              Icons.arrow_forward,
                                            ),
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
                        if (_suggestions.isNotEmpty) ...[
                          const SizedBox(height: 8),
                          Material(
                            elevation: 4,
                            borderRadius: BorderRadius.circular(12),
                            color: Colors.white,
                            child: ConstrainedBox(
                              constraints: const BoxConstraints(maxHeight: 280),
                              child: ListView.separated(
                                padding: EdgeInsets.zero,
                                shrinkWrap: true,
                                itemCount: _suggestions.length,
                                separatorBuilder: (_, __) =>
                                    const Divider(height: 1),
                                itemBuilder: (context, index) {
                                  final s = _suggestions[index];
                                  return ListTile(
                                    leading: const Icon(
                                      Icons.location_on,
                                      color: Colors.grey,
                                    ),
                                    title: Text(s.primaryText),
                                    subtitle: s.secondaryText.isNotEmpty
                                        ? Text(s.secondaryText)
                                        : null,
                                    onTap: () => _selectSuggestion(s),
                                  );
                                },
                              ),
                            ),
                          ),
                        ],
                      ],
                    ),
                  ),
                ),
                Positioned(
                  right: 16,
                  bottom: 16,
                  child: CurrentLocationButton(
                    onPressed: _useCurrentLocation,
                    isLoading: provider.isLoading,
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
