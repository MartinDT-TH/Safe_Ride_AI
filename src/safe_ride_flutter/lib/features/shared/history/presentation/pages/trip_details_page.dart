import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:provider/provider.dart';

import '../../../../../core/constants/app_colors.dart';
import '../../../../../core/constants/app_strings.dart';
import '../../../../../core/maps/models/map_models.dart';
import '../../../../../core/maps/polyline_decoder.dart';
import '../../../../../core/maps/widgets/map_renderer_widget.dart';
import '../../../../../core/widgets/app_loading_screen.dart';
import '../../../../../core/localization/localization_extensions.dart';
import '../../../../../dependency_injection/injection.dart';
import '../../../../auth/presentation/providers/auth_provider.dart';
import '../../../../customer/booking/domain/repositories/booking_repository.dart';
import '../../../../customer/booking/data/models/booking_response.dart';
import '../../../../customer/booking/presentation/pages/rebook_trip_page.dart';
import '../../../../customer/booking/presentation/providers/booking_provider.dart';
import '../../../../shared/onboarding/presentation/providers/role_provider.dart';
import '../../../../shared/feedback/domain/repositories/feedback_repository.dart';
import '../../../../shared/chat/presentation/pages/trip_chat_page.dart';
import '../../../../shared/chat/presentation/providers/chat_unread_provider.dart';
import '../../../../customer/booking/presentation/widgets/booking_cancel_flow.dart';
import '../../data/models/history_trip.dart';
import '../../data/models/trip_details_view_data.dart';
import '../../data/repositories/trip_details_repository_impl.dart';
import '../providers/trip_details_provider.dart';

bool shouldShowHistoryScheduledBookingCancel({
  required bool allowedForRole,
  required BookingResponse? booking,
}) {
  return allowedForRole &&
      booking?.bookingType == AppValues.bookingScheduled &&
      isBookingCancellable(booking);
}

class TripDetailsPage extends StatelessWidget {
  const TripDetailsPage({
    super.key,
    required this.trip,
    required this.canRebook,
    this.canCancelScheduled = false,
  });

  final HistoryTrip trip;
  final bool canRebook;
  final bool canCancelScheduled;

  @override
  Widget build(BuildContext context) {
    final accessToken = context.read<AuthProvider>().token;
    final roleProvider = context.read<RoleProvider>();
    final auth = context.read<AuthProvider>();
    final isDriver = roleProvider.isDriver;

    return ChangeNotifierProvider<TripDetailsProvider>(
      create: (_) =>
          TripDetailsProvider.create(
            TripDetailsRepositoryImpl(getIt<BookingRepository>()),
            trip,
            feedbackRepository: getIt<FeedbackRepository>(),
          )..loadDetails(
            accessToken,
            driverIdForFeedback: isDriver ? auth.userId : null,
          ),
      child: _TripDetailsView(
        trip: trip,
        canRebook: canRebook,
        canCancelScheduled: canCancelScheduled,
        accessToken: accessToken,
        driverId: isDriver ? auth.userId : null,
      ),
    );
  }
}

class _TripDetailsView extends StatelessWidget {
  _TripDetailsView({
    required this.trip,
    required this.canRebook,
    required this.canCancelScheduled,
    required this.accessToken,
    this.driverId,
  });

  final HistoryTrip trip;
  final bool canRebook;
  final bool canCancelScheduled;
  final String? accessToken;
  final String? driverId;

  Future<void> _reload(BuildContext context) {
    return context.read<TripDetailsProvider>().loadDetails(
      accessToken,
      driverIdForFeedback: driverId,
    );
  }

  Future<void> _handleRebook(BuildContext context) async {
    final bookingProvider = context.read<BookingProvider>();
    final token = context.read<AuthProvider>().token;

    if (token == null || token.isEmpty) {
      _showMessage(context, context.l10n.sessionExpired);
      return;
    }

    AppLoadingScreen.show(context, message: context.l10n.loadingTrip);
    final details = await bookingProvider.getPastBookingDetails(
      token,
      bookingId: trip.id,
    );
    AppLoadingScreen.hide();

    if (!context.mounted) {
      return;
    }

    if (details == null) {
      _showMessage(
        context,
        bookingProvider.errorMessage ?? context.l10n.tripDetailsLoadFailed,
      );
      return;
    }

    if (details.pickup == null ||
        details.destination == null ||
        details.vehicle == null) {
      _showMessage(context, context.l10n.tripNotRebookable);
      return;
    }

    await Navigator.of(context).push(
      MaterialPageRoute(builder: (_) => RebookTripPage(oldBooking: details)),
    );
  }

  void _showMessage(BuildContext context, String message) {
    ScaffoldMessenger.of(context)
      ..hideCurrentSnackBar()
      ..showSnackBar(
        SnackBar(content: Text(message), behavior: SnackBarBehavior.floating),
      );
  }

