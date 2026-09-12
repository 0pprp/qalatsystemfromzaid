import 'package:flutter_test/flutter_test.dart';
import 'package:sales_filter_application/main.dart';

void main() {
  testWidgets('app boots', (tester) async {
    await tester.pumpWidget(const SalesFilterApp());
    await tester.pump();
    expect(find.byType(SalesFilterApp), findsOneWidget);
  });
}
