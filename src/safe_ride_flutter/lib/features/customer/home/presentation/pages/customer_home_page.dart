import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../../../core/constants/app_strings.dart';
import '../providers/home_provider.dart';
import '../widgets/quick_action_item.dart';
import '../widgets/recent_trip_card.dart';
import '../widgets/promo_banner.dart';
import '../../../../shared/profile/presentation/pages/profile_page.dart';
import '../../../../shared/profile/presentation/pages/my_vehicles_page.dart';
import '../../../../shared/profile/presentation/pages/edit_profile_page.dart';
import '../../../../auth/presentation/providers/auth_provider.dart';
import '../../../../customer/booking/data/models/create_booking_request.dart';
import '../../../../customer/booking/data/models/booking_catalog.dart';
import '../../../../customer/booking/presentation/pages/booking_options_page.dart';
import '../../../../customer/booking/presentation/pages/promotion_page.dart';
import '../../../../shared/history/presentation/pages/history_page.dart';

class CustomerHomePage extends StatefulWidget {
  const CustomerHomePage({super.key});

  @override
  State<CustomerHomePage> createState() => _CustomerHomePageState();
}

class _CustomerHomePageState extends State<CustomerHomePage> {
  int _selectedIndex = 0;
  bool _handledAuthGate = false;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) return;
      context.read<AuthProvider>().addListener(_handleAuthGate);
      _handleAuthGate();
    });
  }

  @override
  void dispose() {
    context.read<AuthProvider>().removeListener(_handleAuthGate);
    super.dispose();
  }

  void _handleAuthGate() {
    if (!mounted || _handledAuthGate) return;

    final auth = context.read<AuthProvider>();
    if (auth.isRestoringSession) {
      return;
    }

    _handledAuthGate = true;
    if (auth.nextStep == AuthNextStep.completeProfile ||
        !auth.isProfileComplete) {
      Navigator.of(context).pushAndRemoveUntil(
        MaterialPageRoute(
          builder: (_) => EditProfilePage(
            requiredCompletion: true,
            phoneNumber: auth.phoneNumber,
          ),
        ),
        (_) => false,
      );
      return;
    }

    context.read<HomeProvider>().loadHomeData();
  }

  // Hàm tiện ích để tạo item cho BottomNavigationBar với hiệu ứng pill background
  BottomNavigationBarItem _buildNavItem(
    IconData icon,
    IconData activeIcon,
    String label,
    int index,
  ) {
    bool isSelected = _selectedIndex == index;
    return BottomNavigationBarItem(
      icon: Container(
        padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 6),
        decoration: BoxDecoration(
          color: isSelected ? const Color(0xFF006B70) : Colors.transparent,
          borderRadius: BorderRadius.circular(14),
        ),
        child: Icon(
          isSelected ? activeIcon : icon,
          color: isSelected ? Colors.white : Colors.grey,
        ),
      ),
      label: label,
    );
  }

  @override
  Widget build(BuildContext context) {
    final auth = context.watch<AuthProvider>();
    final List<Widget> pages = [
      _buildHomeContent(auth),
      const HistoryPage(),
      const Center(child: Text(HomeStrings.walletPage)),
      const ProfilePage(),
    ];

    return Scaffold(
      backgroundColor: const Color(0xFFFCF9F9),
      appBar: _selectedIndex == 0
          ? AppBar(
              backgroundColor: Colors.white,
              elevation: 0.5,
              leading: GestureDetector(
                onTap: () => setState(
                  () => _selectedIndex = 3,
                ), // Chuyển sang tab Profile khi ấn Avatar
                child: Padding(
                  padding: const EdgeInsets.only(left: 16, top: 8, bottom: 8),
                  child: CircleAvatar(
                    backgroundColor: const Color(0xFFE8F2F2),
                    backgroundImage: _avatarImage(auth.avatarUrl),
                    child: _avatarImage(auth.avatarUrl) == null
                        ? Text(
                            _initials(auth.fullName),
                            style: const TextStyle(
                              color: Color(0xFF006B70),
                              fontWeight: FontWeight.bold,
                            ),
                          )
                        : null,
                  ),
                ),
              ),
              title: const Text(
                AppStrings.appName,
                style: TextStyle(
                  color: Color(0xFF006B70),
                  fontWeight: FontWeight.bold,
                  fontSize: 22,
                ),
              ),
              centerTitle: true,
              actions: [
                Stack(
                  alignment: Alignment.center,
                  children: [
                    IconButton(
                      icon: const Icon(
                        Icons.notifications_none_rounded,
                        color: Color(0xFF006B70),
                        size: 28,
                      ),
                      onPressed: () {},
                    ),
                    Positioned(
                      top: 14,
                      right: 14,
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
                const SizedBox(width: 8),
              ],
            )
          : null,
      body: IndexedStack(index: _selectedIndex, children: pages),
      bottomNavigationBar: BottomNavigationBar(
        currentIndex: _selectedIndex,
        type: BottomNavigationBarType.fixed,
        selectedItemColor: const Color(0xFF006B70),
        unselectedItemColor: Colors.grey,
        showUnselectedLabels: true,
        selectedLabelStyle: const TextStyle(
          fontWeight: FontWeight.bold,
          fontSize: 12,
        ),
        unselectedLabelStyle: const TextStyle(fontSize: 12),
        onTap: (index) => setState(() => _selectedIndex = index),
        items: [
          _buildNavItem(
            Icons.home_outlined,
            Icons.home_filled,
            HomeStrings.home,
            0,
          ),
          _buildNavItem(
            Icons.assignment_outlined,
            Icons.assignment_rounded,
            HomeStrings.activity,
            1,
          ),
          _buildNavItem(
            Icons.account_balance_wallet_outlined,
            Icons.account_balance_wallet_rounded,
            HomeStrings.wallet,
            2,
          ),
          _buildNavItem(
            Icons.person_outline_rounded,
            Icons.person_rounded,
            HomeStrings.account,
            3,
          ),
        ],
      ),
    );
  }

  Widget _buildHomeContent(AuthProvider auth) {
    return Consumer<HomeProvider>(
      builder: (_, provider, child) {
        if (provider.isLoading) {
          return const Center(child: CircularProgressIndicator());
        }

        return SingleChildScrollView(
          padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 24),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                HomeStrings.greeting(_displayName(auth.fullName)),
                style: const TextStyle(
                  fontSize: 32,
                  fontWeight: FontWeight.bold,
                  color: Color(0xFF1A1A1A),
                ),
              ),
              const Text(
                HomeStrings.destinationQuestion,
                style: TextStyle(fontSize: 16, color: Color(0xFF666666)),
              ),
              const SizedBox(height: 24),

              InkWell(
                onTap: () => _openBooking(context, BookingType.now),
                borderRadius: BorderRadius.circular(20),
                child: Container(
                  width: double.infinity,
                  padding: const EdgeInsets.all(24),
                  decoration: BoxDecoration(
                    color: const Color(0xFF006B70),
                    borderRadius: BorderRadius.circular(20),
                  ),
                  child: const Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            HomeStrings.bookNow,
                            style: TextStyle(
                              color: Colors.white,
                              fontSize: 26,
                              fontWeight: FontWeight.bold,
                            ),
                          ),
                          SizedBox(height: 4),
                          Text(
                            HomeStrings.bookNowDescription,
                            style: TextStyle(
                              color: Colors.white70,
                              fontSize: 14,
                            ),
                          ),
                        ],
                      ),
                      Icon(
                        Icons.directions_car_rounded,
                        color: Colors.white,
                        size: 54,
                      ),
                    ],
                  ),
                ),
              ),
              const SizedBox(height: 12),

              InkWell(
                onTap: () => _openBooking(context, BookingType.scheduled),
                borderRadius: BorderRadius.circular(20),
                child: Container(
                  width: double.infinity,
                  padding: const EdgeInsets.symmetric(
                    horizontal: 20,
                    vertical: 18,
                  ),
                  decoration: BoxDecoration(
                    color: Colors.white,
                    borderRadius: BorderRadius.circular(20),
                    border: Border.all(color: Colors.grey.shade200),
                  ),
                  child: const Row(
                    children: [
                      Icon(
                        Icons.calendar_month_outlined,
                        color: Color(0xFF006B70),
                        size: 28,
                      ),
                      SizedBox(width: 12),
                      Text(
                        HomeStrings.scheduleBooking,
                        style: TextStyle(
                          fontSize: 18,
                          fontWeight: FontWeight.bold,
                          color: Color(0xFF1A1A1A),
                        ),
                      ),
                      Spacer(),
                      Icon(
                        Icons.arrow_forward_ios_rounded,
                        color: Colors.black,
                        size: 16,
                      ),
                    ],
                  ),
                ),
              ),
              const SizedBox(height: 32),

              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  QuickActionItem(
                    icon: Icons.history_rounded,
                    title: HomeStrings.history,
                    backgroundColor: const Color(0xFFF2F2F2),
                    iconColor: Colors.black,
                    onTap: () {
                      Navigator.push(
                        context,
                        MaterialPageRoute(
                          builder: (context) => const HistoryPage(),
                        ),
                      );
                    },
                  ),
                  QuickActionItem(
                    icon: Icons.directions_car_filled_rounded,
                    title: HomeStrings.myVehicles,
                    backgroundColor: const Color(0xFFF2F2F2),
                    iconColor: Colors.black,
                    onTap: () {
                      Navigator.push(
                        context,
                        MaterialPageRoute(
                          builder: (context) => const MyVehiclesPage(),
                        ),
                      );
                    },
                  ),
                  QuickActionItem(
                    icon: Icons.local_offer_rounded,
                    title: HomeStrings.promotions,
                    backgroundColor: const Color(0xFFF2F2F2),
                    iconColor: Colors.black,
                    onTap: () {
                      showModalBottomSheet(
                        context: context,
                        isScrollControlled: true,
                        backgroundColor: Colors.transparent,
                        builder: (context) => const PromotionPage(),
                      );
                    },
                  ),
                  QuickActionItem(
                    icon: Icons.star_rounded,
                    title: HomeStrings.sos,
                    backgroundColor: const Color(0xFFFFE8E8),
                    iconColor: Colors.red,
                    textColor: Colors.red,
                    onTap: () {},
                  ),
                ],
              ),
              const SizedBox(height: 32),

              const Text(
                HomeStrings.recentTrips,
                style: TextStyle(
                  fontSize: 18,
                  fontWeight: FontWeight.bold,
                  color: Color(0xFF1A1A1A),
                ),
              ),
              const SizedBox(height: 16),
              const RecentTripCard(
                pickup: HomeStrings.recentPickup,
                destination: HomeStrings.recentDestination,
                time: HomeStrings.recentTime,
              ),
              const SizedBox(height: 24),
              GestureDetector(
                onTap: () {
                  showModalBottomSheet(
                    context: context,
                    isScrollControlled: true,
                    backgroundColor: Colors.transparent,
                    builder: (context) => const PromotionPage(),
                  );
                },
                child: const PromoBanner(
                  title: HomeStrings.promotionTitle,
                  code: HomeStrings.promotionCode,
                ),
              ),
            ],
          ),
        );
      },
    );
  }

  String _displayName(String? fullName) {
    final value = fullName?.trim() ?? '';
    if (value.isEmpty || value == HomeStrings.defaultUser) {
      return HomeStrings.friendlyUser;
    }
    return value;
  }

  String _initials(String? fullName) {
    final name = _displayName(fullName);
    if (name == HomeStrings.friendlyUser) return HomeStrings.defaultInitials;
    final words = name.split(RegExp(r'\s+'));
    return words.take(2).map((word) => word[0].toUpperCase()).join();
  }

  ImageProvider? _avatarImage(String? avatarUrl) {
    final value = avatarUrl?.trim() ?? '';
    return value.isEmpty ? null : NetworkImage(value);
  }

  void _openBooking(BuildContext context, BookingType bookingType) {
    Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => BookingOptionsPage(
          initialMode: BookingServiceMode.perTrip,
          showSchedule: bookingType == BookingType.scheduled,
        ),
      ),
    );
  }
}

