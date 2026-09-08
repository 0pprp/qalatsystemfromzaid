/// Client-side mirror of follower authorization expectations (server enforces).
class FollowerAuthRules {
  static const sourceFollower = 'Follower';
  static const canEditOrDeleteNote = false;
  static const salesmanCanReadEmployeeNotes = false;
  static const salesEmployeeUsedInDelegateNoteFlow = false;

  static bool canNoteCustomer({
    required bool isLinked,
    required int customerDelegateId,
    required int listId,
  }) =>
      isLinked && listId > 0 && customerDelegateId == listId;

  static bool canNoteListDelegate({
    required bool isLinked,
    required int listId,
    required int requestedDelegateId,
  }) =>
      isLinked && listId > 0 && requestedDelegateId == listId;

  static bool canSubmitNewCustomerRequest({
    required bool isLinked,
    required int listId,
  }) =>
      isLinked && listId > 0;

  static int resolveCreatedByUserId({required int authenticated, int? claimed}) =>
      authenticated;

  static String? buildImageUrl({required String apiBase, String? fileName}) {
    if (fileName == null || fileName.trim().isEmpty) return null;
    if (fileName.startsWith('http')) return fileName;
    final images = apiBase.replaceFirst(RegExp(r'api/?$'), 'Images/');
    final leaf = fileName.replaceAll('\\', '/').split('/').last;
    return '$images$leaf';
  }
}
