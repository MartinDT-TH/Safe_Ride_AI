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
      body: SafeArea(
        child: Consumer<RoleProvider>(
          builder: (_, provider, __) {
            return Padding(
              padding: const EdgeInsets.all(24),

              child: Column(
                children: [
                  const SizedBox(height: 50),

                  const Text(
                    'Chào mừng bạn',
                    style: TextStyle(fontSize: 30, fontWeight: FontWeight.bold),
                  ),

                  const SizedBox(height: 12),

                  const Text('Chọn vai trò để tiếp tục'),

                  const SizedBox(height: 40),

                  RoleCard(
                    icon: Icons.local_taxi,

                    title: 'Khách hàng',

                    description: 'Đặt xe nhanh chóng',

                    isSelected: provider.selectedRole == 'customer',

                    onTap: () {
                      provider.selectRole('customer');
                    },
                  ),

                  const SizedBox(height: 20),

                  RoleCard(
                    icon: Icons.drive_eta,

                    title: 'Tài xế',

                    description: 'Nhận chuyến đi',

                    isSelected: provider.selectedRole == 'driver',

                    onTap: () {
                      provider.selectRole('driver');
                    },
                  ),

                  const Spacer(),

                  Row(
                    children: [
                      Switch(
                        value: provider.rememberRole,

                        onChanged: provider.setRememberRole,
                      ),

                      const Text('Ghi nhớ lựa chọn'),
                    ],
                  ),

                  const SizedBox(height: 20),

                  SizedBox(
                    width: double.infinity,

                    height: 55,

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

                      child: const Text(
                        'Tiếp tục',
                      ),
                    ),
                  ),
                ],
              ),
            );
          },
        ),
      ),
    );
  }
}
