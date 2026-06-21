import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../../../core/constants/app_colors.dart';
import '../../../../../core/constants/app_strings.dart';
import '../../../../../core/widgets/app_dialog.dart';
import '../../../../auth/presentation/pages/login_page.dart';
import '../../../../auth/presentation/providers/auth_provider.dart';
import '../../../../driver/registration/presentation/pages/identity_verification_page.dart';
import 'edit_profile_page.dart';
import '../widgets/profile_menu_tile.dart';
import '../../../../driver/dashboard/presentation/pages/driver_dashboard_page.dart';
import '../../../../customer/booking/presentation/providers/booking_provider.dart';
import '../../../../customer/home/presentation/pages/customer_home_page.dart';
import '../../../../shared/onboarding/presentation/providers/role_provider.dart';

class ProfilePage extends StatefulWidget {
  const ProfilePage({super.key});

  @override
  State<ProfilePage> createState() => _ProfilePageState();
}

class _ProfilePageState extends State<ProfilePage> {
  bool _isDarkMode = false;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) return;
      context.read<AuthProvider>().loadLinkedAccounts();
    });
  }

  @override
  Widget build(BuildContext context) {
    final auth = context.watch<AuthProvider>();
    final roleProvider = context.watch<RoleProvider>();
    final bookingProvider = context.watch<BookingProvider>();
    final hasActiveBooking = bookingProvider.activeBooking != null;

    return Scaffold(
      backgroundColor: const Color(0xFFFCF9F9), //0xFFFDFBFA), // Light warm background as seen in image
      appBar: AppBar(
        backgroundColor: Colors.white,
        elevation: 0.5, //0
        // leading: IconButton( // tắt bỏ luôn cho đẹp
        //   icon: const Icon(Icons.arrow_back, color: Color(0xFF006B70)), // 0xFF263238
        //   onPressed: () {}, //onPressed: () => Navigator.pop(context) // lỗi
        // ),
        title: const Text(
          ProfileStrings.profileAndSettings,
          style: TextStyle(
            color: Color(0xFF006B70), // Color(0xFF007A87), // Primary teal
            fontWeight: FontWeight.bold, //FontWeight.w700,
            fontSize: 20, // fontSize: 18,
          ),
        ),
        centerTitle: true,
      ),
      body: SingleChildScrollView(
        // padding: const EdgeInsets.symmetric(vertical: 20),
        physics: const BouncingScrollPhysics(),
        padding: const EdgeInsets.fromLTRB(20, 24, 20, 40),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            // 1. Profile Summary Card
            _buildProfileSummary(auth),
            const SizedBox(height: 32),

            // 2. Chuyển sang chế độ Tài xế
            if (auth.isDriverEligible)
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
                      const Icon(
                        Icons.directions_car_rounded,
                        color: AppColors.primary, //Color(0xFF006B70)
                        size: 28,
                      ),
                      const SizedBox(width: 16),
                      const Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              ProfileStrings.switchToDriver,
                              style: TextStyle(
                                fontWeight: FontWeight.bold,
                                color: Color(0xFF006B70),
                                fontSize: 15,
                              ),
                            ),
                            Text(
                              ProfileStrings.startReceivingTrips,
                              style: TextStyle(
                                color: Color(0xFF666666),
                                fontSize: 13,
                              ),
                            ),
                          ],
                        ),
                      ),
                      Switch(
                        value: roleProvider.isDriver,
                        onChanged: hasActiveBooking 
                          ? (val) {
                              ScaffoldMessenger.of(context).showSnackBar(
                                const SnackBar(
                                  content: Text('Bạn không thể chuyển chế độ khi đang có chuyến đi hoạt động.'),
                                ),
                              );
                            }
                          : (val) async {
                              final role = val ? AppValues.roleDriver : AppValues.roleCustomer;
                              final navigator = Navigator.of(context);
                              await roleProvider.selectRole(role);

                              if (!mounted) return;

                              final Widget destination = val
                                  ? const DriverDashboardPage()
                                  : const CustomerHomePage();

                              navigator.pushAndRemoveUntil(
                                MaterialPageRoute(builder: (_) => destination),
                                (route) => false,
                              );
                            },
                        activeThumbColor: Colors.white,
                        activeTrackColor: const Color(0xFF006B70),
                      ),
                    ],
                  ),
                ),
              ),

              const SizedBox(height: 24),



            // 3. Section: TÀI KHOẢN
            _buildSectionLabel(ProfileStrings.accountSection),
            _buildMenuContainer([
              ProfileMenuTile(
                icon: Icons.person_search_outlined,
                title: ProfileStrings.editProfile,
                onTap: () => _navigateToEditProfile(auth),
              ),
              ProfileMenuTile(
                icon: Icons.link_rounded,
                title: ProfileStrings.linkedAccounts,
                trailingWidget: _buildLinkedAccountStatus(auth),
                onTap: auth.isLoading ? null : () => _handleLinkedAccounts(auth),
              ),
              ProfileMenuTile(
                icon: Icons.badge_outlined,
                title: 'Đăng ký tài xế',
                showDivider: false,
                onTap: () {
                  Navigator.of(context).push(
                    MaterialPageRoute(
                      builder: (_) => const IdentityVerificationPage(),
                    ),
                  );
                },
              ),
            ]),
            const SizedBox(height: 24),

            // 4. Section: ỨNG DỤNG & THÔNG BÁO
            _buildSectionLabel(ProfileStrings.appAndNotifications),
            _buildMenuContainer([
              const ProfileMenuTile(
                icon: Icons.notifications_none_rounded,
                title: ProfileStrings.notificationSettings,
              ),
              const ProfileMenuTile(
                icon: Icons.language_rounded,
                title: ProfileStrings.language,
                trailingText: ProfileStrings.vietnamese,
              ),
              ProfileMenuTile(
                icon: Icons.nightlight_round_outlined,
                title: ProfileStrings.darkMode,
                showDivider: false,
                trailingWidget: Transform.scale(
                  scale: 0.8,
                  child: Switch(
                    value: _isDarkMode,
                    onChanged: (val) => setState(() => _isDarkMode = val),
                    activeThumbColor: Colors.white,
                    activeTrackColor: AppColors.primary,
                  ),
                ),
              ),
            ]),

            const SizedBox(height: 24),

            // 5. Section: HỖ TRỢ & PHÁP LÝ
            _buildSectionLabel(ProfileStrings.supportAndLegal),
            _buildMenuContainer([
              const ProfileMenuTile(
                icon: Icons.help_outline_rounded,
                title: ProfileStrings.helpCenter,
              ),
              const ProfileMenuTile(
                icon: Icons.security_outlined, 
                title: AuthStrings.privacyPolicy,
              ),
              const ProfileMenuTile(
                icon: Icons.description_outlined,
                title: AuthStrings.termsOfService,
                showDivider: false,
              ),
            ]),

            const SizedBox(height: 24),
            const Center(
              child: Text(
                ProfileStrings.appVersion,
                style: TextStyle(
                  color: Color(0xFF90A4AE),
                  fontSize: 13,
                  fontWeight: FontWeight.w500,
                ),
              ),
            ),
            const SizedBox(height: 24),

            // 6. Logout Button
            _buildLogoutButton(context),
            const SizedBox(height: 24),
          ],
        ),
      ),
    );
  }

  Widget _buildProfileSummary(AuthProvider auth) {
    return Container(
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: const Color(0xFFF0F0F0)),
        boxShadow: [
          BoxShadow(
            color: Colors.black.withOpacity(0.02),
            blurRadius: 10,
            offset: const Offset(0, 4),
          ),
        ],
      ),
      child: Row(
        children: [
          Stack(
            children: [
              Container(
                padding: const EdgeInsets.all(3),
                decoration: BoxDecoration(
                  shape: BoxShape.circle,
                  border: Border.all(color: const Color(0xFF007A87), width: 1.5),
                ),
                child: CircleAvatar(
                  radius: 40,
                  backgroundColor: const Color(0xFFF5F5F5),
                  backgroundImage: auth.avatarUrl != null ? NetworkImage(auth.avatarUrl!) : null,
                  child: auth.avatarUrl == null
                      ? const Icon(Icons.person, size: 40, color: Color(0xFFBDBDBD))
                      : null,
                ),
              ),
              Positioned(
                bottom: 2,
                right: 2,
                child: Container(
                  padding: const EdgeInsets.all(1.5),
                  decoration: const BoxDecoration(
                    color: Colors.white,
                    shape: BoxShape.circle,
                  ),
                  child: const Icon(
                    Icons.verified,
                    color: Color(0xFF007A87),
                    size: 20,
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(width: 16), 
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  auth.fullName ?? 'Alex Johnson',
                  style: const TextStyle(
                    fontSize: 22,
                    fontWeight: FontWeight.w800,
                    color: Color(0xFF1F1F1F),
                    letterSpacing: -0.5,
                  ),
                ),
                const SizedBox(height: 4),
                Text(
                  auth.email ?? 'alex.johnson@example.com',
                  style: const TextStyle(
                    fontSize: 14,
                    color: Color(0xFF78909C),
                    fontWeight: FontWeight.w500,
                  ),
                ),
                Text(
                  auth.phoneNumber ?? '+84 123 456 789',
                  style: const TextStyle(
                    fontSize: 14,
                    color: Color(0xFF78909C),
                    fontWeight: FontWeight.w500,
                  ),
                ),
              ],
            ),
          ),
        ],
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
            fontWeight: FontWeight.w700,
            color: Color(0xFF78909C),
            letterSpacing: 0.5,
          ),
        ),
      ),
    );
  }

  Widget _buildLinkedAccountStatus(AuthProvider auth) {
    final status = auth.googleLinked
        ? auth.googleEmail ?? ProfileStrings.linked
        : ProfileStrings.notLinked;
    final color = auth.googleLinked
        ? const Color(0xFF006B70)
        : const Color(0xFFF59E0B);

    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 150),
          child: Text(
            status,
            overflow: TextOverflow.ellipsis,
            style: TextStyle(
              fontSize: 13,
              color: color,
              fontWeight: FontWeight.w600,
            ),
          ),
        ),
        const SizedBox(width: 4),
        Icon(Icons.chevron_right, color: Colors.grey.shade400, size: 20),
      ],
    );
  }

   void _navigateToEditProfile(AuthProvider auth) {
    Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => EditProfilePage(phoneNumber: auth.phoneNumber),
      ),
    );
  }

  void _handleLinkedAccounts(AuthProvider auth) {
    if (auth.googleLinked) {
      _confirmUnlinkGoogle(context);
    } else {
      _linkGoogle(context);
    }
  }

  Future<void> _linkGoogle(BuildContext context) async {
    final ok = await context.read<AuthProvider>().linkGoogleAccount();
    if (!context.mounted || ok) return;
    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(content: Text(ProfileStrings.linkGoogleFailed)),
    );
  }

  void _confirmUnlinkGoogle(BuildContext context) {
    AppDialog.show(
      context: context,
      icon: Icons.link_off_rounded,
      title: ProfileStrings.unlinkGoogleQuestion,
      description: ProfileStrings.unlinkGoogleDescription,
      confirmText: ProfileStrings.unlinkAccount,
      cancelText: AppStrings.cancel,
      onConfirm: () async {
        Navigator.of(context, rootNavigator: true).pop();
        final ok = await context.read<AuthProvider>().unlinkGoogleAccount();
        if (!context.mounted || ok) return;
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text(ProfileStrings.unlinkGoogleFailed)),
        );
      },
    );
  }


  //  Widget _buildMenuContainer(List<Widget> children) {
  //   return Container(
  //     decoration: BoxDecoration(
  //       color: Colors.white,
  //       borderRadius: BorderRadius.circular(16),
  //       border: Border.all(color: const Color(0xFFF0F0F0)),
  //     ),
  //     child: Column(children: children),
  //   );
  // }
  Widget _buildMenuContainer(List<Widget> children) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 20),
      child: Container(
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(20),
          border: Border.all(color: Colors.grey.shade100),
        ),
        child: Column(children: children),
      ),
    );
  }

  // String _displayName(String? fullName) {
  //   final value = fullName?.trim() ?? '';
  //   return value.isEmpty || value == HomeStrings.defaultUser
  //       ? HomeStrings.defaultUser
  //       : value;
  // }

  // String _initials(String? fullName) {
  //   final name = _displayName(fullName);
  //   if (name == HomeStrings.defaultUser) return HomeStrings.defaultInitials;
  //   final words = name.split(RegExp(r'\s+'));
  //   return words.take(2).map((word) => word[0].toUpperCase()).join();
  // }

  // ImageProvider? _avatarImage(String? avatarUrl) {
  //   final value = avatarUrl?.trim() ?? '';
  //   return value.isEmpty ? null : NetworkImage(value);
  // }




  // String _displayName(String? fullName) {
  //   final value = fullName?.trim() ?? '';
  //   return value.isEmpty || value == HomeStrings.defaultUser
  //       ? HomeStrings.defaultUser
  //       : value;
  // }

  // String _initials(String? fullName) {
  //   final name = _displayName(fullName);
  //   if (name == HomeStrings.defaultUser) return HomeStrings.defaultInitials;
  //   final words = name.split(RegExp(r'\s+'));
  //   return words.take(2).map((word) => word[0].toUpperCase()).join();
  // }

  // ImageProvider? _avatarImage(String? avatarUrl) {
  //   final value = avatarUrl?.trim() ?? '';
  //   return value.isEmpty ? null : NetworkImage(value);
  // }



  Widget _buildLogoutButton(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 20),
      child: OutlinedButton(
        onPressed: () => _confirmLogout(context),
        style: OutlinedButton.styleFrom(
          side: const BorderSide(color: Color(0xFFB71C1C), width: 1.5),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(12),
          ),
        ),
        child: const Row(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Icon(Icons.logout_rounded, color: Color(0xFFB71C1C)),
            SizedBox(width: 8),
            Text(
              'Đăng xuất',
              style: TextStyle(
                color: Color(0xFFB71C1C),
                fontWeight: FontWeight.w700,
                fontSize: 16,
              ),
            ),
          ],
        ),
      ),
    );
  }

  void _confirmLogout(BuildContext context) {
    AppDialog.show(
      context: context,
      icon: Icons.logout_rounded,
      title: ProfileStrings.logoutQuestion,
      description: ProfileStrings.logoutDescription,
      confirmText: ProfileStrings.logout,
      cancelText: AppStrings.cancel,
      onConfirm: () async {
        Navigator.pop(context);
        final success = await context.read<AuthProvider>().logout();
        if (!mounted) return;
        if (success) {
          Navigator.of(context).pushAndRemoveUntil(
            MaterialPageRoute(builder: (_) => const LoginPage()),
            (route) => false,
          );
        }
      },
    );
  }
}