  void _openChat(BuildContext context) {
    final currentUserId = context.read<AuthProvider>().userId;
    final tripId = trip.tripId;
    if (currentUserId == null || tripId == null) {
      _showMessage(context, context.l10n.chatOpenFailed);
      return;
    }

    Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => TripChatPage(
          tripId: tripId,
          currentUserId: currentUserId,
          receiverName: trip.driverName ?? context.l10n.safeRideDriver,
          canSendMessage: trip.status != HistoryTripStatus.cancelled,
        ),
      ),
    );
  }

  Future<void> _handleCancelScheduled(
    BuildContext context,
    BookingResponse booking,
  ) async {
    final cancelled = await requestBookingCancellation(
      context,
      booking: booking,
    );
    if (cancelled && context.mounted) {
      Navigator.of(context).pop(true);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Consumer<TripDetailsProvider>(
      builder: (context, provider, child) {
        final data = provider.tripDetails;
        final unreadChatCount = context
            .watch<ChatUnreadProvider>()
            .unreadCountForTrip(trip.tripId);
        final booking = data.booking;
        final showCancelScheduled = shouldShowHistoryScheduledBookingCancel(
          allowedForRole: canCancelScheduled,
          booking: booking,
        );

        return Scaffold(
          backgroundColor: Color(0xFFFCF9F8),
          appBar: AppBar(
            backgroundColor: Colors.white,
            elevation: 0,
            centerTitle: true,
            title: Text(
              context.l10n.tripDetails,
              style: TextStyle(
                color: Colors.black,
                fontWeight: FontWeight.bold,
                fontSize: 20,
              ),
            ),
          ),
          bottomNavigationBar: canRebook
              ? SafeArea(
                  top: false,
                  child: Container(
                    padding: const EdgeInsets.fromLTRB(20, 12, 20, 16),
                    decoration: BoxDecoration(
                      color: Colors.white.withValues(alpha: 0.96),
                      border: Border(top: BorderSide(color: Color(0xFFE7E3E2))),
                    ),
                    child: SizedBox(
                      height: 54,
                      child: ElevatedButton.icon(
                        onPressed: () => _handleRebook(context),
                        style: ElevatedButton.styleFrom(
                          backgroundColor: AppColors.primary,
                          foregroundColor: Colors.white,
                          shape: RoundedRectangleBorder(
                            borderRadius: BorderRadius.circular(18),
                          ),
                          elevation: 0,
                        ),
                        icon: Icon(Icons.history_rounded),
                        label: Text(
                          context.l10n.rebookThisTrip,
                          style: TextStyle(
                            fontSize: 16,
                            fontWeight: FontWeight.w700,
                          ),
                        ),
                      ),
                    ),
                  ),
                )
              : null,
          body: RefreshIndicator(
            onRefresh: () => _reload(context),
            color: AppColors.primary,
            child: ListView(
              physics: AlwaysScrollableScrollPhysics(),
              padding: const EdgeInsets.fromLTRB(20, 20, 20, 24),
              children: [
                if (provider.isLoading && !provider.hasLoadedRemoteDetails)
                  Padding(
                    padding: EdgeInsets.only(bottom: 16),
                    child: LinearProgressIndicator(minHeight: 3),
                  ),
                if (provider.errorMessage != null)
                  Padding(
                    padding: const EdgeInsets.only(bottom: 16),
                    child: _InlineFeedbackCard(
                      message: provider.errorMessage!,
                      actionLabel: context.l10n.retry,
                      onPressed: () => _reload(context),
                    ),
                  ),
                _TripMetaHeader(data: data),
                SizedBox(height: 16),
                _TripRouteMapCard(data: data),
                SizedBox(height: 16),
                _TripRouteTimeline(data: data),
                SizedBox(height: 16),
                _TripQuickStats(data: data),
                SizedBox(height: 16),
                _TripDriverCard(data: data),
                SizedBox(height: 16),
                if (trip.tripId != null) ...[
                  _TripChatActionCard(
                    unreadCount: unreadChatCount,
                    onTap: () => _openChat(context),
                  ),
                  SizedBox(height: 16),
                ],
                _TripPaymentCard(data: data),
                SizedBox(height: 16),
                _TripFeedbackCard(data: data),
                if (showCancelScheduled) ...[
                  const SizedBox(height: 16),
                  HistoryScheduledBookingCancelButton(
                    onCancel: () => _handleCancelScheduled(context, booking!),
                  ),
                ],
              ],
            ),
          ),
        );
      },
    );
  }
}

class HistoryScheduledBookingCancelButton extends StatefulWidget {
  const HistoryScheduledBookingCancelButton({
    super.key,
    required this.onCancel,
  });

  static const buttonKey = Key('history-scheduled-booking-cancel-button');

  final Future<void> Function() onCancel;

  @override
  State<HistoryScheduledBookingCancelButton> createState() =>
      _HistoryScheduledBookingCancelButtonState();
}

