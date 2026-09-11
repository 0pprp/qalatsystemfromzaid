import 'package:delegate_application/services/customer_lists_load_rules.dart';
import 'package:delegate_application/services/delegate_data_refresh_service.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('CustomerListsLoadRules', () {
    test('empty local lists should attempt refresh once', () {
      expect(
        CustomerListsLoadRules.shouldAttemptRefreshWhenLocalEmpty(
          localEmpty: true,
          alreadyAttemptedRefresh: false,
        ),
        isTrue,
      );
      expect(
        CustomerListsLoadRules.shouldAttemptRefreshWhenLocalEmpty(
          localEmpty: true,
          alreadyAttemptedRefresh: true,
        ),
        isFalse,
      );
    });

    test('lists appear when data exists (ready path)', () {
      const reps = [
        {'DelegateId': 1, 'DelegateName': 'قائمة أ'},
        {'DelegateId': 2, 'DelegateName': 'قائمة ب'},
      ];
      expect(reps, isNotEmpty);
      expect(
        CustomerListsLoadRules.emptyOrErrorMessage(
          loadError: false,
          localEmpty: reps.isEmpty,
        ),
        isEmpty,
      );
    });

    test('empty list shows empty state copy', () {
      expect(
        CustomerListsLoadRules.emptyOrErrorMessage(
          loadError: false,
          localEmpty: true,
        ),
        'لا توجد قوائم متاحة',
      );
      expect(
        CustomerListsLoadRules.showRetry(loadError: false, localEmpty: true),
        isTrue,
      );
    });

    test('failure shows retry copy', () {
      expect(
        CustomerListsLoadRules.emptyOrErrorMessage(
          loadError: true,
          localEmpty: false,
        ),
        'تعذر تحميل القوائم',
      );
      expect(
        CustomerListsLoadRules.showRetry(loadError: true, localEmpty: false),
        isTrue,
      );
    });

    test('selecting a list loads customers for that DelegateId', () {
      expect(
        CustomerListsLoadRules.selectionLoadsCustomers(
          selectedListId: '42',
          queriedDelegateId: 42,
        ),
        isTrue,
      );
      expect(
        CustomerListsLoadRules.selectionLoadsCustomers(
          selectedListId: '42',
          queriedDelegateId: 7,
        ),
        isFalse,
      );
    });

    test('IndexedStack tab open must reload lists', () {
      expect(
        CustomerListsLoadRules.tabOpenShouldReloadLists(true),
        isTrue,
      );
    });
  });

  group('DelegateDataRefreshService dataRevision', () {
    test('dataRevision notifier exists for Customer reload hook', () {
      final before = DelegateDataRefreshService.instance.dataRevision.value;
      DelegateDataRefreshService.instance.dataRevision.value = before + 1;
      expect(
        DelegateDataRefreshService.instance.dataRevision.value,
        before + 1,
      );
      // restore
      DelegateDataRefreshService.instance.dataRevision.value = before;
    });

    test('failed fetch must not imply local SelectDelegate wipe', () {
      // Contract: _replaceLocalAtomically only runs after successful snapshot.
      final replaceOnlyOnSuccess =
          DelegateDataRefreshRules.shouldReplaceLocal(
        fetchSucceeded: false,
        responseValid: false,
      );
      expect(replaceOnlyOnSuccess, isFalse);
    });
  });
}
