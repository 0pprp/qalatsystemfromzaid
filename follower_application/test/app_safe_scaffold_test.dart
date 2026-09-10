import 'package:follower_application/ui/app_safe_scaffold.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  testWidgets('AppSafeScaffold pads top when no AppBar', (tester) async {
    await tester.pumpWidget(
      const MediaQuery(
        data: MediaQueryData(padding: EdgeInsets.only(top: 40, bottom: 24)),
        child: MaterialApp(
          home: AppSafeScaffold(
            body: SizedBox.expand(
              child: ColoredBox(
                key: Key('body'),
                color: Colors.red,
              ),
            ),
          ),
        ),
      ),
    );

    final body = tester.getTopLeft(find.byKey(const Key('body')));
    expect(body.dy, greaterThanOrEqualTo(40));
  });

  testWidgets('AppSafeScaffold with AppBar builds without error', (tester) async {
    await tester.pumpWidget(
      const MediaQuery(
        data: MediaQueryData(padding: EdgeInsets.only(top: 40, bottom: 24)),
        child: MaterialApp(
          home: AppSafeScaffold(
            appBar: PreferredSize(
              preferredSize: Size.fromHeight(56),
              child: SizedBox(height: 56, child: ColoredBox(color: Colors.green)),
            ),
            body: SizedBox.expand(
              child: ColoredBox(
                key: Key('body'),
                color: Colors.blue,
              ),
            ),
          ),
        ),
      ),
    );

    expect(find.byKey(const Key('body')), findsOneWidget);
    expect(tester.takeException(), isNull);
  });

  for (final size in const [Size(360, 800), Size(390, 844), Size(412, 915)]) {
    testWidgets('scroll padding ok at ${size.width}x${size.height}', (tester) async {
      tester.view.physicalSize = size;
      tester.view.devicePixelRatio = 1.0;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);

      await tester.pumpWidget(
        MaterialApp(
          home: Builder(
            builder: (context) {
              return AppSafeScaffold(
                body: ListView(
                  padding: AppInsets.scrollPadding(context),
                  children: List.generate(
                    8,
                    (i) => ListTile(title: Text('عنصر $i')),
                  ),
                ),
              );
            },
          ),
        ),
      );
      await tester.pumpAndSettle();
      expect(tester.takeException(), isNull);
    });
  }
}