class _HistoryScheduledBookingCancelButtonState
    extends State<HistoryScheduledBookingCancelButton> {
  bool _isCancelling = false;

  Future<void> _cancel() async {
    if (_isCancelling) return;
    setState(() => _isCancelling = true);
    try {
      await widget.onCancel();
    } finally {
      if (mounted) {
        setState(() => _isCancelling = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return OutlinedButton.icon(
      key: HistoryScheduledBookingCancelButton.buttonKey,
      onPressed: _isCancelling ? null : _cancel,
      icon: _isCancelling
          ? const SizedBox(
              width: 18,
              height: 18,
              child: CircularProgressIndicator(strokeWidth: 2),
            )
          : const Icon(Icons.close_rounded),
      label: Text(_isCancelling ? 'Đang hủy...' : 'Hủy chuyến đặt trước'),
      style: OutlinedButton.styleFrom(
        foregroundColor: const Color(0xFFC62828),
        side: const BorderSide(color: Color(0xFFC62828)),
        padding: const EdgeInsets.symmetric(vertical: 15),
        textStyle: const TextStyle(fontSize: 16, fontWeight: FontWeight.bold),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
      ),
    );
  }
}

class _TripChatActionCard extends StatelessWidget {
  const _TripChatActionCard({required this.unreadCount, required this.onTap});

  final int unreadCount;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.white,
      borderRadius: BorderRadius.circular(16),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(16),
        child: Container(
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(16),
            border: Border.all(color: Color(0xFFE7E3E2)),
          ),
          child: Row(
            children: [
              Stack(
                clipBehavior: Clip.none,
                children: [
                  Icon(
                    Icons.chat_bubble_outline_rounded,
                    color: AppColors.primary,
                  ),
                  if (unreadCount > 0)
                    Positioned(
                      top: -3,
                      right: -4,
                      child: Container(
                        width: 8,
                        height: 8,
                        decoration: BoxDecoration(
                          color: Color(0xFFE11D48),
                          shape: BoxShape.circle,
                          border: Border.all(color: Colors.white, width: 1),
                        ),
                      ),
                    ),
                ],
              ),
              SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      context.l10n.chat,
                      style: TextStyle(
                        fontSize: 15,
                        fontWeight: FontWeight.w700,
                        color: Color(0xFF101828),
                      ),
                    ),
                    if (unreadCount > 0)
                      Text(
                        'Tin nhắn mới',
                        style: TextStyle(
                          fontSize: 12,
                          fontWeight: FontWeight.w700,
                          color: Color(0xFFBE123C),
                        ),
                      ),
                  ],
                ),
              ),
              Icon(Icons.chevron_right_rounded, color: Color(0xFF98A2B3)),
            ],
          ),
        ),
      ),
    );
  }
}

class _TripMetaHeader extends StatelessWidget {
  _TripMetaHeader({required this.data});

  final TripDetailsViewData data;

