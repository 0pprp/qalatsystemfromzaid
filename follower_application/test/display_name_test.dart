import 'package:flutter_test/flutter_test.dart';
import 'package:follower_application/utils/display_name.dart';
import 'package:flutter/material.dart';

void main() {
  testWidgets('PersonNameText shows raw name without commas', (tester) async {
    await tester.pumpWidget(
      const MaterialApp(
        home: Scaffold(body: PersonNameText('محمد علي حسين')),
      ),
    );
    expect(find.text('محمد علي حسين'), findsOneWidget);
    expect(find.text('محمد، علي، حسين'), findsNothing);
  });
}
