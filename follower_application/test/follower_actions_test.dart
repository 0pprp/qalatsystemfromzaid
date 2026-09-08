import 'package:flutter_test/flutter_test.dart';
import 'package:follower_application/services/follower_auth_rules.dart';

void main() {
  test('follower can add customer note inside assigned list', () {
    expect(FollowerAuthRules.canNoteCustomer(isLinked: true, customerDelegateId: 5, listId: 5), isTrue);
  });

  test('follower cannot edit or delete customer note after save', () {
    expect(FollowerAuthRules.canEditOrDeleteNote, isFalse);
  });

  test('follower can add employee note only inside assigned lists', () {
    expect(FollowerAuthRules.canNoteEmployee(isLinked: true, employeeOnList: true), isTrue);
    expect(FollowerAuthRules.canNoteEmployee(isLinked: true, employeeOnList: false), isFalse);
  });

  test('salesman cannot read follower employee notes', () {
    expect(FollowerAuthRules.salesmanCanReadEmployeeNotes, isFalse);
  });

  test('follower cannot note employee outside assigned scope', () {
    expect(FollowerAuthRules.canNoteEmployee(isLinked: false, employeeOnList: true), isFalse);
  });

  test('follower can send sales request in scope', () {
    expect(FollowerAuthRules.canSubmitSalesRequest(isLinked: true, customerDelegateId: 3, listId: 3), isTrue);
  });

  test('sales manager sees SourceType Follower constant', () {
    expect(FollowerAuthRules.sourceFollower, 'Follower');
  });

  test('follower cannot spoof CreatedByUserId', () {
    expect(FollowerAuthRules.resolveCreatedByUserId(authenticated: 77, claimed: 999), 77);
  });

  test('follower cannot submit request outside allowed scope', () {
    expect(FollowerAuthRules.canSubmitSalesRequest(isLinked: true, customerDelegateId: 9, listId: 3), isFalse);
  });
}
