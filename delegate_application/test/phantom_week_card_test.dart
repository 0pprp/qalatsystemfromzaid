import 'package:delegate_application/services/phantom_customer_rating.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('trendArrow compares current vs week-ago', () {
    expect(PhantomCustomerRating.trendArrow(5, 3), 'up');
    expect(PhantomCustomerRating.trendArrow(2, 5), 'down');
    expect(PhantomCustomerRating.trendArrow(4.01, 4.0), 'flat');
  });

  test('summarizeCohort and targetInstallment are display-only math', () {
    final today = DateTime(2026, 10, 6);
    final rows = [
      {
        'AmountRemaining': 100000,
        'LastPaymentDate': '2026-10-01',
        'AmountTotalSales': 1000000,
        'ReceiptsTotal': 200000,
        'AmountDaySales': 50000,
        'DateSaleDevice': '2026-06-01',
      },
      {
        'AmountRemaining': 0,
        'LastPaymentDate': '2026-10-01',
        'AmountTotalSales': 1000000,
        'ReceiptsTotal': 1000000,
        'AmountDaySales': 40000,
        'DateSaleDevice': '2026-06-01',
      },
    ];
    final s = PhantomCustomerRating.summarizeCohort(rows, today);
    expect(s.customerCount, 1);
    expect(s.installmentSum, 50000);
    final target = PhantomCustomerRating.targetInstallmentForClimb(s);
    expect(target >= s.installmentSum, isTrue);
  });
}
