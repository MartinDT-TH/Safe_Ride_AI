import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../../../core/constants/app_colors.dart';
import '../../../../../core/constants/app_strings.dart';
import '../../../../../core/widgets/app_loading_screen.dart';
import '../../../../../core/localization/localization_extensions.dart';
import '../../../../auth/presentation/providers/auth_provider.dart';
import '../../../../customer/booking/presentation/pages/rebook_trip_page.dart';
import '../../../../customer/booking/presentation/providers/booking_provider.dart';
import '../../../../shared/onboarding/presentation/providers/role_provider.dart';
import '../../data/models/history_trip.dart';
import '../providers/history_provider.dart';
import 'trip_details_page.dart';
import '../widgets/interactive_button.dart';
import '../widgets/trip_history_card.dart';
import 'package:safe_ride/features/shared/feedback/presentation/pages/report_trip_page.dart';
import 'package:safe_ride/features/shared/chat/presentation/pages/trip_chat_page.dart';
import 'package:safe_ride/features/shared/chat/presentation/providers/chat_unread_provider.dart';

class HistoryPage extends StatefulWidget {
  HistoryPage({super.key});

  @override
  State<HistoryPage> createState() => _HistoryPageState();
}

class _HistoryPageState extends State<HistoryPage> {
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
      _showMessage(context.l10n.sessionExpired);
      return;
    }

    AppLoadingScreen.show(context, message: context.l10n.loadingTrip);
    final details = await bookingProvider.getPastBookingDetails(
      token,
      bookingId: trip.id,
    );
    AppLoadingScreen.hide();

    if (!mounted) return;

    if (details == null) {
      _showMessage(bookingProvider.errorMessage ?? context.l10n.genericError);
      return;
    }

    if (details.pickup == null ||
        details.destination == null ||
        details.vehicle == null) {
      _showMessage(context.l10n.tripNotRebookable);
      return;
    }

    await Navigator.of(context).push(
      MaterialPageRoute(builder: (_) => RebookTripPage(oldBooking: details)),
    );
  }

  Future<void> _handleReport(HistoryTrip trip) async {
    final result = await Navigator.of(
      context,
    ).push(MaterialPageRoute(builder: (_) => ReportTripPage(trip: trip)));

    if (result == true) {
      _loadHistory();
    }
  }

  void _handleChat(HistoryTrip trip) {
    final auth = context.read<AuthProvider>();
    final currentUserId = auth.userId;

    if (trip.tripId == null || currentUserId == null) {
      _showMessage(context.l10n.chatOpenFailed);
      return;
    }

    Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => TripChatPage(
          tripId: trip.tripId!,
          currentUserId: currentUserId,
          receiverName: trip.driverName ?? context.l10n.safeRideDriver,
          canSendMessage: _canSendChat(trip.status.name),
        ),
      ),
    );
  }

  void _handleViewFeedback(HistoryTrip trip) {
    _openTripDetails(trip, canRebook: false);
  }

  bool _canSendChat(String? status) {
    if (status == null) return true;
    final normalized = status.trim().toUpperCase();
    return normalized != 'CANCELLED';
  }

  Future<void> _openTripDetails(HistoryTrip trip, {required bool canRebook}) {
    return Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => TripDetailsPage(trip: trip, canRebook: canRebook),
      ),
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
      backgroundColor: Color(0xFFFCF9F9),
      appBar: AppBar(
        backgroundColor: Colors.white,
        elevation: 0,
        title: Text(
          context.l10n.tripHistory,
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
                  return Center(child: CircularProgressIndicator());
                }

                return RefreshIndicator(
                  onRefresh: _loadHistory,
                  color: AppColors.primary,
                  child: provider.errorMessage != null && provider.trips.isEmpty
                      ? _buildFeedbackList(
                          child: Column(
                            children: [
                              Text(
                                context.l10n.historyLoadFailed,
                                style: TextStyle(
                                  fontSize: 16,
                                  fontWeight: FontWeight.w600,
                                ),
                              ),
                              SizedBox(height: 8),
                              Text(
                                provider.errorMessage!,
                                textAlign: TextAlign.center,
                                style: TextStyle(color: Color(0xFF626A6C)),
                              ),
                              SizedBox(height: 16),
                              ElevatedButton(
                                onPressed: _loadHistory,
                                style: ElevatedButton.styleFrom(
                                  backgroundColor: AppColors.primary,
                                  foregroundColor: Colors.white,
                                ),
                                child: Text(context.l10n.confirm),
                              ),
                            ],
                          ),
                        )
                      : provider.trips.isEmpty
                      ? _buildFeedbackList(
                          child: Text(context.l10n.noTripHistory),
                        )
                      : ListView.builder(
                          physics: AlwaysScrollableScrollPhysics(),
                          padding: const EdgeInsets.symmetric(
                            horizontal: 20,
                            vertical: 16,
                          ),
                          itemCount: provider.trips.length,
                          itemBuilder: (context, index) {
                            final trip = provider.trips[index];
                            final canRebook =
                                !isDriver &&
                                trip.status != HistoryTripStatus.booked;

                            return InteractiveButton(
                              onTap: () =>
                                  _openTripDetails(trip, canRebook: canRebook),
                              borderRadius: BorderRadius.circular(24),
                              child: TripHistoryCard(
                                unreadChatCount: context
                                    .watch<ChatUnreadProvider>()
                                    .unreadCountForTrip(trip.tripId),
                                onChat: trip.tripId != null
                                    ? () => _handleChat(trip)
                                    : null,
                                onViewFeedback:
                                    isDriver &&
                                        trip.status ==
                                            HistoryTripStatus.completed &&
                                        trip.tripId != null
                                    ? () => _handleViewFeedback(trip)
                                    : null,
                                trip: trip,
                                onReport:
                                    (isDriver ||
                                        trip.status == HistoryTripStatus.booked)
                                    ? null
                                    : () => _handleReport(trip),
                                onRebook: canRebook
                                    ? () => _handleRebook(trip)
                                    : null,
                              ),
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
      physics: AlwaysScrollableScrollPhysics(),
      children: [
        SizedBox(height: 120),
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
          color: Color(0xFFF2F4F4),
          borderRadius: BorderRadius.circular(16),
        ),
        child: Consumer<HistoryProvider>(
          builder: (context, provider, child) {
            return Row(
              children: [
                _buildFilterItem(
                  context.l10n.historyFilterAll,
                  HistoryFilter.all,
                  provider.currentFilter == HistoryFilter.all,
                  provider,
                ),
                _buildFilterItem(
                  context.l10n.completed,
                  HistoryFilter.completed,
                  provider.currentFilter == HistoryFilter.completed,
                  provider,
                ),
                _buildFilterItem(
                  context.l10n.historyFilterCancelled,
                  HistoryFilter.cancelled,
                  provider.currentFilter == HistoryFilter.cancelled,
                  provider,
                ),
                _buildFilterItem(
                  context.l10n.historyFilterBooked,
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
          duration: Duration(milliseconds: 200),
          padding: const EdgeInsets.symmetric(vertical: 10),
          decoration: BoxDecoration(
            color: isSelected ? AppColors.primary : Colors.transparent,
            borderRadius: BorderRadius.circular(12),
          ),
          child: Text(
            label,
            textAlign: TextAlign.center,
            style: TextStyle(
              color: isSelected ? Colors.white : Color(0xFF626A6C),
              fontWeight: isSelected ? FontWeight.bold : FontWeight.w500,
            ),
          ),
        ),
      ),
    );
  }
}
