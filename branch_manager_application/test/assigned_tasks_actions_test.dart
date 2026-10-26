import 'package:branch_manager_application/ui/assigned_tasks_actions.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('BmAssignedTaskActions.forStatus', () {
    test('مرسلة allows accept and reject only', () {
      final a = BmAssignedTaskActions.forStatus('مرسلة');
      expect(a.canAccept, isTrue);
      expect(a.canReject, isTrue);
      expect(a.canComplete, isFalse);
      expect(a.canTransfer, isFalse);
    });

    test('منقولة allows accept and reject only', () {
      final a = BmAssignedTaskActions.forStatus('منقولة');
      expect(a.canAccept, isTrue);
      expect(a.canReject, isTrue);
      expect(a.canComplete, isFalse);
      expect(a.canTransfer, isFalse);
    });

    test('مقبولة allows complete only', () {
      final a = BmAssignedTaskActions.forStatus('مقبولة');
      expect(a.canAccept, isFalse);
      expect(a.canReject, isFalse);
      expect(a.canComplete, isTrue);
      expect(a.canTransfer, isFalse);
    });

    test('معادة allows no BM actions', () {
      final a = BmAssignedTaskActions.forStatus('معادة');
      expect(a.canAccept, isFalse);
      expect(a.canReject, isFalse);
      expect(a.canComplete, isFalse);
      expect(a.canTransfer, isFalse);
    });

    test('منجزة allows no BM actions', () {
      final a = BmAssignedTaskActions.forStatus('منجزة');
      expect(a.canAccept, isFalse);
      expect(a.canReject, isFalse);
      expect(a.canComplete, isFalse);
      expect(a.canTransfer, isFalse);
    });

    test('بانتظار_تصديق_المفوض allows no BM actions', () {
      final a = BmAssignedTaskActions.forStatus('بانتظار_تصديق_المفوض');
      expect(a.canAccept, isFalse);
      expect(a.canReject, isFalse);
      expect(a.canComplete, isFalse);
      expect(a.canTransfer, isFalse);
    });

    test('unknown status allows no actions', () {
      final a = BmAssignedTaskActions.forStatus('something-else');
      expect(a.canAccept, isFalse);
      expect(a.canReject, isFalse);
      expect(a.canComplete, isFalse);
      expect(a.canTransfer, isFalse);
    });

    test('BM never exposes transfer regardless of status', () {
      for (final status in const [
        'مرسلة',
        'منقولة',
        'مقبولة',
        'معادة',
        'منجزة',
        'بانتظار_تصديق_المفوض',
      ]) {
        expect(
          BmAssignedTaskActions.forStatus(status).canTransfer,
          isFalse,
          reason: status,
        );
      }
    });
  });

  group('BmAssignedTask.fromJson', () {
    test('parses dueAtUtc transferredAtUtc attachments', () {
      final task = BmAssignedTask.fromJson({
        'id': 'bm-1',
        'title': 'مهمة',
        'dueDate': '2026-10-25',
        'dueAtUtc': '2026-10-25T12:30:00Z',
        'priority': 1,
        'status': 'منقولة',
        'transferredAtUtc': '2026-10-21T08:00:00Z',
        'branchCompletedAtUtc': null,
        'attachments': [
          {'id': 'a1', 'fileName': 'x.pdf', 'sizeBytes': 10},
        ],
      });
      expect(task.status, 'منقولة');
      expect(task.actions.canAccept, isTrue);
      expect(task.dueAtUtc, isNotNull);
      expect(task.transferredAtUtc, isNotNull);
      expect(task.attachments, hasLength(1));
      expect(task.dueDisplay, contains('مساء'));
    });
  });

  group('BmAssignedTaskActions.sortInbox', () {
    test('sorts عالية before متوسطة before ضعيفة then sooner due', () {
      final rows = [
        _task(id: 'low-late', priority: 3, due: '2026-12-01'),
        _task(id: 'high-late', priority: 1, due: '2026-12-01'),
        _task(id: 'med', priority: 2, due: '2026-11-01'),
        _task(id: 'high-soon', priority: 1, due: '2026-10-01'),
        _task(id: 'low-soon', priority: 3, due: '2026-10-15'),
      ];
      final sorted = BmAssignedTaskActions.sortInbox(rows);
      expect(sorted.map((e) => e.id).toList(), [
        'high-soon',
        'high-late',
        'med',
        'low-soon',
        'low-late',
      ]);
    });
  });
}

BmAssignedTask _task({
  required String id,
  required int priority,
  required String due,
}) {
  return BmAssignedTask(
    id: id,
    title: id,
    dueDate: DateTime.parse(due),
    priority: priority,
    status: 'مرسلة',
  );
}
