class TrackingConfig {
  static const Duration movingInterval = Duration(seconds: 3);
  static const Duration stationaryInterval = Duration(seconds: 45);
  static const double minimumDistanceMeters = 5;
  static const int syncBatchSize = 200;
  static const int backendBatchLimit = 500;
  static const Duration syncInterval = Duration(seconds: 20);
  static const double maxAcceptedAccuracyMeters = 45;
}
