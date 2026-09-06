import 'package:flutter_test/flutter_test.dart';
import 'package:sales_employee_application/services/api_client.dart';
import 'package:sales_employee_application/services/session_kicked.dart';

void main() {
  test('Session replaced is detected from 401 body/code only', () {
    expect(
      ApiClient.isSessionReplaced(
        401,
        SessionKicked.message,
        '{"code":"SESSION_REPLACED"}',
      ),
      isTrue,
    );
    expect(SessionKicked.matches(statusCode: 401, body: '{"code":"SESSION_REPLACED"}'), isTrue);
    expect(ApiClient.isSessionReplaced(401, 'انتهت صلاحية الجلسة'), isFalse);
    expect(ApiClient.isSessionReplaced(400, SessionKicked.message), isFalse);
  });
}
