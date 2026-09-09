import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:follower_application/customer_profile_page.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  Map<String, dynamic> seedWithDocs() => {
        'customerId': 10,
        'customerName': 'زبون مستمسكات',
        'phoneNumber': '07701234567',
        'images': [
          {
            'kind': 'NationalIdFront',
            'label': 'البطاقة الوطنية - أمامية',
            'fileName': '1.jpg',
            'url': 'http://example.test/Images/1.jpg',
          },
          {
            'kind': 'NationalIdBack',
            'label': 'البطاقة الوطنية - خلفية',
            'fileName': '2.jpg',
            'url': 'http://example.test/Images/2.jpg',
          },
          {
            'kind': 'ResidenceCertificate',
            'label': 'تأييد السكن',
            'fileName': '3.jpg',
            'url': 'http://broken.test/missing.jpg',
          },
        ],
        'notes': [],
      };

  testWidgets('shows all customer documents with labels', (tester) async {
    await tester.pumpWidget(
      MaterialApp(
        home: CustomerProfilePage(
          customer: const {'customerId': 10},
          listId: 1,
          seedProfile: seedWithDocs(),
        ),
      ),
    );
    await tester.pump();

    expect(find.text('الصور والمستمسكات'), findsOneWidget);
    for (final label in [
      'البطاقة الوطنية - أمامية',
      'البطاقة الوطنية - خلفية',
      'تأييد السكن',
    ]) {
      await tester.scrollUntilVisible(find.text(label), 500);
      expect(find.text(label), findsOneWidget);
    }
    expect(find.text('لا توجد صور للزبون'), findsNothing);
  });

  testWidgets('one failed image keeps sibling document labels in tree', (tester) async {
    await tester.pumpWidget(
      MaterialApp(
        home: CustomerProfilePage(
          customer: const {'customerId': 10},
          listId: 1,
          seedProfile: seedWithDocs(),
        ),
      ),
    );
    await tester.pump();

    // Reach the broken one; remaining labels must still be findable when scrolled to.
    await tester.scrollUntilVisible(find.text('تأييد السكن'), 500);
    expect(find.text('تأييد السكن'), findsOneWidget);
    await tester.scrollUntilVisible(find.text('البطاقة الوطنية - أمامية'), -500);
    expect(find.text('البطاقة الوطنية - أمامية'), findsOneWidget);
    await tester.scrollUntilVisible(find.text('البطاقة الوطنية - خلفية'), 500);
    expect(find.text('البطاقة الوطنية - خلفية'), findsOneWidget);
  });

  testWidgets('empty images list shows no-photos message only', (tester) async {
    await tester.pumpWidget(
      MaterialApp(
        home: CustomerProfilePage(
          customer: const {'customerId': 10},
          listId: 1,
          seedProfile: {
            'customerId': 10,
            'customerName': 'بدون صور',
            'images': <dynamic>[],
            'notes': <dynamic>[],
          },
        ),
      ),
    );
    await tester.pump();
    expect(find.text('لا توجد صور للزبون'), findsOneWidget);
  });

  testWidgets('profile listview has bottom safe padding against system nav', (tester) async {
    await tester.pumpWidget(
      MediaQuery(
        data: const MediaQueryData(viewPadding: EdgeInsets.only(bottom: 48), padding: EdgeInsets.only(bottom: 48)),
        child: MaterialApp(
          home: CustomerProfilePage(
            customer: const {'customerId': 10},
            listId: 1,
            seedProfile: {
              'customerId': 10,
              'customerName': 'Safe',
              'images': <dynamic>[],
              'notes': <dynamic>[],
            },
          ),
        ),
      ),
    );
    await tester.pump();

    expect(find.byType(SafeArea), findsWidgets);
    final list = tester.widget<ListView>(find.byType(ListView));
    final padding = list.padding! as EdgeInsets;
    expect(padding.bottom, greaterThanOrEqualTo(16));
  });
}
