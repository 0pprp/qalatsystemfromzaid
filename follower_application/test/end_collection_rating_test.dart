import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:follower_application/collection_delegate/collection_delegate_shell.dart';
import 'package:follower_application/services/follower_day_rating_service.dart';
import 'package:follower_application/ui/day_rating_gate.dart';

void main() {
  testWidgets('CollectionDelegateShell shows إنهاء الجباية action',
      (tester) async {
    await tester.pumpWidget(
      const MaterialApp(
        home: CollectionDelegateShell(listId: 7, listName: 'قائمة تجريبية'),
      ),
    );
    await tester.pump();

    expect(find.textContaining('إنهاء الجباية'), findsWidgets);
  });

  test('promptDayRatingForList helper builds missing row from ids', () {
    final item = FollowerDayRatingMissing(listId: 12, listName: 'مندوب أ');
    expect(item.listId, 12);
    expect(item.listName, 'مندوب أ');
    expect(endCollectionRatingHint, contains('إنهاء الجباية'));
  });
}
