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

  test('manual send retries stuck rejects except wrong account and tiny amount', () {
    expect(
      PaymentSyncService.isHardPermanentError('OWNER_MISMATCH'),
      isTrue,
    );
    expect(
      PaymentSyncService.isHardPermanentError(
        'أقل مبلغ مسموح للتسديد هو 2,000 د.ع',
      ),
      isTrue,
    );
    expect(
      PaymentSyncService.isHardPermanentError('العميل غير موجود'),
      isFalse,
    );
    expect(
      PaymentSyncService.isHardPermanentError(
        'جباية هذه القائمة مفعّلة للمتابعين — التسديد من تطبيق المندوب مقفل',
      ),
      isFalse,
    );
    expect(
      PaymentSyncService.isHardPermanentError('SESSION_EXPIRED'),
      isFalse,
    );
    expect(
      PaymentSyncService.isHardPermanentError('وقت إنشاء التسديد قديم جدًا'),
      isTrue,
    );
    expect(PaymentSyncService.isUuid('legacy-local-12'), isFalse);
    expect(
      PaymentSyncService.isUuid('aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee'),
      isTrue,
    );
    expect(
      PaymentSyncService.describeSyncError('العميل غير موجود'),
      'العميل غير موجود',
    );
  });
}
