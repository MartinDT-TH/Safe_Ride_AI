import 'package:flutter/material.dart';

class RecentTripCard extends StatelessWidget {
  final String pickup;

  final String destination;

  final String time;

  final VoidCallback onRebook;

  const RecentTripCard({
    super.key,
    required this.pickup,
    required this.destination,
    required this.time,
    required this.onRebook,
  });

  @override
  Widget build(BuildContext context) {
    return Card(
      child: ListTile(
        title: Text(destination),

        subtitle: Text('$pickup • $time'),

        trailing: IconButton(
          onPressed: onRebook,

          icon: const Icon(Icons.refresh),
        ),
      ),
    );
  }
}
