import 'dart:async';

import 'package:flutter_test/flutter_test.dart';

/// Documents mutex behavior expected by PaymentSyncService.
void main() {
  test('concurrent callers share one lock future', () async {
    Completer<void>? lock;
    var runs = 0;

    Future<void> syncOnce() async {
      if (lock != null) return lock!.future;
      final c = Completer<void>();
      lock = c;
      try {
        runs++;
        await Future<void>.delayed(const Duration(milliseconds: 30));
      } finally {
        lock = null;
        if (!c.isCompleted) c.complete();
      }
    }

    await Future.wait([syncOnce(), syncOnce(), syncOnce()]);
    expect(runs, 1);
  });
}
