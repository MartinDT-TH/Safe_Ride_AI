import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../../core/constants/app_colors.dart';
import '../../../../core/constants/app_strings.dart';
import '../../data/models/booking_location.dart';
import '../../data/models/create_booking_request.dart';
import '../providers/booking_provider.dart';
import 'booking_options_page.dart';

class RouteSearchPage extends StatefulWidget {
  const RouteSearchPage({super.key, required this.bookingType});

  final BookingType bookingType;

  @override
  State<RouteSearchPage> createState() => _RouteSearchPageState();
}

class _RouteSearchPageState extends State<RouteSearchPage> {
  final _destinationController = TextEditingController();
  BookingLocation? _pickup;

  static const _recentLocations = [
    BookingLocation(
      address: BookingStrings.airportAddress,
      latitude: 10.818797,
      longitude: 106.651856,
    ),
    BookingLocation(
      address: BookingStrings.recentFullAddress,
      latitude: 10.729524,
      longitude: 106.701716,
    ),
    BookingLocation(
      address: BookingStrings.officeAddress,
      latitude: 10.771767,
      longitude: 106.704467,
    ),
  ];

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _useCurrentLocation());
  }

  @override
  void dispose() {
    _destinationController.dispose();
    super.dispose();
  }

  Future<void> _useCurrentLocation() async {
    final location = await context.read<BookingProvider>().getCurrentLocation();
    if (!mounted || location == null) return;
    setState(() => _pickup = location);
  }

  Future<void> _searchDestination() async {
    final provider = context.read<BookingProvider>();
    if (_pickup == null) {
      await _useCurrentLocation();
      if (!mounted || _pickup == null) return;
    }

    final destination = await provider.resolveAddress(
      _destinationController.text,
    );
    if (!mounted || destination == null) return;
    _openOptions(destination);
  }

  void _openOptions(BookingLocation destination) {
    final pickup = _pickup;
    if (pickup == null) return;

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
        child: Column(
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(20, 18, 20, 16),
              child: Column(
                children: [
                  _LocationInput(
                    color: AppColors.primary,
                    icon: Icons.my_location,
                    child: Text(
                      _pickup?.address ??
                          BookingStrings.locatingCurrentPosition,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(fontSize: 16),
                    ),
                  ),
                  const SizedBox(height: 14),
                  _LocationInput(
                    color: const Color(0xFFD71920),
                    icon: Icons.location_on,
                    child: TextField(
                      controller: _destinationController,
                      textInputAction: TextInputAction.search,
                      onSubmitted: (_) => _searchDestination(),
                      decoration: const InputDecoration(
                        hintText: BookingStrings.destination,
                        border: InputBorder.none,
                        isDense: true,
                      ),
                    ),
                  ),
                  const SizedBox(height: 16),
                  SizedBox(
                    width: double.infinity,
                    height: 52,
                    child: FilledButton.icon(
                      onPressed: provider.isLoading ? null : _searchDestination,
                      style: FilledButton.styleFrom(
                        backgroundColor: const Color(0xFFDDE8E9),
                        foregroundColor: AppColors.primary,
                        shape: RoundedRectangleBorder(
                          borderRadius: BorderRadius.circular(28),
                        ),
                      ),
                      icon: provider.isLoading
                          ? const SizedBox.square(
                              dimension: 20,
                              child: CircularProgressIndicator(strokeWidth: 2),
                            )
                          : const Icon(Icons.search),
                      label: const Text(
                        BookingStrings.searchDestination,
                        style: TextStyle(fontWeight: FontWeight.w700),
                      ),
                    ),
                  ),
                  if (provider.errorMessage != null) ...[
                    const SizedBox(height: 10),
                    Text(
                      provider.errorMessage!,
                      style: const TextStyle(color: Colors.red),
                    ),
                  ],
                ],
              ),
            ),
            const Divider(height: 8, thickness: 8, color: Color(0xFFF0EEEE)),
            Expanded(
              child: ListView(
                padding: const EdgeInsets.fromLTRB(20, 22, 20, 24),
                children: [
                  const Text(
                    BookingStrings.locationHistory,
                    style: TextStyle(
                      color: Color(0xFF5F6466),
                      fontWeight: FontWeight.w700,
                      letterSpacing: .6,
                    ),
                  ),
                  const SizedBox(height: 12),
                  _RecentLocationTile(
                    icon: Icons.flight,
                    title: BookingStrings.airport,
                    subtitle: _recentLocations[0].address,
                    onTap: () => _openOptions(_recentLocations[0]),
                  ),
                  _RecentLocationTile(
                    icon: Icons.location_on_outlined,
                    title: BookingStrings.recentAddress,
                    subtitle: _recentLocations[1].address,
                    onTap: () => _openOptions(_recentLocations[1]),
                  ),
                  _RecentLocationTile(
                    icon: Icons.work_outline,
                    title: BookingStrings.office,
                    subtitle: _recentLocations[2].address,
                    onTap: () => _openOptions(_recentLocations[2]),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _LocationInput extends StatelessWidget {
  const _LocationInput({
    required this.color,
    required this.icon,
    required this.child,
  });

  final Color color;
  final IconData icon;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Container(
          width: 38,
          height: 38,
          decoration: BoxDecoration(
            shape: BoxShape.circle,
            border: Border.all(color: color, width: 2),
          ),
          child: Icon(icon, color: color, size: 20),
        ),
        const SizedBox(width: 12),
        Expanded(
          child: Container(
            height: 54,
            alignment: Alignment.centerLeft,
            padding: const EdgeInsets.symmetric(horizontal: 16),
            decoration: BoxDecoration(
              color: Colors.white,
              borderRadius: BorderRadius.circular(10),
              border: Border.all(color: const Color(0xFFCAD6D8)),
            ),
            child: child,
          ),
        ),
      ],
    );
  }
}

class _RecentLocationTile extends StatelessWidget {
  const _RecentLocationTile({
    required this.icon,
    required this.title,
    required this.subtitle,
    required this.onTap,
  });

  final IconData icon;
  final String title;
  final String subtitle;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return ListTile(
      contentPadding: const EdgeInsets.symmetric(vertical: 5),
      onTap: onTap,
      leading: CircleAvatar(
        backgroundColor: const Color(0xFFF0EEEE),
        foregroundColor: const Color(0xFF4C5557),
        child: Icon(icon),
      ),
      title: Text(title, style: const TextStyle(fontWeight: FontWeight.w700)),
      subtitle: Text(subtitle, maxLines: 1, overflow: TextOverflow.ellipsis),
    );
  }
}
