import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:promoters_app/core/app_state.dart';
import 'package:promoters_app/core/push_service.dart';
import 'package:promoters_app/core/utils/formatters.dart';
import 'package:promoters_app/data/models.dart';
import 'package:promoters_app/features/auth/login_screen.dart';
import 'package:promoters_app/features/orders/new_order_screen.dart';
import 'package:promoters_app/features/orders/order_widgets.dart';
import 'package:promoters_app/features/wallet/wallet_screen.dart';

void main() {
  group('models', () {
    test('Me reads the account (nothing about who created it)', () {
      final me = Me.fromJson({
        'id': 7,
        'username': 'daughter1',
        'phone': '07702222222',
        'isFull': false,
        'canCreateAccounts': false,
        'createdAt': '2026-11-07T20:00:00Z',
      });
      expect(me.isFull, isFalse);
      expect(me.canCreateAccounts, isFalse);
      expect(me.toJson().keys, isNot(contains('parentUsername')));
      expect(Me.fromJson(me.toJson()).username, 'daughter1');
    });

    test('Wallet entry details join product, member and note', () {
      final e = WalletEntry.fromJson({'id': 1, 'amount': 15000, 'type': 'Team', 'typeLabel': 'عمولة من فريقي', 'productName': 'ايفون', 'fromUsername': 'daughter1'});
      expect(e.details, 'ايفون · من daughter1');
      expect(e.amount, 15000);
    });

    test('Paged envelopes and tolerant numbers', () {
      final p = Payout.fromJson({'id': '3', 'amount': 5000.0, 'status': 'Pending', 'method': 'ZainCash'});
      expect(p.id, 3);
      expect(p.amount, 5000);
      expect(p.isPending, isTrue);
    });

    test('Material kinds and file extension', () {
      final m = MarketingMaterial.fromJson({'id': 2, 'kind': 'pdf', 'url': 'http://x/media/materials/a.pdf', 'sizeBytes': 2 * 1024 * 1024});
      expect(m.isPdf, isTrue);
      expect(m.extension, '.pdf');
      expect(m.sizeLabel, '2.0 MB');
    });
  });

  test('version comparison for the forced update', () {
    expect(compareVersions('1.0.0', '1.0.1'), lessThan(0));
    expect(compareVersions('1.2.10', '1.2.9'), greaterThan(0));
    expect(compareVersions('1.0.0+5', '1.0.0'), 0);
  });

  test('a tapped notification opens the matching screen', () {
    expect(routeForPushData({'type': 'Order', 'orderId': '12'}), '/orders/12');
    expect(routeForPushData({'type': 'Payout', 'payoutId': '3'}), '/payouts/3');
    expect(routeForPushData({'type': 'Wallet'}), '/wallet');
    expect(routeForPushData({'type': 'System'}), '/notifications');
  });

  test('customer name: same rule and messages as the server', () {
    expect(customerNameError('محمدعلي عبد الحسن'), contains('ثلاثة أسماء'));
    expect(customerNameError('محمد علي عبد الحسن'), isNull);
    expect(customerNameError('محمد Ali حسن'), contains('بالحروف العربية'));
    expect(customerNameError('مُحَمَّد عـلي\u200F حسن'), isNull);
  });

  test('chat attachments and receipts are read from the API', () {
    final m = ChatMessage.fromJson({'id': 5, 'fromAdmin': true, 'senderName': 'الإدارة', 'body': '', 'attachmentName': 'كشف.pdf', 'attachmentKind': 'file', 'attachmentSize': 2048});
    expect(m.hasFile, isTrue);
    expect(m.hasImage, isFalse);
    expect(m.sizeLabel, '2 KB');
    expect(Payout.fromJson({'id': 1, 'status': 'Paid', 'hasReceipt': true}).hasReceipt, isTrue);
  });

  test('Qi Card numbers: Arabic digits become 0-9', () {
    const f = LatinDigitsFormatter();
    final out = f.formatEditUpdate(TextEditingValue.empty, const TextEditingValue(text: '١٢٣٤٥٦٧٨٩٠'));
    expect(out.text, '1234567890');
    expect(RegExp(r'^\d{10,13}$').hasMatch(out.text), isTrue);
    expect(RegExp(r'^\d{10,13}$').hasMatch('123456789'), isFalse);
    expect(qiCardRule, contains('10 إلى 13'));
  });

  test('money format', () {
    expect(Fmt.money(25000), '25,000 د.ع');
    expect(Fmt.money(-5000), '- 5,000 د.ع');
  });

  testWidgets('login screen shows username, password and the sign-in button', (tester) async {
    await tester.pumpWidget(const ProviderScope(
      child: MaterialApp(home: Directionality(textDirection: TextDirection.rtl, child: LoginScreen())),
    ));
    expect(find.text('اسم المستخدم'), findsOneWidget);
    expect(find.text('كلمة المرور'), findsOneWidget);
    await tester.tap(find.text('دخول'));
    await tester.pump();
    expect(find.text('أدخل اسم المستخدم وكلمة المرور'), findsOneWidget);
  });

  testWidgets('product row shows the promoter commission', (tester) async {
    await tester.pumpWidget(const MaterialApp(
      home: Scaffold(body: ProductRow(product: Product(id: 1, name: 'ايفون 15', price: 1450000, myCommission: 10000))),
    ));
    expect(find.text('ايفون 15'), findsOneWidget);
    expect(find.text('10,000 د.ع'), findsOneWidget);
    expect(find.text('1,450,000 د.ع'), findsOneWidget);
  });
}
