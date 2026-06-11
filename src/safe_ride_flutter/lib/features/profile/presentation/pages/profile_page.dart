import 'package:flutter/material.dart';
import '../../../../core/constants/app_colors.dart';
import '../../../../core/widgets/app_dialog.dart';
import '../../../auth/presentation/pages/login_page.dart';
import '../widgets/profile_menu_tile.dart';

class ProfilePage extends StatefulWidget {
  const ProfilePage({super.key});

  @override
  State<ProfilePage> createState() => _ProfilePageState();
}

class _ProfilePageState extends State<ProfilePage> {
  bool _isDarkMode = false;
  bool _isDriverMode = false;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFFCF9F9),
      appBar: AppBar(
        backgroundColor: Colors.white,
        elevation: 0.5,
        leading: IconButton(
          icon: const Icon(Icons.arrow_back, color: Color(0xFF006B70)),
          onPressed: () {
          },
        ),
        title: const Text(
          'Hồ sơ & Cài đặt',
          style: TextStyle(
            color: Color(0xFF006B70),
            fontWeight: FontWeight.bold,
            fontSize: 20,
          ),
        ),
        centerTitle: true,
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.symmetric(vertical: 20),
        child: Column(
          children: [
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 20),
              child: Container(
                padding: const EdgeInsets.all(20),
                decoration: BoxDecoration(
                  color: Colors.white,
                  borderRadius: BorderRadius.circular(20),
                  border: Border.all(color: Colors.grey.shade100),
                ),
                child: Row(
                  children: [
                    Stack(
                      children: [
                        const CircleAvatar(
                          radius: 40,
                          backgroundImage: NetworkImage('https://i.pravatar.cc/150?img=11'),
                        ),
                        Positioned(
                          bottom: 0,
                          right: 0,
                          child: Container(
                            padding: const EdgeInsets.all(2),
                            decoration: const BoxDecoration(
                              color: Colors.white,
                              shape: BoxShape.circle,
                            ),
                            child: const Icon(
                              Icons.verified,
                              color: Color(0xFF006B70),
                              size: 20,
                            ),
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(width: 16),
                    const Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            'Alex Johnson',
                            style: TextStyle(
                              fontSize: 22,
                              fontWeight: FontWeight.bold,
                              color: Color(0xFF1A1A1A),
                            ),
                          ),
                          SizedBox(height: 4),
                          Text(
                            'alex.johnson@example.com',
                            style: TextStyle(fontSize: 14, color: Color(0xFF666666)),
                          ),
                          Text(
                            '+84 123 456 789',
                            style: TextStyle(fontSize: 14, color: Color(0xFF666666)),
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
              ),
            ),
            const SizedBox(height: 20),

            // 2. Chuyển sang chế độ Tài xế
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 20),
              child: Container(
                padding: const EdgeInsets.all(16),
                decoration: BoxDecoration(
                  color: const Color(0xFFE8F2F2),
                  borderRadius: BorderRadius.circular(16),
                ),
                child: Row(
                  children: [
                    const Icon(Icons.directions_car_rounded, color: Color(0xFF006B70), size: 28),
                    const SizedBox(width: 16),
                    const Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          const Text(
                            'Chuyển sang chế độ Tài xế',
                            style: TextStyle(
                              fontWeight: FontWeight.bold,
                              color: Color(0xFF006B70),
                              fontSize: 15,
                            ),
                          ),
                          Text(
                            'Bắt đầu nhận chuyến đi',
                            style: TextStyle(color: Color(0xFF666666), fontSize: 13),
                          ),
                        ],
                      ),
                    ),
                    Switch(
                      value: _isDriverMode,
                      onChanged: (val) => setState(() => _isDriverMode = val),
                      activeColor: Colors.white,
                      activeTrackColor: const Color(0xFF006B70),
                    ),
                  ],
                ),
              ),
            ),

            const SizedBox(height: 24),

            // 3. Section: TÀI KHOẢN
            _buildSectionLabel('TÀI KHOẢN'),
            _buildMenuContainer([
              const ProfileMenuTile(
                icon: Icons.person_outline_rounded,
                title: 'Chỉnh sửa hồ sơ',
              ),
              const ProfileMenuTile(
                icon: Icons.link_rounded,
                title: 'Tài khoản liên kết',
                showDivider: false,
              ),
            ]),

            const SizedBox(height: 24),

            // 4. Section: ỨNG DỤNG & THÔNG BÁO
            _buildSectionLabel('ỨNG DỤNG & THÔNG BÁO'),
            _buildMenuContainer([
              const ProfileMenuTile(
                icon: Icons.notifications_none_rounded,
                title: 'Cài đặt thông báo',
              ),
              const ProfileMenuTile(
                icon: Icons.language_rounded,
                title: 'Ngôn ngữ',
                trailingText: 'Tiếng Việt',
              ),
              ProfileMenuTile(
                icon: Icons.nightlight_round_outlined,
                title: 'Chế độ tối',
                showDivider: false,
                trailingWidget: Switch(
                  value: _isDarkMode,
                  onChanged: (val) => setState(() => _isDarkMode = val),
                  activeColor: Colors.white,
                  activeTrackColor: const Color(0xFF006B70),
                ),
              ),
            ]),

            const SizedBox(height: 24),

            // 5. Section: HỖ TRỢ & PHÁP LÝ
            _buildSectionLabel('HỖ TRỢ & PHÁP LÝ'),
            _buildMenuContainer([
              const ProfileMenuTile(
                icon: Icons.help_outline_rounded,
                title: 'Trung tâm trợ giúp',
              ),
              const ProfileMenuTile(
                icon: Icons.shield_outlined,
                title: 'Chính sách bảo mật',
              ),
              const ProfileMenuTile(
                icon: Icons.description_outlined,
                title: 'Điều khoản dịch vụ',
                showDivider: false,
              ),
            ]),

            const SizedBox(height: 20),
            const Text(
              'Phiên bản ứng dụng: 2.4.1',
              style: TextStyle(color: Color(0xFF666666), fontSize: 13),
            ),
            const SizedBox(height: 24),

            // 6. Nút Đăng xuất
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 20),
              child: SizedBox(
                width: double.infinity,
                height: 56,
                child: OutlinedButton.icon(
                  onPressed: () {
                    AppDialog.show(
                      context: context,
                      icon: Icons.logout_rounded,
                      title: 'Đăng xuất?',
                      description: 'Bạn có chắc chắn muốn đăng xuất khỏi ứng dụng?',
                      confirmText: 'Đăng xuất',
                      cancelText: 'Hủy',
                      onConfirm: () {
                        // 1. Đóng Dialog bằng rootNavigator
                        Navigator.of(context, rootNavigator: true).pop();
                        
                        // 2. Chuyển về màn hình Login và xóa toàn bộ stack cũ
                        Navigator.of(context, rootNavigator: true).pushAndRemoveUntil(
                          MaterialPageRoute(builder: (context) => const LoginPage()),
                          (route) => false,
                        );
                      },
                    );
                  },
                  icon: const Icon(Icons.logout_rounded, color: Colors.red),
                  label: const Text(
                    'Đăng xuất',
                    style: TextStyle(color: Colors.red, fontWeight: FontWeight.bold, fontSize: 16),
                  ),
                  style: OutlinedButton.styleFrom(
                    side: const BorderSide(color: Colors.red, width: 1.5),
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(16),
                    ),
                  ),
                ),
              ),
            ),
            const SizedBox(height: 40),
          ],
        ),
      ),
    );
  }

  Widget _buildSectionLabel(String label) {
    return Padding(
      padding: const EdgeInsets.only(left: 24, bottom: 10),
      child: Align(
        alignment: Alignment.centerLeft,
        child: Text(
          label,
          style: const TextStyle(
            fontSize: 13,
            fontWeight: FontWeight.bold,
            color: Color(0xFF666666),
            letterSpacing: 0.5,
          ),
        ),
      ),
    );
  }

  Widget _buildMenuContainer(List<Widget> children) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 20),
      child: Container(
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(20),
          border: Border.all(color: Colors.grey.shade100),
        ),
        child: Column(
          children: children,
        ),
      ),
    );
  }
}
