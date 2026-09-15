import 'dart:convert';

import 'package:delegated_manager_application/core/models/paged_result.dart';
import 'package:delegated_manager_application/core/theme/app_theme.dart';
import 'package:delegated_manager_application/features/authentication/data/auth_repository.dart';
import 'package:delegated_manager_application/features/complaints/domain/complaint.dart';
import 'package:delegated_manager_application/features/dashboard/domain/dashboard_summary.dart';
import 'package:delegated_manager_application/features/exceptions/domain/sales_exception.dart';
import 'package:flutter_test/flutter_test.dart';

/// Payloads copied from the shapes returned by
/// `BE_SalesEmployee/Controllers/DelegatedManagerController.cs`.
void main() {
  group('login parsing', () {
    test('reads the central delegated-manager response', () {
      final json = jsonDecode('''
      {
        "token": "jwt-token",
        "expiration": "2026-09-16T00:00:00Z",
        "userId": 0,
        "userName": "المدير المفوض",
        "userType": "مدير مفوض",
        "cityLink": "",
        "cityName": "كل المحافظات",
        "cityValue": "",
        "central": true
      }''') as Map<String, dynamic>;

      final login = DelegatedManagerLogin.fromJson(json);

      expect(login.token, 'jwt-token');
      expect(login.userType, 'مدير مفوض');
      expect(login.isDelegatedManager, isTrue);
      expect(login.central, isTrue);
    });

    test('rejects a non delegated-manager user type', () {
      final login = DelegatedManagerLogin.fromJson(const {
        'token': 'jwt',
        'userName': 'مدير مبيعات',
        'userType': 'مدير مبيعات',
      });

      expect(login.isDelegatedManager, isFalse);
    });
  });

  group('dashboard parsing', () {
    test('reads unread complaints and exception counters', () {
      final json = jsonDecode('''
      {
        "unreadComplaints": 4,
        "exceptions": {
          "pending": 3,
          "approved": 7,
          "rejected": 2,
          "cancelled": 1,
          "total": 13
        }
      }''') as Map<String, dynamic>;

      final summary = DashboardSummary.fromJson(json);

      expect(summary.unreadComplaints, 4);
      expect(summary.pendingExceptions, 3);
      expect(summary.approvedExceptions, 7);
      expect(summary.rejectedExceptions, 2);
      expect(summary.cancelledExceptions, 1);
      expect(summary.totalExceptions, 13);
      expect(summary.hasWork, isTrue);
    });

    test('missing sections fall back to zero', () {
      final summary = DashboardSummary.fromJson(const {});

      expect(summary.unreadComplaints, 0);
      expect(summary.pendingExceptions, 0);
      expect(summary.hasWork, isFalse);
    });

    test('composed summary mirrors the fallback endpoints', () {
      final summary =
          DashboardSummary.compose(unreadComplaints: 2, pendingExceptions: 5);

      expect(summary.unreadComplaints, 2);
      expect(summary.pendingExceptions, 5);
      expect(summary.totalExceptions, 5);
    });
  });

  group('complaints parsing', () {
    test('reads the paged inbox and unread flags', () {
      final json = jsonDecode('''
      {
        "page": 1,
        "pageSize": 20,
        "totalCount": 2,
        "totalPages": 1,
        "items": [
          {
            "id": "11111111-1111-1111-1111-111111111111",
            "sourceApp": "delegate_application",
            "sourceType": "Complaint",
            "senderDisplayName": "مندوب النجف",
            "senderRole": "مندوب",
            "cityValue": "najaf-demo",
            "cityName": "النجف - DEMO",
            "subject": "شكوى تأخير",
            "status": "Unread",
            "createdAtUtc": "2026-09-14T08:00:00",
            "readAtUtc": null
          },
          {
            "id": "22222222-2222-2222-2222-222222222222",
            "sourceApp": "sales_employee_application",
            "sourceType": "Message",
            "senderDisplayName": "موظف مبيعات",
            "senderRole": "موظف",
            "cityValue": "babil-demo",
            "cityName": "بابل - DEMO",
            "subject": "استفسار",
            "status": "Read",
            "createdAtUtc": "2026-09-13T09:30:00",
            "readAtUtc": "2026-09-13T10:00:00"
          }
        ]
      }''');

      final page = PagedResult.fromJson(json, ComplaintSummary.fromJson);

      expect(page.totalCount, 2);
      expect(page.hasMore, isFalse);
      expect(page.items.first.isUnread, isTrue);
      expect(page.items.first.cityName, 'النجف - DEMO');
      expect(page.items.last.isUnread, isFalse);
      expect(page.items.last.readAtUtc?.toUtc(),
          DateTime.utc(2026, 9, 13, 10));
      expect(ComplaintStatuses.arabic(page.items.last.status), 'مقروءة');
    });

    test('reads the detail payload including body and metadata', () {
      final json = jsonDecode('''
      {
        "id": "11111111-1111-1111-1111-111111111111",
        "sourceApp": "delegate_application",
        "sourceType": "Complaint",
        "senderUserId": 42,
        "senderUserName": "rep1",
        "senderDisplayName": "مندوب النجف",
        "senderRole": "مندوب",
        "cityValue": "najaf-demo",
        "cityName": "النجف - DEMO",
        "subject": "شكوى تأخير",
        "body": "لم يتم تسليم الطلب",
        "status": "Unread",
        "createdAtUtc": "2026-09-14T08:00:00",
        "readAtUtc": null,
        "metadataJson": "{\\"requestId\\":99}"
      }''') as Map<String, dynamic>;

      final detail = ComplaintDetail.fromJson(json);

      expect(detail.id, '11111111-1111-1111-1111-111111111111');
      expect(detail.body, 'لم يتم تسليم الطلب');
      expect(detail.senderUserId, 42);
      expect(detail.senderUserName, 'rep1');
      expect(detail.metadataJson, '{"requestId":99}');
      expect(detail.summary.isUnread, isTrue);
    });

    test('missing fields use Arabic fallbacks', () {
      final detail = ComplaintDetail.fromJson(const {'id': 'x'});

      expect(detail.summary.subject, 'بدون عنوان');
      expect(detail.summary.senderDisplayName, 'غير محدد');
      expect(detail.body, '-');
      expect(detail.summary.cityName, isNull);
    });
  });

  group('exceptions parsing', () {
    test('reads the paged decision queue', () {
      final json = jsonDecode('''
      {
        "page": 1,
        "pageSize": 20,
        "totalCount": 25,
        "totalPages": 2,
        "items": [
          {
            "id": "33333333-3333-3333-3333-333333333333",
            "cityValue": "najaf-demo",
            "cityName": "النجف - DEMO",
            "customerId": 12,
            "customerName": "زبون تجريبي",
            "salesRequestId": 99,
            "requestingManagerDisplayName": "مدير مبيعات",
            "status": "Pending",
            "targetApproverType": "DelegatedManager",
            "requestedAtUtc": "2026-09-15T07:00:00",
            "decidedAtUtc": null
          }
        ]
      }''');

      final page = PagedResult.fromJson(json, ExceptionSummary.fromJson);

      expect(page.hasMore, isTrue);
      final item = page.items.single;
      expect(item.status, ExceptionStatuses.pending);
      expect(ExceptionStatuses.arabic(item.status), 'قيد الانتظار');
      expect(item.customerId, 12);
      expect(item.salesRequestId, 99);
      expect(item.decidedAtUtc, isNull);
    });

    test('reads the detail payload with the audit trail', () {
      final json = jsonDecode('''
      {
        "request": {
          "id": "33333333-3333-3333-3333-333333333333",
          "cityValue": "najaf-demo",
          "cityName": "النجف - DEMO",
          "customerId": 12,
          "customerName": "زبون تجريبي",
          "customerPhone": "07700000000",
          "salesRequestId": 99,
          "requestingManagerUserName": "sm1",
          "requestingManagerDisplayName": "مدير مبيعات",
          "reason": "الزبون لديه ملاحظة سابقة",
          "targetApproverType": "DelegatedManager",
          "status": "Approved",
          "requestedAtUtc": "2026-09-15T07:00:00",
          "decidedAtUtc": "2026-09-15T08:15:00",
          "decisionMakerUserName": "dm1",
          "decisionMakerDisplayName": "المدير المفوض",
          "decisionNote": "موافقة استثنائية",
          "branchCustomerNotePosted": true
        },
        "audit": [
          {
            "id": "44444444-4444-4444-4444-444444444444",
            "actorUserName": "dm1",
            "actorDisplayName": "المدير المفوض",
            "actorRole": "DelegatedManager",
            "previousStatus": "Pending",
            "newStatus": "Approved",
            "decisionNote": "موافقة استثنائية",
            "createdAtUtc": "2026-09-15T08:15:00"
          }
        ]
      }''') as Map<String, dynamic>;

      final detail = ExceptionDetail.fromJson(json);

      expect(detail.request.status, ExceptionStatuses.approved);
      expect(detail.request.customerPhone, '07700000000');
      expect(detail.request.reason, 'الزبون لديه ملاحظة سابقة');
      expect(detail.request.branchCustomerNotePosted, isTrue);
      expect(detail.request.summary.decidedAtUtc?.toUtc(),
          DateTime.utc(2026, 9, 15, 8, 15));
      expect(detail.audit.single.previousStatus, ExceptionStatuses.pending);
      expect(detail.audit.single.newStatus, ExceptionStatuses.approved);
    });

    test('decision response parses as a bare request object', () {
      final request = ExceptionRequest.fromJson(const {
        'id': '33333333-3333-3333-3333-333333333333',
        'status': 'Rejected',
        'customerName': 'زبون تجريبي',
        'decisionNote': 'مرفوض لعدم اكتمال البيانات',
      });

      expect(request.status, ExceptionStatuses.rejected);
      expect(ExceptionStatuses.arabic(request.status), 'مرفوض');
      expect(request.decisionNote, 'مرفوض لعدم اكتمال البيانات');
      expect(request.summary.requestedAtUtc, isNull);
    });

    test('empty audit list and missing sections are tolerated', () {
      final detail = ExceptionDetail.fromJson(const {});

      expect(detail.audit, isEmpty);
      expect(detail.request.summary.customerName, 'غير محدد');
    });
  });
}
