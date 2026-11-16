import 'package:customer_app/core/network/json.dart';
import 'package:customer_app/core/utils/formatters.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('money uses thousands separators and د.ع', () {
    expect(Fmt.money(650000), '650,000 د.ع');
    expect(Fmt.money(0), '0 د.ع');
    expect(Fmt.money(1500000), '1,500,000 د.ع');
    expect(Fmt.money(-3000), '- 3,000 د.ع');
    expect(Fmt.number(6500), '6,500');
  });

  test('Iraqi month names', () {
    expect(Fmt.month('2026-11'), 'تشرين الثاني 2026');
    expect(Fmt.month('2026-01'), 'كانون الثاني 2026');
    expect(Fmt.month('bad'), 'bad');
    expect(Fmt.date(DateTime(2026, 3, 1)), '1 آذار 2026');
    expect(Fmt.date(null), '—');
  });

  test('relative time', () {
    final now = DateTime(2026, 11, 3, 12);
    expect(Fmt.relative(now.subtract(const Duration(seconds: 20)), now: now), 'الآن');
    expect(Fmt.relative(now.subtract(const Duration(minutes: 5)), now: now), 'قبل 5 دقائق');
    expect(Fmt.relative(now.subtract(const Duration(hours: 2)), now: now), 'قبل ساعتين');
    expect(Fmt.relative(now.subtract(const Duration(days: 1)), now: now), 'أمس');
  });

  test('times are shown in Iraq time whatever the phone time zone is', () {
    // 21:30 UTC is 00:30 the next day in Baghdad.
    final utc = DateTime.utc(2026, 11, 6, 21, 30);
    expect(Fmt.date(utc), '7 تشرين الثاني 2026');
    expect(Fmt.time(utc), '12:30 ص');
    expect(Fmt.dateTime(DateTime.utc(2026, 11, 6, 11, 5)), '6 تشرين الثاني 2026 - 2:05 م');
  });

  test('server times without a zone are read as UTC', () {
    expect(asDate('2026-11-06T11:05:00'), DateTime.utc(2026, 11, 6, 11, 5));
    expect(asDate('2026-11-06T11:05:00Z'), DateTime.utc(2026, 11, 6, 11, 5));
    expect(asDate('2026-11-06T14:05:00+03:00'), DateTime.utc(2026, 11, 6, 11, 5));
    expect(asDate(''), isNull);
  });
}
