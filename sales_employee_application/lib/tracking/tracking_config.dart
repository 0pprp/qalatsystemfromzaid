class TrackingConfig {
  /// Internal GPS refresh only. Official points are persisted natively every [officialInterval].
  static const Duration movingInterval = Duration(seconds: 30);
  static const Duration _productionOfficialInterval = Duration(minutes: 10);
  static const Duration stationaryInterval = Duration(seconds: 45);
  static const double minimumDistanceMeters = 0;
  static const int syncBatchSize = 200;
  static const int backendBatchLimit = 500;
  static const Duration syncInterval = Duration(seconds: 20);
  /// Continuity over precision: ~50m (and a bit more) must still be kept.
  static const double maxAcceptedAccuracyMeters = 250;

  /// Temporary QA override. Production builds leave this at 0 (10 minutes).
  /// Example: `--dart-define=TRACKING_DEBUG_INTERVAL_MS=20000`
  static const int debugIntervalMs =
      int.fromEnvironment('TRACKING_DEBUG_INTERVAL_MS', defaultValue: 0);

  static bool get isDebugInterval => debugIntervalMs >= 5000;

  /// Official snapshot cadence. Always 10 minutes unless a debug define is set.
  static Duration get officialInterval => isDebugInterval
      ? Duration(milliseconds: debugIntervalMs)
      : _productionOfficialInterval;

  static int get officialIntervalMs => officialInterval.inMilliseconds;
}
