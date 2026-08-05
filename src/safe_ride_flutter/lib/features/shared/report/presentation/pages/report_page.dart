import 'package:flutter/material.dart';
import '../../../../../core/localization/localization_extensions.dart';

class ReportPage extends StatelessWidget {
  ReportPage({super.key});

  @override
  Widget build(BuildContext context) {
    const tealColor = Color(0xFF006B70);
    return Scaffold(
      appBar: AppBar(
        title: Text(context.l10n.reportIncident),
        backgroundColor: Colors.white,
        foregroundColor: tealColor,
        elevation: 0.5,
      ),
      body: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              context.l10n.reportHelpQuestion,
              style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
            ),
            SizedBox(height: 16),
            _ReportTypeItem(
              title: context.l10n.tripIncident,
              icon: Icons.minor_crash_rounded,
              onTap: () {},
            ),
            _ReportTypeItem(
              title: context.l10n.paymentIssue,
              icon: Icons.payments_rounded,
              onTap: () {},
            ),
            _ReportTypeItem(
              title: context.l10n.partyFeedback,
              icon: Icons.person_search_rounded,
              onTap: () {},
            ),
            _ReportTypeItem(
              title: context.l10n.appIssue,
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

  _ReportTypeItem({
    required this.title,
    required this.icon,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return ListTile(
      leading: Icon(icon, color: Color(0xFF006B70)),
      title: Text(title),
      trailing: Icon(Icons.chevron_right),
      onTap: onTap,
    );
  }
}
