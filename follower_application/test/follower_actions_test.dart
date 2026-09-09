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

  test('phone valid: starts with 07 and length 11', () {
    expect(FollowerAuthRules.isValidFollowerPhone('07701234567'), isTrue);
    expect(FollowerAuthRules.phoneValidationMessage('07701234567'), isNull);
  });

  test('phone invalid: shorter or longer than 11', () {
    expect(FollowerAuthRules.isValidFollowerPhone('0770123456'), isFalse);
    expect(FollowerAuthRules.isValidFollowerPhone('077012345678'), isFalse);
  });

  test('phone invalid: does not start with 07', () {
    expect(FollowerAuthRules.isValidFollowerPhone('08701234567'), isFalse);
    expect(FollowerAuthRules.phoneValidationMessage('08701234567'), contains('07'));
  });

  test('province is resolved from follower city; client value ignored', () {
    expect(
      FollowerAuthRules.resolveProvince(followerCityName: 'الناصرية', clientProvince: 'بغداد'),
      'الناصرية',
    );
    expect(
      FollowerAuthRules.resolveProvince(followerCityName: 'النجف', clientProvince: null),
      'النجف',
    );
  });
}
