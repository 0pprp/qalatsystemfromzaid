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

  /// Iraqi mobile: exactly 11 digits, must start with 07.
  static bool isValidFollowerPhone(String? phone) {
    final digits = (phone ?? '').trim().replaceAll(RegExp(r'\s+'), '');
    return RegExp(r'^07\d{9}$').hasMatch(digits);
  }

  static String? phoneValidationMessage(String? phone) {
    final digits = (phone ?? '').trim().replaceAll(RegExp(r'\s+'), '');
    if (digits.isEmpty) return 'رقم الهاتف مطلوب';
    if (!digits.startsWith('07')) return 'يجب أن يبدأ الرقم بـ 07';
    if (digits.length != 11) return 'يجب أن يكون الرقم 11 خانة بالضبط';
    if (!RegExp(r'^07\d{9}$').hasMatch(digits)) return 'رقم الهاتف غير صالح';
    return null;
  }

  /// Province is server-owned from follower account; client value is ignored.
  static String? resolveProvince({
    required String? followerCityName,
    String? clientProvince,
  }) =>
      (followerCityName ?? '').trim().isEmpty ? null : followerCityName!.trim();

  static String? buildImageUrl({required String apiBase, String? fileName}) {
    if (fileName == null || fileName.trim().isEmpty) return null;
    if (fileName.startsWith('http')) return fileName;
    final images = apiBase.replaceFirst(RegExp(r'api/?$'), 'Images/');
    final leaf = fileName.replaceAll('\\', '/').split('/').last;
    return '$images$leaf';
  }
}
