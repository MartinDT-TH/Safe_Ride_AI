import 'package:flutter/material.dart';
import 'package:google_maps_flutter/google_maps_flutter.dart';
import 'package:provider/provider.dart';

import '../providers/driver_dashboard_provider.dart';
import '../widgets/driver_bottom_nav_bar.dart';
import '../../../../../core/constants/app_strings.dart';
import '../../../../customer/booking/presentation/providers/booking_provider.dart';
import '../../../../customer/home/presentation/pages/customer_home_page.dart';
import '../../../../shared/history/presentation/pages/history_page.dart';
import '../../../../shared/onboarding/presentation/providers/role_provider.dart';
import '../../../../auth/presentation/providers/auth_provider.dart';
import '../../../../shared/profile/presentation/pages/profile_page.dart';

class DriverDashboardPage extends StatefulWidget {
  const DriverDashboardPage({super.key});

  @override
  State<DriverDashboardPage> createState() => _DriverDashboardPageState();
}

class _DriverDashboardPageState extends State<DriverDashboardPage> {
  GoogleMapController? _mapController;
  int _selectedIndex = 0;

  static const _tealColor = Color(0xFF006B70);

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      final token = context.read<AuthProvider>().token;
      if (token != null) {
        context.read<DriverDashboardProvider>().initializeRealtime(token);
      }
      _checkActiveCustomerBooking();
    });
  }

  void _checkActiveCustomerBooking() {
    final bookingProvider = context.read<BookingProvider>();
    final roleProvider = context.read<RoleProvider>();
    
    if (bookingProvider.activeBooking != null) {
      debugPrint('DRIVER_DASHBOARD: Active customer booking detected. Forcing switch to customer mode.');
      roleProvider.setRole(AppValues.roleCustomer);
      Navigator.of(context).pushAndRemoveUntil(
        MaterialPageRoute(builder: (_) => const CustomerHomePage()),
        (route) => false,
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final List<Widget> pages = [
      _buildHomeContent(),
      const HistoryPage(),
      const Center(child: Text('Wallet Page')),
      const ProfilePage(),
    ];

    return Scaffold(
      body: IndexedStack(index: _selectedIndex, children: pages),
      bottomNavigationBar: DriverBottomNavBar(
        currentIndex: _selectedIndex,
        onTap: (index) => setState(() => _selectedIndex = index),
      ),
    );
  }

  Widget _buildHomeContent() {
    return Stack(
      children: [
        // 1. Map Background
        GoogleMap(
          initialCameraPosition: const CameraPosition(
            target: LatLng(10.762622, 106.660172), // HCM City
            zoom: 14,
          ),
          onMapCreated: (controller) => _mapController = controller,
          zoomControlsEnabled: false,
          myLocationButtonEnabled: false,
        ),

        // 2. Top Bar (Income & Drawer/Notification)
        SafeArea(
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
            child: Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                _CircleIconButton(icon: Icons.menu, onPressed: () {}),
                _IncomeHeader(),
                _CircleIconButton(
                  icon: Icons.notifications_none_rounded,
                  hasBadge: true,
                  onPressed: () {},
                ),
              ],
            ),
          ),
        ),

        // 3. Bottom Controls (Online/Offline Toggle & Current Request)
        Positioned(
          bottom: 0,
          left: 0,
          right: 0,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              // Centering Button
              Align(
                alignment: Alignment.centerRight,
                child: Padding(
                  padding: const EdgeInsets.only(right: 16, bottom: 16),
                  child: _CircleIconButton(
                    icon: Icons.my_location,
                    onPressed: () {},
                  ),
                ),
              ),

              // Request Card or Online/Offline Toggle
              Consumer<DriverDashboardProvider>(
                builder: (context, provider, child) {
                  if (provider.activeTrip != null) {
                    return _ActiveTripCard(
                      trip: provider.activeTrip!,
                      isUpdating: provider.isUpdatingTrip,
                    );
                  }
                  if (provider.hasNewRequest &&
                      provider.currentRequest != null) {
                    return _NewRequestCard(
                      request: provider.currentRequest!,
                      isResponding: provider.isResponding,
                    );
                  }
                  return _StatusToggle();
                },
              ),

              const SizedBox(height: 16),
            ],
          ),
        ),
      ],
    );
  }
}

class _ActiveTripCard extends StatelessWidget {
  const _ActiveTripCard({required this.trip, required this.isUpdating});

  final ActiveDriverTrip trip;
  final bool isUpdating;

