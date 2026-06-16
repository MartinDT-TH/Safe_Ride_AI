import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../../../core/constants/app_strings.dart';
import '../providers/role_provider.dart';
import '../widgets/role_card.dart';
import '../../../../customer/home/presentation/pages/customer_home_page.dart';
import '../../../../driver/dashboard/presentation/pages/driver_dashboard_page.dart';

class RoleSelectionPage extends StatelessWidget {
  const RoleSelectionPage({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: Colors.white,
      body: SafeArea(
        child: LayoutBuilder(
          builder: (context, constraints) {
            return SingleChildScrollView(
              child: ConstrainedBox(
                constraints: BoxConstraints(minHeight: constraints.maxHeight),
                child: IntrinsicHeight(
                  child: Consumer<RoleProvider>(
                    builder: (context, provider, child) {
                      return Padding(
                        padding: const EdgeInsets.symmetric(horizontal: 24),
                        child: Column(
                          children: [
                            const SizedBox(height: 50),
                            Center(
                              child: Container(
                                width: 85,
                                height: 85,
                                decoration: const BoxDecoration(
                                  color: Color(0xFFE8F2F2),
                                  shape: BoxShape.circle,
                                ),
                                child: const Icon(
                                  Icons.handshake_rounded,
                                  color: Color(0xFF006B70),
                                  size: 42,
                                ),
                              ),
                            ),
                            const SizedBox(height: 32),
                            const Text(
                              OnboardingStrings.welcome,
                              style: TextStyle(
                                fontSize: 34,
                                fontWeight: FontWeight.bold,
                                color: Color(0xFF006B70),
                                letterSpacing: -0.5,
                              ),
                            ),
                            const SizedBox(height: 8),
                            const Text(
                              OnboardingStrings.selectRoleQuestion,
                              style: TextStyle(
                                fontSize: 16,
                                color: Color(0xFF666666),
                              ),
                            ),
                            const SizedBox(height: 48),
                            RoleCard(
                              icon: Icons.directions_car_filled_rounded,
                              title: OnboardingStrings.customerTitle,
                              description:
                                  OnboardingStrings.customerDescription,
                              isSelected:
                                  provider.selectedRole ==
                                  AppValues.roleCustomer,
                              onTap: () =>
                                  provider.selectRole(AppValues.roleCustomer),
                            ),
                            const SizedBox(height: 16),
                            RoleCard(
                              icon: Icons.heat_pump_rounded,
                              title: OnboardingStrings.driverTitle,
                              description: OnboardingStrings.driverDescription,
                              isSelected:
                                  provider.selectedRole == AppValues.roleDriver,
                              onTap: () =>
                                  provider.selectRole(AppValues.roleDriver),
                            ),
                            const Spacer(),
                            Container(
                              padding: const EdgeInsets.symmetric(
                                horizontal: 16,
                                vertical: 8,
                              ),
                              decoration: BoxDecoration(
                                color: const Color(0xFFF9F6F4),
                                borderRadius: BorderRadius.circular(16),
                              ),
                              child: Row(
                                children: [
                                  Transform.scale(
                                    scale: 0.85,
                                    child: Switch(
                                      value: provider.rememberRole,
                                      activeThumbColor: Colors.white,
                                      activeTrackColor: const Color(0xFF2B61E1),
                                      onChanged: provider.setRememberRole,
                                    ),
                                  ),
                                  const SizedBox(width: 8),
                                  const Text(
                                    OnboardingStrings.rememberRole,
                                    style: TextStyle(
                                      fontSize: 14,
                                      fontWeight: FontWeight.w600,
                                      color: Color(0xFF333333),
                                    ),
                                  ),
                                ],
                              ),
                            ),
                            const SizedBox(height: 16),
                            SizedBox(
                              width: double.infinity,
                              height: 58,
                              child: ElevatedButton(
                                onPressed: provider.selectedRole == null
                                    ? null
                                    : () {
                                        if (provider.selectedRole ==
                                            AppValues.roleCustomer) {
                                          Navigator.pushReplacement(
                                            context,
                                            MaterialPageRoute(
                                              builder: (_) =>
                                                  const CustomerHomePage(),
                                            ),
                                          );
                                        } else if (provider.selectedRole ==
                                            AppValues.roleDriver) {
                                          Navigator.pushReplacement(
                                            context,
                                            MaterialPageRoute(
                                              builder: (_) =>
                                                  const DriverDashboardPage(),
                                            ),
                                          );
                                        }
                                      },
                                style: ElevatedButton.styleFrom(
                                  backgroundColor: const Color(0xFFEBE9E7),
                                  foregroundColor: const Color(0xFF666666),
                                  disabledBackgroundColor: const Color(
                                    0xFFEBE9E7,
                                  ).withValues(alpha: 0.5),
                                  elevation: 0,
                                  shape: RoundedRectangleBorder(
                                    borderRadius: BorderRadius.circular(18),
                                  ),
                                ),
                                child: Row(
                                  mainAxisAlignment: MainAxisAlignment.center,
                                  children: [
                                    const Text(
                                      OnboardingStrings.continueLabel,
                                      style: TextStyle(
                                        fontSize: 16,
                                        fontWeight: FontWeight.bold,
                                      ),
                                    ),
                                    const SizedBox(width: 8),
                                    Icon(Icons.arrow_forward, size: 20),
                                  ],
                                ),
                              ),
                            ),
                            const SizedBox(height: 32),
                          ],
                        ),
                      );
                    },
                  ),
                ),
              ),
            );
          },
        ),
      ),
    );
  }
}

