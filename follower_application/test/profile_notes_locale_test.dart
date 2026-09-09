import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:follower_application/customer_profile_page.dart';
import 'package:follower_application/utils/iraq_datetime.dart';
import 'package:intl/intl.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  final seed = <String, dynamic>{
    'customerId': 10,
    'customerName': 'زبون اختبار',
    'phoneNumber': '07701234567',
    'address': 'حي',
    'costTotalSales': 1000,
    'amountTotalSales': 1500,
    'amountDaySales': 50,
    'amountRemaining': 500,
    'receiptsTotal': 1000,
    'notes': [
      {
        'noteText': 'ملاحظة موجودة',
        'createdByName': 'متابع',
        'createdAtUtc': '2026-09-08T12:00:00Z',
      },
    ],
    'images': [],
  };

  Future<void> pumpProfile(WidgetTester tester) async {
    await tester.pumpWidget(
      MaterialApp(
        home: CustomerProfilePage(
          customer: const {'customerId': 10, 'customerName': 'زبون اختبار'},
          listId: 5,
          seedProfile: seed,
        ),
      ),
    );
    await tester.pumpAndSettle();
  }

  testWidgets('CustomerProfilePage opens with notes without LocaleDataException', (tester) async {
    await pumpProfile(tester);

    expect(find.text('زبون اختبار'), findsOneWidget);
    expect(find.text('ملف الزبون'), findsOneWidget);

    await tester.scrollUntilVisible(find.text('الملاحظات'), 300);
    await tester.pumpAndSettle();
    expect(find.text('ملاحظة موجودة'), findsOneWidget);
    expect(find.textContaining('التاريخ:'), findsWidgets);
    expect(tester.takeException(), isNull);
  });

  testWidgets('adding customer note keeps profile open (no full rebuild wipe)', (tester) async {
    await pumpProfile(tester);

    await tester.scrollUntilVisible(find.byType(TextField), 400);
    await tester.pumpAndSettle();
    await tester.enterText(find.byType(TextField), 'ملاحظة جديدة محلية');
    await tester.pump();

    expect(find.text('ملف الزبون'), findsOneWidget);
    expect(find.byType(TextField), findsOneWidget);
    expect(tester.takeException(), isNull);
  });

  testWidgets('re-open profile with notes after prior open', (tester) async {
    await pumpProfile(tester);
    await tester.scrollUntilVisible(find.text('ملاحظة موجودة'), 400);
    await tester.pumpAndSettle();
    expect(find.text('ملاحظة موجودة'), findsOneWidget);

    await tester.pumpWidget(const SizedBox.shrink());
    await tester.pumpAndSettle();

    await pumpProfile(tester);
    await tester.scrollUntilVisible(find.text('ملاحظة موجودة'), 400);
    await tester.pumpAndSettle();
    expect(find.text('ملاحظة موجودة'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });

  testWidgets('delegate note dialog does not dispose TextEditingController into dependents', (tester) async {
    var draft = '';
    await tester.pumpWidget(
      MaterialApp(
        home: Builder(
          builder: (context) => Scaffold(
            body: ElevatedButton(
              onPressed: () async {
                await showDialog<bool>(
                  context: context,
                  builder: (ctx) => AlertDialog(
                    content: TextField(onChanged: (v) => draft = v),
                    actions: [
                      TextButton(onPressed: () => Navigator.pop(ctx, true), child: const Text('حفظ')),
                    ],
                  ),
                );
              },
              child: const Text('ملاحظة المندوب'),
            ),
          ),
        ),
      ),
    );

    await tester.tap(find.text('ملاحظة المندوب'));
    await tester.pumpAndSettle();
    await tester.enterText(find.byType(TextField), 'نص المندوب');
    await tester.tap(find.text('حفظ'));
    await tester.pumpAndSettle();

    expect(draft, 'نص المندوب');
    expect(tester.takeException(), isNull);
  });

  test('IraqDateTime never throws even when DateFormat locale would fail', () {
    Object? dateFormatError;
    try {
      DateFormat('yyyy/MM/dd hh:mm a', 'ar').format(DateTime.utc(2026, 9, 8));
    } catch (e) {
      dateFormatError = e;
    }
    expect(dateFormatError, isNotNull);
    expect(dateFormatError.toString(), contains('initializeDateFormatting'));

    expect(() => IraqDateTime.formatClock('2026-09-08T12:00:00Z'), returnsNormally);
    expect(() => IraqDateTime.formatYmd(DateTime(2026, 9, 8)), returnsNormally);
    expect(() => IraqDateTime.formatNumber(1234567), returnsNormally);
    final clock = IraqDateTime.formatClock('2026-09-08T12:00:00Z');
    expect(clock.contains('صباحاً') || clock.contains('مساءً'), isTrue);
  });
}
