import 'package:follower_application/AsyncIdChecker.dart';
import 'package:follower_application/services/follower_tracking_repository.dart';
import 'package:follower_application/services/follower_tracking_session.dart';
import 'package:follower_application/tracking/shift_tracking_controller.dart';
import 'package:follower_application/tracking/work_shift.dart';

enum ShiftGateDestination { login, startShift, home }

/// Central shift/session resolver — Backend is source of truth for Active Shift.
class ShiftGateCoordinator {
  ShiftGateCoordinator({
    FollowerTrackingRepository? repository,
    ShiftTrackingController? tracking,
  })  : _repo = repository ?? ApiFollowerTrackingRepository(),
        _tracking = tracking;

  final FollowerTrackingRepository _repo;
  final ShiftTrackingController? _tracking;

  static const operationalRoutes = <String>{
    '/HomePage',
    '/CustomerProfile',
    '/FollowerSalesRequest',
  };

  static bool isOperationalRoute(String? name) =>
      name != null && operationalRoutes.contains(name);

  /// Resolves next shell without flashing Home before the check completes.
  Future<ShiftGateDestination> resolve({bool attachIfActive = true}) async {
    final loggedIn = await AsyncIdChecker.isLoggedIn();
    if (!loggedIn) return ShiftGateDestination.login;

    final sessionOk = await AsyncIdChecker.checkAsyncId();
    if (!sessionOk) {
      await AsyncIdChecker.logout();
      await FollowerTrackingSession.clearShift();
      return ShiftGateDestination.login;
    }

    WorkShift? remote;
    try {
      remote = await _repo.currentShift();
    } catch (_) {
      // Network failure: do not invent an active shift from stale local only.
      remote = null;
    }

    if (remote == null || !remote.isActive) {
      final tracking = _tracking ?? TrackingRuntime.instance;
      tracking?.activeShift = null;
      try {
        await FollowerTrackingSession.clearShift();
      } catch (_) {}
      return ShiftGateDestination.startShift;
    }

    if (attachIfActive) {
      final tracking = _tracking ?? TrackingRuntime.instance;
      try {
        await tracking?.attach(remote);
      } catch (_) {
        // Still allow Home if shift is active; native may retry.
      }
    }
    return ShiftGateDestination.home;
  }

  static String routeFor(ShiftGateDestination dest) {
    switch (dest) {
      case ShiftGateDestination.login:
        return '/Login';
      case ShiftGateDestination.startShift:
        return '/StartShift';
      case ShiftGateDestination.home:
        return '/HomePage';
    }
  }
}
