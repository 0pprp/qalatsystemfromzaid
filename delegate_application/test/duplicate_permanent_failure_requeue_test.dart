import 'package:delegate_application/services/payment_sync_service.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('duplicate same-day server errors are recognized for requeue', () {
    expect(
      PaymentSyncService.isDuplicateAlreadyOnServerError(
        'تم تسجيل تسديد بنفس المبلغ لهذا الزبون اليوم — مرفوض لمنع التكرار',
      ),
      isTrue,
    );
    expect(
      PaymentSyncService.isDuplicateAlreadyOnServerError(
        'التسديد مسجل مسبقاً بنفس المبلغ لهذا الزبون اليوم',
      ),
      isTrue,
    );
    expect(
      PaymentSyncService.isDuplicateAlreadyOnServerError('OWNER_MISMATCH'),
      isFalse,
    );
    expect(
      PaymentSyncService.isDuplicateAlreadyOnServerError('SESSION_EXPIRED'),
      isFalse,
    );
  });
}
