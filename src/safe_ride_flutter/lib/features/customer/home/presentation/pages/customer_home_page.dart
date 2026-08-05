import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:provider/provider.dart';

import '../../../../../core/localization/localization_extensions.dart';
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
import '../../../../customer/ai_chat/presentation/widgets/ai_chat_sheet.dart';
import '../../../../shared/history/presentation/pages/history_page.dart';
import '../../../../trip_sharing/trip_share_deep_link_coordinator.dart';
import '../../../../trip_sharing/presentation/pages/shared_trip_tracking_page.dart';
import '../../../../trip_sharing/presentation/providers/received_trip_shares_provider.dart';
import '../../../../../dependency_injection/injection.dart';
import '../../../../shared/notifications/presentation/pages/notifications_page.dart';
import '../../../../shared/notifications/presentation/providers/notification_provider.dart';

class CustomerHomePage extends StatefulWidget {
  CustomerHomePage({super.key});

  @override
  State<CustomerHomePage> createState() => _CustomerHomePageState();
}

class _CustomerHomePageState extends State<CustomerHomePage> {
  bool _handledAuthGate = false;

  AuthProvider? _authProvider;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) return;
      _authProvider = context.read<AuthProvider>();
      _authProvider?.addListener(_handleAuthGate);
      _handleAuthGate();
    });
  }

  @override
  void dispose() {
    _authProvider?.removeListener(_handleAuthGate);
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
    context.read<NotificationProvider>().initialize(auth.token);
    _loadActiveBooking(auth.token);
    context.read<ReceivedTripSharesProvider>().load();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) {
        unawaited(
          getIt<TripShareDeepLinkCoordinator>().processPendingAfterNavigation(),
        );
      }
    });
  }

  Future<void> _loadActiveBooking(String? token) async {
    if (token == null || token.isEmpty) return;
    final booking = await context.read<BookingProvider>().loadActiveBooking(
      token,
    );
    if (booking != null && mounted) {
      context.read<HomeProvider>().setSelectedIndex(1);
    }
  }

  @override
  Widget build(BuildContext context) {
    final auth = context.watch<AuthProvider>();
    final bookingProvider = context.watch<BookingProvider>();
    final homeProvider = context.watch<HomeProvider>();
    final hasUnreadNotifications = context.select<NotificationProvider, bool>(
      (provider) => provider.unreadCount > 0,
    );

    final activeBooking = bookingProvider.activeBooking;
    final activePickup = bookingProvider.activePickup ?? activeBooking?.pickup;
    final activeDestination =
        bookingProvider.activeDestination ?? activeBooking?.destination;
    final activeVehicle =
        bookingProvider.activeVehicle ?? activeBooking?.vehicle;

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
          : HistoryPage(),
      ProfilePage(),
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
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(16),
              ),
              title: Text(context.l10n.exitAppQuestion),
              content: Text(context.l10n.exitAppDescription),
              actions: [
                TextButton(
                  onPressed: () => Navigator.pop(context, false),
                  child: Text(
                    context.l10n.cancel,
                    style: TextStyle(color: Colors.grey),
                  ),
                ),
                TextButton(
                  onPressed: () => Navigator.pop(context, true),
                  style: TextButton.styleFrom(foregroundColor: Colors.red),
                  child: Text(context.l10n.exit),
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
        backgroundColor: Color(0xFFFCF9F9),
        appBar: selectedIndex == 0
            ? AppBar(
                backgroundColor: Colors.white,
                elevation: 0.5,
                leading: GestureDetector(
                  onTap: () => homeProvider.setSelectedIndex(2),
                  child: Padding(
                    padding: const EdgeInsets.only(left: 16, top: 8, bottom: 8),
                    child: CircleAvatar(
                      backgroundColor: Color(0xFFE8F2F2),
                      backgroundImage: _avatarImage(auth.avatarUrl),
                      child: _avatarImage(auth.avatarUrl) == null
                          ? Text(
                              _initials(auth.fullName),
                              style: TextStyle(
                                color: Color(0xFF006B70),
                                fontWeight: FontWeight.bold,
                              ),
                            )
                          : null,
                    ),
                  ),
                ),
                title: Text(
                  'SafeRide',
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
                        icon: Icon(
                          Icons.notifications_none_rounded,
                          color: Color(0xFF006B70),
                          size: 28,
                        ),
                        onPressed: () {
                          Navigator.of(context).push(
                            MaterialPageRoute(
                              builder: (_) => NotificationsPage(),
                            ),
                          );
                        },
                      ),
                      if (hasUnreadNotifications)
                        Positioned(
                          top: 14,
                          right: 14,
                          child: Container(
                            width: 8,
                            height: 8,
                            decoration: BoxDecoration(
                              color: Colors.red,
                              shape: BoxShape.circle,
                            ),
                          ),
                        ),
                    ],
                  ),
                  SizedBox(width: 8),
                ],
              )
            : (selectedIndex == 1 && activeBooking == null
                  ? AppBar(
                      backgroundColor: Colors.white,
                      elevation: 0,
                      title: Text(
                        context.l10n.activity,
                        style: TextStyle(
                          color: Color(0xFF1A1A1A),
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                      centerTitle: true,
                    )
                  : null),
        body: IndexedStack(index: selectedIndex, children: pages),
        floatingActionButton: selectedIndex == 0
            ? FloatingActionButton(
                onPressed: () => AiChatSheet.show(context),
                backgroundColor: Color(0xFF006B70),
                foregroundColor: Colors.white,
                tooltip: context.l10n.safeRideAssistant,
                child: Icon(Icons.smart_toy_outlined),
              )
            : null,
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
          return Center(
            child: CircularProgressIndicator(color: Color(0xFF006B70)),
          );
        }

        if (provider.errorMessage != null && provider.recentTrips.isEmpty) {
          return RefreshIndicator(
            onRefresh: () async {
              final receivedShares = context.read<ReceivedTripSharesProvider>();
              await provider.loadHomeData();
              await receivedShares.refresh();
            },
            color: const Color(0xFF006B70),
            child: LayoutBuilder(
              builder: (context, constraints) {
                return SingleChildScrollView(
                  physics: AlwaysScrollableScrollPhysics(),
                  child: ConstrainedBox(
                    constraints: BoxConstraints(
                      minHeight: constraints.maxHeight,
                    ),
                    child: Center(
                      child: Padding(
                        padding: const EdgeInsets.all(24.0),
                        child: Column(
                          mainAxisAlignment: MainAxisAlignment.center,
                          children: [
                            Icon(
                              Icons.cloud_off_rounded,
                              size: 80,
                              color: Colors.grey,
                            ),
                            SizedBox(height: 16),
                            Text(
                              context.l10n.serverConnectionErrorTitle,
                              style: TextStyle(
                                fontSize: 20,
                                fontWeight: FontWeight.bold,
                                color: Color(0xFF1A1A1A),
                              ),
                            ),
                            SizedBox(height: 8),
                            Text(
                              provider.errorMessage!,
                              textAlign: TextAlign.center,
                              style: TextStyle(
                                fontSize: 15,
                                color: Colors.black54,
                              ),
                            ),
                            SizedBox(height: 32),
                            ElevatedButton.icon(
                              onPressed: () => provider.loadHomeData(),
                              icon: Icon(Icons.refresh_rounded),
                              label: Text(
                                context.l10n.tryAgain,
                                style: TextStyle(
                                  fontSize: 16,
                                  fontWeight: FontWeight.bold,
                                ),
                              ),
                              style: ElevatedButton.styleFrom(
                                backgroundColor: Color(0xFF006B70),
                                foregroundColor: Colors.white,
                                padding: const EdgeInsets.symmetric(
                                  horizontal: 32,
                                  vertical: 14,
                                ),
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
          onRefresh: () async {
            final receivedShares = context.read<ReceivedTripSharesProvider>();
            await provider.loadHomeData();
            await receivedShares.refresh();
          },
          color: Color(0xFF006B70),
          child: SingleChildScrollView(
            physics: AlwaysScrollableScrollPhysics(),
            padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 24),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  context.l10n.greeting(_displayName(auth.fullName)),
                  style: TextStyle(
                    fontSize: 32,
                    fontWeight: FontWeight.bold,
                    color: Color(0xFF1A1A1A),
                  ),
                ),
                Text(
                  context.l10n.destinationQuestion,
                  style: TextStyle(fontSize: 16, color: Color(0xFF666666)),
                ),
                SizedBox(height: 24),

                _buildReceivedShares(),

                InkWell(
                  onTap: hasActiveBooking
                      ? () {
                          _showMessage(context.l10n.activeTripNotice);
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
                          : Color(0xFF006B70),
                      borderRadius: BorderRadius.circular(20),
                    ),
                    child: Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              context.l10n.bookNow,
                              style: TextStyle(
                                color: hasActiveBooking
                                    ? Colors.white70
                                    : Colors.white,
                                fontSize: 26,
                                fontWeight: FontWeight.bold,
                              ),
                            ),
                            SizedBox(height: 4),
                            Text(
                              hasActiveBooking
                                  ? context.l10n.trackingTrip
                                  : context.l10n.bookNowDescription,
                              style: TextStyle(
                                color: hasActiveBooking
                                    ? Colors.white60
                                    : Colors.white70,
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
                SizedBox(height: 12),

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
                    child: Row(
                      children: [
                        Icon(
                          Icons.calendar_month_outlined,
                          color: Color(0xFF006B70),
                          size: 28,
                        ),
                        SizedBox(width: 12),
                        Text(
                          context.l10n.scheduleBooking,
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
                SizedBox(height: 32),

                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    QuickActionItem(
                      icon: Icons.history_rounded,
                      title: context.l10n.history,
                      backgroundColor: Color(0xFFF2F2F2),
                      iconColor: Colors.black,
                      onTap: () {
                        Navigator.push(
                          context,
                          MaterialPageRoute(
                            builder: (context) => HistoryPage(),
                          ),
                        );
                      },
                    ),
                    QuickActionItem(
                      icon: Icons.directions_car_filled_rounded,
                      title: context.l10n.myVehiclesShort,
                      backgroundColor: Color(0xFFF2F2F2),
                      iconColor: Colors.black,
                      onTap: () {
                        Navigator.push(
                          context,
                          MaterialPageRoute(
                            builder: (context) => MyVehiclesPage(),
                          ),
                        );
                      },
                    ),
                    QuickActionItem(
                      icon: Icons.local_offer_rounded,
                      title: context.l10n.promotions,
                      backgroundColor: Color(0xFFF2F2F2),
                      iconColor: Colors.black,
                      onTap: () {
                        showModalBottomSheet(
                          context: context,
                          isScrollControlled: true,
                          backgroundColor: Colors.transparent,
                          builder: (context) => PromotionPage(),
                        );
                      },
                    ),
                    QuickActionItem(
                      icon: Icons.star_rounded,
                      title: context.l10n.sos,
                      backgroundColor: Color(0xFFFFE8E8),
                      iconColor: Colors.red,
                      textColor: Colors.red,
                      onTap: () {
                        final activeTrip =
                            bookingProvider.activeBooking?.tripId != null;
                        if (!activeTrip) {
                          _showMessage(context.l10n.noActiveTripForSos);
                          return;
                        }
                        homeProvider.setSelectedIndex(1);
                      },
                    ),
                  ],
                ),
                SizedBox(height: 32),

                Text(
                  context.l10n.recentTrips,
                  style: TextStyle(
                    fontSize: 18,
                    fontWeight: FontWeight.bold,
                    color: Color(0xFF1A1A1A),
                  ),
                ),
                SizedBox(height: 16),
                RecentTripCard(
                  pickup: context.l10n.sampleRecentPickup,
                  destination: context.l10n.sampleRecentDestination,
                  time: context.l10n.sampleRecentTime,
                ),
                SizedBox(height: 24),
                _buildPromotionSection(bookingProvider),
              ],
            ),
          ),
        );
      },
    );
  }

  Widget _buildReceivedShares() {
    return Consumer<ReceivedTripSharesProvider>(
      builder: (context, provider, _) {
        if (provider.shares.isEmpty) return const SizedBox.shrink();
        return Padding(
          padding: const EdgeInsets.only(bottom: 20),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Text(
                'Chuyến đi được chia sẻ với bạn',
                style: TextStyle(fontWeight: FontWeight.w700, fontSize: 17),
              ),
              const SizedBox(height: 8),
              ...provider.shares.map(
                (share) => Card(
                  child: ListTile(
                    leading: const Icon(Icons.share_location_outlined),
                    title: Text(share.sharedByName),
                    subtitle: Text('Trạng thái: ${share.tripStatus}'),
                    trailing: const Icon(Icons.chevron_right),
                    onTap: () async {
                      await Navigator.of(context).push(
                        MaterialPageRoute(
                          builder: (_) => SharedTripTrackingPage(
                            tripShareId: share.tripShareId,
                          ),
                        ),
                      );
                      if (context.mounted) {
                        await context.read<ReceivedTripSharesProvider>().load();
                      }
                    },
                  ),
                ),
              ),
            ],
          ),
        );
      },
  Widget _buildPromotionSection(BookingProvider bookingProvider) {
    if (bookingProvider.isLoadingPromotions &&
        bookingProvider.availablePromotions.isEmpty) {
      return Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          _buildSectionHeader(context.l10n.promotions),
          SizedBox(height: 16),
          Container(
            width: double.infinity,
            height: 176,
            decoration: BoxDecoration(
              color: Colors.grey[200],
              borderRadius: BorderRadius.circular(20),
            ),
            child: Center(
              child: CircularProgressIndicator(strokeWidth: 2),
            ),
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
            _buildSectionHeader(context.l10n.promotions),
            TextButton(
              onPressed: () {
                showModalBottomSheet(
                  context: context,
                  isScrollControlled: true,
                  backgroundColor: Colors.transparent,
                  builder: (context) => PromotionPage(),
                );
              },
              child: Text(
                context.l10n.viewAll,
                style: TextStyle(
                  color: Color(0xFF006B70),
                  fontWeight: FontWeight.bold,
                ),
              ),
            ),
          ],
        ),
        SizedBox(height: 8),
        SizedBox(
          height: 176,
          child: ListView.separated(
            scrollDirection: Axis.horizontal,
            itemCount: bookingProvider.availablePromotions.length,
            separatorBuilder: (context, index) => SizedBox(width: 16),
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
      style: TextStyle(
        fontSize: 18,
        fontWeight: FontWeight.bold,
        color: Color(0xFF1A1A1A),
      ),
    );
  }

  String _displayName(String? fullName) {
    final value = fullName?.trim() ?? '';
    if (value.isEmpty) {
      return context.l10n.friendlyUser;
    }
    return value;
  }

  String _initials(String? fullName) {
    final name = _displayName(fullName);
    if (name == context.l10n.friendlyUser) return 'SR';
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
            booking.tripStatus == 'WAITING_RETURN_CONFIRM' ||
            booking.tripStatus == 'RETURN_CONFIRMED' ||
            booking.tripStatus == 'WAITING_PAYMENT' ||
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