  @override
  Widget build(BuildContext context) {
    final statusStyle = _StatusStyle.fromStatus(data.normalizedStatus);
    final locale = Localizations.localeOf(context).toLanguageTag();
    final dateText = DateFormat.yMd(locale).add_Hm().format(data.bookingTime);

    return Container(
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(24),
        boxShadow: [
          BoxShadow(
            color: Colors.black.withValues(alpha: 0.03),
            blurRadius: 14,
            offset: Offset(0, 6),
          ),
        ],
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  context.l10n.tripCode,
                  style: TextStyle(
                    fontSize: 12,
                    letterSpacing: 0.4,
                    fontWeight: FontWeight.w700,
                    color: Color(0xFF667085),
                  ),
                ),
                SizedBox(height: 6),
                Text(
                  '#${data.tripId ?? data.bookingId}',
                  style: TextStyle(
                    fontSize: 24,
                    fontWeight: FontWeight.w900,
                    color: Color(0xFF101828),
                  ),
                ),
                if (data.tripId != null) ...[
                  SizedBox(height: 4),
                  Text(
                    context.l10n.bookingOrder(data.bookingId),
                    style: TextStyle(fontSize: 13, color: Color(0xFF667085)),
                  ),
                ],
              ],
            ),
          ),
          Column(
            crossAxisAlignment: CrossAxisAlignment.end,
            children: [
              Container(
                padding: const EdgeInsets.symmetric(
                  horizontal: 12,
                  vertical: 7,
                ),
                decoration: BoxDecoration(
                  color: statusStyle.backgroundColor,
                  borderRadius: BorderRadius.circular(999),
                ),
                child: Text(
                  data.statusLabel,
                  style: TextStyle(
                    color: statusStyle.textColor,
                    fontSize: 12,
                    fontWeight: FontWeight.w800,
                  ),
                ),
              ),
              SizedBox(height: 10),
              Text(
                dateText,
                textAlign: TextAlign.right,
                style: TextStyle(fontSize: 13, color: Color(0xFF667085)),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _TripRouteMapCard extends StatefulWidget {
  _TripRouteMapCard({required this.data});

  final TripDetailsViewData data;

  @override
  State<_TripRouteMapCard> createState() => _TripRouteMapCardState();
}

class _TripRouteMapCardState extends State<_TripRouteMapCard> {
  AppMapController? _mapController;
  bool _hasFittedBounds = false;

  List<AppLatLng> get _routePoints {
    final routePolyline = widget.data.routePolyline;
    if (routePolyline == null || routePolyline.isEmpty) {
      return [];
    }

    try {
      return decodePolyline(routePolyline);
    } catch (_) {
      return [];
    }
  }

  List<AppLatLng> get _cameraPoints {
    final points = <AppLatLng>[..._routePoints];
    final pickup = widget.data.pickupLocation;
    final destination = widget.data.destinationLocation;

    if (pickup != null && (pickup.latitude != 0 || pickup.longitude != 0)) {
      points.add(AppLatLng(pickup.latitude, pickup.longitude));
    }

    if (destination != null &&
        (destination.latitude != 0 || destination.longitude != 0)) {
      points.add(AppLatLng(destination.latitude, destination.longitude));
    }

    return points;
  }

  Set<AppMarker> get _markers {
    final markers = <AppMarker>{};
    final pickup = widget.data.pickupLocation;
    final destination = widget.data.destinationLocation;

    if (pickup != null && (pickup.latitude != 0 || pickup.longitude != 0)) {
      markers.add(
        AppMarker(
          id: 'trip_detail_pickup',
          position: AppLatLng(pickup.latitude, pickup.longitude),
          markerType: AppMarkerType.pickup,
        ),
      );
    }

    if (destination != null &&
        (destination.latitude != 0 || destination.longitude != 0)) {
      markers.add(
        AppMarker(
          id: 'trip_detail_destination',
          position: AppLatLng(destination.latitude, destination.longitude),
          markerType: AppMarkerType.destination,
        ),
      );
    }

    return markers;
  }

  Set<AppPolyline> get _polylines {
    if (_routePoints.length < 2) {
      return {};
    }

    return {
      AppPolyline(
        id: 'trip_detail_route',
        points: _routePoints,
        color: AppColors.primary,
        width: 5,
        zIndex: 2,
        endCapRound: true,
      ),
    };
  }

  AppCameraPosition get _initialCameraPosition {
    final points = _cameraPoints;
    if (points.isNotEmpty) {
      return AppCameraPosition(target: points.first, zoom: 14);
    }

    return AppCameraPosition(target: AppLatLng(10.7769, 106.7009));
  }

  @override
  void didUpdateWidget(covariant _TripRouteMapCard oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.data.routePolyline != widget.data.routePolyline ||
        oldWidget.data.pickupLocation != widget.data.pickupLocation ||
        oldWidget.data.destinationLocation != widget.data.destinationLocation) {
      _hasFittedBounds = false;
      WidgetsBinding.instance.addPostFrameCallback((_) => _fitBounds());
    }
  }

  Future<void> _fitBounds() async {
    if (!mounted || _hasFittedBounds || _mapController == null) {
      return;
    }

    final points = _cameraPoints;
    if (points.isEmpty) {
      return;
    }

    if (points.length == 1) {
      _hasFittedBounds = true;
      await _mapController!.moveCamera(
        AppCameraPosition(target: points.first, zoom: 15),
      );
      return;
    }

    var minLat = points.first.latitude;
    var maxLat = points.first.latitude;
    var minLng = points.first.longitude;
    var maxLng = points.first.longitude;

    for (final point in points.skip(1)) {
      if (point.latitude < minLat) minLat = point.latitude;
      if (point.latitude > maxLat) maxLat = point.latitude;
      if (point.longitude < minLng) minLng = point.longitude;
      if (point.longitude > maxLng) maxLng = point.longitude;
    }

    final latPadding = (maxLat - minLat).abs() < 0.002 ? 0.004 : 0.0015;
    final lngPadding = (maxLng - minLng).abs() < 0.002 ? 0.004 : 0.0015;

    _hasFittedBounds = true;
    await _mapController!.animateCameraToBounds(
      AppLatLng(minLat - latPadding, minLng - lngPadding),
      AppLatLng(maxLat + latPadding, maxLng + lngPadding),
      28,
      top: 32,
      bottom: 32,
      left: 28,
      right: 28,
    );
  }

  @override
  Widget build(BuildContext context) {
    if (!widget.data.hasMapCoordinates) {
      return _TripSectionCard(
        child: SizedBox(
          height: 180,
          child: Center(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Icon(Icons.map_outlined, size: 34, color: Color(0xFF98A2B3)),
                SizedBox(height: 10),
                Text(
                  context.l10n.routeMapUnavailable,
                  textAlign: TextAlign.center,
                  style: TextStyle(color: Color(0xFF667085)),
                ),
              ],
            ),
          ),
        ),
      );
    }

    return _TripSectionCard(
      padding: EdgeInsets.zero,
      clipBehavior: Clip.antiAlias,
      child: SizedBox(
        height: 190,
        child: Stack(
          children: [
            MapRendererWidget(
              initialCameraPosition: _initialCameraPosition,
              markers: _markers,
              polylines: _polylines,
              onMapCreated: (controller) {
                _mapController = controller;
                _fitBounds();
              },
            ),
            Positioned(
              top: 14,
              left: 14,
              child: Container(
                padding: const EdgeInsets.symmetric(
                  horizontal: 10,
                  vertical: 7,
                ),
                decoration: BoxDecoration(
                  color: Colors.white.withValues(alpha: 0.92),
                  borderRadius: BorderRadius.circular(999),
                  boxShadow: [
                    BoxShadow(
                      color: Colors.black.withValues(alpha: 0.08),
                      blurRadius: 10,
                    ),
                  ],
                ),
                child: Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Icon(
                      Icons.alt_route_rounded,
                      size: 16,
                      color: AppColors.primary,
                    ),
                    SizedBox(width: 6),
                    Text(
                      context.l10n.route,
                      style: TextStyle(
                        fontSize: 12,
                        fontWeight: FontWeight.w700,
                        color: AppColors.primary,
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _TripRouteTimeline extends StatelessWidget {
  _TripRouteTimeline({required this.data});

  final TripDetailsViewData data;

  @override
  Widget build(BuildContext context) {
    return _TripSectionCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            context.l10n.tripRoute,
            style: TextStyle(
              fontSize: 18,
              fontWeight: FontWeight.w800,
              color: Color(0xFF101828),
            ),
          ),
          SizedBox(height: 18),
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Column(
                children: [
                  _RouteDot(
                    color: AppColors.primary,
                    icon: Icons.my_location_rounded,
                  ),
                  Container(
                    width: 2,
                    height: 36,
                    margin: const EdgeInsets.symmetric(vertical: 4),
                    color: Color(0xFFE4E7EC),
                  ),
                  _RouteDot(
                    color: Color(0xFFEF4444),
                    icon: Icons.location_on_rounded,
                  ),
                ],
              ),
              SizedBox(width: 14),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    _RouteInfoBlock(
                      label: context.l10n.pickupPoint,
                      address: data.pickupAddress,
                    ),
                    SizedBox(height: 22),
                    _RouteInfoBlock(
                      label: context.l10n.destinationPoint,
                      address: data.destinationAddress,
                    ),
                  ],
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _TripQuickStats extends StatelessWidget {
  _TripQuickStats({required this.data});

  final TripDetailsViewData data;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Expanded(
          child: _TripStatCard(
            icon: data.isMotorbike
                ? Icons.two_wheeler_rounded
                : Icons.route_rounded,
            label: context.l10n.distance,
            value: '${data.distanceKm.toStringAsFixed(1)} km',
          ),
        ),
        SizedBox(width: 12),
        Expanded(
          child: _TripStatCard(
            icon: Icons.schedule_rounded,
            label: context.l10n.duration,
            value: data.durationMinutes != null
                ? context.l10n.minutesValue(data.durationMinutes!)
                : context.l10n.unknown,
          ),
        ),
      ],
    );
  }
}

class _TripDriverCard extends StatelessWidget {
  _TripDriverCard({required this.data});

  final TripDetailsViewData data;

  @override
  Widget build(BuildContext context) {
    return _TripSectionCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            context.l10n.driverAndVehicle,
            style: TextStyle(
              fontSize: 18,
              fontWeight: FontWeight.w800,
              color: Color(0xFF101828),
            ),
          ),
          SizedBox(height: 16),
          if (!data.hasDriverInfo)
            Container(
              width: double.infinity,
              padding: const EdgeInsets.all(16),
              decoration: BoxDecoration(
                color: Color(0xFFF8FAFC),
                borderRadius: BorderRadius.circular(18),
                border: Border.all(color: Color(0xFFE4E7EC)),
              ),
              child: Text(
                context.l10n.driverInfoUnavailable,
                style: TextStyle(color: Color(0xFF667085)),
              ),
            )
          else
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                CircleAvatar(
                  radius: 30,
                  backgroundColor: Color(0xFFD0D5DD),
                  backgroundImage: data.driverAvatarUrl != null
                      ? NetworkImage(data.driverAvatarUrl!)
                      : null,
                  child: data.driverAvatarUrl == null
                      ? Icon(Icons.person, color: Colors.white, size: 30)
                      : null,
                ),
                SizedBox(width: 14),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Row(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Expanded(
                            child: Text(
                              data.driverName!,
                              style: TextStyle(
                                fontSize: 16,
                                fontWeight: FontWeight.w800,
                                color: Color(0xFF101828),
                              ),
                            ),
                          ),
                          if (data.driverRating != null)
                            Container(
                              padding: const EdgeInsets.symmetric(
                                horizontal: 10,
                                vertical: 6,
                              ),
                              decoration: BoxDecoration(
                                color: Color(0xFFFFF3D6),
                                borderRadius: BorderRadius.circular(999),
                              ),
                              child: Row(
                                mainAxisSize: MainAxisSize.min,
                                children: [
                                  Icon(
                                    Icons.star_rounded,
                                    size: 15,
                                    color: Color(0xFFF59E0B),
                                  ),
                                  SizedBox(width: 4),
                                  Text(
                                    data.driverRating!.toStringAsFixed(1),
                                    style: TextStyle(
                                      fontSize: 12,
                                      fontWeight: FontWeight.w800,
                                      color: Color(0xFF92400E),
                                    ),
                                  ),
                                ],
                              ),
                            ),
                        ],
                      ),
                      SizedBox(height: 8),
                      Text(
                        data.vehicleName,
                        style: TextStyle(
                          fontSize: 14,
                          fontWeight: FontWeight.w600,
                          color: Color(0xFF344054),
                        ),
                      ),
                      if (data.plateNumber != null) ...[
                        SizedBox(height: 4),
                        Text(
                          context.l10n.plateValue(data.plateNumber!),
                          style: TextStyle(
                            fontSize: 13,
                            color: Color(0xFF667085),
                          ),
                        ),
                      ],
                      if (data.vehicleColor != null) ...[
                        SizedBox(height: 2),
                        Text(
                          context.l10n.vehicleColorValue(data.vehicleColor!),
                          style: TextStyle(
                            fontSize: 13,
                            color: Color(0xFF667085),
                          ),
                        ),
                      ],
                    ],
                  ),
                ),
              ],
            ),
          if (data.driverTripCount != null ||
              data.driverExperienceYears != null ||
              data.driverLicenseClass != null) ...[
            SizedBox(height: 16),
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: [
                if (data.driverTripCount != null)
                  _InfoChip(
                    label: context.l10n.tripCountValue(data.driverTripCount!),
                  ),
                if (data.driverExperienceYears != null)
                  _InfoChip(
                    label: context.l10n.experienceYearsValue(
                      data.driverExperienceYears!,
                    ),
                  ),
                if (data.driverLicenseClass != null)
                  _InfoChip(
                    label: context.l10n.requiredLicense(
                      data.driverLicenseClass!,
                    ),
                  ),
              ],
            ),
          ],
        ],
      ),
    );
  }
}

