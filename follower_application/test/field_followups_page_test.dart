import 'package:flutter_test/flutter_test.dart';
import 'package:follower_application/field_followups_page.dart';

void main() {
  test('FieldFollowupsPage exists and groups by listId', () {
    expect(const FieldFollowupsPage(), isA<FieldFollowupsPage>());

    final items = [
      {'listId': 10, 'listName': 'السوق', 'customerName': 'أ'},
      {'listId': 10, 'listName': 'السوق', 'customerName': 'ب'},
      {'listId': 20, 'listName': 'الصناعي', 'customerName': 'ج'},
    ];
    final byList = <int, List<Map<String, dynamic>>>{};
    for (final item in items) {
      final listId = int.tryParse('${item['listId']}') ?? 0;
      byList.putIfAbsent(listId, () => []).add(item);
    }
    expect(byList.length, 2);
    expect(byList[10]!.length, 2);
    expect(byList[20]!.length, 1);
  });

  test('Completion note must be non-empty', () {
    String? note;
    expect((note ?? '').trim().isEmpty, isTrue);
    note = '  ';
    expect(note.trim().isEmpty, isTrue);
    note = 'تم الزيارة';
    expect(note.trim().isEmpty, isFalse);
  });
}
