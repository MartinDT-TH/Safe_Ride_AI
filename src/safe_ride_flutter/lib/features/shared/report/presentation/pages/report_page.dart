import 'package:flutter/material.dart';

class ReportPage extends StatelessWidget {
  const ReportPage({super.key});

  @override
  Widget build(BuildContext context) {
    const tealColor = Color(0xFF006B70);
    return Scaffold(
      appBar: AppBar(
        title: const Text('Báo cáo sự cố'),
        backgroundColor: Colors.white,
        foregroundColor: tealColor,
        elevation: 0.5,
      ),
      body: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text(
              'Chúng tôi có thể giúp gì cho bạn?',
              style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
            ),
            const SizedBox(height: 16),
            _ReportTypeItem(
              title: 'Sự cố chuyến đi',
              icon: Icons.minor_crash_rounded,
              onTap: () {},
            ),
            _ReportTypeItem(
              title: 'Vấn đề thanh toán',
              icon: Icons.payments_rounded,
              onTap: () {},
            ),
            _ReportTypeItem(
              title: 'Phản hồi về tài xế/khách hàng',
              icon: Icons.person_search_rounded,
              onTap: () {},
            ),
            _ReportTypeItem(
              title: 'Lỗi ứng dụng',
              icon: Icons.bug_report_rounded,
              onTap: () {},
            ),
          ],
        ),
      ),
    );
  }
}

class _ReportTypeItem extends StatelessWidget {
  final String title;
  final IconData icon;
  final VoidCallback onTap;

  const _ReportTypeItem({
    required this.title,
    required this.icon,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return ListTile(
      leading: Icon(icon, color: const Color(0xFF006B70)),
      title: Text(title),
      trailing: const Icon(Icons.chevron_right),
      onTap: onTap,
    );
  }
}
