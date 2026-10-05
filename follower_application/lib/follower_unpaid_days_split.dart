/// Split Daily «الغير مسددين» by days since last payment (NumberOfDayPayment).
enum FollowerUnpaidDayBucket {
  days1to3,
  days4to7,
  days8to14,
  days15Plus,
}

class FollowerUnpaidDaysSplit {
  FollowerUnpaidDaysSplit._();

  static int daysSincePayment(Map<String, dynamic> row) {
    final n = row['numberOfDayPayment'] ?? row['NumberOfDayPayment'];
    if (n is num) return n.toInt();
    return int.tryParse('$n') ?? 0;
  }

  static FollowerUnpaidDayBucket? bucketFor(int days) {
    if (days <= 0) return FollowerUnpaidDayBucket.days1to3;
    if (days <= 3) return FollowerUnpaidDayBucket.days1to3;
    if (days <= 7) return FollowerUnpaidDayBucket.days4to7;
    if (days <= 14) return FollowerUnpaidDayBucket.days8to14;
    return FollowerUnpaidDayBucket.days15Plus;
  }

  static List<Map<String, dynamic>> inBucket(
    Iterable<Map<String, dynamic>> rows,
    FollowerUnpaidDayBucket bucket,
  ) {
    return rows
        .where((r) => bucketFor(daysSincePayment(r)) == bucket)
        .toList(growable: false);
  }

  static List<Map<String, dynamic>> days1to3(Iterable<Map<String, dynamic>> rows) =>
      inBucket(rows, FollowerUnpaidDayBucket.days1to3);

  static List<Map<String, dynamic>> days4to7(Iterable<Map<String, dynamic>> rows) =>
      inBucket(rows, FollowerUnpaidDayBucket.days4to7);

  static List<Map<String, dynamic>> days8to14(Iterable<Map<String, dynamic>> rows) =>
      inBucket(rows, FollowerUnpaidDayBucket.days8to14);

  static List<Map<String, dynamic>> days15Plus(Iterable<Map<String, dynamic>> rows) =>
      inBucket(rows, FollowerUnpaidDayBucket.days15Plus);
}
