import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ma_short_company/main.dart';
import 'package:webview_flutter_platform_interface/webview_flutter_platform_interface.dart';

import 'fake_webview_platform.dart';

void main() {
  setUpAll(() {
    WebViewPlatform.instance = FakeWebViewPlatform();
  });

  testWidgets('ShortCompanyApp smoke: splash branding while WebView loads',
      (WidgetTester tester) async {
    await tester.pumpWidget(const ShortCompanyApp());
    // First frame: splash still visible (fake load finishes after Duration.zero).
    await tester.pump();

    expect(find.text('قلعة الضمان'), findsOneWidget);
    expect(find.text('لأنظمة الإدارة'), findsOneWidget);
    expect(find.byType(CircularProgressIndicator), findsOneWidget);

    // Allow deferred onPageFinished + setState.
    await tester.pump(const Duration(milliseconds: 1));
    await tester.pump();

    expect(find.byType(CircularProgressIndicator), findsNothing);
    expect(find.byKey(const Key('fake-webview')), findsOneWidget);

    await tester.pumpWidget(const SizedBox.shrink());
    await tester.pump();
  });

  testWidgets('ShortCompanyApp shows retry UI on WebView resource error',
      (WidgetTester tester) async {
    WebViewPlatform.instance = FakeWebViewPlatform(failLoad: true);
    await tester.pumpWidget(const ShortCompanyApp());
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 1));
    await tester.pump();

    expect(find.text('إعادة المحاولة'), findsOneWidget);
    expect(find.textContaining('تعذر'), findsOneWidget);

    await tester.pumpWidget(const SizedBox.shrink());
    await tester.pump();
  });
}