class _TripPaymentCard extends StatelessWidget {
  _TripPaymentCard({required this.data});

  final TripDetailsViewData data;

  String _formatCurrency(BuildContext context, double value) {
    return NumberFormat.currency(
      locale: Localizations.localeOf(context).toLanguageTag(),
      symbol: '₫',
      decimalDigits: 0,
    ).format(value);
  }

  @override
  Widget build(BuildContext context) {
    final hasDiscount = data.discountAmount > 0;

    return _TripSectionCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: Text(
                  context.l10n.tripCost,
                  style: TextStyle(
                    fontSize: 18,
                    fontWeight: FontWeight.w800,
                    color: Color(0xFF101828),
                  ),
                ),
              ),
              Column(
                crossAxisAlignment: CrossAxisAlignment.end,
                children: [
                  Text(
                    data.paymentMethod ?? context.l10n.unknownPaymentMethod,
                    style: TextStyle(
                      fontSize: 12,
                      fontWeight: FontWeight.w700,
                      color: AppColors.primary,
                    ),
                  ),
                  SizedBox(height: 4),
                  Text(
                    data.paymentStatusLabel,
                    style: TextStyle(fontSize: 12, color: Color(0xFF667085)),
                  ),
                ],
              ),
            ],
          ),
          SizedBox(height: 16),
          _PriceLine(
            label: context.l10n.fare,
            value: _formatCurrency(context, data.baseFare),
          ),
          SizedBox(height: 10),
          _PriceLine(
            label: context.l10n.discount,
            value: hasDiscount
                ? '-${_formatCurrency(context, data.discountAmount)}'
                : _formatCurrency(context, 0),
            valueColor: hasDiscount ? AppColors.primary : null,
          ),
          SizedBox(height: 14),
          Divider(height: 1, color: Color(0xFFE4E7EC)),
          SizedBox(height: 14),
          _PriceLine(
            label: context.l10n.total,
            value: _formatCurrency(context, data.totalFare),
            labelStyle: TextStyle(
              fontSize: 16,
              fontWeight: FontWeight.w800,
              color: Color(0xFF101828),
            ),
            valueStyle: TextStyle(
              fontSize: 22,
              fontWeight: FontWeight.w900,
              color: AppColors.primary,
            ),
          ),
          if (data.paymentMessage != null) ...[
            SizedBox(height: 14),
            Text(
              data.paymentMessage!,
              style: TextStyle(
                fontSize: 13,
                height: 1.5,
                color: Color(0xFF667085),
              ),
            ),
          ],
          if (data.paidAt != null) ...[
            SizedBox(height: 10),
            Text(
              context.l10n.paidAtValue(
                DateFormat.yMd(
                  Localizations.localeOf(context).toLanguageTag(),
                ).add_Hm().format(data.paidAt!),
              ),
              style: TextStyle(fontSize: 13, color: Color(0xFF667085)),
            ),
          ],
        ],
      ),
    );
  }
}

