import 'package:delegated_manager_application/features/assigned_tasks/domain/assigned_task.dart';
import 'package:delegated_manager_application/features/assigned_tasks/presentation/widgets/assigned_task_actions_bar.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

AssignedTask _task({
  required String id,
  required int priority,
  required DateTime dueDate,
  required String status,
}) {
  return AssignedTask(
    id: id,
    title: 'مهمة $id',
    dueDate: dueDate,
    priority: priority,
    status: status,
  );
}

void main() {
  group('sortAssignedTasks', () {
    test('orders by priority high→medium→low then sooner due date', () {
      final lowEarly = _task(
        id: 'low-early',
        priority: AssignedTaskPriority.low,
        dueDate: DateTime(2026, 10, 1),
        status: AssignedTaskStatus.sent,
      );
      final highLate = _task(
        id: 'high-late',
        priority: AssignedTaskPriority.high,
        dueDate: DateTime(2026, 12, 1),
        status: AssignedTaskStatus.sent,
      );
      final highEarly = _task(
        id: 'high-early',
        priority: AssignedTaskPriority.high,
        dueDate: DateTime(2026, 10, 5),
        status: AssignedTaskStatus.sent,
      );
      final medium = _task(
        id: 'medium',
        priority: AssignedTaskPriority.medium,
        dueDate: DateTime(2026, 10, 2),
        status: AssignedTaskStatus.accepted,
      );

      final sorted = sortAssignedTasks([lowEarly, highLate, medium, highEarly]);

      expect(sorted.map((t) => t.id).toList(), [
        'high-early',
        'high-late',
        'medium',
        'low-early',
      ]);
    });
  });

  group('AssignedTaskActions', () {
    test('مرسلة shows accept and reject only', () {
      final actions = AssignedTaskActions.forStatus(AssignedTaskStatus.sent);
      expect(actions.canAccept, isTrue);
      expect(actions.canReject, isTrue);
      expect(actions.canTransfer, isFalse);
      expect(actions.canComplete, isFalse);
      expect(actions.canConfirm, isFalse);
    });

    test('مقبولة shows transfer and complete only', () {
      final actions =
          AssignedTaskActions.forStatus(AssignedTaskStatus.accepted);
      expect(actions.canAccept, isFalse);
      expect(actions.canReject, isFalse);
      expect(actions.canTransfer, isTrue);
      expect(actions.canComplete, isTrue);
      expect(actions.canConfirm, isFalse);
    });

    test('بانتظار_تصديق_المفوض shows confirm only', () {
      final actions = AssignedTaskActions.forStatus(
          AssignedTaskStatus.awaitingDelegatedConfirm);
      expect(actions.canAccept, isFalse);
      expect(actions.canReject, isFalse);
      expect(actions.canTransfer, isFalse);
      expect(actions.canComplete, isFalse);
      expect(actions.canConfirm, isTrue);
    });

    test('معادة and منجزة show no actions', () {
      for (final status in [
        AssignedTaskStatus.returned,
        AssignedTaskStatus.completed,
      ]) {
        final actions = AssignedTaskActions.forStatus(status);
        expect(actions.canAccept, isFalse, reason: status);
        expect(actions.canReject, isFalse, reason: status);
        expect(actions.canTransfer, isFalse, reason: status);
        expect(actions.canComplete, isFalse, reason: status);
        expect(actions.canConfirm, isFalse, reason: status);
      }
    });

    test('منقولة shows no DM actions (visible only)', () {
      final actions =
          AssignedTaskActions.forStatus(AssignedTaskStatus.transferred);
      expect(actions.canAccept, isFalse);
      expect(actions.canReject, isFalse);
      expect(actions.canTransfer, isFalse);
      expect(actions.canComplete, isFalse);
      expect(actions.canConfirm, isFalse);
    });
  });

  group('AssignedTaskActionsBar widget', () {
    testWidgets('renders accept/reject for مرسلة', (tester) async {
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: AssignedTaskActionsBar(
              actions: AssignedTaskActions.forStatus(AssignedTaskStatus.sent),
              onAccept: () {},
              onReject: () {},
              onTransfer: () {},
              onComplete: () {},
              onConfirm: () {},
            ),
          ),
        ),
      );

      expect(find.text('قبول'), findsOneWidget);
      expect(find.text('رفض'), findsOneWidget);
      expect(find.text('نقل'), findsNothing);
      expect(find.text('تم الإنجاز'), findsNothing);
      expect(find.text('تصديق'), findsNothing);
    });

    testWidgets('renders transfer/complete for مقبولة', (tester) async {
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: AssignedTaskActionsBar(
              actions:
                  AssignedTaskActions.forStatus(AssignedTaskStatus.accepted),
              onAccept: () {},
              onReject: () {},
              onTransfer: () {},
              onComplete: () {},
              onConfirm: () {},
            ),
          ),
        ),
      );

      expect(find.text('قبول'), findsNothing);
      expect(find.text('رفض'), findsNothing);
      expect(find.text('نقل'), findsOneWidget);
      expect(find.text('تم الإنجاز'), findsOneWidget);
      expect(find.text('تصديق'), findsNothing);
    });

    testWidgets('renders confirm for بانتظار_تصديق_المفوض', (tester) async {
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: AssignedTaskActionsBar(
              actions: AssignedTaskActions.forStatus(
                  AssignedTaskStatus.awaitingDelegatedConfirm),
              onAccept: () {},
              onReject: () {},
              onTransfer: () {},
              onComplete: () {},
              onConfirm: () {},
            ),
          ),
        ),
      );

      expect(find.text('تصديق'), findsOneWidget);
      expect(find.text('قبول'), findsNothing);
      expect(find.text('نقل'), findsNothing);
      expect(find.text('تم الإنجاز'), findsNothing);
    });
  });

  group('AssignedTask.fromJson', () {
    test('parses inbox DTO fields from gateway', () {
      final task = AssignedTask.fromJson({
        'id': 'a1b2c3d4-e5f6-7890-abcd-ef1234567890',
        'title': 'متابعة فرع النجف',
        'dueDate': '2026-10-25',
        'dueAtUtc': '2026-10-25T07:15:00Z',
        'priority': 1,
        'status': 'مرسلة',
        'assigneeKind': 'DelegatedManager',
        'assigneeKey': 'ayman',
        'assigneeDisplayName': 'أيمن',
        'currentHolderKind': 'DelegatedManager',
        'currentHolderKey': 'ayman',
        'rejectNote': null,
        'createdAtUtc': '2026-10-20T10:00:00Z',
        'updatedAtUtc': '2026-10-20T10:00:00Z',
        'transferredAtUtc': '2026-10-21T08:00:00Z',
        'branchCompletedAtUtc': '2026-10-22T09:30:00Z',
        'delegatedConfirmedAtUtc': '2026-10-22T11:00:00Z',
        'attachments': [
          {
            'id': '11111111-1111-1111-1111-111111111111',
            'fileName': 'plan.pdf',
            'contentType': 'application/pdf',
            'sizeBytes': 1024,
            'downloadUrl': null,
          }
        ],
      });

      expect(task.id, 'a1b2c3d4-e5f6-7890-abcd-ef1234567890');
      expect(task.title, 'متابعة فرع النجف');
      expect(task.priority, 1);
      expect(task.status, AssignedTaskStatus.sent);
      expect(task.dueDate, DateTime(2026, 10, 25));
      expect(task.dueAtUtc, isNotNull);
      expect(task.transferredAtUtc, isNotNull);
      expect(task.branchCompletedAtUtc, isNotNull);
      expect(task.delegatedConfirmedAtUtc, isNotNull);
      expect(task.attachments, hasLength(1));
      expect(task.attachments.first.fileName, 'plan.pdf');
      expect(task.priorityLabel, 'عالية');
      expect(task.isOverdue(DateTime.utc(2026, 10, 26)), isTrue);
      expect(task.dueDisplay, contains('صباح'));
    });

    test('parses منقولة status label', () {
      final task = AssignedTask.fromJson({
        'id': 't1',
        'title': 'منقولة',
        'dueDate': '2026-10-25',
        'priority': 2,
        'status': 'منقولة',
      });
      expect(task.status, AssignedTaskStatus.transferred);
      expect(task.statusLabel, 'منقولة');
      expect(task.actions.canConfirm, isFalse);
    });
  });
}
