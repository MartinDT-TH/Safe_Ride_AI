import 'package:flutter/material.dart';

class PromoBanner extends StatelessWidget {
  final String title;

  final String code;

  const PromoBanner({super.key, required this.title, required this.code});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(20),

      decoration: BoxDecoration(
        color: Colors.green.shade100,

        borderRadius: BorderRadius.circular(20),
      ),

      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,

        children: [
          Text(
            title,
            style: const TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
          ),

          const SizedBox(height: 10),

          Text(code),
        ],
      ),
    );
  }
}