class _TripFeedbackCard extends StatelessWidget {
  _TripFeedbackCard({required this.data});

  final TripDetailsViewData data;

  @override
  Widget build(BuildContext context) {
    final roleProvider = context.read<RoleProvider>();
    final isDriver = roleProvider.isDriver;

    return _TripSectionCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            isDriver
                ? context.l10n.customerReview
                : context.l10n.reviewAndFeedback,
            style: TextStyle(
              fontSize: 18,
              fontWeight: FontWeight.w800,
              color: Color(0xFF101828),
            ),
          ),
          SizedBox(height: 16),
          if (data.hasFeedback) ...[
            if (isDriver && data.feedbackCustomerName != null) ...[
              Row(
                children: [
                  CircleAvatar(
                    radius: 20,
                    backgroundColor: Color(0xFFE0EAEB),
                    backgroundImage: data.feedbackCustomerAvatarUrl != null
                        ? NetworkImage(data.feedbackCustomerAvatarUrl!)
                        : null,
                    child: data.feedbackCustomerAvatarUrl == null
                        ? Text(
                            data.feedbackCustomerName![0].toUpperCase(),
                            style: TextStyle(
                              color: AppColors.primary,
                              fontWeight: FontWeight.bold,
                            ),
                          )
                        : null,
                  ),
                  SizedBox(width: 12),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          data.feedbackCustomerName!,
                          style: TextStyle(
                            fontSize: 15,
                            fontWeight: FontWeight.w700,
                            color: Color(0xFF101828),
                          ),
                        ),
                        if (data.feedbackCreatedAt != null)
                          Text(
                            DateFormat(
                              'dd/MM/yyyy',
                            ).format(data.feedbackCreatedAt!),
                            style: TextStyle(
                              fontSize: 12,
                              color: Color(0xFF667085),
                            ),
                          ),
                      ],
                    ),
                  ),
                ],
              ),
              SizedBox(height: 12),
            ],
            Row(
              children: List.generate(5, (index) {
                final selected = index < (data.ratingScore ?? 0);
                return Icon(
                  selected ? Icons.star_rounded : Icons.star_outline_rounded,
                  color: selected ? Color(0xFFF59E0B) : Color(0xFFD0D5DD),
                  size: 24,
                );
              }),
            ),
            if (data.feedbackComment != null &&
                data.feedbackComment!.isNotEmpty) ...[
              SizedBox(height: 12),
              Container(
                width: double.infinity,
                padding: const EdgeInsets.all(14),
                decoration: BoxDecoration(
                  color: Color(0xFFF8FAFC),
                  borderRadius: BorderRadius.circular(16),
                ),
                child: Text(
                  data.feedbackComment!,
                  style: TextStyle(
                    fontSize: 14,
                    height: 1.6,
                    color: Color(0xFF475467),
                  ),
                ),
              ),
            ],
          ] else
            Container(
              width: double.infinity,
              padding: const EdgeInsets.all(16),
              decoration: BoxDecoration(
                color: Color(0xFFF8FAFC),
                borderRadius: BorderRadius.circular(18),
                border: Border.all(color: Color(0xFFE4E7EC)),
              ),
              child: Text(
                isDriver
                    ? context.l10n.customerHasNotReviewed
                    : context.l10n.noReviewData,
                style: TextStyle(fontSize: 14, color: Color(0xFF667085)),
              ),
            ),
        ],
      ),
    );
  }
}

