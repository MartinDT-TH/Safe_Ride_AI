import 'dart:io';

import 'package:flutter_test/flutter_test.dart';

void main() {
  test('vehicle management navigation remains without insurance policy UI', () {
    final customerHome = File(
      'lib/features/customer/home/presentation/pages/customer_home_page.dart',
    ).readAsStringSync();
    final vehiclesPage = File(
      'lib/features/shared/profile/presentation/pages/my_vehicles_page.dart',
    ).readAsStringSync();
    final vehicleCard = File(
      'lib/features/shared/profile/presentation/widgets/vehicle_card.dart',
    ).readAsStringSync();

    expect(customerHome, contains('MyVehiclesPage()'));
    expect(vehiclesPage, contains('VehicleFormSheet.show'));
    expect(vehiclesPage, contains('provider.deleteVehicle'));
    expect(vehicleCard, contains('onEdit'));
    expect(vehicleCard, contains('onDelete'));

    final activeVehicleUi = '$customerHome\n$vehiclesPage\n$vehicleCard';
    expect(
      activeVehicleUi,
      isNot(matches(RegExp(
        r'VehicleInsurance|vehicle_insurance|insurance-policies|'
        r'PHYSICAL_DAMAGE|MANDATORY_TPL',
      ))),
    );
  });

  test('removed vehicle insurance page and model are absent', () {
    expect(
      File(
        'lib/features/shared/profile/presentation/pages/'
        'vehicle_insurance_page.dart',
      ).existsSync(),
      isFalse,
    );

    final vehicleModel = File(
      'lib/features/shared/profile/data/models/vehicle_model.dart',
    ).readAsStringSync();
    expect(
      vehicleModel,
      isNot(matches(RegExp(
        r'VehicleInsurance|insurancePolicies|insurancePolicyDocuments',
      ))),
    );
  });
}
