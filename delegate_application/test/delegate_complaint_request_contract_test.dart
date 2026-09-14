import 'package:flutter_test/flutter_test.dart';
import 'package:delegate_application/ui/delegate_complaint_request_contract.dart';

void main() {
  test('complaint POST body has message only — no identity spoof fields', () {
    final body = DelegateComplaintRequestContract.body('نص شكوى كافٍ للاختبار');
    expect(body.keys, ['message']);
    expect(body.containsKey('asyncId'), isFalse);
    expect(body.containsKey('delegateId'), isFalse);
    expect(body.containsKey('senderName'), isFalse);
    expect(body.containsKey('branchLink'), isFalse);
  });

  test('complaint auth uses headers for AsyncId + session DelegateId', () {
    final headers = DelegateComplaintRequestContract.headers(
      asyncId: 'secret-a',
      delegateId: '42',
    );
    expect(headers['X-Async-Id'], 'secret-a');
    expect(headers['X-Delegate-Id'], '42');
    expect(headers.containsKey('asyncId'), isFalse);
  });
}
