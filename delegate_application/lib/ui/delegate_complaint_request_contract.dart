/// Pure helpers for complaint POST contract (testable without HTTP).
class DelegateComplaintRequestContract {
  static const asyncIdHeader = 'X-Async-Id';
  static const delegateIdHeader = 'X-Delegate-Id';

  /// Body must contain message only — never identity fields.
  static Map<String, dynamic> body(String message) => {
        'message': message.trim(),
      };

  static Map<String, String> headers({
    required String asyncId,
    String? delegateId,
  }) {
    final h = <String, String>{
      'Content-Type': 'application/json',
      asyncIdHeader: asyncId,
    };
    if (delegateId != null && delegateId.isNotEmpty) {
      h[delegateIdHeader] = delegateId;
    }
    return h;
  }
}
