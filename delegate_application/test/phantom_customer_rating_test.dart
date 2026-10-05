import 'package:delegate_application/client.dart';
import 'package:delegate_application/services/phantom_customer_rating.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('phantom sale date floor and +30 boost', () {
    expect(
      PhantomCustomerRating.adjustSaleDate(DateTime(2025, 1, 1)),
      DateTime(2026, 5, 1),
    );
    expect(
      PhantomCustomerRating.adjustSaleDate(DateTime(2026, 6, 1)),
      DateTime(2026, 7, 1),
    );
    expect(
      PhantomCustomerRating.adjustSaleDate(DateTime(2026, 9, 15)),
      DateTime(2026, 9, 15),
    );
  });

  test('scores are only 10 5 0 -5 -10', () {
    final r = PhantomCustomerRating.evaluate(
      amountTotalSales: 1000000,
      receiptsTotal: 800000,
      amountRemaining: 200000,
      dateSaleDevice: DateTime(2026, 5, 1),
      asOf: DateTime(2026, 10, 4),
    );
    expect([10, 5, 0, -5, -10].contains(r.score), isTrue);
  });

  test('formatScore uses التقييم : score', () {
    expect(PhantomCustomerRating.formatScore(-10), 'التقييم : -10');
    expect(PhantomCustomerRating.formatScore(10), 'التقييم : 10');
  });

  test('scoreFromAverage bands', () {
    expect(PhantomCustomerRating.scoreFromAverage(8), 10);
    expect(PhantomCustomerRating.scoreFromAverage(4), 5);
    expect(PhantomCustomerRating.scoreFromAverage(1), 0);
    expect(PhantomCustomerRating.scoreFromAverage(-3), -5);
    expect(PhantomCustomerRating.scoreFromAverage(-9), -10);
  });

  test('scoreForClient null without sale date', () {
    final c = Client(
      name: 'x',
      phone: '1',
      address: '',
      shopName: '',
      salePrice: 1000,
      receipt: 100,
      dailyInstallment: 10,
      remaining: 900,
      itemsNames: '',
      amount1: 0,
      amount2: 0,
      amount3: 0,
      amount4: 0,
      amount5: 0,
      amount6: 0,
      amount7: 0,
      phoneNumberCompany: '',
      countReceiptDevice: '',
      numberOfDayPayment: '',
      isLegal: 'false',
      lastPaymentDate: '2026-10-01',
      dateSaleDevice: '',
      customerId: 1,
    );
    expect(PhantomCustomerRating.scoreForClient(c, DateTime(2026, 10, 4)), isNull);
  });

  test('no payment in 30 days is excluded from تقييمك (overall stays 0)', () {
    final asOf = DateTime(2026, 10, 4);
    final rows = [
      {
        'AmountTotalSales': 1000000.0,
        'ReceiptsTotal': 0.0,
        'AmountRemaining': 1000000.0,
        'DateSaleDevice': '2026-05-01',
        'LastPaymentDate': null,
      },
      {
        'AmountTotalSales': 1000000.0,
        'ReceiptsTotal': 0.0,
        'AmountRemaining': 1000000.0,
        'DateSaleDevice': '2026-05-01',
        'LastPaymentDate': '2026-08-01', // older than 30 days
      },
    ];
    // Card may still show -10, but overall equation ignores them.
    expect(PhantomCustomerRating.scoreForRow(rows.first, asOf), -10);
    expect(PhantomCustomerRating.isInOverallRatingCohort(rows[0], asOf), isFalse);
    expect(PhantomCustomerRating.isInOverallRatingCohort(rows[1], asOf), isFalse);
    expect(PhantomCustomerRating.averageCustomerScore(rows, asOf), 0);
  });

  test('averageCustomerScore: paid within 30d enters sum/count', () {
    final asOf = DateTime(2026, 10, 4);
    final rows = [
      // -10, recent payment → in equation
      {
        'AmountTotalSales': 1000000.0,
        'ReceiptsTotal': 0.0,
        'AmountRemaining': 1000000.0,
        'DateSaleDevice': '2026-05-01',
        'LastPaymentDate': '2026-10-01',
      },
      // settled / zero remaining → excluded from overall cohort
      {
        'AmountTotalSales': 1000000.0,
        'ReceiptsTotal': 1000000.0,
        'AmountRemaining': 0.0,
        'DateSaleDevice': '2026-05-01',
        'LastPaymentDate': '2026-10-01',
      },
    ];
    expect(PhantomCustomerRating.scoreForRow(rows[0], asOf), -10);
    expect(PhantomCustomerRating.isInOverallRatingCohort(rows[0], asOf), isTrue);
    expect(PhantomCustomerRating.isInOverallRatingCohort(rows[1], asOf), isFalse);
    expect(PhantomCustomerRating.averageCustomerScore(rows, asOf), -10);
  });

  test('averageCustomerScore: empty cohort is 0', () {
    expect(PhantomCustomerRating.averageCustomerScore([], DateTime(2026, 10, 4)), 0);
    expect(
      PhantomCustomerRating.averageCustomerScore([
        {
          'AmountTotalSales': 0.0,
          'ReceiptsTotal': 0.0,
          'AmountRemaining': 0.0,
          'DateSaleDevice': '',
          'LastPaymentDate': null,
        },
      ], DateTime(2026, 10, 4)),
      0,
    );
  });

  test('formatAverageScore keeps decimals when needed', () {
    expect(PhantomCustomerRating.formatAverageScore(7.5), '7.50');
    expect(PhantomCustomerRating.formatAverageScore(-10), '-10');
    expect(PhantomCustomerRating.formatAverageScore(0), '0');
  });
}
