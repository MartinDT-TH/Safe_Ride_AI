import 'package:flutter/material.dart';

class QuickActionItem extends StatelessWidget {
  final IconData icon;

  final String title;

  final Color backgroundColor;

  final Color iconColor;

  final VoidCallback onTap;

  const QuickActionItem({
    super.key,
    required this.icon,
    required this.title,
    required this.backgroundColor,
    required this.iconColor,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: onTap,

      child: Column(
        children: [
          CircleAvatar(
            radius: 30,
            backgroundColor: backgroundColor,

            child: Icon(icon, color: iconColor),
          ),

          const SizedBox(height: 8),

          Text(title),
        ],
      ),
    );
  }
}
