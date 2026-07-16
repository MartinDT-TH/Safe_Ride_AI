import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:intl/intl.dart';
import '../../../../../core/constants/app_colors.dart';
import '../../../../../core/constants/app_strings.dart';
import 'package:safe_ride/features/auth/presentation/providers/auth_provider.dart';
import 'package:safe_ride/features/customer/booking/presentation/providers/booking_provider.dart';
import 'package:safe_ride/features/shared/history/data/models/history_trip.dart';

class ReportTripPage extends StatefulWidget {
  const ReportTripPage({super.key, required this.trip});

  final HistoryTrip trip;

  @override
  State<ReportTripPage> createState() => _ReportTripPageState();
}

class _ReportTripPageState extends State<ReportTripPage> {
  final _formKey = GlobalKey<FormState>();
  final _descriptionController = TextEditingController();
  bool _isSubmitting = false;
  String? _selectedIssue;

  final List<String> _commonIssues = [
    'Tài xế đi sai tuyến',
    'Tài xế đến muộn',
    'Thái độ không phù hợp',
    'Khác',
  ];

  @override
  void dispose() {
    _descriptionController.dispose();
    super.dispose();
  }

  Future<void> _submitReport() async {
    if (!_formKey.currentState!.validate()) return;

    setState(() => _isSubmitting = true);

    final token = context.read<AuthProvider>().token;
    if (token == null || token.isEmpty) {
      _showMessage(BookingStrings.sessionExpired);
      setState(() => _isSubmitting = false);
      return;
    }

    final bookingProvider = context.read<BookingProvider>();
    final ok = await bookingProvider.submitTripReport(
      token,
      bookingId: widget.trip.id,
      subject: _selectedIssue ?? 'Báo cáo chuyến đi',
      description: _descriptionController.text.trim(),
    );

    if (!mounted) return;
    setState(() => _isSubmitting = false);

    if (ok) {
      _showMessage('Gửi báo cáo chuyến đi thành công.');
      Navigator.pop(context, true);
    } else {
      _showMessage(
        bookingProvider.errorMessage ??
            'Không thể gửi báo cáo. Vui lòng thử lại.',
      );
    }
  }

