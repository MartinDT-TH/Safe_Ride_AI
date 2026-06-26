import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../../../core/constants/app_colors.dart';
import '../../../../../core/constants/app_strings.dart';
import '../../../../auth/presentation/providers/auth_provider.dart';
import '../../../../shared/onboarding/presentation/providers/role_provider.dart';
import '../../data/models/history_trip.dart';
import '../providers/history_provider.dart';
import '../widgets/interactive_button.dart';
import '../widgets/trip_history_card.dart';

class HistoryPage extends StatefulWidget {
  const HistoryPage({super.key});

  @override
  State<HistoryPage> createState() => _HistoryPageState();
}

class _HistoryPageState extends State<HistoryPage> {
  static const _loadErrorTitle =
      'Kh\u00f4ng th\u1ec3 t\u1ea3i l\u1ecbch s\u1eed chuy\u1ebfn \u0111i.';
  static const _emptyTitle =
      'Kh\u00f4ng c\u00f3 d\u1eef li\u1ec7u chuy\u1ebfn \u0111i.';

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      _loadHistory();
    });
  }

  Future<void> _loadHistory() {
    final auth = context.read<AuthProvider>();
    final roleProvider = context.read<RoleProvider>();
    final role = roleProvider.selectedRole ?? auth.lastSelectedRole;
    if (role != null && roleProvider.selectedRole != role) {
      roleProvider.setRole(role);
    }
    return context.read<HistoryProvider>().loadHistory(auth.token, role: role);
  }

  @override
  Widget build(BuildContext context) {
    final isDriver = context.watch<RoleProvider>().isDriver;

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
                  onRefresh: _loadHistory,
                  color: AppColors.primary,
                  child: provider.errorMessage != null && provider.trips.isEmpty
                      ? _buildFeedbackList(
                          child: Column(
                            children: [
                              const Text(
                                _loadErrorTitle,
                                style: TextStyle(
                                  fontSize: 16,
                                  fontWeight: FontWeight.w600,
                                ),
                              ),
                              const SizedBox(height: 8),
                              Text(
                                provider.errorMessage!,
                                textAlign: TextAlign.center,
                                style: const TextStyle(
                                  color: Color(0xFF626A6C),
                                ),
                              ),
                              const SizedBox(height: 16),
                              ElevatedButton(
                                onPressed: _loadHistory,
                                style: ElevatedButton.styleFrom(
                                  backgroundColor: AppColors.primary,
                                  foregroundColor: Colors.white,
                                ),
                                child: const Text(AppStrings.confirm),
                              ),
                            ],
                          ),
                        )
                      : provider.trips.isEmpty
                          ? _buildFeedbackList(
                              child: const Text(_emptyTitle),
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
                                  onRebook: isDriver ||
                                          trip.status ==
                                              HistoryTripStatus.booked
                                      ? null
                                      : () {
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

  Widget _buildFeedbackList({required Widget child}) {
    return ListView(
      physics: const AlwaysScrollableScrollPhysics(),
      children: [
        const SizedBox(height: 120),
        Center(child: child),
      ],
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
                _buildFilterItem(
                  HistoryStrings.booked,
                  HistoryFilter.booked,
                  provider.currentFilter == HistoryFilter.booked,
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
