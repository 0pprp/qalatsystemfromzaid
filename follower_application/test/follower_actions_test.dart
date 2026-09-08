import 'package:flutter_test/flutter_test.dart';
import 'package:follower_application/services/follower_auth_rules.dart';

void main() {
  test('follower can create sales request for NEW customer on assigned list', () {
    expect(FollowerAuthRules.canSubmitNewCustomerRequest(isLinked: true, listId: 3), isTrue);
    expect(FollowerAuthRules.canSubmitNewCustomerRequest(isLinked: false, listId: 3), isFalse);
  });

  test('follower can create sales request for existing customer in scope', () {
    expect(
      FollowerAuthRules.canNoteCustomer(isLinked: true, customerDelegateId: 3, listId: 3),
      isTrue,
    );
  });

  test('new request source is Follower', () {
    expect(FollowerAuthRules.sourceFollower, 'Follower');
  });

  test('server controls CreatedByUserId', () {
    expect(FollowerAuthRules.resolveCreatedByUserId(authenticated: 77, claimed: 999), 77);
  });

  test('customer outside assigned scope denied', () {
    expect(
      FollowerAuthRules.canNoteCustomer(isLinked: true, customerDelegateId: 9, listId: 3),
      isFalse,
    );
  });

  test('follower can add customer note', () {
    expect(
      FollowerAuthRules.canNoteCustomer(isLinked: true, customerDelegateId: 5, listId: 5),
      isTrue,
    );
  });

  test('follower can add Delegate note only for assigned list Delegate', () {
    expect(FollowerAuthRules.canNoteListDelegate(isLinked: true, listId: 12, requestedDelegateId: 12), isTrue);
    expect(FollowerAuthRules.canNoteListDelegate(isLinked: true, listId: 12, requestedDelegateId: 99), isFalse);
  });

  test('Sales Employee is not used in this note flow', () {
    expect(FollowerAuthRules.salesEmployeeUsedInDelegateNoteFlow, isFalse);
  });

  test('profile image url uses Images folder', () {
    expect(
      FollowerAuthRules.buildImageUrl(apiBase: 'http://x/api/', fileName: 'c.jpg'),
      'http://x/Images/c.jpg',
    );
  });
}
