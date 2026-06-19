import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../../../../core/constants/app_colors.dart';
import '../../../../../core/constants/app_strings.dart';
import '../../../../auth/presentation/providers/auth_provider.dart';
import '../providers/history_provider.dart';
import '../widgets/trip_history_card.dart';
import '../widgets/interactive_button.dart';

class HistoryPage extends StatefulWidget {
  const HistoryPage({super.key});

  @override
  State<HistoryPage> createState() => _HistoryPageState();
}

class _HistoryPageState extends State<HistoryPage> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      final auth = context.read<AuthProvider>();
      context.read<HistoryProvider>().loadHistory(auth.token);
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
          HistoryStrings.tripHistory,
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
            child: Consumer<HistoryProvider>(
              builder: (context, provider, child) {
                if (provider.isLoading && provider.trips.isEmpty) {
                  return const Center(child: CircularProgressIndicator());
                }

                return RefreshIndicator(
                  onRefresh: () async {
                    final auth = context.read<AuthProvider>();
                    await provider.loadHistory(auth.token);
                  },
                  color: AppColors.primary,
                  child: provider.trips.isEmpty
                      ? ListView(
                          children: const [
                            SizedBox(height: 100),
                            Center(child: Text('Không có dữ liệu chuyến đi.')),
                          ],
                        )
                      : ListView.builder(
                          physics: const AlwaysScrollableScrollPhysics(),
                          padding: const EdgeInsets.symmetric(
                            horizontal: 20,
                            vertical: 16,
                          ),
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
                        ),
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
        child: Consumer<HistoryProvider>(
          builder: (context, provider, child) {
            return Row(
              children: [
                _buildFilterItem(
                  HistoryStrings.all,
                  HistoryFilter.all,
                  provider.currentFilter == HistoryFilter.all,
                  provider,
                ),
                _buildFilterItem(
                  HistoryStrings.completed,
                  HistoryFilter.completed,
                  provider.currentFilter == HistoryFilter.completed,
                  provider,
                ),
                _buildFilterItem(
                  HistoryStrings.cancelled,
                  HistoryFilter.cancelled,
                  provider.currentFilter == HistoryFilter.cancelled,
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
    HistoryFilter filter,
    bool isSelected,
    HistoryProvider provider,
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
