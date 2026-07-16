import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:provider/provider.dart';

import '../../../../../core/constants/app_strings.dart';
import '../providers/home_provider.dart';
import '../widgets/customer_bottom_nav_bar.dart';
import '../widgets/quick_action_item.dart';
import '../widgets/recent_trip_card.dart';
import '../widgets/promo_banner.dart';
import '../../../../shared/profile/presentation/pages/profile_page.dart';
import '../../../../shared/profile/presentation/pages/my_vehicles_page.dart';
import '../../../../shared/profile/presentation/pages/edit_profile_page.dart';
import '../../../../auth/presentation/providers/auth_provider.dart';
import '../../../../customer/booking/data/models/create_booking_request.dart';
import '../../../../customer/booking/data/models/booking_catalog.dart';
import '../../../../customer/booking/data/models/booking_response.dart';
import '../../../../customer/booking/presentation/pages/booking_options_page.dart';
import '../../../../customer/booking/presentation/pages/promotion_page.dart';
import '../../../../customer/booking/presentation/pages/trip_tracking_page.dart';
import '../../../../customer/booking/presentation/providers/booking_provider.dart';
import '../../../../shared/history/presentation/pages/history_page.dart';

class CustomerHomePage extends StatefulWidget {
  const CustomerHomePage({super.key});

  @override
  State<CustomerHomePage> createState() => _CustomerHomePageState();
}

