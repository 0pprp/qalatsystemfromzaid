import 'package:follower_application/AsyncIdChecker.dart';
import 'package:follower_application/services/follower_tracking_repository.dart';
import 'package:follower_application/services/follower_tracking_session.dart';
import 'package:follower_application/tracking/shift_tracking_controller.dart';
import 'package:follower_application/tracking/work_shift.dart';

enum ShiftGateDestination { login, startShift, home }

/// Central shift/session resolver — Backend is source of truth for Active Shift,
/// but a network timeout must never kick a user who already has a local active shift.
class ShiftGateCoordinator {
  ShiftGateCoordinator({
    FollowerTrackingRepository? repository,
    ShiftTrackingController? tracking,
  })  : _repo = repository ?? ApiFollowerTrackingRepository(),
        _tracking = tracking;

  final FollowerTrackingRepository _repo;
  final ShiftTrackingController? _tracking;

  /// Only the home shell needs a full gate. Sub-pages inherit an already-attached shift.
  /// Re-checking currentShift on every CustomerProfile push caused ~20s freezes / black screens.
  static const operationalRoutes = <String>{
    '/HomePage',
  };

  static DateTime? _lastHomeOkAt;
  static const _homeOkTtl = Duration(seconds: 90);

  static bool isOperationalRoute(String? name) =>
      name != null && operationalRoutes.contains(name);

  static void markHomeOk() => _lastHomeOkAt = DateTime.now();

  static bool get recentlyConfirmedHome {
    final at = _lastHomeOkAt;
    if (at == null) return false;
    return DateTime.now().difference(at) < _homeOkTtl;
  }

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

    final tracking = _tracking ?? TrackingRuntime.instance;
    final memoryShift = tracking?.activeShift;
    if (memoryShift != null && memoryShift.isActive) {
      if (attachIfActive) {
        try {
          await tracking?.attach(memoryShift);
        } catch (_) {}
      }
      markHomeOk();
      return ShiftGateDestination.home;
    }

    WorkShift? remote;
    var remoteLookupFailed = false;
    try {
      remote = await _repo.currentShift();
    } catch (_) {
      // Network / timeout: never invent "no shift" — keep local active if any.
      remoteLookupFailed = true;
      remote = null;
    }

    if (remoteLookupFailed) {
      final local =
          TrackingShiftPolicy.parseLocal(await FollowerTrackingSession.shift);
      if (local != null && local.isActive) {
        if (attachIfActive) {
          try {
            await tracking?.attach(local);
          } catch (_) {}
        }
        markHomeOk();
        return ShiftGateDestination.home;
      }
      // No local shift either — stay where we are only if we recently confirmed home.
      if (recentlyConfirmedHome) {
        return ShiftGateDestination.home;
      }
      return ShiftGateDestination.startShift;
    }

    if (remote == null || !remote.isActive) {
      tracking?.activeShift = null;
      try {
        await FollowerTrackingSession.clearShift();
      } catch (_) {}
      return ShiftGateDestination.startShift;
    }

    if (attachIfActive) {
      try {
        await tracking?.attach(remote);
      } catch (_) {
        // Still allow Home if shift is active; native may retry.
      }
    }
    markHomeOk();
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
