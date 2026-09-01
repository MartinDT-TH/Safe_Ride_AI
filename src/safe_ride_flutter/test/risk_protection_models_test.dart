import 'package:flutter_test/flutter_test.dart';
import 'package:safe_ride/features/shared/risk_protection/data/models/risk_protection_models.dart';

void main() {
  test('accident detail parses claim and confirmed liability assessment', () {
    final accident = RiskProtectionAccident.fromJson({
      'id': 41,
      'tripId': 17,
      'category': 'MULTIPLE',
      'status': 'SETTLEMENT',
      'occurredAtUtc': '2026-08-20T03:00:00Z',
      'description': 'Va chạm trong chuyến đi',
      'evidence': [
        {
          'id': 8,
          'evidenceType': 'PHOTO',
          'fileUrl': 'https://example.test/evidence.jpg',
          'originalFileName': 'evidence.jpg',
          'createdAtUtc': '2026-08-20T03:05:00Z',
        },
      ],
      'liabilityAssessment': {
        'status': 'CONFIRMED',
        'driverFaultPercentage': 40,
        'customerFaultPercentage': 10,
        'thirdPartyFaultPercentage': 0,
        'vehicleFailurePercentage': 20,
        'objectiveCausePercentage': 30,
        'driverFaultLevel': 'ORDINARY_NEGLIGENCE',
      },
      'claim': {
        'id': 9,
        'status': 'PENDING_FUNDING',
        'insuranceStatus': 'APPROVED',
        'eligibleDamageAmount': 10000000,
        'insuranceApprovedAmount': 2000000,
        'riskFundAdvanceAmount': 3000000,
        'driverLiabilityAmount': 800000,
        'outstandingRecoveryAmount': 800000,
      },
    });

    expect(accident.id, 41);
    expect(accident.evidence.single.originalFileName, 'evidence.jpg');
    expect(accident.assessment?.driverFaultPercentage, 40);
    expect(accident.claim?.status, 'PENDING_FUNDING');
    expect(accident.claim?.insuranceApprovedAmount, 2000000);
    expect(accident.claim?.outstandingRecoveryAmount, 800000);
  });

  test(
    'driver liability exposes accident navigation and masked recoveries',
    () {
      final liability = DriverLiabilityItem.fromJson({
        'id': 5,
        'protectionClaimId': 9,
        'accidentReportId': 41,
        'claimStatus': 'RECOVERY_IN_PROGRESS',
        'driverAttributableEligibleDamage': 4000000,
        'faultLevel': 'ORDINARY_NEGLIGENCE',
        'confirmedAmount': 800000,
        'paidAmount': 200000,
        'outstandingAmount': 600000,
        'status': 'PARTIALLY_PAID',
        'recoveries': [
          {
            'id': 11,
            'amount': 200000,
            'maskedPaymentReference': '******1234',
            'recordedAtUtc': '2026-08-20T04:00:00Z',
          },
        ],
      });

      expect(liability.accidentId, 41);
      expect(liability.outstandingAmount, 600000);
      expect(liability.recoveries.single.maskedReference, '******1234');
    },
  );
}
