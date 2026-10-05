import 'package:flutter_test/flutter_test.dart';
import 'package:follower_application/follower_unpaid_days_split.dart';

void main() {
  group('FollowerUnpaidDaysSplit', () {
    test('buckets by days since payment', () {
      expect(FollowerUnpaidDaysSplit.bucketFor(1), FollowerUnpaidDayBucket.days1to3);
      expect(FollowerUnpaidDaysSplit.bucketFor(3), FollowerUnpaidDayBucket.days1to3);
      expect(FollowerUnpaidDaysSplit.bucketFor(4), FollowerUnpaidDayBucket.days4to7);
      expect(FollowerUnpaidDaysSplit.bucketFor(7), FollowerUnpaidDayBucket.days4to7);
      expect(FollowerUnpaidDaysSplit.bucketFor(8), FollowerUnpaidDayBucket.days8to14);
      expect(FollowerUnpaidDaysSplit.bucketFor(14), FollowerUnpaidDayBucket.days8to14);
      expect(FollowerUnpaidDaysSplit.bucketFor(15), FollowerUnpaidDayBucket.days15Plus);
      expect(FollowerUnpaidDaysSplit.bucketFor(40), FollowerUnpaidDayBucket.days15Plus);
    });

    test('splits rows without losing any', () {
      final rows = [
        {'customerName': 'A', 'numberOfDayPayment': 2},
        {'customerName': 'B', 'numberOfDayPayment': 5},
        {'customerName': 'C', 'numberOfDayPayment': 10},
        {'customerName': 'D', 'numberOfDayPayment': 20},
        {'customerName': 'E', 'numberOfDayPayment': 1},
      ];
      final a = FollowerUnpaidDaysSplit.days1to3(rows);
      final b = FollowerUnpaidDaysSplit.days4to7(rows);
      final c = FollowerUnpaidDaysSplit.days8to14(rows);
      final d = FollowerUnpaidDaysSplit.days15Plus(rows);
      expect(a.map((e) => e['customerName']), ['A', 'E']);
      expect(b.map((e) => e['customerName']), ['B']);
      expect(c.map((e) => e['customerName']), ['C']);
      expect(d.map((e) => e['customerName']), ['D']);
      expect(a.length + b.length + c.length + d.length, rows.length);
    });
  });
}
