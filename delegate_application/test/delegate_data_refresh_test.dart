import 'package:delegate_application/services/delegate_data_refresh_service.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('DelegateDataRefreshRules', () {
    test('failed fetch keeps old local (no replace)', () {
      expect(
        DelegateDataRefreshRules.shouldReplaceLocal(
          fetchSucceeded: false,
          responseValid: true,
        ),
        isFalse,
      );
    });

    test('invalid response keeps old local', () {
      expect(
        DelegateDataRefreshRules.shouldReplaceLocal(
          fetchSucceeded: true,
          responseValid: false,
        ),
        isFalse,
      );
    });

    test('successful valid fetch allows atomic replace', () {
      expect(
        DelegateDataRefreshRules.shouldReplaceLocal(
          fetchSucceeded: true,
          responseValid: true,
        ),
        isTrue,
      );
    });

    test('concurrent second refresh is blocked while in-flight', () {
      expect(
        DelegateDataRefreshRules.allowsConcurrentSecondRefresh(
          alreadyInFlight: true,
        ),
        isFalse,
      );
      expect(
        DelegateDataRefreshRules.allowsConcurrentSecondRefresh(
          alreadyInFlight: false,
        ),
        isTrue,
      );
    });
  });
}
