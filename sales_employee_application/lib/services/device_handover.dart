import 'package:sales_employee_application/data/sales_repository_factory.dart';
import 'package:sales_employee_application/services/app_state.dart';
import 'package:sales_employee_application/services/gps_queue.dart';
import 'package:sales_employee_application/services/session.dart';
import 'package:sales_employee_application/services/tracker.dart';
import 'package:sales_employee_application/tracking/shift_tracking_controller.dart';
import 'package:sales_employee_application/tracking/tracking_channel.dart';

class DeviceHandover {
  static Future<void> kickPreviousUser({bool endServerShift = true}) async {
    if (endServerShift && (Session.token ?? '').isNotEmpty) {
      try {
        await SalesRepositoryFactory.instance.endShift();
      } catch (_) {}
    }
    try {
      await Tracker.instance.stop();
    } catch (_) {}
    try {
      await TrackingChannel.stop();
    } catch (_) {}
    try {
      await TrackingRuntime.instance?.dispose();
    } catch (_) {}
    TrackingRuntime.instance = null;
    try {
      await GpsQueue.instance.clear();
    } catch (_) {}
    AppState.instance.resetForUserSwitch();
    await Session.logout();
  }
}
