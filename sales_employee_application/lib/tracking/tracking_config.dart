class TrackingConfig {
  static const Duration movingInterval = Duration(seconds: 12);
  static const Duration stationaryInterval = Duration(seconds: 60);
  static const double minimumDistanceMeters = 20;
  static const int syncBatchSize = 200;
  static const int backendBatchLimit = 500;
  static const Duration syncInterval = Duration(seconds: 45);
  static const double maxAcceptedAccuracyMeters = 80;
}
