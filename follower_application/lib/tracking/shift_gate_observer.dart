import 'package:follower_application/tracking/shift_gate_coordinator.dart';
import 'package:flutter/material.dart';

/// Central navigator gate: operational routes require an Active Shift.
class ShiftGateObserver extends NavigatorObserver {
  ShiftGateObserver({ShiftGateCoordinator? coordinator})
      : _coordinator = coordinator ?? ShiftGateCoordinator();

  final ShiftGateCoordinator _coordinator;
  bool _enforcing = false;

  @override
  void didPush(Route<dynamic> route, Route<dynamic>? previousRoute) {
    _enforce(route);
  }

  @override
  void didReplace({Route<dynamic>? newRoute, Route<dynamic>? oldRoute}) {
    if (newRoute != null) _enforce(newRoute);
  }

  Future<void> _enforce(Route<dynamic> route) async {
    final name = route.settings.name;
    if (!ShiftGateCoordinator.isOperationalRoute(name)) return;
    if (_enforcing) return;
    _enforcing = true;
    try {
      final dest = await _coordinator.resolve(attachIfActive: true);
      if (dest == ShiftGateDestination.home) return;
      final nav = navigator;
      if (nav == null) return;
      final target = ShiftGateCoordinator.routeFor(dest);
      nav.pushNamedAndRemoveUntil(target, (r) => false);
    } finally {
      _enforcing = false;
    }
  }
}
