/// Client-side mirror of follower authorization expectations (server enforces).
class FollowerAuthRules {
  static const sourceFollower = 'Follower';
  static const canEditOrDeleteNote = false;
  static const salesmanCanReadEmployeeNotes = false;

  static bool canNoteCustomer({
    required bool isLinked,
    required int customerDelegateId,
    required int listId,
  }) =>
      isLinked && listId > 0 && customerDelegateId == listId;

  static bool canNoteEmployee({
    required bool isLinked,
    required bool employeeOnList,
  }) =>
      isLinked && employeeOnList;

  static bool canSubmitSalesRequest({
    required bool isLinked,
    required int customerDelegateId,
    required int listId,
  }) =>
      canNoteCustomer(
        isLinked: isLinked,
        customerDelegateId: customerDelegateId,
        listId: listId,
      );

  static int resolveCreatedByUserId({required int authenticated, int? claimed}) =>
      authenticated;
}
