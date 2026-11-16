import 'dart:async';

import 'package:customer_app/app.dart';
import 'package:customer_app/core/fake/fake_backend.dart';
import 'package:customer_app/core/providers.dart';
import 'package:customer_app/core/router/app_router.dart';
import 'package:customer_app/core/storage/token_storage.dart';
import 'package:customer_app/features/work/data/prospects_repository.dart';
import 'package:customer_app/features/work/presentation/prospect_sheets.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';

class _MemoryTokens extends TokenStorage {
  AuthTokens? _t;

  @override
  Future<AuthTokens?> read() async => _t;

  @override
  AuthTokens? get current => _t;

  @override
  Future<void> save(AuthTokens tokens) async => _t = tokens;

  @override
  Future<void> clear() async => _t = null;
}

Future<void> settle(WidgetTester tester, [int frames = 12]) async {
  for (var i = 0; i < frames; i++) {
    await tester.pump(const Duration(milliseconds: 100));
  }
}

/// Signs in to the demo backend on a phone-sized screen and opens every main screen:
/// a layout overflow or a runtime error fails the test.
void main() {
  testWidgets('the app opens on the short order form; overview, orders and details render on a phone', (tester) async {
    tester.view.physicalSize = const Size(1080, 2340);
    tester.view.devicePixelRatio = 2.75;
    addTearDown(tester.view.reset);

    SharedPreferences.setMockInitialValues({});
    final prefs = await SharedPreferences.getInstance();
    await tester.pumpWidget(ProviderScope(
      overrides: [
        sharedPrefsProvider.overrideWithValue(prefs),
        tokenStorageProvider.overrideWithValue(_MemoryTokens()),
        fakeBackendProvider.overrideWithValue(FakeBackend(latency: Duration.zero)),
      ],
      retry: (_, _) => null,
      child: const QalaaApp(enablePlatformServices: false),
    ));
    await settle(tester);

    await tester.enterText(find.widgetWithText(TextField, 'اسم المستخدم'), 'agent1');
    await tester.enterText(find.widgetWithText(TextField, 'كلمة المرور'), '123456');
    await tester.tap(find.widgetWithText(FilledButton, 'تسجيل الدخول'));
    await settle(tester, 20);

    // The app opens on the sale order form.
    expect(find.text('طلب بيع جديد'), findsOneWidget);
    expect(find.text('اختر المنتج'), findsOneWidget);
    if (find.text('لاحقاً').evaluate().isNotEmpty) {
      await tester.tap(find.text('لاحقاً'));
      await settle(tester);
    }

    // Sending empty shows what is missing; photo and location are not required.
    await tester.tap(find.text('إرسال الطلب'));
    await settle(tester, 4);
    expect(find.text('اكتب اسم الزبون'), findsOneWidget);
    expect(find.textContaining('صورة'), findsWidgets); // the optional extras section is there…
    expect(find.text('اختياري'), findsOneWidget); // …and marked optional

    // Product from the list, «اخرى» asks for a name.
    await tester.tap(find.byIcon(Icons.inventory_2_outlined));
    await settle(tester, 4);
    await tester.tap(find.text('اخرى').last);
    await settle(tester, 4);
    expect(find.text('اسم المنتج'), findsOneWidget);
    await tester.tap(find.byIcon(Icons.inventory_2_outlined));
    await settle(tester, 4);
    await tester.tap(find.text('شاشة').last);
    await settle(tester, 4);
    expect(find.text('اسم المنتج'), findsNothing);

    await tester.enterText(find.widgetWithText(TextField, 'اسم الزبون الثلاثي'), 'حيدر كريم جواد');
    await tester.enterText(find.widgetWithText(TextField, 'رقم الهاتف'), '07709998877');
    await tester.tap(find.byIcon(Icons.location_city_rounded));
    await settle(tester, 4);
    await tester.tap(find.text('النجف').last);
    await settle(tester, 4);
    await tester.enterText(find.widgetWithText(TextField, 'العنوان'), 'الكوفة - حي ميثم');
    await tester.tap(find.text('إرسال الطلب'));
    await settle(tester, 15);
    expect(find.text('تم إرسال الطلب'), findsOneWidget);
    expect(find.text('شاشة'), findsWidgets);

    // «طلب جديد» returns to an empty form.
    await tester.tap(find.text('طلب جديد'));
    await settle(tester);
    expect(find.text('اختر المنتج'), findsOneWidget);

    // «المحلات» (replaced «ملخصي»): today's shops and the add button; the daily target is never shown to agents.
    expect(find.text('ملخصي'), findsNothing);
    await tester.tap(find.text('المحلات'));
    await settle(tester);
    expect(find.text('هدف اليوم'), findsNothing);
    expect(find.text('لم تثبّت أي محل اليوم'), findsOneWidget);
    expect(find.text('تثبيت محل'), findsOneWidget);

    // An order's details.
    await tester.tap(find.text('الطلبات').last);
    await settle(tester);
    await tester.tap(find.text('شاشة').first);
    await settle(tester);
    expect(find.text('طلب البيع'), findsOneWidget);
    expect(find.text('مراحل الطلب'), findsOneWidget);
    expect(find.text('أقساط يومية'), findsWidgets);
  });

  testWidgets('«محلاتي» lists the agent pins; a pin opens with its details and location', (tester) async {
    tester.view.physicalSize = const Size(1080, 2340);
    tester.view.devicePixelRatio = 2.75;
    addTearDown(tester.view.reset);

    SharedPreferences.setMockInitialValues({});
    final prefs = await SharedPreferences.getInstance();
    await tester.pumpWidget(ProviderScope(
      overrides: [
        sharedPrefsProvider.overrideWithValue(prefs),
        tokenStorageProvider.overrideWithValue(_MemoryTokens()),
        fakeBackendProvider.overrideWithValue(FakeBackend(latency: Duration.zero)),
      ],
      retry: (_, _) => null,
      child: const QalaaApp(enablePlatformServices: false),
    ));
    await settle(tester);
    await tester.enterText(find.widgetWithText(TextField, 'اسم المستخدم'), 'agent1');
    await tester.enterText(find.widgetWithText(TextField, 'كلمة المرور'), '123456');
    await tester.tap(find.widgetWithText(FilledButton, 'تسجيل الدخول'));
    await settle(tester, 20);
    if (find.text('لاحقاً').evaluate().isNotEmpty) {
      await tester.tap(find.text('لاحقاً'));
      await settle(tester);
    }

    final container = ProviderScope.containerOf(tester.element(find.byType(MaterialApp).first));
    final repo = container.read(prospectsRepositoryProvider);
    // The fake backend answers on (fake) timers: keep pumping while the calls run.
    Future<void> run(Future<Object?> call) async {
      var done = false;
      unawaited(call.whenComplete(() => done = true));
      for (var i = 0; i < 50 && !done; i++) {
        await tester.pump(const Duration(milliseconds: 50));
      }
      expect(done, isTrue);
    }

    await run(repo.create(const ProspectInput(lat: 32.02, lng: 44.33, shopName: 'محلات النور', description: 'أجهزة كهربائية', interest: 5, phone: '07701112233')));
    await run(repo.create(const ProspectInput(lat: 32.03, lng: 44.34, shopName: 'بقالة الأمل', interest: 2)));

    container.read(routerProvider).go('/pins'); // old link → the «المحلات» tab
    await settle(tester);
    expect(find.text('محلات النور'), findsOneWidget);
    expect(find.text('بقالة الأمل'), findsOneWidget);
    expect(find.text('2 محلات'), findsOneWidget);

    // Yesterday: none; back to today.
    await tester.tap(find.text('أمس'));
    await settle(tester);
    expect(find.text('محلات النور'), findsNothing);
    expect(find.text('لا توجد محلات في هذه الفترة'), findsOneWidget);
    await tester.tap(find.text('اليوم'));
    await settle(tester);
    expect(find.text('بقالة الأمل'), findsOneWidget);

    // The pin: what was entered, its map and actions.
    await tester.tap(find.text('محلات النور'));
    await settle(tester);
    expect(find.text('أجهزة كهربائية'), findsOneWidget);
    expect(find.text('07701112233'), findsOneWidget);
    expect(find.textContaining('دقيقة'), findsNothing); // the minutes at the shop are no longer shown
    expect(find.text('الاتجاهات'), findsOneWidget);
    expect(find.text('تعديل'), findsOneWidget);
    expect(find.byIcon(Icons.location_on_rounded), findsOneWidget);
  });

  testWidgets('the pin form asks only for the location, the shop name and the interest', (tester) async {
    tester.view.physicalSize = const Size(1080, 2340);
    tester.view.devicePixelRatio = 2.75;
    addTearDown(tester.view.reset);

    SharedPreferences.setMockInitialValues({});
    final prefs = await SharedPreferences.getInstance();
    await tester.pumpWidget(ProviderScope(
      overrides: [
        sharedPrefsProvider.overrideWithValue(prefs),
        tokenStorageProvider.overrideWithValue(_MemoryTokens()),
        fakeBackendProvider.overrideWithValue(FakeBackend(latency: Duration.zero)),
      ],
      retry: (_, _) => null,
      child: const QalaaApp(enablePlatformServices: false),
    ));
    await settle(tester);
    await tester.enterText(find.widgetWithText(TextField, 'اسم المستخدم'), 'agent1');
    await tester.enterText(find.widgetWithText(TextField, 'كلمة المرور'), '123456');
    await tester.tap(find.widgetWithText(FilledButton, 'تسجيل الدخول'));
    await settle(tester, 20);
    if (find.text('لاحقاً').evaluate().isNotEmpty) {
      await tester.tap(find.text('لاحقاً'));
      await settle(tester);
    }

    Prospect? saved;
    var closed = false;
    unawaited(showProspectForm(rootNavigatorKey.currentContext!, lat: 32.02, lng: 44.33, agentLat: 32.0201, agentLng: 44.3302).then((p) {
      saved = p;
      closed = true;
    }));
    await settle(tester);
    final sheet = find.descendant(of: find.byType(DraggableScrollableSheet), matching: find.byType(Scrollable)).first;
    Future<void> reveal(Finder f) async {
      await tester.scrollUntilVisible(f, 200, scrollable: sheet);
      await settle(tester, 2);
    }

    // Only the location, the name and the interest are asked for.
    for (final gone in ['الوقت المستغرق عند المحل *', 'رقم الهاتف (اختياري)', 'نوع المحل / وصف (اختياري)', 'الطول']) {
      expect(find.widgetWithText(TextField, gone), findsNothing);
    }

    Future<void> save() async {
      final button = find.text('تثبيت المحل على الخريطة');
      await reveal(button);
      await tester.tap(button);
      await settle(tester);
    }

    // Empty: the name and the interest are required.
    await save();
    expect(find.text('أدخل اسم المحل'), findsOneWidget);
    expect(closed, isFalse);

    await tester.scrollUntilVisible(find.widgetWithText(TextField, 'اسم المحل *'), -200, scrollable: sheet);
    await tester.enterText(find.widgetWithText(TextField, 'اسم المحل *'), 'محلات الفرات');
    final interest = find.text('مهتم');
    await reveal(interest);
    await tester.ensureVisible(interest.first);
    await settle(tester, 3);
    await tester.tap(interest.first);
    await settle(tester, 2);
    await save();
    for (var i = 0; i < 30 && !closed; i++) {
      await tester.pump(const Duration(milliseconds: 100));
    }
    expect(find.textContaining('أدخل'), findsNothing, reason: 'no field errors left');
    expect(closed, isTrue);
    expect(saved?.shopName, 'محلات الفرات');
    expect(saved?.agentLat, 32.0201);
  });
}
