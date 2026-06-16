import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../../../core/constants/app_strings.dart';
import '../../../../../core/widgets/app_dialog.dart';
import '../../../../auth/presentation/pages/login_page.dart';
import '../../../../auth/presentation/providers/auth_provider.dart';
import 'edit_profile_page.dart';
import '../widgets/profile_menu_tile.dart';
import '../../../../driver/dashboard/presentation/pages/driver_dashboard_page.dart';
import '../../../../customer/home/presentation/pages/customer_home_page.dart';
import '../../../../shared/onboarding/presentation/providers/role_provider.dart';

class ProfilePage extends StatefulWidget {
  const ProfilePage({super.key});

  @override
  State<ProfilePage> createState() => _ProfilePageState();
}

class _ProfilePageState extends State<ProfilePage> {
  bool _isDarkMode = false;
  bool _isDriverMode = false;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) return;
      final roleProvider = context.read<RoleProvider>();
      setState(() {
        _isDriverMode = roleProvider.selectedRole == AppValues.roleDriver;
      });
      context.read<AuthProvider>().loadLinkedAccounts();
    });
  }

  @override
  Widget build(BuildContext context) {
    final auth = context.watch<AuthProvider>();
    return Scaffold(
      backgroundColor: const Color(0xFFFCF9F9),
      appBar: AppBar(
        backgroundColor: Colors.white,
        elevation: 0.5,
        leading: IconButton(
          icon: const Icon(Icons.arrow_back, color: Color(0xFF006B70)),
          onPressed: () {},
        ),
        title: const Text(
          ProfileStrings.profileAndSettings,
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
                        CircleAvatar(
                          radius: 40,
                          backgroundColor: const Color(0xFFE8F2F2),
                          backgroundImage: _avatarImage(auth.avatarUrl),
                          child: _avatarImage(auth.avatarUrl) == null
                              ? Text(
                                  _initials(auth.fullName),
                                  style: const TextStyle(
                                    color: Color(0xFF006B70),
                                    fontSize: 24,
                                    fontWeight: FontWeight.bold,
                                  ),
                                )
                              : null,
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
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            _displayName(auth.fullName),
                            style: const TextStyle(
                              fontSize: 22,
                              fontWeight: FontWeight.bold,
                              color: Color(0xFF1A1A1A),
                            ),
                          ),
                          const SizedBox(height: 4),
                          if ((auth.email?.trim().isNotEmpty ?? false))
                            Text(
                              auth.email!,
                              style: const TextStyle(
                                fontSize: 14,
                                color: Color(0xFF666666),
                              ),
                            ),
                          Text(
                            auth.phoneNumber ?? '',
                            style: const TextStyle(
                              fontSize: 14,
                              color: Color(0xFF666666),
                            ),
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
                    const Icon(
                      Icons.directions_car_rounded,
                      color: Color(0xFF006B70),
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
                      value: _isDriverMode,
                      onChanged: (val) async {
                        setState(() => _isDriverMode = val);
                        final role = val ? AppValues.roleDriver : AppValues.roleCustomer;
                        await context.read<RoleProvider>().selectRole(role);
                        
                        if (!mounted) return;
                        
                        final Widget destination = val 
                            ? const DriverDashboardPage() 
                            : const CustomerHomePage();
                            
                        Navigator.of(context).pushAndRemoveUntil(
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
                icon: Icons.person_outline_rounded,
                title: ProfileStrings.editProfile,
                onTap: () async {
                  await Navigator.of(context).push(
                    MaterialPageRoute(
                      builder: (_) =>
                          EditProfilePage(phoneNumber: auth.phoneNumber),
                    ),
                  );
                },
              ),
              ProfileMenuTile(
                icon: Icons.link_rounded,
                title: ProfileStrings.linkedAccounts,
                showDivider: false,
                trailingWidget: _buildLinkedAccountStatus(auth),
                onTap: auth.isLoading
                    ? null
                    : () => auth.googleLinked
                          ? _confirmUnlinkGoogle(context)
                          : _linkGoogle(context),
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
                trailingWidget: Switch(
                  value: _isDarkMode,
                  onChanged: (val) => setState(() => _isDarkMode = val),
                  activeThumbColor: Colors.white,
                  activeTrackColor: const Color(0xFF006B70),
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
                icon: Icons.shield_outlined,
                title: AuthStrings.privacyPolicy,
              ),
              const ProfileMenuTile(
                icon: Icons.description_outlined,
                title: AuthStrings.termsOfService,
                showDivider: false,
              ),
            ]),

            const SizedBox(height: 20),
            const Text(
              ProfileStrings.appVersion,
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
                      title: ProfileStrings.logoutQuestion,
                      description: ProfileStrings.logoutDescription,
                      confirmText: ProfileStrings.logout,
                      cancelText: AppStrings.cancel,
                      onConfirm: () async {
                        // 1. Đóng Dialog bằng rootNavigator
                        Navigator.of(context, rootNavigator: true).pop();

                        final loggedOut = await context
                            .read<AuthProvider>()
                            .logout();
                        if (!context.mounted) return;

                        if (!loggedOut) {
                          ScaffoldMessenger.of(context).showSnackBar(
                            const SnackBar(
                              content: Text(ProfileStrings.logoutFailed),
                            ),
                          );
                          return;
                        }

                        // 2. Chuyển về màn hình Login và xóa toàn bộ stack cũ
                        Navigator.of(
                          context,
                          rootNavigator: true,
                        ).pushAndRemoveUntil(
                          MaterialPageRoute(
                            builder: (context) => const LoginPage(),
                          ),
                          (route) => false,
                        );
                      },
                    );
                  },
                  icon: const Icon(Icons.logout_rounded, color: Colors.red),
                  label: const Text(
                    ProfileStrings.logout,
                    style: TextStyle(
                      color: Colors.red,
                      fontWeight: FontWeight.bold,
                      fontSize: 16,
                    ),
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

  String _displayName(String? fullName) {
    final value = fullName?.trim() ?? '';
    return value.isEmpty || value == HomeStrings.defaultUser
        ? HomeStrings.defaultUser
        : value;
  }

  String _initials(String? fullName) {
    final name = _displayName(fullName);
    if (name == HomeStrings.defaultUser) return HomeStrings.defaultInitials;
    final words = name.split(RegExp(r'\s+'));
    return words.take(2).map((word) => word[0].toUpperCase()).join();
  }

  ImageProvider? _avatarImage(String? avatarUrl) {
    final value = avatarUrl?.trim() ?? '';
    return value.isEmpty ? null : NetworkImage(value);
  }
}

