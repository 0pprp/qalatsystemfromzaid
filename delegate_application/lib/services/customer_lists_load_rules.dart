/// Pure rules for Customer lists dropdown (SelectDelegate cache).
class CustomerListsLoadRules {
  /// IndexedStack creates Customer before refresh may finish — empty cache
  /// is not a permanent failure; caller should retry after refresh.
  static bool shouldAttemptRefreshWhenLocalEmpty({
    required bool localEmpty,
    required bool alreadyAttemptedRefresh,
  }) =>
      localEmpty && !alreadyAttemptedRefresh;

  static bool showRetry({
    required bool loadError,
    required bool localEmpty,
  }) =>
      loadError || localEmpty;

  static String emptyOrErrorMessage({
    required bool loadError,
    required bool localEmpty,
  }) {
    if (loadError) return 'تعذر تحميل القوائم';
    if (localEmpty) return 'لا توجد قوائم متاحة';
    return '';
  }

  /// Selecting a list id should drive customer query by DelegateId.
  static bool selectionLoadsCustomers({
    required String? selectedListId,
    required int queriedDelegateId,
  }) {
    if (selectedListId == null) return false;
    return int.tryParse(selectedListId) == queriedDelegateId;
  }

  /// Opening Customers tab in IndexedStack must trigger reload even if
  /// initState already ran with an empty SelectDelegate.
  static bool tabOpenShouldReloadLists(bool indexedStackKeepsAlive) =>
      indexedStackKeepsAlive;
}