class _CustomerHomePageState extends State<CustomerHomePage> {
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
    context.read<BookingProvider>().loadAvailablePromotions(auth.token!);
    _loadActiveBooking(auth.token);
  }

  Future<void> _loadActiveBooking(String? token) async {
    if (token == null || token.isEmpty) return;
    final booking = await context.read<BookingProvider>().loadActiveBooking(token);
    if (booking != null && mounted) {
      context.read<HomeProvider>().setSelectedIndex(1);
    }
  }

  @override
  Widget build(BuildContext context) {
    final auth = context.watch<AuthProvider>();
    final bookingProvider = context.watch<BookingProvider>();
    final homeProvider = context.watch<HomeProvider>();

    final activeBooking = bookingProvider.activeBooking;
    final activePickup = bookingProvider.activePickup ?? activeBooking?.pickup;
    final activeDestination = bookingProvider.activeDestination ?? activeBooking?.destination;
    final activeVehicle = bookingProvider.activeVehicle ?? activeBooking?.vehicle;

    final List<Widget> pages = [
      _buildHomeContent(auth, bookingProvider),
      (activeBooking != null && activePickup != null)
          ? TripTrackingPage(
              state: _trackingState(activeBooking),
              booking: activeBooking,
              pickup: activePickup,
              destination: activeDestination,
              vehicle: activeVehicle,
              onSwitchTab: (index) => homeProvider.setSelectedIndex(index),
            )
          : const HistoryPage(),
      const ProfilePage(),
    ];

    final selectedIndex = homeProvider.selectedIndex;

    return PopScope(
      canPop: false,
      onPopInvokedWithResult: (didPop, result) async {
        if (didPop) return;

        if (selectedIndex != 0) {
          homeProvider.setSelectedIndex(0);
        } else {
          final shouldExit = await showDialog<bool>(
            context: context,
            builder: (context) => AlertDialog(
              shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
              title: const Text('Thoát ứng dụng?'),
              content: const Text('Bạn có chắc chắn muốn thoát khỏi SafeRide không?'),
              actions: [
                TextButton(
                  onPressed: () => Navigator.pop(context, false),
                  child: const Text('Hủy', style: TextStyle(color: Colors.grey)),
                ),
                TextButton(
                  onPressed: () => Navigator.pop(context, true),
                  style: TextButton.styleFrom(foregroundColor: Colors.red),
                  child: const Text('Thoát'),
                ),
              ],
            ),
          );

          if (shouldExit == true) {
            await SystemNavigator.pop();
          }
        }
      },
      child: Scaffold(
        backgroundColor: const Color(0xFFFCF9F9),
        appBar: selectedIndex == 0
            ? AppBar(
                backgroundColor: Colors.white,
                elevation: 0.5,
                leading: GestureDetector(
                  onTap: () => homeProvider.setSelectedIndex(2),
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
            : (selectedIndex == 1 && activeBooking == null
                ? AppBar(
                    backgroundColor: Colors.white,
                    elevation: 0,
                    title: const Text(
                      'Hoạt động',
                      style: TextStyle(
                        color: Color(0xFF1A1A1A),
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                    centerTitle: true,
                  )
                : null),
        body: IndexedStack(index: selectedIndex, children: pages),
        bottomNavigationBar: CustomerBottomNavBar(
          currentIndex: selectedIndex,
          onTap: (index) {
            homeProvider.setSelectedIndex(index);
          },
        ),
      ),
    );
  }

  Widget _buildHomeContent(AuthProvider auth, BookingProvider bookingProvider) {
    final hasActiveBooking = bookingProvider.activeBooking != null;
    final homeProvider = context.read<HomeProvider>();

    return Consumer<HomeProvider>(
      builder: (_, provider, child) {
        if (provider.isLoading && provider.recentTrips.isEmpty) {
          return const Center(child: CircularProgressIndicator(color: Color(0xFF006B70)));
        }

        if (provider.errorMessage != null && provider.recentTrips.isEmpty) {
          return RefreshIndicator(
            onRefresh: () => provider.loadHomeData(),
            color: const Color(0xFF006B70),
            child: LayoutBuilder(
              builder: (context, constraints) {
                return SingleChildScrollView(
                  physics: const AlwaysScrollableScrollPhysics(),
                  child: ConstrainedBox(
                    constraints: BoxConstraints(minHeight: constraints.maxHeight),
                    child: Center(
                      child: Padding(
                        padding: const EdgeInsets.all(24.0),
                        child: Column(
                          mainAxisAlignment: MainAxisAlignment.center,
                          children: [
                            const Icon(
                              Icons.cloud_off_rounded,
                              size: 80,
                              color: Colors.grey,
                            ),
                            const SizedBox(height: 16),
                            const Text(
                              'Lỗi kết nối máy chủ',
                              style: TextStyle(
                                fontSize: 20,
                                fontWeight: FontWeight.bold,
                                color: Color(0xFF1A1A1A),
                              ),
                            ),
                            const SizedBox(height: 8),
                            Text(
                              provider.errorMessage!,
                              textAlign: TextAlign.center,
                              style: const TextStyle(fontSize: 15, color: Colors.black54),
                            ),
                            const SizedBox(height: 32),
                            ElevatedButton.icon(
                              onPressed: () => provider.loadHomeData(),
                              icon: const Icon(Icons.refresh_rounded),
                              label: const Text(
                                'Thử lại',
                                style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold),
                              ),
                              style: ElevatedButton.styleFrom(
                                backgroundColor: const Color(0xFF006B70),
                                foregroundColor: Colors.white,
                                padding: const EdgeInsets.symmetric(
                                    horizontal: 32, vertical: 14),
                                shape: RoundedRectangleBorder(
                                  borderRadius: BorderRadius.circular(16),
                                ),
                                elevation: 0,
                              ),
                            ),
                          ],
                        ),
                      ),
                    ),
                  ),
                );
              },
            ),
          );
        }

        return RefreshIndicator(
          onRefresh: () => provider.loadHomeData(),
          color: const Color(0xFF006B70),
          child: SingleChildScrollView(
            physics: const AlwaysScrollableScrollPhysics(),
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
                  onTap: hasActiveBooking
                      ? () {
                          _showMessage(
                            'Bạn đang có chuyến đang hoạt động. Vui lòng theo dõi ở mục Hoạt động.',
                          );
                          homeProvider.setSelectedIndex(1);
                        }
                      : () => _openBooking(context, BookingType.now),
                  borderRadius: BorderRadius.circular(20),
                  child: Container(
                    width: double.infinity,
                    padding: const EdgeInsets.all(24),
                    decoration: BoxDecoration(
                      color: hasActiveBooking
                          ? Colors.grey.shade400
                          : const Color(0xFF006B70),
                      borderRadius: BorderRadius.circular(20),
                    ),
                    child: Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              HomeStrings.bookNow,
                              style: TextStyle(
                                color: hasActiveBooking
                                    ? Colors.white70
                                    : Colors.white,
                                fontSize: 26,
                                fontWeight: FontWeight.bold,
                              ),
                            ),
                            const SizedBox(height: 4),
                            Text(
                              hasActiveBooking
                                  ? 'Đang theo dõi chuyến đi'
                                  : HomeStrings.bookNowDescription,
                              style: TextStyle(
                                color: hasActiveBooking
                                    ? Colors.white60
                                    : Colors.white70,
                                fontSize: 14,
                              ),
                            ),
                          ],
                        ),
                        const Icon(
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
                _buildPromotionSection(bookingProvider),
              ],
            ),
          ),
        );
      },
    );
  }

  Widget _buildPromotionSection(BookingProvider bookingProvider) {
    if (bookingProvider.isLoadingPromotions &&
        bookingProvider.availablePromotions.isEmpty) {
      return Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          _buildSectionHeader(HomeStrings.promotions),
          const SizedBox(height: 16),
          Container(
            width: double.infinity,
            height: 160,
            decoration: BoxDecoration(
              color: Colors.grey[200],
              borderRadius: BorderRadius.circular(20),
            ),
            child: const Center(child: CircularProgressIndicator(strokeWidth: 2)),
          ),
        ],
      );
    }

    if (bookingProvider.availablePromotions.isEmpty) {
      return const SizedBox.shrink();
    }

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [
            _buildSectionHeader(HomeStrings.promotions),
            TextButton(
              onPressed: () {
                showModalBottomSheet(
                  context: context,
                  isScrollControlled: true,
                  backgroundColor: Colors.transparent,
                  builder: (context) => const PromotionPage(),
                );
              },
              child: const Text(
                'Xem tất cả',
                style: TextStyle(
                  color: Color(0xFF006B70),
                  fontWeight: FontWeight.bold,
                ),
              ),
            ),
          ],
        ),
        const SizedBox(height: 8),
        SizedBox(
          height: 160,
          child: ListView.separated(
            scrollDirection: Axis.horizontal,
            itemCount: bookingProvider.availablePromotions.length,
            separatorBuilder: (context, index) => const SizedBox(width: 16),
            itemBuilder: (context, index) {
              final promo = bookingProvider.availablePromotions[index];
              return GestureDetector(
                onTap: () {
                  bookingProvider.selectPromo(promo);
                  _openBooking(context, BookingType.now);
                },
                child: PromoBanner(promo: promo),
              );
            },
          ),
        ),
      ],
    );
  }

  Widget _buildSectionHeader(String title) {
    return Text(
      title,
      style: const TextStyle(
        fontSize: 18,
        fontWeight: FontWeight.bold,
        color: Color(0xFF1A1A1A),
      ),
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

  TripTrackingState _trackingState(BookingResponse booking) {
    return booking.tripStatus == 'IN_PROGRESS' ||
            booking.tripStatus == 'COMPLETED'
        ? TripTrackingState.inProgress
        : TripTrackingState.arriving;
  }

  void _showMessage(String message) {
    ScaffoldMessenger.of(context)
      ..hideCurrentSnackBar()
      ..showSnackBar(SnackBar(content: Text(message)));
  }
}
