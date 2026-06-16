import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../../../core/constants/app_colors.dart';
import '../../../../core/constants/app_strings.dart';
import '../../../auth/presentation/providers/auth_provider.dart';
import '../providers/activity_provider.dart';
import '../widgets/trip_history_card.dart';
import '../widgets/interactive_button.dart';

class ActivityPage extends StatefulWidget {
  const ActivityPage({super.key});

  @override
  State<ActivityPage> createState() => _ActivityPageState();
}

class _ActivityPageState extends State<ActivityPage> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      final auth = context.read<AuthProvider>();
      context.read<ActivityProvider>().loadHistory(auth.token);
    });
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFFCF9F9),
      appBar: AppBar(
        backgroundColor: Colors.white,
        elevation: 0,
        title: const Text(
          ActivityStrings.tripHistory,
          style: TextStyle(
            color: Colors.black,
            fontWeight: FontWeight.bold,
            fontSize: 20,
          ),
        ),
        centerTitle: true,
      ),
      body: Column(
        children: [
          _buildFilterBar(),
          Expanded(
            child: Consumer<ActivityProvider>(
              builder: (context, provider, child) {
                if (provider.isLoading) {
                  return const Center(child: CircularProgressIndicator());
                }

                if (provider.trips.isEmpty) {
                  return const Center(
                    child: Text('Không có dữ liệu chuyến đi.'),
                  );
                }

                return ListView.builder(
                  padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 16),
                  itemCount: provider.trips.length,
                  itemBuilder: (context, index) {
                    final trip = provider.trips[index];
                    return TripHistoryCard(
                      trip: trip,
                      onRebook: () {
                        // Handle rebook logic
                      },
                    );
                  },
                );
              },
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildFilterBar() {
    return Container(
      color: Colors.white,
      padding: const EdgeInsets.fromLTRB(20, 8, 20, 16),
      child: Container(
        padding: const EdgeInsets.all(6),
        decoration: BoxDecoration(
          color: const Color(0xFFF2F4F4),
          borderRadius: BorderRadius.circular(16),
        ),
        child: Consumer<ActivityProvider>(
          builder: (context, provider, child) {
            return Row(
              children: [
                _buildFilterItem(
                  ActivityStrings.all,
                  ActivityFilter.all,
                  provider.currentFilter == ActivityFilter.all,
                  provider,
                ),
                _buildFilterItem(
                  ActivityStrings.completed,
                  ActivityFilter.completed,
                  provider.currentFilter == ActivityFilter.completed,
                  provider,
                ),
                _buildFilterItem(
                  ActivityStrings.cancelled,
                  ActivityFilter.cancelled,
                  provider.currentFilter == ActivityFilter.cancelled,
                  provider,
                ),
              ],
            );
          },
        ),
      ),
    );
  }

  Widget _buildFilterItem(
    String label,
    ActivityFilter filter,
    bool isSelected,
    ActivityProvider provider,
  ) {
    return Expanded(
      child: InteractiveButton(
        onTap: () => provider.setFilter(filter),
        borderRadius: BorderRadius.circular(12),
        child: AnimatedContainer(
          duration: const Duration(milliseconds: 200),
          padding: const EdgeInsets.symmetric(vertical: 10),
          decoration: BoxDecoration(
            color: isSelected ? AppColors.primary : Colors.transparent,
            borderRadius: BorderRadius.circular(12),
          ),
          child: Text(
            label,
            textAlign: TextAlign.center,
            style: TextStyle(
              color: isSelected ? Colors.white : const Color(0xFF626A6C),
              fontWeight: isSelected ? FontWeight.bold : FontWeight.w500,
            ),
          ),
        ),
      ),
    );
  }
}
