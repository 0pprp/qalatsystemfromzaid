import 'package:flutter_test/flutter_test.dart';
import 'package:sales_employee_application/data/archive_models.dart';

void main() {
  test('toggle hides sent and approved bundles', () {
    expect(archiveShownWhenHidingSentAndApproved('Open'), isTrue);
    expect(archiveShownWhenHidingSentAndApproved('Ready'), isTrue);
    expect(archiveShownWhenHidingSentAndApproved('NeedsFix'), isTrue);
    expect(archiveShownWhenHidingSentAndApproved('InTransit'), isFalse);
    expect(archiveShownWhenHidingSentAndApproved('Approved'), isFalse);
  });

  test('dates and labels stay readable', () {
    expect(formatArchiveDate('2026-02-10'), '2026/02/10');
    expect(archivePhaseLabel('NeedsFix'), 'غير مكتملة');
    final bundle = ArchiveBundle.fromJson({
      'bundleId': 4,
      'bundleNumber': 1,
      'periodStart': '2026-02-01',
      'periodEnd': '2026-02-10',
      'phase': 'Ready',
      'canSend': true,
      'itemCount': 10,
      'items': [
        {
          'archiveId': 9,
          'customerName': 'أحمد',
          'saleDate': '2026-02-03',
          'salePrice': 150000,
          'itemName': 'ثلاجة',
          'listName': 'قائمة الكرادة',
          'status': 'Refused',
          'refusalNote': 'العقد ناقص',
        }
      ],
    });
    expect(bundle.bundleNumber, 1);
    expect(bundle.periodLabel, '2026/02/01 — 2026/02/10');
    expect(bundle.items.single.hasProblem, isTrue);
    expect(bundle.items.single.listName, 'قائمة الكرادة');
  });
}
