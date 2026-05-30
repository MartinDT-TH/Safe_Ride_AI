import 'package:flutter/material.dart';

class RoleCard extends StatelessWidget {
  final IconData icon;

  final String title;

  final String description;

  final bool isSelected;

  final VoidCallback onTap;

  const RoleCard({
    super.key,
    required this.icon,
    required this.title,
    required this.description,
    required this.isSelected,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,

      borderRadius: BorderRadius.circular(20),

      child: Container(
        padding: const EdgeInsets.all(20),

        decoration: BoxDecoration(
          borderRadius: BorderRadius.circular(20),

          border: Border.all(
            width: 2,

            color: isSelected ? Colors.teal : Colors.grey.shade300,
          ),
        ),

        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,

          children: [
            Icon(icon, size: 40, color: Colors.teal),

            const SizedBox(height: 16),

            Text(
              title,

              style: const TextStyle(fontSize: 20, fontWeight: FontWeight.bold),
            ),

            const SizedBox(height: 8),

            Text(description),
          ],
        ),
      ),
    );
  }
}
