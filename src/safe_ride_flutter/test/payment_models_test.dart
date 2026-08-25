import 'package:flutter_test/flutter_test.dart';
import 'package:safe_ride/features/driver/dashboard/data/models/payment_models.dart';

void main() {
  test('underpayment is not successful while a payable balance remains', () {
    final result = PaymentStatusResult.fromJson({
      'tripId': 7,
      'paymentStatus': 'Success',
      'amount': 60000,
      'originalFare': 120000,
      'finalFare': 120000,
      'driverShare': 0,
      'platformShare': 0,
      'currency': 'VND',
      'tripStatus': 'WAITING_PAYMENT',
      'message': 'Payment pending',
      'successfulPaymentAmount': 60000,
      'remainingPayableAmount': 60000,
      'refundObligationAmount': 0,
    });

    expect(result.isSuccess, isFalse);
    expect(result.requiresPayment, isTrue);
  });

  test('overpayment keeps the refund obligation after payable is covered', () {
    final result = PaymentStatusResult.fromJson({
      'tripId': 8,
      'paymentStatus': 'Success',
      'amount': 120000,
      'originalFare': 60000,
      'finalFare': 60000,
      'driverShare': 0,
      'platformShare': 0,
      'currency': 'VND',
      'tripStatus': 'WAITING_RETURN_CONFIRM',
      'message': 'Refund pending',
      'successfulPaymentAmount': 120000,
      'remainingPayableAmount': 0,
      'refundObligationAmount': 60000,
      'reconciliationStatus': 'REFUND_PENDING',
    });

    expect(result.isSuccess, isTrue);
    expect(result.refundObligationAmount, 60000);
  });
}