class _TripSectionCard extends StatelessWidget {
  _TripSectionCard({
    required this.child,
    this.padding = const EdgeInsets.all(20),
    this.clipBehavior = Clip.none,
  });

  final Widget child;
  final EdgeInsetsGeometry padding;
  final Clip clipBehavior;

  @override
  Widget build(BuildContext context) {
    return Container(
      clipBehavior: clipBehavior,
      padding: padding,
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(24),
        border: Border.all(color: Color(0xFFE7E3E2)),
        boxShadow: [
          BoxShadow(
            color: Colors.black.withValues(alpha: 0.03),
            blurRadius: 14,
            offset: Offset(0, 6),
          ),
        ],
      ),
      child: child,
    );
  }
}

class _TripStatCard extends StatelessWidget {
  _TripStatCard({required this.icon, required this.label, required this.value});

  final IconData icon;
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(20),
        border: Border.all(color: Color(0xFFE7E3E2)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Icon(icon, size: 18, color: AppColors.primary),
              SizedBox(width: 8),
              Flexible(
                child: Text(
                  label,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(
                    fontSize: 12,
                    fontWeight: FontWeight.w700,
                    color: Color(0xFF667085),
                  ),
                ),
              ),
            ],
          ),
          SizedBox(height: 12),
          Text(
            value,
            style: TextStyle(
              fontSize: 18,
              fontWeight: FontWeight.w900,
              color: Color(0xFF101828),
            ),
          ),
        ],
      ),
    );
  }
}

