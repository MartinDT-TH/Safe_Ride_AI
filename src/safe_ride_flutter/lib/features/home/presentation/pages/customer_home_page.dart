import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../providers/home_provider.dart';
import '../widgets/quick_action_item.dart';
import '../widgets/recent_trip_card.dart';
import '../widgets/promo_banner.dart';
import '../../../profile/presentation/pages/profile_page.dart';

class CustomerHomePage extends StatefulWidget {
  const CustomerHomePage({super.key});

  @override
  State<CustomerHomePage> createState() => _CustomerHomePageState();
}

class _CustomerHomePageState extends State<CustomerHomePage> {
  int _selectedIndex = 0;

  @override
  void initState() {
    super.initState();
    Future.microtask(() {
      context.read<HomeProvider>().loadHomeData();
    });
  }

  // Hàm tiện ích để tạo item cho BottomNavigationBar với hiệu ứng pill background
  BottomNavigationBarItem _buildNavItem(IconData icon, IconData activeIcon, String label, int index) {
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
    final List<Widget> pages = [
      _buildHomeContent(),
      const Center(child: Text('Activity Page')),
      const Center(child: Text('Wallet Page')),
      const ProfilePage(),
    ];

    return Scaffold(
      backgroundColor: const Color(0xFFFCF9F9),
      appBar: _selectedIndex == 0 
        ? AppBar(
            backgroundColor: Colors.white,
            elevation: 0.5,
            leading: GestureDetector(
              onTap: () => setState(() => _selectedIndex = 3), // Chuyển sang tab Profile khi ấn Avatar
              child: const Padding(
                padding: EdgeInsets.only(left: 16, top: 8, bottom: 8),
                child: CircleAvatar(
                  backgroundImage: NetworkImage('https://i.pravatar.cc/150?img=11'),
                ),
              ),
            ),
            title: const Text(
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
                    icon: const Icon(Icons.notifications_none_rounded, color: Color(0xFF006B70), size: 28),
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
                  )
                ],
              ),
              const SizedBox(width: 8),
            ],
          )
        : null,
      body: IndexedStack(
        index: _selectedIndex,
        children: pages,
      ),
      bottomNavigationBar: BottomNavigationBar(
        currentIndex: _selectedIndex,
        type: BottomNavigationBarType.fixed,
        selectedItemColor: const Color(0xFF006B70),
        unselectedItemColor: Colors.grey,
        showUnselectedLabels: true,
        selectedLabelStyle: const TextStyle(fontWeight: FontWeight.bold, fontSize: 12),
        unselectedLabelStyle: const TextStyle(fontSize: 12),
        onTap: (index) => setState(() => _selectedIndex = index),
        items: [
          _buildNavItem(Icons.home_outlined, Icons.home_filled, 'Trang chủ', 0),
          _buildNavItem(Icons.assignment_outlined, Icons.assignment_rounded, 'Hoạt động', 1),
          _buildNavItem(Icons.account_balance_wallet_outlined, Icons.account_balance_wallet_rounded, 'Ví', 2),
          _buildNavItem(Icons.person_outline_rounded, Icons.person_rounded, 'Tài khoản', 3),
        ],
      ),
    );
  }

  Widget _buildHomeContent() {
    return Consumer<HomeProvider>(
      builder: (_, provider, __) {
        if (provider.isLoading) {
          return const Center(child: CircularProgressIndicator());
        }

        return SingleChildScrollView(
          padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 24),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Text(
                'Chào Alex,',
                style: TextStyle(
                  fontSize: 32,
                  fontWeight: FontWeight.bold,
                  color: Color(0xFF1A1A1A),
                ),
              ),
              const Text(
                'Bạn muốn đi đâu hôm nay?',
                style: TextStyle(fontSize: 16, color: Color(0xFF666666)),
              ),
              const SizedBox(height: 24),
              
              Container(
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
                          'Đặt ngay',
                          style: TextStyle(color: Colors.white, fontSize: 26, fontWeight: FontWeight.bold),
                        ),
                        SizedBox(height: 4),
                        Text(
                          'Có tài xế sau 2 phút',
                          style: TextStyle(color: Colors.white70, fontSize: 14),
                        ),
                      ],
                    ),
                    Icon(Icons.directions_car_rounded, color: Colors.white, size: 54),
                  ],
                ),
              ),
              const SizedBox(height: 12),
              
              Container(
                width: double.infinity,
                padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 18),
                decoration: BoxDecoration(
                  color: Colors.white,
                  borderRadius: BorderRadius.circular(20),
                  border: Border.all(color: Colors.grey.shade200),
                ),
                child: const Row(
                  children: [
                    Icon(Icons.calendar_month_outlined, color: Color(0xFF006B70), size: 28),
                    SizedBox(width: 12),
                    Text(
                      'Đặt lịch trước',
                      style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold, color: Color(0xFF1A1A1A)),
                    ),
                    Spacer(),
                    Icon(Icons.arrow_forward_ios_rounded, color: Colors.black, size: 16),
                  ],
                ),
              ),
              const SizedBox(height: 32),
              
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  QuickActionItem(
                    icon: Icons.history_rounded,
                    title: 'Lịch sử',
                    backgroundColor: const Color(0xFFF2F2F2),
                    iconColor: Colors.black,
                    onTap: () {},
                  ),
                  QuickActionItem(
                    icon: Icons.directions_car_filled_rounded,
                    title: 'Xe của tôi',
                    backgroundColor: const Color(0xFFF2F2F2),
                    iconColor: Colors.black,
                    onTap: () {},
                  ),
                  QuickActionItem(
                    icon: Icons.local_offer_rounded,
                    title: 'Khuyến mãi',
                    backgroundColor: const Color(0xFFF2F2F2),
                    iconColor: Colors.black,
                    onTap: () {},
                  ),
                  QuickActionItem(
                    icon: Icons.star_rounded,
                    title: 'SOS',
                    backgroundColor: const Color(0xFFFFE8E8),
                    iconColor: Colors.red,
                    textColor: Colors.red,
                    onTap: () {},
                  ),
                ],
              ),
              const SizedBox(height: 32),
              
              const Text(
                'Chuyến đi gần đây',
                style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold, color: Color(0xFF1A1A1A)),
              ),
              const SizedBox(height: 16),
              const RecentTripCard(
                pickup: '123 Nguyễn Văn Linh, Q.7',
                destination: 'Sân bay Tân Sơn Nhất',
                time: 'Hôm qua, 14:30',
              ),
              const SizedBox(height: 24),
              const PromoBanner(
                title: 'Giảm 20% cho\nchuyến đi Tối',
                code: 'SAFENIGHT',
              ),
            ],
          ),
        );
      },
    );
  }
}
