import 'package:flutter/material.dart';
import '../../../../../core/localization/localization_extensions.dart';
import '../../data/models/vehicle_model.dart';

class VehicleCard extends StatelessWidget {
  final VehicleModel vehicle;
  final VoidCallback onEdit;
  final VoidCallback onDelete;

  VehicleCard({
    super.key,
    required this.vehicle,
    required this.onEdit,
    required this.onDelete,
  });

  @override
  Widget build(BuildContext context) {
    const tealColor = Color(0xFF006B70);
    const borderColor = Color(0xFFE5E7EB);

    return Container(
      margin: const EdgeInsets.only(bottom: 16),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: borderColor),
      ),
      child: ClipRRect(
        borderRadius: BorderRadius.circular(16),
        child: Stack(
          children: [
            Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                children: [
                  Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Container(
                        width: 52,
                        height: 52,
                        decoration: BoxDecoration(
                          color: Color(0xFFF3F4F6),
                          borderRadius: BorderRadius.circular(12),
                        ),
                        child: Icon(
                          vehicle.type == VehicleType.motorbike
                              ? Icons.directions_bike_rounded
                              : Icons.directions_car_rounded,
                          color: tealColor,
                          size: 28,
                        ),
                      ),
                      SizedBox(width: 16),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              vehicle.name,
                              style: TextStyle(
                                fontSize: 16,
                                fontWeight: FontWeight.bold,
                                color: Color(0xFF1F2937),
                              ),
                            ),
                            SizedBox(height: 4),
                            Text(
                              _summaryText(context, vehicle),
                              style: TextStyle(
                                fontSize: 14,
                                color: Color(0xFF6B7280),
                              ),
                            ),
                          ],
                        ),
                      ),
                    ],
                  ),
                  SizedBox(height: 12),
                  Divider(height: 1, color: Color(0xFFF3F4F6)),
                  SizedBox(height: 12),
                  Row(
                    children: [
                      Spacer(),
                      InkWell(
                        onTap: onEdit,
                        child: Text(
                          context.l10n.edit,
                          style: TextStyle(
                            color: Color(0xFF4B5563),
                            fontWeight: FontWeight.w600,
                            fontSize: 14,
                          ),
                        ),
                      ),
                      SizedBox(width: 24),
                      InkWell(
                        onTap: onDelete,
                        child: Text(
                          context.l10n.delete,
                          style: TextStyle(
                            color: Color(0xFFEF4444),
                            fontWeight: FontWeight.w600,
                            fontSize: 14,
                          ),
                        ),
                      ),
                    ],
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  static String _summaryText(BuildContext context, VehicleModel vehicle) {
    final details = <String>[
      vehicle.plateNumber,
      if (vehicle.color.trim().isNotEmpty) vehicle.color,
      if (vehicle.type == VehicleType.motorbike &&
          vehicle.engineCapacityCc != null)
        '${vehicle.engineCapacityCc}cc',
      if (vehicle.requiredLicenseClass.trim().isNotEmpty)
        context.l10n.requiredLicense(vehicle.requiredLicenseClass!),
    ];

    return details.join(' • ');
  }
}
