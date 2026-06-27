import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../../../core/constants/app_colors.dart';
import '../../../../../core/constants/app_strings.dart';
import '../../../../../core/widgets/app_loading_screen.dart';
import '../../../../auth/presentation/providers/auth_provider.dart';
import '../../../../customer/booking/presentation/pages/rebook_trip_page.dart';
import '../../../../customer/booking/presentation/providers/booking_provider.dart';
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
  static const _loadErrorTitle = 'Không thể tải lịch sử chuyến đi.';
  static const _emptyTitle = 'Không có dữ liệu chuyến đi.';
  static const _invalidRebookDataMessage =
      'Chuyến đi này chưa có đủ dữ liệu để đặt lại.';

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

  Future<void> _handleRebook(HistoryTrip trip) async {
    final authProvider = context.read<AuthProvider>();
    final bookingProvider = context.read<BookingProvider>();
    final token = authProvider.token;

    if (token == null || token.isEmpty) {
      _showMessage(BookingStrings.sessionExpired);
      return;
    }

    AppLoadingScreen.show(context, message: 'Đang tải thông tin chuyến đi...');
    final details = await bookingProvider.getPastBookingDetails(
      token,
      bookingId: trip.id,
    );
    AppLoadingScreen.hide();

    if (!mounted) return;

    if (details == null) {
      _showMessage(bookingProvider.errorMessage ?? AppStrings.genericError);
      return;
    }

    if (details.pickup == null ||
        details.destination == null ||
        details.vehicle == null) {
      _showMessage(_invalidRebookDataMessage);
      return;
    }

    await Navigator.of(context).push(
      MaterialPageRoute(builder: (_) => RebookTripPage(oldBooking: details)),
    );
  }

  void _showMessage(String message) {
    if (!mounted) return;

    ScaffoldMessenger.of(context)
      ..hideCurrentSnackBar()
      ..showSnackBar(SnackBar(content: Text(message)));
  }

  @override
  Widget build(BuildContext context) {
    final roleProvider = context.watch<RoleProvider>();
    final authProvider = context.watch<AuthProvider>();
    final currentRole =
        roleProvider.selectedRole ?? authProvider.lastSelectedRole;
    final isDriver = currentRole == AppValues.roleDriver;

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
                      ? _buildFeedbackList(child: const Text(_emptyTitle))
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
                              onRebook:
                                  (isDriver ||
                                      trip.status == HistoryTripStatus.booked)
                                  ? null
                                  : () => _handleRebook(trip),
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