  @override
  Widget build(BuildContext context) {
    final status = trip.tripStatus;
    final canCancel = status == 'DRIVER_ARRIVING';
    final canComplete = status == 'ARRIVED' || status == 'IN_PROGRESS';
    final canMarkArrived = status == 'DRIVER_ARRIVING';
    final canStartArriving = status == 'ACCEPTED';

    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 20),
      child: Container(
        padding: const EdgeInsets.all(20),
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(24),
          boxShadow: [
            BoxShadow(
              color: Colors.black.withOpacity(0.16),
              blurRadius: 20,
              offset: const Offset(0, 10),
            ),
          ],
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Container(
                  padding: const EdgeInsets.all(10),
                  decoration: const BoxDecoration(
                    color: Color(0xFFE8F2F2),
                    shape: BoxShape.circle,
                  ),
                  child: const Icon(
                    Icons.route_rounded,
                    color: Color(0xFF006B70),
                    size: 22,
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const Text(
                        'Chuyến đang thực hiện',
                        style: TextStyle(
                          fontSize: 18,
                          fontWeight: FontWeight.bold,
                          color: Colors.black87,
                        ),
                      ),
                      const SizedBox(height: 4),
                      Text(
                        _statusLabel(status),
                        style: const TextStyle(
                          fontSize: 13,
                          fontWeight: FontWeight.w600,
                          color: Color(0xFF667085),
                        ),
                      ),
                    ],
                  ),
                ),
              ],
            ),
            const SizedBox(height: 20),
            if (canStartArriving)
              SizedBox(
                width: double.infinity,
                child: ElevatedButton.icon(
                  onPressed: isUpdating
                      ? null
                      : () => _runTripAction(
                          context,
                          () => context
                              .read<DriverDashboardProvider>()
                              .startArriving(),
                        ),
                  icon: const Icon(Icons.navigation_rounded),
                  label: Text(isUpdating ? 'Đang xử lý...' : 'Bắt đầu đến đón'),
                  style: _primaryButtonStyle(),
                ),
              )
            else if (canCancel || canMarkArrived)
              Row(
                children: [
                  if (canCancel) ...[
                    Expanded(
                      child: OutlinedButton.icon(
                        onPressed: isUpdating
                            ? null
                            : () => _runTripAction(
                                context,
                                () => context
                                    .read<DriverDashboardProvider>()
                                    .cancelActiveTrip(),
                              ),
                        icon: const Icon(Icons.close_rounded),
                        label: const Text('Hủy chuyến'),
                        style: OutlinedButton.styleFrom(
                          foregroundColor: const Color(0xFFE53935),
                          side: const BorderSide(color: Color(0xFFE53935)),
                          padding: const EdgeInsets.symmetric(vertical: 14),
                          shape: RoundedRectangleBorder(
                            borderRadius: BorderRadius.circular(12),
                          ),
                        ),
                      ),
                    ),
                    const SizedBox(width: 12),
                  ],
                  if (canMarkArrived)
                    Expanded(
                      child: ElevatedButton.icon(
                        onPressed: isUpdating
                            ? null
                            : () => _runTripAction(
                                context,
                                () => context
                                    .read<DriverDashboardProvider>()
                                    .markArrived(),
                              ),
                        icon: const Icon(Icons.flag_rounded),
                        label: const Text('Đã tới đón'),
                        style: _primaryButtonStyle(),
                      ),
                    ),
                ],
              )
            else if (canComplete)
              SizedBox(
                width: double.infinity,
                child: ElevatedButton.icon(
                  onPressed: isUpdating
                      ? null
                      : () => _runTripAction(
                          context,
                          () => context
                              .read<DriverDashboardProvider>()
                              .completeActiveTrip(),
                          successMessage:
                              'Đã kết thúc chuyến. Chờ khách xác nhận nhận lại xe.',
                        ),
                  icon: const Icon(Icons.check_circle_rounded),
                  label: Text(
                    isUpdating ? 'Đang xử lý...' : 'Kết thúc chuyến đi',
                  ),
                  style: _primaryButtonStyle(),
                ),
              ),
          ],
        ),
      ),
    );
  }

  static ButtonStyle _primaryButtonStyle() {
    return ElevatedButton.styleFrom(
      backgroundColor: const Color(0xFF006B70),
      foregroundColor: Colors.white,
      padding: const EdgeInsets.symmetric(vertical: 14),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
    );
  }

  static String _statusLabel(String status) {
    return switch (status) {
      'ACCEPTED' => 'Đã nhận chuyến',
      'DRIVER_ARRIVING' => 'Đang đến điểm đón',
      'ARRIVED' => 'Đã tới điểm đón',
      'IN_PROGRESS' => 'Đang thực hiện chuyến',
      _ => status,
    };
  }

  static Future<void> _runTripAction(
    BuildContext context,
    Future<bool> Function() action, {
    String? successMessage,
  }) async {
    try {
      final ok = await action();
      if (!context.mounted || !ok || successMessage == null) {
        return;
      }
      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(SnackBar(content: Text(successMessage)));
    } catch (_) {
      if (!context.mounted) return;
      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(
          const SnackBar(
            content: Text('Không thể cập nhật trạng thái chuyến.'),
          ),
        );
    }
  }
}

