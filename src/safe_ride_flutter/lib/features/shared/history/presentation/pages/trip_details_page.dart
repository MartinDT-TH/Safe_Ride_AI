import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:provider/provider.dart';

import '../../../../../core/constants/app_colors.dart';
import '../../../../../core/maps/models/map_models.dart';
import '../../../../../core/maps/polyline_decoder.dart';
import '../../../../../core/maps/widgets/map_renderer_widget.dart';
import '../../../../../core/widgets/app_loading_screen.dart';
import '../../../../../dependency_injection/injection.dart';
import '../../../../auth/presentation/providers/auth_provider.dart';
import '../../../../customer/booking/domain/repositories/booking_repository.dart';
import '../../../../customer/booking/presentation/pages/rebook_trip_page.dart';
import '../../../../customer/booking/presentation/providers/booking_provider.dart';
import '../../data/models/history_trip.dart';
import '../../data/models/trip_details_view_data.dart';
import '../../data/repositories/trip_details_repository_impl.dart';
import '../providers/trip_details_provider.dart';

class TripDetailsPage extends StatelessWidget {
  const TripDetailsPage({
    super.key,
    required this.trip,
    required this.canRebook,
  });

  final HistoryTrip trip;
  final bool canRebook;

  @override
  Widget build(BuildContext context) {
    final accessToken = context.read<AuthProvider>().token;

    return ChangeNotifierProvider<TripDetailsProvider>(
      create: (_) => TripDetailsProvider.create(
        TripDetailsRepositoryImpl(getIt<BookingRepository>()),
        trip,
      )..loadDetails(accessToken),
      child: _TripDetailsView(
        trip: trip,
        canRebook: canRebook,
        accessToken: accessToken,
      ),
    );
  }
}

class _TripDetailsView extends StatelessWidget {
  const _TripDetailsView({
    required this.trip,
    required this.canRebook,
    required this.accessToken,
  });

  final HistoryTrip trip;
  final bool canRebook;
  final String? accessToken;

  Future<void> _reload(BuildContext context) {
    return context.read<TripDetailsProvider>().loadDetails(accessToken);
  }

