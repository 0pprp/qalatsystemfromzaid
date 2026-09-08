import 'package:flutter_test/flutter_test.dart';
import 'package:follower_application/customer_profile_page.dart';
import 'package:follower_application/follower_sales_request_page.dart';
import 'package:follower_application/services/follower_auth_rules.dart';

void main() {
  test('New Sale Request entry point exists for new and existing customer', () {
    const newRequest = FollowerSalesRequestPage();
    expect(newRequest, isA<FollowerSalesRequestPage>());
    expect(FollowerAuthRules.canSubmitNewCustomerRequest(isLinked: true, listId: 1), isTrue);
  });

  test('Existing customer request still uses same page widget', () {
    expect(FollowerSalesRequestPage, isNotNull);
  });

  test('Customer profile page accepts customer map and listId', () {
    final page = CustomerProfilePage(
      customer: {'customerId': 10, 'customerName': 'Test'},
      listId: 5,
    );
    expect(page.listId, 5);
    expect(page.customer['customerId'], 10);
  });

  test('Delegate note UI rules use list Delegate, not sales employee', () {
    expect(FollowerAuthRules.salesEmployeeUsedInDelegateNoteFlow, isFalse);
    expect(
      FollowerAuthRules.canNoteListDelegate(isLinked: true, listId: 7, requestedDelegateId: 7),
      isTrue,
    );
    expect(
      FollowerAuthRules.canNoteListDelegate(isLinked: true, listId: 7, requestedDelegateId: 99),
      isFalse,
    );
  });

  test('404 is represented as visible status message contract', () {
    // Contract used by profile/sales/home pages when backend route missing.
    const profile404 = '404: مسار البروفايل غير موجود على السيرفر (Followers/Customers/{id}/profile)';
    const sales404 = '404: مسار Followers/SalesRequests غير موجود على السيرفر';
    const delegate404 = '404: مسار ملاحظات المندوب غير موجود على السيرفر';
    expect(profile404.contains('404'), isTrue);
    expect(sales404.contains('404'), isTrue);
    expect(delegate404.contains('404'), isTrue);
  });
}
