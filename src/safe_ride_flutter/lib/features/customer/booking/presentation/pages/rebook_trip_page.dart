import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../../../core/constants/app_colors.dart';
import '../../../../../core/maps/models/map_models.dart';
import '../../../../../core/maps/widgets/map_renderer_widget.dart';
import '../../../../../core/widgets/app_loading_screen.dart';
import '../../../../auth/presentation/providers/auth_provider.dart';
import '../../data/models/booking_response.dart';
import '../../data/models/create_booking_request.dart';
import '../providers/booking_provider.dart';
import 'searching_driver_page.dart';

class RebookTripPage extends StatefulWidget {
  const RebookTripPage({super.key, required this.oldBooking});

  final BookingResponse oldBooking;

  @override
  State<RebookTripPage> createState() => _RebookTripPageState();
}

class _RebookTripPageState extends State<RebookTripPage> {
  bool _isScheduled = false;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _loadData());
  }

  Future<void> _loadData() async {
    final token = context.read<AuthProvider>().token;
    if (token == null) return;

    final provider = context.read<BookingProvider>();
    
    // Fetch catalog to ensure we have the latest services
    await provider.loadCatalog(token);
    
    // Estimate fare based on old locations and vehicle
    if (widget.oldBooking.pickup != null && widget.oldBooking.destination != null && widget.oldBooking.vehicle != null) {
      // Find the service id, assuming PerTrip = 1 or matching the original mode. We'll find from catalog.
      final service = provider.catalog?.services.firstWhere(
        (s) => s.mode == BookingServiceMode.perTrip, 
        orElse: () => provider.catalog!.services.first
      );
      
      if (service != null) {
        await provider.estimateFare(
          token,
          vehicleId: widget.oldBooking.vehicle!.id,
          serviceTypeId: service.id,
          pickup: widget.oldBooking.pickup!,
          destination: widget.oldBooking.destination!,
        );
      }
    }

    // Load available promotions and auto-apply best one
    await provider.loadAvailablePromotions(token);
    if (provider.availablePromotions.isNotEmpty) {
      // Logic to auto select best promotion. For now just pick the first available.
      // Assuming sorting or picking best depends on estimate
      final bestPromo = provider.availablePromotions.first;
      provider.selectPromo(bestPromo);
    }
  }

  void _submitRebook() async {
    final token = context.read<AuthProvider>().token;
    if (token == null) return;
    
    final provider = context.read<BookingProvider>();
    final pickup = widget.oldBooking.pickup;
    final destination = widget.oldBooking.destination;
    final vehicle = widget.oldBooking.vehicle;
    final estimate = provider.fareEstimate;

    if (pickup == null || destination == null || vehicle == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Dữ liệu chuyến đi cũ không hợp lệ.')),
      );
      return;
    }

    if (!_isScheduled && estimate == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Đang tính toán giá, vui lòng đợi.')),
      );
      return;
    }

    final service = provider.catalog?.services.firstWhere(
      (s) => s.mode == BookingServiceMode.perTrip,
      orElse: () => provider.catalog!.services.first
    );

    if (service == null) return;

    AppLoadingScreen.show(context);
    final result = await provider.createBooking(
      token,
      CreateBookingRequest(
        vehicleId: vehicle.id,
        serviceTypeId: service.id,
        bookingType: _isScheduled ? BookingType.scheduled : BookingType.now,
        scheduledAt: _isScheduled ? DateTime.now().add(const Duration(minutes: 35)) : null,
        pickup: pickup,
        destination: destination,
      ),
    );
    if (!mounted) return;
    AppLoadingScreen.hide(context);

    if (result == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(provider.errorMessage ?? 'Có lỗi xảy ra')),
      );
    } else {
      if (result.bookingType == 'Now') {
        provider.setSearchingBooking(result);
        Navigator.pushReplacement(
          context,
          MaterialPageRoute(builder: (_) => const SearchingDriverPage()),
        );
      } else {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Đặt xe thành công. Tài xế sẽ đến đón bạn đúng giờ.')),
        );
        Navigator.pop(context);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<BookingProvider>();
    final pickup = widget.oldBooking.pickup;
    final destination = widget.oldBooking.destination;
    final vehicle = widget.oldBooking.vehicle;

    final markers = <AppMarker>{};
    if (pickup != null) {
      markers.add(AppMarker(
        id: 'pickup',
        type: AppMarkerType.pickup,
        position: AppLatLng(pickup.latitude, pickup.longitude),
      ));
    }
    if (destination != null) {
      markers.add(AppMarker(
        id: 'destination',
        type: AppMarkerType.destination,
        position: AppLatLng(destination.latitude, destination.longitude),
      ));
    }

    // Determine center of map
    AppCameraPosition cameraPos = const AppCameraPosition(
      target: AppLatLng(10.762622, 106.660172),
      zoom: 14,
    );
    if (pickup != null && destination != null) {
      final centerLat = (pickup.latitude + destination.latitude) / 2;
      final centerLng = (pickup.longitude + destination.longitude) / 2;
      cameraPos = AppCameraPosition(
        target: AppLatLng(centerLat, centerLng),
        zoom: 13,
      );
    } else if (pickup != null) {
      cameraPos = AppCameraPosition(
        target: AppLatLng(pickup.latitude, pickup.longitude),
        zoom: 15,
      );
    }

    double fare = provider.fareEstimate?.estimatedFare ?? 0;
    double discount = 0;
    if (provider.selectedPromo != null && fare > 0) {
      discount = provider.selectedPromo!.discountAmount;
      if (provider.selectedPromo!.discountType == PromoDiscountType.percentage) {
        discount = fare * provider.selectedPromo!.discountPercentage! / 100;
        if (provider.selectedPromo!.maxDiscountAmount != null && discount > provider.selectedPromo!.maxDiscountAmount!) {
          discount = provider.selectedPromo!.maxDiscountAmount!;
        }
      }
    }
    double finalFare = (fare - discount).clamp(0, double.infinity);

    return Scaffold(
      backgroundColor: const Color(0xFFF9FAFB),
      appBar: AppBar(
        backgroundColor: Colors.white,
        elevation: 0,
        centerTitle: true,
        title: const Text(
          'Đặt lại chuyến đi',
          style: TextStyle(color: AppColors.textPrimary, fontSize: 18, fontWeight: FontWeight.bold),
        ),
        leading: IconButton(
          icon: const Icon(Icons.arrow_back, color: AppColors.textPrimary),
          onPressed: () => Navigator.pop(context),
        ),
      ),
      body: SingleChildScrollView(
        child: Padding(
          padding: const EdgeInsets.all(16.0),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Text(
                'Xác nhận thông tin cũ',
                style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
              ),
              const SizedBox(height: 4),
              const Text(
                'Vui lòng kiểm tra lại lộ trình và phương tiện cho chuyến đi sắp tới của bạn.',
                style: TextStyle(color: Colors.grey, fontSize: 14),
              ),
              const SizedBox(height: 16),
              
              // Map & Route Card
              Container(
                decoration: BoxDecoration(
                  color: Colors.white,
                  borderRadius: BorderRadius.circular(16),
                  border: Border.all(color: Colors.grey.shade200),
                ),
                child: Column(
                  children: [
                    // Map snippet
                    ClipRRect(
                      borderRadius: const BorderRadius.vertical(top: Radius.circular(16)),
                      child: SizedBox(
                        height: 120,
                        child: AbsorbPointer(
                          child: MapRendererWidget(
                            initialCameraPosition: cameraPos,
                            markers: markers,
                            myLocationButtonEnabled: false,
                          ),
                        ),
                      ),
                    ),
                    Padding(
                      padding: const EdgeInsets.all(16.0),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Row(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Column(
                                children: [
                                  const Icon(Icons.circle, size: 12, color: AppColors.primary),
                                  Container(height: 20, width: 2, color: Colors.grey.shade300),
                                  const Icon(Icons.location_on, size: 14, color: Colors.red),
                                ],
                              ),
                              const SizedBox(width: 12),
                              Expanded(
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    const Text('ĐIỂM ĐÓN', style: TextStyle(color: Colors.grey, fontSize: 12)),
                                    Text(pickup?.address ?? 'N/A', style: const TextStyle(fontWeight: FontWeight.bold), maxLines: 2),
                                    const SizedBox(height: 8),
                                    const Text('ĐIỂM ĐẾN', style: TextStyle(color: Colors.grey, fontSize: 12)),
                                    Text(destination?.address ?? 'N/A', style: const TextStyle(fontWeight: FontWeight.bold), maxLines: 2),
                                  ],
                                ),
                              )
                            ],
                          ),
                          const Padding(
                            padding: EdgeInsets.symmetric(vertical: 12),
                            child: Divider(),
                          ),
                          Row(
                            children: [
                              Container(
                                padding: const EdgeInsets.all(8),
                                decoration: BoxDecoration(
                                  color: Colors.grey.shade100,
                                  borderRadius: BorderRadius.circular(8),
                                ),
                                child: const Icon(Icons.directions_car, color: AppColors.primary),
                              ),
                              const SizedBox(width: 12),
                              Expanded(
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Text(vehicle?.name ?? 'N/A', style: const TextStyle(fontWeight: FontWeight.bold)),
                                    Text('${vehicle?.capacity ?? 4} Chỗ • Điều hòa • Tiêu chuẩn', style: const TextStyle(color: Colors.grey, fontSize: 13)),
                                  ],
                                ),
                              )
                            ],
                          )
                        ],
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 24),

              // Time Selection
              const Text('Thời gian khởi hành', style: TextStyle(fontWeight: FontWeight.bold, fontSize: 16)),
              const SizedBox(height: 12),
              Row(
                children: [
                  Expanded(
                    child: GestureDetector(
                      onTap: () => setState(() => _isScheduled = false),
                      child: Container(
                        padding: const EdgeInsets.symmetric(vertical: 16),
                        decoration: BoxDecoration(
                          color: !_isScheduled ? AppColors.primary.withOpacity(0.05) : Colors.white,
                          border: Border.all(color: !_isScheduled ? AppColors.primary : Colors.grey.shade300),
                          borderRadius: BorderRadius.circular(12),
                        ),
                        child: Row(
                          mainAxisAlignment: MainAxisAlignment.center,
                          children: [
                            Icon(Icons.flash_on, color: !_isScheduled ? AppColors.primary : Colors.grey, size: 20),
                            const SizedBox(width: 8),
                            Text('Đi ngay', style: TextStyle(color: !_isScheduled ? AppColors.primary : Colors.grey, fontWeight: FontWeight.bold)),
                          ],
                        ),
                      ),
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: GestureDetector(
                      onTap: () => setState(() => _isScheduled = true),
                      child: Container(
                        padding: const EdgeInsets.symmetric(vertical: 16),
                        decoration: BoxDecoration(
                          color: _isScheduled ? AppColors.primary.withOpacity(0.05) : Colors.white,
                          border: Border.all(color: _isScheduled ? AppColors.primary : Colors.grey.shade300),
                          borderRadius: BorderRadius.circular(12),
                        ),
                        child: Row(
                          mainAxisAlignment: MainAxisAlignment.center,
                          children: [
                            Icon(Icons.calendar_today, color: _isScheduled ? AppColors.primary : Colors.grey, size: 18),
                            const SizedBox(width: 8),
                            Text('Đặt trước', style: TextStyle(color: _isScheduled ? AppColors.primary : Colors.grey, fontWeight: FontWeight.bold)),
                          ],
                        ),
                      ),
                    ),
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
      bottomNavigationBar: Container(
        padding: const EdgeInsets.all(20),
        decoration: BoxDecoration(
          color: Colors.white,
          boxShadow: [
            BoxShadow(color: Colors.black.withOpacity(0.05), blurRadius: 10, offset: const Offset(0, -5))
          ],
        ),
        child: SafeArea(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              if (provider.isEstimating)
                const LinearProgressIndicator()
              else ...[
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  crossAxisAlignment: CrossAxisAlignment.end,
                  children: [
                    Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        const Text('Tổng cộng', style: TextStyle(color: Colors.grey, fontSize: 14)),
                        const SizedBox(height: 4),
                        Text(
                          '${finalFare.toStringAsFixed(0)} đ',
                          style: const TextStyle(color: AppColors.primary, fontSize: 20, fontWeight: FontWeight.bold),
                        ),
                      ],
                    ),
                    Column(
                      crossAxisAlignment: CrossAxisAlignment.end,
                      children: [
                        if (discount > 0)
                          Text(
                            '↓ Đã giảm ${discount.toStringAsFixed(0)} k',
                            style: const TextStyle(color: Colors.red, fontSize: 13, fontWeight: FontWeight.bold),
                          ),
                        const SizedBox(height: 2),
                        const Text('Bao gồm thuế phí', style: TextStyle(color: Colors.grey, fontSize: 12)),
                      ],
                    )
                  ],
                ),
                const SizedBox(height: 16),
                SizedBox(
                  width: double.infinity,
                  height: 50,
                  child: ElevatedButton(
                    onPressed: provider.isLoading ? null : _submitRebook,
                    style: ElevatedButton.styleFrom(
                      backgroundColor: AppColors.primary,
                      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                    ),
                    child: provider.isLoading
                        ? const CircularProgressIndicator(color: Colors.white)
                        : const Row(
                            mainAxisAlignment: MainAxisAlignment.center,
                            children: [
                              Text('Xác nhận & Tìm tài xế', style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold, color: Colors.white)),
                              SizedBox(width: 8),
                              Icon(Icons.arrow_forward, size: 20, color: Colors.white),
                            ],
                          ),
                  ),
                ),
              ]
            ],
          ),
        ),
      ),
    );
  }
}