  void _showMessage(String message) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(message), behavior: SnackBarBehavior.floating),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFF9FAFB),
      appBar: AppBar(
        backgroundColor: Colors.white,
        elevation: 0,
        leading: IconButton(
          icon: const Icon(Icons.arrow_back, color: Color(0xFF1D2939)),
          onPressed: () => Navigator.pop(context),
        ),
        title: const Text(
          'Báo cáo chuyến đi',
          style: TextStyle(
            color: Color(0xFF1D2939),
            fontSize: 18,
            fontWeight: FontWeight.w700,
          ),
        ),
        centerTitle: true,
      ),
      body: Column(
        children: [
          Expanded(
            child: SingleChildScrollView(
              padding: const EdgeInsets.all(20),
              child: Form(
                key: _formKey,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    _buildTripSummaryCard(),
                    const SizedBox(height: 28),
                    const Text(
                      'Vấn đề phổ biến',
                      style: TextStyle(
                        fontSize: 16,
                        fontWeight: FontWeight.w700,
                        color: Color(0xFF1D2939),
                      ),
                    ),
                    const SizedBox(height: 12),
                    Wrap(
                      spacing: 8,
                      runSpacing: 8,
                      children: _commonIssues.map((issue) {
                        final isSelected = _selectedIssue == issue;
                        return ChoiceChip(
                          label: Text(issue),
                          selected: isSelected,
                          onSelected: (selected) {
                            setState(() {
                              _selectedIssue = selected ? issue : null;
                            });
                          },
                          selectedColor: AppColors.primary.withOpacity(0.1),
                          labelStyle: TextStyle(
                            color: isSelected
                                ? AppColors.primary
                                : const Color(0xFF475467),
                            fontWeight:
                                isSelected ? FontWeight.w700 : FontWeight.w500,
                            fontSize: 14,
                          ),
                          backgroundColor: Colors.white,
                          shape: RoundedRectangleBorder(
                            borderRadius: BorderRadius.circular(100),
                            side: BorderSide(
                              color: isSelected
                                  ? AppColors.primary
                                  : const Color(0xFFD0D5DD),
                              width: 1,
                            ),
                          ),
                          showCheckmark: false,
                        );
                      }).toList(),
                    ),
                    const SizedBox(height: 28),
                    const Text(
                      'Vấn đề gặp phải',
                      style: TextStyle(
                        fontSize: 16,
                        fontWeight: FontWeight.w700,
                        color: Color(0xFF1D2939),
                      ),
                    ),
                    const SizedBox(height: 12),
                    TextFormField(
                      controller: _descriptionController,
                      maxLines: 6,
                      decoration: InputDecoration(
                        hintText: 'Mô tả chi tiết vấn đề bạn gặp phải...',
                        hintStyle: const TextStyle(
                          fontSize: 14,
                          color: Color(0xFF98A2B3),
                        ),
                        filled: true,
                        fillColor: Colors.white,
                        border: OutlineInputBorder(
                          borderRadius: BorderRadius.circular(12),
                          borderSide:
                              const BorderSide(color: Color(0xFFD0D5DD)),
                        ),
                        enabledBorder: OutlineInputBorder(
                          borderRadius: BorderRadius.circular(12),
                          borderSide:
                              const BorderSide(color: Color(0xFFD0D5DD)),
                        ),
                        focusedBorder: OutlineInputBorder(
                          borderRadius: BorderRadius.circular(12),
                          borderSide: const BorderSide(
                            color: AppColors.primary,
                            width: 2,
                          ),
                        ),
                      ),
                      validator: (value) {
                        if (value == null || value.trim().isEmpty) {
                          return 'Vui lòng nhập nội dung báo cáo.';
                        }
                        return null;
                      },
                    ),
                  ],
                ),
              ),
            ),
          ),
          _buildSubmitButton(),
        ],
      ),
    );
  }

  Widget _buildTripSummaryCard() {
    final dateStr = DateFormat('HH:mm, dd/MM/yyyy').format(widget.trip.time);

    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: const Color(0xFFEAECF0)),
        boxShadow: [
          BoxShadow(
            color: Colors.black.withOpacity(0.02),
            blurRadius: 10,
            offset: const Offset(0, 4),
          ),
        ],
      ),
      child: Column(
        children: [
          Row(
            children: [
              Container(
                padding: const EdgeInsets.all(8),
                decoration: BoxDecoration(
                  color: const Color(0xFFF2F4F7),
                  shape: BoxShape.circle,
                ),
                child: Icon(
                  widget.trip.isMotorbike
                      ? Icons.two_wheeler
                      : Icons.directions_car,
                  size: 20,
                  color: const Color(0xFF475467),
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      widget.trip.driverName ?? 'Tài xế SafeRide',
                      style: const TextStyle(
                        fontWeight: FontWeight.w700,
                        fontSize: 15,
                        color: Color(0xFF1D2939),
                      ),
                    ),
                    Text(
                      dateStr,
                      style: const TextStyle(
                        fontSize: 13,
                        color: Color(0xFF667085),
                      ),
                    ),
                  ],
                ),
              ),
              Text(
                '#${widget.trip.id}',
                style: const TextStyle(
                  fontWeight: FontWeight.w600,
                  fontSize: 13,
                  color: Color(0xFF98A2B3),
                ),
              ),
            ],
          ),
          const Padding(
            padding: EdgeInsets.symmetric(vertical: 12),
            child: Divider(height: 1, color: Color(0xFFF2F4F7)),
          ),
          _buildLocationRow(
            icon: Icons.circle,
            iconColor: AppColors.primary,
            address: widget.trip.pickup,
          ),
          const SizedBox(height: 12),
          _buildLocationRow(
            icon: Icons.location_on,
            iconColor: Colors.redAccent,
            address: widget.trip.destination,
          ),
        ],
      ),
    );
  }

  Widget _buildLocationRow({
    required IconData icon,
    required Color iconColor,
    required String address,
  }) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Padding(
          padding: const EdgeInsets.only(top: 2),
          child: Icon(icon, size: 14, color: iconColor),
        ),
        const SizedBox(width: 10),
        Expanded(
          child: Text(
            address,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: const TextStyle(
              fontSize: 14,
              color: Color(0xFF475467),
              fontWeight: FontWeight.w500,
            ),
          ),
        ),
      ],
    );
  }

  Widget _buildSubmitButton() {
    return Container(
      padding: const EdgeInsets.fromLTRB(20, 16, 20, 32),
      decoration: BoxDecoration(
        color: Colors.white,
        boxShadow: [
          BoxShadow(
            color: Colors.black.withOpacity(0.05),
            blurRadius: 10,
            offset: const Offset(0, -4),
          ),
        ],
      ),
      child: SizedBox(
        width: double.infinity,
        height: 56,
        child: ElevatedButton(
          onPressed: _isSubmitting ? null : _submitReport,
          style: ElevatedButton.styleFrom(
            backgroundColor: AppColors.primary,
            foregroundColor: Colors.white,
            disabledBackgroundColor: const Color(0xFFEAECF0),
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(16),
            ),
            elevation: 0,
          ),
          child: _isSubmitting
              ? const SizedBox(
                  width: 24,
                  height: 24,
                  child: CircularProgressIndicator(
                    color: Colors.white,
                    strokeWidth: 3,
                  ),
                )
              : const Text(
                  'Gửi báo cáo',
                  style: TextStyle(fontSize: 16, fontWeight: FontWeight.w800),
                ),
        ),
      ),
    );
  }
}