class _CircleIconButton extends StatelessWidget {
  final IconData icon;
  final VoidCallback onPressed;
  final bool hasBadge;

  const _CircleIconButton({
    required this.icon,
    required this.onPressed,
    this.hasBadge = false,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        color: Colors.white,
        shape: BoxShape.circle,
        boxShadow: [
          BoxShadow(
            color: Colors.black.withOpacity(0.1),
            blurRadius: 8,
            offset: const Offset(0, 2),
          ),
        ],
      ),
      child: Stack(
        children: [
          IconButton(
            icon: Icon(icon, color: Colors.black87),
            onPressed: onPressed,
          ),
          if (hasBadge)
            Positioned(
              top: 10,
              right: 10,
              child: Container(
                width: 8,
                height: 8,
                decoration: const BoxDecoration(
                  color: Colors.red,
                  shape: BoxShape.circle,
                ),
              ),
            ),
        ],
      ),
    );
  }
}

class _IncomeHeader extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    return Consumer<DriverDashboardProvider>(
      builder: (context, provider, child) {
        return Container(
          padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 8),
          decoration: BoxDecoration(
            color: Colors.white,
            borderRadius: BorderRadius.circular(30),
            boxShadow: [
              BoxShadow(
                color: Colors.black.withOpacity(0.1),
                blurRadius: 10,
                offset: const Offset(0, 4),
              ),
            ],
          ),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Text(
                'THU NHẬP HÔM NAY',
                style: TextStyle(
                  fontSize: 10,
                  fontWeight: FontWeight.bold,
                  color: Colors.grey,
                ),
              ),
              Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text(
                    '${provider.todayIncome.toInt().toString().replaceAllMapped(RegExp(r"(\d{3})(?=\d)"), (m) => "${m[1]},")}đ',
                    style: const TextStyle(
                      fontSize: 18,
                      fontWeight: FontWeight.bold,
                      color: Color(0xFF006B70),
                    ),
                  ),
                  const SizedBox(width: 8),
                  Container(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 6,
                      vertical: 2,
                    ),
                    decoration: BoxDecoration(
                      color: const Color(0xFFE8F2F2),
                      borderRadius: BorderRadius.circular(10),
                    ),
                    child: Text(
                      '${provider.todayTrips} chuyến',
                      style: const TextStyle(
                        fontSize: 10,
                        fontWeight: FontWeight.bold,
                        color: Color(0xFF006B70),
                      ),
                    ),
                  ),
                ],
              ),
            ],
          ),
        );
      },
    );
  }
}

class _StatusToggle extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    return Consumer<DriverDashboardProvider>(
      builder: (context, provider, child) {
        final isOnline = provider.status == DriverStatus.online;
        return Padding(
          padding: const EdgeInsets.symmetric(horizontal: 24),
          child: Container(
            height: 60,
            decoration: BoxDecoration(
              color: Colors.white,
              borderRadius: BorderRadius.circular(35),
              boxShadow: [
                BoxShadow(
                  color: Colors.black.withOpacity(0.1),
                  blurRadius: 10,
                  offset: const Offset(0, 4),
                ),
              ],
            ),
            child: Row(
              children: [
                Expanded(
                  child: GestureDetector(
                    onTap: !isOnline ? null : () => provider.toggleStatus(),
                    child: Center(
                      child: Text(
                        'Offline',
                        style: TextStyle(
                          fontSize: 16,
                          fontWeight: FontWeight.bold,
                          color: !isOnline ? Colors.black87 : Colors.grey,
                        ),
                      ),
                    ),
                  ),
                ),
                Expanded(
                  child: GestureDetector(
                    onTap: isOnline ? null : () => provider.toggleStatus(),
                    child: Container(
                      margin: const EdgeInsets.all(4),
                      decoration: BoxDecoration(
                        color: isOnline
                            ? const Color(0xFF006B70)
                            : Colors.transparent,
                        borderRadius: BorderRadius.circular(30),
                      ),
                      child: Center(
                        child: Row(
                          mainAxisAlignment: MainAxisAlignment.center,
                          children: [
                            if (isOnline) ...[
                              Container(
                                width: 8,
                                height: 8,
                                decoration: const BoxDecoration(
                                  color: Colors.cyanAccent,
                                  shape: BoxShape.circle,
                                ),
                              ),
                              const SizedBox(width: 8),
                            ],
                            Text(
                              'Online',
                              style: TextStyle(
                                fontSize: 16,
                                fontWeight: FontWeight.bold,
                                color: isOnline ? Colors.white : Colors.grey,
                              ),
                            ),
                          ],
                        ),
                      ),
                    ),
                  ),
                ),
              ],
            ),
          ),
        );
      },
    );
  }
}

