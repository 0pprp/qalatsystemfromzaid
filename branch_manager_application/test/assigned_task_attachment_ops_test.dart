import 'package:branch_manager_application/ui/assigned_task_attachment_ops.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('BmAssignedTaskAttachmentOps.sanitizeFileName', () {
    test('replaces illegal path characters', () {
      expect(
        BmAssignedTaskAttachmentOps.sanitizeFileName(r'a\b/c:d*e?.pdf'),
        'a_b_c_d_e_.pdf',
      );
    });

    test('falls back when empty', () {
      expect(BmAssignedTaskAttachmentOps.sanitizeFileName('  '), 'attachment');
    });
  });
}
