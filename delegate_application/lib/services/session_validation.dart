/// Explicit session validation outcome — never treat network failure as Valid.
enum SessionValidation {
  valid,
  invalid,
  unverified,
}

extension SessionValidationX on SessionValidation {
  /// Offline/unverified may keep UI session, but must not authorize destructive wipe.
  bool get allowsSensitiveCachedUse =>
      this == SessionValidation.valid || this == SessionValidation.unverified;

  bool get isDefinitelyInvalid => this == SessionValidation.invalid;
}
