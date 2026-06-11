import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../providers/role_provider.dart';
import '../widgets/role_card.dart';
import '../../../home/presentation/pages/customer_home_page.dart';

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
                    builder: (_, provider, __) {
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
                              'Chào mừng bạn!',
                              style: TextStyle(
                                fontSize: 34,
                                fontWeight: FontWeight.bold,
                                color: Color(0xFF006B70),
                                letterSpacing: -0.5,
                              ),
                            ),
                            const SizedBox(height: 8),
                            const Text(
                              'Bạn muốn bắt đầu với vai trò nào?',
                              style: TextStyle(
                                fontSize: 16,
                                color: Color(0xFF666666),
                              ),
                            ),
                            const SizedBox(height: 48),
                            RoleCard(
                              icon: Icons.directions_car_filled_rounded,
                              title: 'Tôi là Khách hàng',
                              description:
                                  'Đặt xe nhanh chóng, an toàn và theo dõi hành trình trực tiếp.',
                              isSelected: provider.selectedRole == 'customer',
                              onTap: () => provider.selectRole('customer'),
                            ),
                            const SizedBox(height: 16),
                            RoleCard(
                              icon: Icons.heat_pump_rounded,
                              title: 'Tôi là Tài xế',
                              description:
                                  'Nhận việc linh hoạt, tăng thu nhập và quản lý chuyến đi dễ dàng.',
                              isSelected: provider.selectedRole == 'driver',
                              onTap: () => provider.selectRole('driver'),
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
                                      activeColor: Colors.white,
                                      activeTrackColor: const Color(0xFF2B61E1),
                                      onChanged: provider.setRememberRole,
                                    ),
                                  ),
                                  const SizedBox(width: 8),
                                  const Text(
                                    'Ghi nhớ lựa chọn cho lần sau',
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
                                            'customer') {
                                          Navigator.pushReplacement(
                                            context,
                                            MaterialPageRoute(
                                              builder: (_) =>
                                                  const CustomerHomePage(),
                                            ),
                                          );
                                        }
                                      },
                                style: ElevatedButton.styleFrom(
                                  backgroundColor: const Color(0xFFEBE9E7),
                                  foregroundColor: const Color(0xFF666666),
                                  disabledBackgroundColor: const Color(
                                    0xFFEBE9E7,
                                  ).withOpacity(0.5),
                                  elevation: 0,
                                  shape: RoundedRectangleBorder(
                                    borderRadius: BorderRadius.circular(18),
                                  ),
                                ),
                                child: Row(
                                  mainAxisAlignment: MainAxisAlignment.center,
                                  children: [
                                    const Text(
                                      'Tiếp tục',
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
