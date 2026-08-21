import 'package:flutter_test/flutter_test.dart';
import 'package:safe_ride/features/shared/profile/data/models/vehicle_model.dart';

void main() {
  test('vehicle insurance preserves optional policy and verification status', () {
    final policy = VehicleInsurancePolicyModel.fromJson({
      'id': 7,
      'vehicleId': 3,
      'insuranceType': 'MANDATORY_TPL',
      'provider': 'Safe Insurer',
      'policyNumber': 'POL-7',
      'effectiveFromUtc': '2026-08-01T00:00:00Z',
      'expiresAtUtc': '2027-08-01T00:00:00Z',
      'coverageAmount': 20000000,
      'deductible': 500000,
      'documentUrl': null,
      'verificationStatus': 'PENDING',
    });

    expect(policy.vehicleId, 3);
    expect(policy.verificationStatus, 'PENDING');
    expect(policy.toRequestJson()['documentUrl'], isNull);
    expect(policy.toRequestJson()['coverageAmount'], 20000000);
  });
}