  Future<void> _handleRebook(BuildContext context) async {
    final bookingProvider = context.read<BookingProvider>();
    final token = context.read<AuthProvider>().token;

    if (token == null || token.isEmpty) {
      _showMessage(
        context,
        'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.',
      );
      return;
    }

    AppLoadingScreen.show(context, message: 'Đang tải thông tin chuyến đi...');
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
        bookingProvider.errorMessage ?? 'Không thể tải thông tin chuyến đi.',
      );
      return;
    }

    if (details.pickup == null ||
        details.destination == null ||
        details.vehicle == null) {
      _showMessage(context, 'Chuyến đi này chưa có đủ dữ liệu để đặt lại.');
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

  @override
  Widget build(BuildContext context) {
    return Consumer<TripDetailsProvider>(
      builder: (context, provider, child) {
        final data = provider.tripDetails;

        return Scaffold(
          backgroundColor: const Color(0xFFFCF9F8),
          appBar: AppBar(
            backgroundColor: Colors.white,
            elevation: 0,
            centerTitle: true,
            title: const Text(
              'Chi tiết chuyến đi',
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
                      border: const Border(
                        top: BorderSide(color: Color(0xFFE7E3E2)),
                      ),
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
                        icon: const Icon(Icons.history_rounded),
                        label: const Text(
                          'Đặt lại chuyến này',
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
              physics: const AlwaysScrollableScrollPhysics(),
              padding: const EdgeInsets.fromLTRB(20, 20, 20, 24),
              children: [
                if (provider.isLoading && !provider.hasLoadedRemoteDetails)
                  const Padding(
                    padding: EdgeInsets.only(bottom: 16),
                    child: LinearProgressIndicator(minHeight: 3),
                  ),
                if (provider.errorMessage != null)
                  Padding(
                    padding: const EdgeInsets.only(bottom: 16),
                    child: _InlineFeedbackCard(
                      message: provider.errorMessage!,
                      actionLabel: 'Thử lại',
                      onPressed: () => _reload(context),
                    ),
                  ),
                _TripMetaHeader(data: data),
                const SizedBox(height: 16),
                _TripRouteMapCard(data: data),
                const SizedBox(height: 16),
                _TripRouteTimeline(data: data),
                const SizedBox(height: 16),
                _TripQuickStats(data: data),
                const SizedBox(height: 16),
                _TripDriverCard(data: data),
                const SizedBox(height: 16),
                _TripPaymentCard(data: data),
                const SizedBox(height: 16),
                _TripFeedbackCard(data: data),
              ],
            ),
          ),
        );
      },
    );
  }
}

class _TripMetaHeader extends StatelessWidget {
  const _TripMetaHeader({required this.data});

  final TripDetailsViewData data;

  @override
  Widget build(BuildContext context) {
    final statusStyle = _StatusStyle.fromStatus(data.normalizedStatus);
    final dateText = DateFormat(
      'HH:mm, dd/MM/yyyy',
      'vi_VN',
    ).format(data.bookingTime);

    return Container(
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(24),
        boxShadow: [
          BoxShadow(
            color: Colors.black.withValues(alpha: 0.03),
            blurRadius: 14,
            offset: const Offset(0, 6),
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
                const Text(
                  'Mã chuyến',
                  style: TextStyle(
                    fontSize: 12,
                    letterSpacing: 0.4,
                    fontWeight: FontWeight.w700,
                    color: Color(0xFF667085),
                  ),
                ),
                const SizedBox(height: 6),
                Text(
                  '#${data.tripId ?? data.bookingId}',
                  style: const TextStyle(
                    fontSize: 24,
                    fontWeight: FontWeight.w900,
                    color: Color(0xFF101828),
                  ),
                ),
                if (data.tripId != null) ...[
                  const SizedBox(height: 4),
                  Text(
                    'Đơn đặt xe #${data.bookingId}',
                    style: const TextStyle(
                      fontSize: 13,
                      color: Color(0xFF667085),
                    ),
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
              const SizedBox(height: 10),
              Text(
                dateText,
                textAlign: TextAlign.right,
                style: const TextStyle(fontSize: 13, color: Color(0xFF667085)),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _TripRouteMapCard extends StatefulWidget {
  const _TripRouteMapCard({required this.data});

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
      return const [];
    }

    try {
      return decodePolyline(routePolyline);
    } catch (_) {
      return const [];
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
      return const {};
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

    return const AppCameraPosition(target: AppLatLng(10.7769, 106.7009));
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
              children: const [
                Icon(Icons.map_outlined, size: 34, color: Color(0xFF98A2B3)),
                SizedBox(height: 10),
                Text(
                  'Chưa có bản đồ tuyến đường cho chuyến này.',
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
                  children: const [
                    Icon(
                      Icons.alt_route_rounded,
                      size: 16,
                      color: AppColors.primary,
                    ),
                    SizedBox(width: 6),
                    Text(
                      'Tuyến đường',
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
  const _TripRouteTimeline({required this.data});

  final TripDetailsViewData data;

  @override
  Widget build(BuildContext context) {
    return _TripSectionCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            'Lộ trình chuyến đi',
            style: TextStyle(
              fontSize: 18,
              fontWeight: FontWeight.w800,
              color: Color(0xFF101828),
            ),
          ),
          const SizedBox(height: 18),
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
                    color: const Color(0xFFE4E7EC),
                  ),
                  const _RouteDot(
                    color: Color(0xFFEF4444),
                    icon: Icons.location_on_rounded,
                  ),
                ],
              ),
              const SizedBox(width: 14),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    _RouteInfoBlock(
                      label: 'Điểm đón',
                      address: data.pickupAddress,
                    ),
                    const SizedBox(height: 22),
                    _RouteInfoBlock(
                      label: 'Điểm đến',
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
  const _TripQuickStats({required this.data});

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
            label: 'Quãng đường',
            value: '${data.distanceKm.toStringAsFixed(1)} km',
          ),
        ),
        const SizedBox(width: 12),
        Expanded(
          child: _TripStatCard(
            icon: Icons.schedule_rounded,
            label: 'Thời gian',
            value: data.durationMinutes != null
                ? '${data.durationMinutes} phút'
                : 'Chưa rõ',
          ),
        ),
      ],
    );
  }
}

class _TripDriverCard extends StatelessWidget {
  const _TripDriverCard({required this.data});

  final TripDetailsViewData data;

  @override
  Widget build(BuildContext context) {
    return _TripSectionCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            'Tài xế và phương tiện',
            style: TextStyle(
              fontSize: 18,
              fontWeight: FontWeight.w800,
              color: Color(0xFF101828),
            ),
          ),
          const SizedBox(height: 16),
          if (!data.hasDriverInfo)
            Container(
              width: double.infinity,
              padding: const EdgeInsets.all(16),
              decoration: BoxDecoration(
                color: const Color(0xFFF8FAFC),
                borderRadius: BorderRadius.circular(18),
                border: Border.all(color: const Color(0xFFE4E7EC)),
              ),
              child: const Text(
                'Chưa có thông tin tài xế cho chuyến đi này.',
                style: TextStyle(color: Color(0xFF667085)),
              ),
            )
          else
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                CircleAvatar(
                  radius: 30,
                  backgroundColor: const Color(0xFFD0D5DD),
                  backgroundImage: data.driverAvatarUrl != null
                      ? NetworkImage(data.driverAvatarUrl!)
                      : null,
                  child: data.driverAvatarUrl == null
                      ? const Icon(Icons.person, color: Colors.white, size: 30)
                      : null,
                ),
                const SizedBox(width: 14),
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
                              style: const TextStyle(
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
                                color: const Color(0xFFFFF3D6),
                                borderRadius: BorderRadius.circular(999),
                              ),
                              child: Row(
                                mainAxisSize: MainAxisSize.min,
                                children: [
                                  const Icon(
                                    Icons.star_rounded,
                                    size: 15,
                                    color: Color(0xFFF59E0B),
                                  ),
                                  const SizedBox(width: 4),
                                  Text(
                                    data.driverRating!.toStringAsFixed(1),
                                    style: const TextStyle(
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
                      const SizedBox(height: 8),
                      Text(
                        data.vehicleName,
                        style: const TextStyle(
                          fontSize: 14,
                          fontWeight: FontWeight.w600,
                          color: Color(0xFF344054),
                        ),
                      ),
                      if (data.plateNumber != null) ...[
                        const SizedBox(height: 4),
                        Text(
                          'Biển số: ${data.plateNumber}',
                          style: const TextStyle(
                            fontSize: 13,
                            color: Color(0xFF667085),
                          ),
                        ),
                      ],
                      if (data.vehicleColor != null) ...[
                        const SizedBox(height: 2),
                        Text(
                          'Màu xe: ${data.vehicleColor}',
                          style: const TextStyle(
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
            const SizedBox(height: 16),
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: [
                if (data.driverTripCount != null)
                  _InfoChip(label: '${data.driverTripCount} chuyến'),
                if (data.driverExperienceYears != null)
                  _InfoChip(
                    label: '${data.driverExperienceYears} năm kinh nghiệm',
                  ),
                if (data.driverLicenseClass != null)
                  _InfoChip(label: 'GPLX ${data.driverLicenseClass}'),
              ],
            ),
          ],
        ],
      ),
    );
  }
}

class _TripPaymentCard extends StatelessWidget {
  const _TripPaymentCard({required this.data});

  final TripDetailsViewData data;

  String _formatCurrency(double value) {
    return NumberFormat.currency(
      locale: 'vi_VN',
      symbol: 'đ',
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
              const Expanded(
                child: Text(
                  'Chi phí chuyến đi',
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
                    data.paymentMethod ?? 'Chưa rõ phương thức',
                    style: const TextStyle(
                      fontSize: 12,
                      fontWeight: FontWeight.w700,
                      color: AppColors.primary,
                    ),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    data.paymentStatusLabel,
                    style: const TextStyle(
                      fontSize: 12,
                      color: Color(0xFF667085),
                    ),
                  ),
                ],
              ),
            ],
          ),
          const SizedBox(height: 16),
          _PriceLine(label: 'Cước phí', value: _formatCurrency(data.baseFare)),
          const SizedBox(height: 10),
          _PriceLine(
            label: 'Giảm giá',
            value: hasDiscount
                ? '-${_formatCurrency(data.discountAmount)}'
                : _formatCurrency(0),
            valueColor: hasDiscount ? AppColors.primary : null,
          ),
          const SizedBox(height: 14),
          const Divider(height: 1, color: Color(0xFFE4E7EC)),
          const SizedBox(height: 14),
          _PriceLine(
            label: 'Tổng cộng',
            value: _formatCurrency(data.totalFare),
            labelStyle: const TextStyle(
              fontSize: 16,
              fontWeight: FontWeight.w800,
              color: Color(0xFF101828),
            ),
            valueStyle: const TextStyle(
              fontSize: 22,
              fontWeight: FontWeight.w900,
              color: AppColors.primary,
            ),
          ),
          if (data.paymentMessage != null) ...[
            const SizedBox(height: 14),
            Text(
              data.paymentMessage!,
              style: const TextStyle(
                fontSize: 13,
                height: 1.5,
                color: Color(0xFF667085),
              ),
            ),
          ],
          if (data.paidAt != null) ...[
            const SizedBox(height: 10),
            Text(
              'Thanh toán lúc ${DateFormat('HH:mm, dd/MM/yyyy', 'vi_VN').format(data.paidAt!)}',
              style: const TextStyle(fontSize: 13, color: Color(0xFF667085)),
            ),
          ],
        ],
      ),
    );
  }
}

class _TripFeedbackCard extends StatelessWidget {
  const _TripFeedbackCard({required this.data});

  final TripDetailsViewData data;

  @override
  Widget build(BuildContext context) {
    return _TripSectionCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            'Đánh giá và phản hồi',
            style: TextStyle(
              fontSize: 18,
              fontWeight: FontWeight.w800,
              color: Color(0xFF101828),
            ),
          ),
          const SizedBox(height: 16),
          if (data.hasFeedback) ...[
            Row(
              children: List.generate(5, (index) {
                final selected = index < (data.ratingScore ?? 0);
                return Icon(
                  selected ? Icons.star_rounded : Icons.star_outline_rounded,
                  color: selected
                      ? const Color(0xFFF59E0B)
                      : const Color(0xFFD0D5DD),
                  size: 24,
                );
              }),
            ),
            if (data.feedbackComment != null) ...[
              const SizedBox(height: 12),
              Container(
                width: double.infinity,
                padding: const EdgeInsets.all(14),
                decoration: BoxDecoration(
                  color: const Color(0xFFF8FAFC),
                  borderRadius: BorderRadius.circular(16),
                ),
                child: Text(
                  data.feedbackComment!,
                  style: const TextStyle(
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
                color: const Color(0xFFF8FAFC),
                borderRadius: BorderRadius.circular(18),
                border: Border.all(color: const Color(0xFFE4E7EC)),
              ),
              child: const Text(
                'Chưa có dữ liệu đánh giá cho chuyến đi này.',
                style: TextStyle(fontSize: 14, color: Color(0xFF667085)),
              ),
            ),
        ],
      ),
    );
  }
}

class _TripSectionCard extends StatelessWidget {
  const _TripSectionCard({
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
        border: Border.all(color: const Color(0xFFE7E3E2)),
        boxShadow: [
          BoxShadow(
            color: Colors.black.withValues(alpha: 0.03),
            blurRadius: 14,
            offset: const Offset(0, 6),
          ),
        ],
      ),
      child: child,
    );
  }
}

