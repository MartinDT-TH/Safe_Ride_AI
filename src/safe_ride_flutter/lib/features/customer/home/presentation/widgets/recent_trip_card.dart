import 'package:flutter/material.dart';

class RecentTripCard extends StatelessWidget {
  final String pickup;
  final String destination;
  final String time;
  final VoidCallback? onRebook;

  const RecentTripCard({
    super.key,
    required this.pickup,
    required this.destination,
    required this.time,
    this.onRebook,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: Colors.grey.shade100),
      ),
      child: Column(
        children: [
          Row(
            children: [
              Column(
                children: [
                  const Icon(Icons.circle, color: Color(0xFF006B70), size: 10),
                  Container(
                    width: 1,
                    height: 24,
                    color: Colors.grey.shade300,
                  ),
                  Container(
                    width: 10,
                    height: 10,
                    decoration: BoxDecoration(
                      border: Border.all(color: Colors.red, width: 2),
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
                      pickup,
                      style: const TextStyle(fontSize: 15, color: Color(0xFF333333), fontWeight: FontWeight.w500),
                    ),
                    const SizedBox(height: 14),
                    Text(
                      destination,
                      style: const TextStyle(fontSize: 15, color: Color(0xFF333333), fontWeight: FontWeight.w500),
                    ),
                  ],
                ),
              ),
            ],
          ),
          const SizedBox(height: 16),
          Divider(color: Colors.grey.shade100, height: 1),
          const SizedBox(height: 12),
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text(
                time,
                style: TextStyle(fontSize: 13, color: Colors.grey.shade400),
              ),
              SizedBox(
                height: 32,
                child: ElevatedButton(
                  onPressed: onRebook ?? () {},
                  style: ElevatedButton.styleFrom(
                    backgroundColor: const Color(0xFFE8F2F2),
                    foregroundColor: const Color(0xFF006B70),
                    elevation: 0,
                    padding: const EdgeInsets.symmetric(horizontal: 16),
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(20),
                    ),
                  ),
                  child: const Text(
                    'Đặt lại',
                    style: TextStyle(fontSize: 13, fontWeight: FontWeight.bold),
                  ),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

