import 'package:customer_app/app.dart';
import 'package:customer_app/core/fake/fake_backend.dart';
import 'package:customer_app/core/providers.dart';
import 'package:customer_app/core/storage/token_storage.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';

class _MemoryTokens extends TokenStorage {
  @override
  Future<AuthTokens?> read() async => null;

  @override
  AuthTokens? get current => null;

  @override
  Future<void> clear() async {}
}

void main() {
  testWidgets('signed-out user lands on the username/password login', (tester) async {
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
    for (var i = 0; i < 10; i++) {
      await tester.pump(const Duration(milliseconds: 100));
    }

    expect(find.text('تسجيل الدخول'), findsOneWidget);
    expect(find.text('اسم المستخدم'), findsOneWidget);
    expect(find.text('كلمة المرور'), findsOneWidget);
    expect(find.text('الرئيسية'), findsNothing); // no guest browsing
    expect(Directionality.of(tester.element(find.text('تسجيل الدخول'))), TextDirection.rtl);
  });
}