class _RouteDot extends StatelessWidget {
  _RouteDot({required this.color, required this.icon});

  final Color color;
  final IconData icon;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 24,
      height: 24,
      decoration: BoxDecoration(
        color: color,
        shape: BoxShape.circle,
        boxShadow: [
          BoxShadow(color: color.withValues(alpha: 0.25), blurRadius: 10),
        ],
      ),
      child: Icon(icon, size: 13, color: Colors.white),
    );
  }
}

class _RouteInfoBlock extends StatelessWidget {
  _RouteInfoBlock({required this.label, required this.address});

  final String label;
  final String address;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          label,
          style: TextStyle(
            fontSize: 12,
            fontWeight: FontWeight.w700,
            color: Color(0xFF667085),
          ),
        ),
        SizedBox(height: 6),
        Text(
          address,
          style: TextStyle(
            fontSize: 15,
            height: 1.5,
            fontWeight: FontWeight.w700,
            color: Color(0xFF101828),
          ),
        ),
      ],
    );
  }
}

class _PriceLine extends StatelessWidget {
  _PriceLine({
    required this.label,
    required this.value,
    this.labelStyle,
    this.valueStyle,
    this.valueColor,
  });

  final String label;
  final String value;
  final TextStyle? labelStyle;
  final TextStyle? valueStyle;
  final Color? valueColor;

  @override
  Widget build(BuildContext context) {
    final resolvedValueStyle =
        valueStyle ??
        TextStyle(
          fontSize: 14,
          fontWeight: FontWeight.w700,
          color: valueColor ?? Color(0xFF101828),
        );

    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Expanded(
          child: Text(
            label,
            style:
                labelStyle ?? TextStyle(fontSize: 14, color: Color(0xFF667085)),
          ),
        ),
        SizedBox(width: 12),
        Text(value, style: resolvedValueStyle),
      ],
    );
  }
}

class _InfoChip extends StatelessWidget {
  _InfoChip({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
      decoration: BoxDecoration(
        color: Color(0xFFF2F4F7),
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(
        label,
        style: TextStyle(
          fontSize: 12,
          fontWeight: FontWeight.w700,
          color: Color(0xFF475467),
        ),
      ),
    );
  }
}

class _InlineFeedbackCard extends StatelessWidget {
  _InlineFeedbackCard({
    required this.message,
    required this.actionLabel,
    required this.onPressed,
  });

  final String message;
  final String actionLabel;
  final VoidCallback onPressed;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Color(0xFFFFF6ED),
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: Color(0xFFFED7AA)),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Padding(
            padding: EdgeInsets.only(top: 1),
            child: Icon(Icons.info_outline_rounded, color: Color(0xFFEA580C)),
          ),
          SizedBox(width: 12),
          Expanded(
            child: Text(
              message,
              style: TextStyle(
                fontSize: 14,
                height: 1.5,
                color: Color(0xFF9A3412),
              ),
            ),
          ),
          SizedBox(width: 12),
          TextButton(
            onPressed: onPressed,
            child: Text(
              actionLabel,
              style: TextStyle(
                fontWeight: FontWeight.w700,
                color: Color(0xFFEA580C),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _StatusStyle {
  _StatusStyle({required this.backgroundColor, required this.textColor});

  final Color backgroundColor;
  final Color textColor;

  factory _StatusStyle.fromStatus(String status) {
    return switch (status) {
      'COMPLETED' || '5' => _StatusStyle(
        backgroundColor: Color(0xFFDCFCE7),
        textColor: Color(0xFF166534),
      ),
      'WAITING_PAYMENT' || '6' => _StatusStyle(
        backgroundColor: Color(0xFFFEF3C7),
        textColor: Color(0xFF92400E),
      ),
      'CANCELLED' || 'CANCEL' || 'EXPIRED' || '3' || '4' || '8' => _StatusStyle(
        backgroundColor: Color(0xFFFEE2E2),
        textColor: Color(0xFFB91C1C),
      ),
      _ => _StatusStyle(
        backgroundColor: Color(0xFFE0F2FE),
        textColor: Color(0xFF0C4A6E),
      ),
    };
  }
}
