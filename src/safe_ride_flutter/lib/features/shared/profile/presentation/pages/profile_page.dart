import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:dio/dio.dart';

import '../../../../../core/constants/app_colors.dart';
import '../../../../../core/constants/app_strings.dart';
import '../../../../../core/network/dio_client.dart';
import '../../../../../core/localization/locale_provider.dart';
import '../../../../../core/localization/localization_extensions.dart';
import '../../../../../core/widgets/app_dialog.dart';
import '../../../../auth/presentation/providers/auth_provider.dart';
import '../../../../driver/registration/presentation/pages/identity_verification_page.dart';
import 'edit_profile_page.dart';
import '../widgets/profile_menu_tile.dart';
import '../../../../driver/dashboard/presentation/pages/driver_dashboard_page.dart';
import '../../../../customer/booking/presentation/providers/booking_provider.dart';
import '../../../../customer/home/presentation/pages/customer_home_page.dart';
import '../../../../shared/onboarding/presentation/providers/role_provider.dart';
import '../../../../driver/dashboard/presentation/providers/driver_dashboard_provider.dart';
import '../../../../shared/feedback/presentation/pages/driver_reviews_page.dart';
import '../widgets/language_picker_sheet.dart';
import '../widgets/driver_reviews_profile_menu_tile.dart';
import '../../../risk_protection/presentation/pages/driver_liabilities_page.dart';

class ProfilePage extends StatefulWidget {
  ProfilePage({super.key});

  @override
  State<ProfilePage> createState() => _ProfilePageState();
}

class _ProfilePageState extends State<ProfilePage> {
  bool _isDarkMode = false;

