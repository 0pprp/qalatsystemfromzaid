class TrackingConfig {
  /// Internal GPS refresh only. Official points are persisted natively every [officialInterval].
  static const Duration movingInterval = Duration(seconds: 30);
  static const Duration officialInterval = Duration(minutes: 10);
  static const Duration stationaryInterval = Duration(seconds: 45);
  static const double minimumDistanceMeters = 0;
  static const int syncBatchSize = 200;
  static const int backendBatchLimit = 500;
  static const Duration syncInterval = Duration(seconds: 20);
  /// Continuity over precision: ~50m (and a bit more) must still be kept.
  static const double maxAcceptedAccuracyMeters = 250;
}
