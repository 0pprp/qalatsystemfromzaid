import 'package:delegate_application/services/receipt_week_amounts.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('ReceiptWeekAmounts', () {
    test('always merges today payment into index 0 (Amount1 contract)', () {
      final merged = ReceiptWeekAmounts.mergeLocalPayment(
        serverAmounts: [0, 10, 0, 0, 0, 0, 0],
        dayLabels: ['2026-10-04', '2026-10-03', 'ق1', 'ق2', 'ق3', 'ق4', 'ق5'],
        paymentAmount: 2500,
      );
      expect(merged[0], 2500);
      expect(merged[1], 10);
    });

    test('guaranteeTodayShowsPayment fills zero today from amountPush', () {
      final guaranteed = ReceiptWeekAmounts.guaranteeTodayShowsPayment(
        weekAmounts: [0, 10, 0, 0, 0, 0, 0],
        amountPush: 2500,
      );
      expect(guaranteed[0], 2500);
      expect(guaranteed[1], 10);
    });

    test('does not double when alreadyIncluded', () {
      final merged = ReceiptWeekAmounts.mergeLocalPayment(
        serverAmounts: [2500, 0, 0, 0, 0, 0, 0],
        dayLabels: ['اليوم', 'أ', 'ب', 'ج', 'د', 'ه', 'و'],
        paymentAmount: 2500,
        alreadyIncluded: true,
      );
      expect(merged[0], 2500);
    });

    test('ensureDayLabels falls back to Arabic اليوم', () {
      final labels = ReceiptWeekAmounts.ensureDayLabels(['', null]);
      expect(labels[0], 'اليوم');
      expect(labels.length, 7);
    });

    test('formatExactIqd never rounds 2500 to 3000', () {
      expect(ReceiptWeekAmounts.formatExactIqd(2500), '2,500');
      expect(ReceiptWeekAmounts.formatExactIqd(2370), '2,370');
    });

    test('printedReceiptNumber is stable and safe for UUID', () {
      expect(ReceiptWeekAmounts.printedReceiptNumber('5'), '6');
      expect(
        ReceiptWeekAmounts.printedReceiptNumber('not-a-number-uuid'),
        'not-a-number-uuid',
      );
      expect(ReceiptWeekAmounts.printedReceiptNumber(''), '1');
    });
  });
}
