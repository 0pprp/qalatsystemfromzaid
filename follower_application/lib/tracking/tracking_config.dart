class TrackingConfig {
  static const Duration movingInterval = Duration(seconds: 20);
  static const Duration _productionOfficialInterval = Duration(minutes: 10);
  static const Duration stationaryInterval = Duration(seconds: 45);
  static const double minimumDistanceMeters = 0;
  static const int syncBatchSize = 200;
  static const int backendBatchLimit = 500;
  static const Duration syncInterval = Duration(seconds: 20);
  static const double maxAcceptedAccuracyMeters = 250;

  static const int debugIntervalMs =
      int.fromEnvironment('TRACKING_DEBUG_INTERVAL_MS', defaultValue: 0);

  static bool get isDebugInterval => debugIntervalMs >= 5000;

  static Duration get officialInterval => isDebugInterval
      ? Duration(milliseconds: debugIntervalMs)
      : _productionOfficialInterval;

  static int get officialIntervalMs => officialInterval.inMilliseconds;
}
