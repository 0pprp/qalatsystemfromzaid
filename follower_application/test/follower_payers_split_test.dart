import 'package:flutter_test/flutter_test.dart';
import 'package:follower_application/follower_payers_split.dart';

void main() {
  group('FollowerPayersSplit', () {
    test('under due when paid < installment', () {
      expect(FollowerPayersSplit.isUnderDue(15000, 20000), isTrue);
      expect(FollowerPayersSplit.isCorrect(15000, 20000), isFalse);
    });

    test('correct when paid == due', () {
      expect(FollowerPayersSplit.isUnderDue(20000, 20000), isFalse);
      expect(FollowerPayersSplit.isCorrect(20000, 20000), isTrue);
    });

    test('correct when paid > due', () {
      expect(FollowerPayersSplit.isUnderDue(25000, 20000), isFalse);
      expect(FollowerPayersSplit.isCorrect(25000, 20000), isTrue);
    });

    test('splits lists without losing rows', () {
      final rows = [
        {'amountReceipt': 10000.0, 'amountDaySales': 20000.0, 'customerName': 'A'},
        {'amountReceipt': 20000.0, 'amountDaySales': 20000.0, 'customerName': 'B'},
        {'amountReceipt': 30000.0, 'amountDaySales': 20000.0, 'customerName': 'C'},
        {'amountReceipt': 0.0, 'amountDaySales': 5000.0, 'customerName': 'D'},
      ];
      final under = FollowerPayersSplit.underDue(rows);
      final ok = FollowerPayersSplit.correct(rows);
      expect(under.map((e) => e['customerName']), ['A', 'D']);
      expect(ok.map((e) => e['customerName']), ['B', 'C']);
      expect(under.length + ok.length, rows.length);
    });
  });
}