class _TripStatCard extends StatelessWidget {
  const _TripStatCard({
    required this.icon,
    required this.label,
    required this.value,
  });

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
        border: Border.all(color: const Color(0xFFE7E3E2)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Icon(icon, size: 18, color: AppColors.primary),
              const SizedBox(width: 8),
              Flexible(
                child: Text(
                  label,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(
                    fontSize: 12,
                    fontWeight: FontWeight.w700,
                    color: Color(0xFF667085),
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: 12),
          Text(
            value,
            style: const TextStyle(
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
  const _RouteDot({required this.color, required this.icon});

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
  const _RouteInfoBlock({required this.label, required this.address});

  final String label;
  final String address;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          label,
          style: const TextStyle(
            fontSize: 12,
            fontWeight: FontWeight.w700,
            color: Color(0xFF667085),
          ),
        ),
        const SizedBox(height: 6),
        Text(
          address,
          style: const TextStyle(
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
  const _PriceLine({
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
          color: valueColor ?? const Color(0xFF101828),
        );

    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Expanded(
          child: Text(
            label,
            style:
                labelStyle ??
                const TextStyle(fontSize: 14, color: Color(0xFF667085)),
          ),
        ),
        const SizedBox(width: 12),
        Text(value, style: resolvedValueStyle),
      ],
    );
  }
}

class _InfoChip extends StatelessWidget {
  const _InfoChip({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
      decoration: BoxDecoration(
        color: const Color(0xFFF2F4F7),
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(
        label,
        style: const TextStyle(
          fontSize: 12,
          fontWeight: FontWeight.w700,
          color: Color(0xFF475467),
        ),
      ),
    );
  }
}

class _InlineFeedbackCard extends StatelessWidget {
  const _InlineFeedbackCard({
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
        color: const Color(0xFFFFF6ED),
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: const Color(0xFFFED7AA)),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Padding(
            padding: EdgeInsets.only(top: 1),
            child: Icon(Icons.info_outline_rounded, color: Color(0xFFEA580C)),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Text(
              message,
              style: const TextStyle(
                fontSize: 14,
                height: 1.5,
                color: Color(0xFF9A3412),
              ),
            ),
          ),
          const SizedBox(width: 12),
          TextButton(
            onPressed: onPressed,
            child: Text(
              actionLabel,
              style: const TextStyle(
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
  const _StatusStyle({required this.backgroundColor, required this.textColor});

  final Color backgroundColor;
  final Color textColor;

  factory _StatusStyle.fromStatus(String status) {
    return switch (status) {
      'COMPLETED' || '5' => const _StatusStyle(
        backgroundColor: Color(0xFFDCFCE7),
        textColor: Color(0xFF166534),
      ),
      'WAITING_PAYMENT' || '6' => const _StatusStyle(
        backgroundColor: Color(0xFFFEF3C7),
        textColor: Color(0xFF92400E),
      ),
      'CANCELLED' ||
      'CANCEL' ||
      'EXPIRED' ||
      '3' ||
      '4' ||
      '8' => const _StatusStyle(
        backgroundColor: Color(0xFFFEE2E2),
        textColor: Color(0xFFB91C1C),
      ),
      _ => const _StatusStyle(
        backgroundColor: Color(0xFFE0F2FE),
        textColor: Color(0xFF0C4A6E),
      ),
    };
  }
}
