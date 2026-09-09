import 'package:flutter_test/flutter_test.dart';
import 'package:follower_application/utils/iraq_datetime.dart';

void main() {
  test('iraq clock does not crash when locale data is uninitialized', () {
    final text = IraqDateTime.formatClock('2026-09-08T12:00:00Z');
    expect(text, isNot(equals('')));
    expect(text.contains('2026'), isTrue);
    expect(text.contains('صباحاً') || text.contains('مساءً'), isTrue);
  });

  test('iraq clock returns dash for null or invalid note timestamps', () {
    expect(IraqDateTime.formatClock(null), '—');
    expect(IraqDateTime.formatClock(''), '—');
    expect(IraqDateTime.formatClock('not-a-date'), '—');
  });

  test('customer note CreatedAtUtc formats without throwing (regression)', () {
    final note = {
      'noteText': 'ملاحظة اختبار',
      'createdByName': 'متابع',
      'createdAtUtc': '2026-09-08T21:30:00.000Z',
    };
    expect(() => IraqDateTime.formatClock(note['createdAtUtc']), returnsNormally);
    final formatted = IraqDateTime.formatClock(note['createdAtUtc']);
    expect(formatted, isNot(equals('—')));
  });

  test('number format never crashes page', () {
    expect(IraqDateTime.formatNumber(null), '—');
    expect(IraqDateTime.formatNumber(1500), '1,500');
  });

  test('ymd format never uses DateFormat', () {
    expect(IraqDateTime.formatYmd(DateTime(2026, 9, 8)), '2026-09-08');
  });
}