class _NewRequestCard extends StatelessWidget {
  final TripRequest request;
  final bool isResponding;

  const _NewRequestCard({required this.request, required this.isResponding});

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 20),
      child: Container(
        padding: const EdgeInsets.all(20),
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(24),
          boxShadow: [
            BoxShadow(
              color: Colors.black.withOpacity(0.2),
              blurRadius: 20,
              offset: const Offset(0, 10),
            ),
          ],
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Container(
                  padding: const EdgeInsets.all(8),
                  decoration: const BoxDecoration(
                    color: Color(0xFF006B70),
                    shape: BoxShape.circle,
                  ),
                  child: const Icon(Icons.check, color: Colors.white, size: 20),
                ),
                const SizedBox(width: 12),
                const Text(
                  'Bạn đã có chuyến mới!',
                  style: TextStyle(
                    fontSize: 18,
                    fontWeight: FontWeight.bold,
                    color: Colors.black87,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 20),
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Text(
                      'THU NHẬP DỰ KIẾN',
                      style: TextStyle(
                        fontSize: 10,
                        color: Colors.grey,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                    Text(
                      '${request.expectedIncome.toInt().toString().replaceAllMapped(RegExp(r"(\d{3})(?=\d)"), (m) => "${m[1]},")}đ',
                      style: const TextStyle(
                        fontSize: 22,
                        fontWeight: FontWeight.bold,
                        color: Color(0xFF006B70),
                      ),
                    ),
                  ],
                ),
                Column(
                  crossAxisAlignment: CrossAxisAlignment.end,
                  children: [
                    const Text(
                      'ĐÓN KHÁCH',
                      style: TextStyle(
                        fontSize: 10,
                        color: Colors.grey,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                    Text(
                      '${request.pickupDistance} (${request.pickupTime})',
                      style: const TextStyle(
                        fontSize: 14,
                        fontWeight: FontWeight.bold,
                        color: Colors.black87,
                      ),
                    ),
                  ],
                ),
              ],
            ),
            const SizedBox(height: 20),
            _AddressItem(
              icon: Icons.radio_button_checked,
              iconColor: Colors.teal,
              label: 'Điểm đón (A)',
              address: request.pickupAddress,
            ),
            const Padding(
              padding: EdgeInsets.only(left: 11),
              child: SizedBox(
                height: 20,
                child: VerticalDivider(
                  width: 2,
                  thickness: 1,
                  color: Colors.grey,
                ),
              ),
            ),
            _AddressItem(
              icon: Icons.location_on,
              iconColor: Colors.red,
              label: 'Điểm đến (B)',
              address: request.destinationAddress,
            ),
            const SizedBox(height: 24),
            Row(
              children: [
                Expanded(
                  child: OutlinedButton(
                    onPressed: isResponding
                        ? null
                        : () => context
                              .read<DriverDashboardProvider>()
                              .declineRequest(),
                    style: OutlinedButton.styleFrom(
                      padding: const EdgeInsets.symmetric(vertical: 16),
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(12),
                      ),
                    ),
                    child: const Text('Từ chối'),
                  ),
                ),
                const SizedBox(width: 16),
                Expanded(
                  child: ElevatedButton(
                    onPressed: isResponding
                        ? null
                        : () => context
                              .read<DriverDashboardProvider>()
                              .acceptRequest(),
                    style: ElevatedButton.styleFrom(
                      backgroundColor: const Color(0xFF006B70),
                      foregroundColor: Colors.white,
                      padding: const EdgeInsets.symmetric(vertical: 16),
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(12),
                      ),
                    ),
                    child: Text(isResponding ? 'Đang xử lý...' : 'Chấp nhận'),
                  ),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

class _AddressItem extends StatelessWidget {
  final IconData icon;
  final Color iconColor;
  final String label;
  final String address;

  const _AddressItem({
    required this.icon,
    required this.iconColor,
    required this.label,
    required this.address,
  });

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Icon(icon, color: iconColor, size: 22),
        const SizedBox(width: 12),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                label,
                style: const TextStyle(
                  fontSize: 10,
                  color: Colors.grey,
                  fontWeight: FontWeight.bold,
                ),
              ),
              Text(
                address,
                style: const TextStyle(fontSize: 14, color: Colors.black87),
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
              ),
            ],
          ),
        ),
      ],
    );
  }
}

// _BottomNavBar logic removed, now using DriverBottomNavBar widget