  Future<void> _showMatchingPreferences(AuthProvider auth) async {
    final token = auth.token;
    if (token == null || token.isEmpty) return;

    final dio = DioClient().dio;
    try {
      final response = await dio.get(
        ApiEndpoints.driverMatchingPreferences,
        options: Options(headers: {'Authorization': 'Bearer $token'}),
      );
      final data = Map<String, dynamic>.from(response.data as Map);
      var acceptLongPickup = data['acceptLongPickupTrips'] == true;
      var acceptLongDistance = data['acceptLongDistanceTrips'] == true;
      if (!mounted) return;

      await showDialog<void>(
        context: context,
        builder: (dialogContext) => StatefulBuilder(
          builder: (context, setDialogState) => AlertDialog(
            title: const Text('Tùy chọn nhận chuyến'),
            content: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                SwitchListTile(
                  contentPadding: EdgeInsets.zero,
                  title: const Text('Nhận chuyến có quãng đón xa'),
                  value: acceptLongPickup,
                  onChanged: (value) => setDialogState(
                    () => acceptLongPickup = value,
                  ),
                ),
                SwitchListTile(
                  contentPadding: EdgeInsets.zero,
                  title: const Text('Nhận chuyến đường dài'),
                  value: acceptLongDistance,
                  onChanged: (value) => setDialogState(
                    () => acceptLongDistance = value,
                  ),
                ),
              ],
            ),
            actions: [
              TextButton(
                onPressed: () => Navigator.of(dialogContext).pop(),
                child: const Text('Hủy'),
              ),
              FilledButton(
                onPressed: () async {
                  await dio.put(
                    ApiEndpoints.driverMatchingPreferences,
                    data: {
                      'acceptLongPickupTrips': acceptLongPickup,
                      'acceptLongDistanceTrips': acceptLongDistance,
                    },
                    options: Options(headers: {'Authorization': 'Bearer $token'}),
                  );
                  if (dialogContext.mounted) {
                    Navigator.of(dialogContext).pop();
                  }
                },
                child: const Text('Lưu'),
              ),
            ],
          ),
        ),
      );
    } on DioException {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Không thể tải tùy chọn nhận chuyến.')),
      );
    }
  }

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
    final l10n = context.l10n;
    final auth = context.watch<AuthProvider>();
    final roleProvider = context.watch<RoleProvider>();
    final bookingProvider = context.watch<BookingProvider>();
    final hasActiveBooking = bookingProvider.activeBooking != null;

    return Scaffold(
      backgroundColor: Color(
        0xFFFCF9F9,
      ), //0xFFFDFBFA), // Light warm background as seen in image
      appBar: AppBar(
        backgroundColor: Colors.white,
        elevation: 0.5, //0
        // leading: IconButton( // tắt bỏ luôn cho đẹp
        //   icon: Icon(Icons.arrow_back, color: Color(0xFF006B70)), // 0xFF263238
        //   onPressed: () {}, //onPressed: () => Navigator.pop(context) // lỗi
        // ),
        title: Text(
          l10n.profileAndSettings,
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
        physics: BouncingScrollPhysics(),
        padding: const EdgeInsets.fromLTRB(20, 24, 20, 40),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            // 1. Profile Summary Card
            _buildProfileSummary(auth),
            SizedBox(height: 32),

            // 2. Chuyển sang chế độ Tài xế
            if (auth.isDriverEligible)
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: 20),
                child: Container(
                  padding: const EdgeInsets.all(16),
                  decoration: BoxDecoration(
                    color: Color(0xFFE8F2F2),
                    borderRadius: BorderRadius.circular(16),
                  ),
                  child: Row(
                    children: [
                      Icon(
                        Icons.directions_car_rounded,
                        color: AppColors.primary, //Color(0xFF006B70)
                        size: 28,
                      ),
                      SizedBox(width: 16),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              l10n.switchToDriver,
                              style: TextStyle(
                                fontWeight: FontWeight.bold,
                                color: Color(0xFF006B70),
                                fontSize: 15,
                              ),
                            ),
                            Text(
                              l10n.startReceivingTrips,
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
                        onChanged: (val) async {
                          final driverProvider = context
                              .read<DriverDashboardProvider>();
                          // Allow switching BACK to customer regardless of active booking
                          // (since they are ALREADY in customer mode conceptually if hasActiveBooking is true)
                          if (val && hasActiveBooking) {
                            ScaffoldMessenger.of(context).showSnackBar(
                              SnackBar(
                                content: Text(l10n.cannotSwitchToDriver),
                                behavior: SnackBarBehavior.floating,
                              ),
                            );
                            return;
                          }
                          if (!val && driverProvider.activeTrip != null) {
                            ScaffoldMessenger.of(context).showSnackBar(
                              SnackBar(
                                content: Text(l10n.cannotSwitchToCustomer),
                                behavior: SnackBarBehavior.floating,
                              ),
                            );
                            return;
                          }

                          final role = val
                              ? AppValues.roleDriver
                              : AppValues.roleCustomer;
                          final navigator = Navigator.of(context);
                          await driverProvider.goOffline(
                            accessToken: auth.token,
                          );
                          if (!mounted) return;
                          await roleProvider.selectRole(role);

                          if (!mounted) return;

                          final Widget destination = val
                              ? DriverDashboardPage()
                              : CustomerHomePage();

                          navigator.pushAndRemoveUntil(
                            MaterialPageRoute(builder: (_) => destination),
                            (route) => false,
                          );
                        },
                        activeThumbColor: Colors.white,
                        activeTrackColor: Color(0xFF006B70),
                      ),
                    ],
                  ),
                ),
              ),

            SizedBox(height: 24),

            // 3. Section: TÀI KHOẢN
            _buildSectionLabel(l10n.accountSection),
            _buildMenuContainer([
              ProfileMenuTile(
                icon: Icons.person_search_outlined,
                title: l10n.editProfile,
                onTap: () => _navigateToEditProfile(auth),
              ),
              DriverReviewsProfileMenuTile(
                isDriver: roleProvider.isDriver,
                onTap: () => _navigateToDriverReviews(auth),
              ),
              if (roleProvider.isDriver)
                ProfileMenuTile(
                  icon: Icons.tune_rounded,
                  title: 'Tùy chọn nhận chuyến',
                  onTap: () => _showMatchingPreferences(auth),
                ),
              if (roleProvider.isDriver)
                ProfileMenuTile(
                  icon: Icons.gavel_outlined,
                  title: l10n.driverLiabilities,
                  onTap: () => Navigator.of(context).push(
                    MaterialPageRoute(
                      builder: (_) => const DriverLiabilitiesPage(),
                    ),
                  ),
                ),
              _buildLinkedAccountItem(auth),
              ProfileMenuTile(
                icon: Icons.badge_outlined,
                title: l10n.registerAsDriver,
                showDivider: false,
                onTap: () {
                  Navigator.of(context).push(
                    MaterialPageRoute(
                      builder: (_) => IdentityVerificationPage(),
                    ),
                  );
                },
              ),
            ]),
            SizedBox(height: 24),

            // 4. Section: ỨNG DỤNG & THÔNG BÁO
            _buildSectionLabel(l10n.appAndNotifications),
            _buildMenuContainer([
              ProfileMenuTile(
                icon: Icons.notifications_none_rounded,
                title: l10n.notificationSettings,
              ),
              ProfileMenuTile(
                icon: Icons.language_rounded,
                title: l10n.language,
                trailingText: _currentLanguageName(context),
                onTap: () => _showLanguagePicker(context),
              ),
              ProfileMenuTile(
                icon: Icons.nightlight_round_outlined,
                title: l10n.darkMode,
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

            SizedBox(height: 24),

            // 5. Section: HỖ TRỢ & PHÁP LÝ
            _buildSectionLabel(l10n.supportAndLegal),
            _buildMenuContainer([
              ProfileMenuTile(
                icon: Icons.help_outline_rounded,
                title: l10n.helpCenter,
              ),
              ProfileMenuTile(
                icon: Icons.security_outlined,
                title: l10n.privacyPolicy,
              ),
              ProfileMenuTile(
                icon: Icons.description_outlined,
                title: l10n.termsOfService,
                showDivider: false,
              ),
            ]),

            SizedBox(height: 24),
            Center(
              child: Text(
                context.l10n.appVersion,
                style: TextStyle(
                  color: Color(0xFF90A4AE),
                  fontSize: 13,
                  fontWeight: FontWeight.w500,
                ),
              ),
            ),
            SizedBox(height: 24),

            // 6. Logout Button
            _buildLogoutButton(context),
            SizedBox(height: 24),
          ],
        ),
      ),
    );
  }

  String _currentLanguageName(BuildContext context) {
    final l10n = context.l10n;
    switch (context.watch<LocaleProvider>().locale.languageCode) {
      case 'en':
        return l10n.english;
      case 'ko':
        return l10n.korean;
      case 'ja':
        return l10n.japanese;
      case 'zh':
        return l10n.simplifiedChinese;
      default:
        return l10n.vietnamese;
    }
  }

  void _showLanguagePicker(BuildContext context) {
    showModalBottomSheet<void>(
      context: context,
      showDragHandle: true,
      builder: (_) => LanguagePickerSheet(),
    );
  }

  Widget _buildProfileSummary(AuthProvider auth) {
    return Container(
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: Color(0xFFF0F0F0)),
        boxShadow: [
          BoxShadow(
            color: Colors.black.withOpacity(0.02),
            blurRadius: 10,
            offset: Offset(0, 4),
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
                  border: Border.all(color: Color(0xFF007A87), width: 1.5),
                ),
                child: CircleAvatar(
                  radius: 40,
                  backgroundColor: Color(0xFFF5F5F5),
                  backgroundImage: auth.avatarUrl != null
                      ? NetworkImage(auth.avatarUrl!)
                      : null,
                  child: auth.avatarUrl == null
                      ? Icon(Icons.person, size: 40, color: Color(0xFFBDBDBD))
                      : null,
                ),
              ),
              Positioned(
                bottom: 2,
                right: 2,
                child: Container(
                  padding: const EdgeInsets.all(1.5),
                  decoration: BoxDecoration(
                    color: Colors.white,
                    shape: BoxShape.circle,
                  ),
                  child: Icon(
                    Icons.verified,
                    color: Color(0xFF007A87),
                    size: 20,
                  ),
                ),
              ),
            ],
          ),
          SizedBox(width: 16),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  auth.fullName ?? 'Alex Johnson',
                  style: TextStyle(
                    fontSize: 22,
                    fontWeight: FontWeight.w800,
                    color: Color(0xFF1F1F1F),
                    letterSpacing: -0.5,
                  ),
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                ),
                SizedBox(height: 4),
                Text(
                  auth.email ?? 'alex.johnson@example.com',
                  style: TextStyle(
                    fontSize: 14,
                    color: Color(0xFF78909C),
                    fontWeight: FontWeight.w500,
                  ),
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  softWrap: false,
                ),
                Text(
                  auth.phoneNumber ?? '+84 123 456 789',
                  style: TextStyle(
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
          style: TextStyle(
            fontSize: 13,
            fontWeight: FontWeight.w700,
            color: Color(0xFF78909C),
            letterSpacing: 0.5,
          ),
        ),
      ),
    );
  }

  Widget _buildLinkedAccountItem(AuthProvider auth) {
    final status = auth.googleLinked
        ? auth.googleEmail ?? context.l10n.linked
        : context.l10n.notLinked;
    final color = auth.googleLinked ? Color(0xFF006B70) : Color(0xFFF59E0B);

    return InkWell(
      onTap: auth.isLoading ? null : () => _handleLinkedAccounts(auth),
      child: Column(
        children: [
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
            child: Row(
              children: [
                Icon(Icons.link_rounded, color: Colors.grey.shade600, size: 24),
                SizedBox(width: 16),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Text(
                        context.l10n.linkedAccounts,
                        style: TextStyle(
                          fontSize: 16,
                          fontWeight: FontWeight.w400,
                          color: Color(0xFF333333),
                        ),
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                      ),
                      if (auth.googleLinked)
                        Text(
                          status,
                          style: TextStyle(
                            fontSize: 13,
                            color: color,
                            fontWeight: FontWeight.w500,
                          ),
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          softWrap: false,
                        ),
                    ],
                  ),
                ),
                if (!auth.googleLinked)
                  Text(
                    status,
                    style: TextStyle(
                      fontSize: 13,
                      color: color,
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                SizedBox(width: 4),
                Icon(
                  Icons.chevron_right,
                  color: Colors.grey.shade400,
                  size: 20,
                ),
              ],
            ),
          ),
          Divider(
            height: 1,
            indent: 16,
            endIndent: 16,
            color: Colors.grey.shade100,
          ),
        ],
      ),
    );
  }

  void _navigateToEditProfile(AuthProvider auth) {
    Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => EditProfilePage(phoneNumber: auth.phoneNumber),
      ),
    );
  }

  void _navigateToDriverReviews(AuthProvider auth) {
    final driverId = auth.userId?.trim();
    if (driverId == null || driverId.isEmpty) {
      ScaffoldMessenger.of(
        context,
      ).showSnackBar(SnackBar(content: Text(context.l10n.sessionExpired)));
      return;
    }

    Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) =>
            DriverReviewsPage(driverId: driverId, driverName: auth.fullName),
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
    ScaffoldMessenger.of(
      context,
    ).showSnackBar(SnackBar(content: Text(context.l10n.linkGoogleFailed)));
  }

  void _confirmUnlinkGoogle(BuildContext context) {
    AppDialog.show(
      context: context,
      icon: Icons.link_off_rounded,
      title: context.l10n.unlinkGoogleQuestion,
      description: context.l10n.unlinkGoogleDescription,
      confirmText: context.l10n.unlinkAccount,
      cancelText: context.l10n.cancel,
      onConfirm: () async {
        Navigator.of(context, rootNavigator: true).pop();
        final ok = await context.read<AuthProvider>().unlinkGoogleAccount();
        if (!context.mounted || ok) return;
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(context.l10n.unlinkGoogleFailed)),
        );
      },
    );
  }

  //  Widget _buildMenuContainer(List<Widget> children) {
  //   return Container(
  //     decoration: BoxDecoration(
  //       color: Colors.white,
  //       borderRadius: BorderRadius.circular(16),
  //       border: Border.all(color: Color(0xFFF0F0F0)),
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
          side: BorderSide(color: Color(0xFFB71C1C), width: 1.5),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(12),
          ),
        ),
        child: Row(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Icon(Icons.logout_rounded, color: Color(0xFFB71C1C)),
            SizedBox(width: 8),
            Text(
              context.l10n.logout,
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
      title: context.l10n.logoutQuestion,
      description: context.l10n.logoutDescription,
      confirmText: context.l10n.logout,
      cancelText: context.l10n.cancel,
      onConfirm: () async {
        Navigator.pop(context);
        final messenger = ScaffoldMessenger.of(context);
        final success = await context.read<AuthProvider>().logout();
        if (!mounted) return;
        if (!success) {
          messenger.showSnackBar(
            SnackBar(
              content: Text(context.l10n.logoutFailed),
              behavior: SnackBarBehavior.floating,
            ),
          );
        }
      },
    );
  }
}
