/// Mirrors `BE_SalesEmployee.DelegatedManager.Domain.MobileUpdateKind`.
enum MobileUpdateKind { none, optional, mandatory }

class MobileUpdateRules {
  static MobileUpdateKind evaluate({
    required int currentVersionCode,
    required int latestVersionCode,
    required int minimumSupportedVersionCode,
    required bool forceUpdate,
  }) {
    if (currentVersionCode >= latestVersionCode) return MobileUpdateKind.none;
    if (currentVersionCode < minimumSupportedVersionCode) {
      return MobileUpdateKind.mandatory;
    }
    if (forceUpdate) return MobileUpdateKind.mandatory;
    return MobileUpdateKind.optional;
  }

  static MobileUpdateKind parseKind(String? value) {
    switch (value?.trim().toLowerCase()) {
      case 'none':
        return MobileUpdateKind.none;
      case 'optional':
        return MobileUpdateKind.optional;
      case 'mandatory':
        return MobileUpdateKind.mandatory;
    }
    return MobileUpdateKind.none;
  }
}
