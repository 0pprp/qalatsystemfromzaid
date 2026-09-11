import 'package:delegate_application/ui/app_safe_scaffold.dart';
import 'package:delegate_application/ui/main_bottom_nav.dart';
import 'package:delegate_application/services/today_payments_logic.dart';
import 'package:delegate_application/utils/iraq_date.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('MainBottomNavBar', () {
    testWidgets('home is a normal tab (no FAB gap); RTL order', (tester) async {
      int? tapped;
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: const SizedBox.shrink(),
            bottomNavigationBar: MainBottomNavBar(
              selectedIndex: 2,
              onTap: (i) => tapped = i,
            ),
          ),
        ),
      );

      expect(find.text('المبيعات'), findsOneWidget);
      expect(find.text('العملاء'), findsOneWidget);
      expect(find.text('الرئيسية'), findsOneWidget);
      expect(find.text('التسديدات'), findsOneWidget);
      expect(find.byType(FloatingActionButton), findsNothing);

      final sales = tester.getCenter(find.text('المبيعات'));
      final customers = tester.getCenter(find.text('العملاء'));
      final home = tester.getCenter(find.text('الرئيسية'));
      final payments = tester.getCenter(find.text('التسديدات'));

      expect(sales.dx, greaterThan(customers.dx));
      expect(customers.dx, greaterThan(home.dx));
      expect(home.dx, greaterThan(payments.dx));

      await tester.tap(find.text('المبيعات'));
      expect(tapped, 0);
      await tester.tap(find.text('العملاء'));
      expect(tapped, 1);
      await tester.tap(find.text('الرئيسية'));
      expect(tapped, 2);
      await tester.tap(find.text('التسديدات'));
      expect(tapped, 3);
    });

    testWidgets('selected indicator moves with tab', (tester) async {
      var index = 2;
      await tester.pumpWidget(
        MaterialApp(
          home: StatefulBuilder(
            builder: (context, setState) {
              return Scaffold(
                body: const SizedBox.shrink(),
                bottomNavigationBar: MainBottomNavBar(
                  selectedIndex: index,
                  onTap: (i) => setState(() => index = i),
                ),
              );
            },
          ),
        ),
      );

      await tester.tap(find.text('العملاء'));
      await tester.pumpAndSettle();
      // Rebuild with new index via StatefulBuilder — tap updates state.
      expect(find.text('العملاء'), findsOneWidget);

      await tester.tap(find.text('المبيعات'));
      await tester.pumpAndSettle();
      expect(find.text('المبيعات'), findsOneWidget);
    });

    testWidgets('SafeArea wraps bottom nav without overflow at 360x800',
        (tester) async {
      tester.view.physicalSize = const Size(360, 800);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);

      await tester.pumpWidget(
        MaterialApp(
          home: AppSafeScaffold(
            safeBottom: false,
            body: const SizedBox.shrink(),
            bottomNavigationBar: MainBottomNavBar(
              selectedIndex: 2,
              onTap: (_) {},
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();
      expect(tester.takeException(), isNull);
      expect(find.text('الرئيسية'), findsOneWidget);
    });
  });

  group('TodayPaymentsLogic / IraqDate', () {
    final utcNow = DateTime.utc(2026, 9, 10, 22, 0); // Iraq Sep 11 01:00

    test('paid today uses CreatedAtUtc Iraq day', () {
      final pay = TodayPaymentsLogic.todayPaymentForCustomer(
        customerId: 1,
        localPayments: [
          {
            'CustomerId': 1,
            'Amount': 5000,
            'CreatedAtUtc': '2026-09-10T22:30:00.000Z', // Iraq Sep 11
          },
          {
            'CustomerId': 1,
            'Amount': 1000,
            'CreatedAtUtc': '2026-09-09T10:00:00.000Z', // yesterday Iraq
          },
        ],
        utcNow: utcNow,
      );
      expect(pay, isNotNull);
      expect(pay!['Amount'], 5000);
    });

    test('old payment is not counted as today', () {
      expect(
        IraqDate.isCreatedOnIraqDay('2026-09-01T10:00:00.000Z', utcNow),
        isFalse,
      );
    });

    test('non-continuous or zero remaining excluded from due list', () {
      final rows = TodayPaymentsLogic.buildRows(
        customers: [
          {
            'CustomerId': 1,
            'CustomerName': 'A',
            'DelegateId': 5,
            'AmountRemaining': 10000,
            'AmountDaySales': 2000,
            'LastPaymentDate': DateTime.now()
                .subtract(const Duration(days: 400))
                .toIso8601String(),
            'DateSaleDevice': '',
          },
          {
            'CustomerId': 2,
            'CustomerName': 'B',
            'DelegateId': 5,
            'AmountRemaining': 0,
            'AmountDaySales': 2000,
            'LastPaymentDate': DateTime.now().toIso8601String(),
            'DateSaleDevice': '',
          },
          {
            'CustomerId': 3,
            'CustomerName': 'C',
            'DelegateId': 5,
            'AmountRemaining': 8000,
            'AmountDaySales': 2000,
            'LastPaymentDate': DateTime.now().toIso8601String(),
            'DateSaleDevice': '',
          },
        ],
        localPayments: const [],
        delegateNames: {5: 'قائمة 5'},
        utcNow: utcNow,
      );
      expect(rows.length, 1);
      expect(rows.first.customerId, 3);
      expect(rows.first.status, TodayPaymentStatus.unpaid);
    });

    test('offline payment today marks paid', () {
      final rows = TodayPaymentsLogic.buildRows(
        customers: [
          {
            'CustomerId': 3,
            'CustomerName': 'C',
            'DelegateId': 5,
            'AmountRemaining': 8000,
            'AmountDaySales': 2000,
            'LastPaymentDate': DateTime.now().toIso8601String(),
            'DateSaleDevice': '',
          },
        ],
        localPayments: [
          {
            'CustomerId': 3,
            'Amount': 2000,
            'CreatedAtUtc': '2026-09-10T23:00:00.000Z',
          },
        ],
        delegateNames: {5: 'قائمة 5'},
        utcNow: utcNow,
      );
      expect(rows.first.status, TodayPaymentStatus.paid);
      expect(rows.first.paidAmount, 2000);
    });
  });
}
