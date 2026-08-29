import 'package:flutter/material.dart';

/// Directional page transitions for professional navigation feel.
class AppPageTransitions {
  AppPageTransitions._();

  /// Slide + fade for forward navigation.
  /// In RTL (Arabic), slides from right → left.
  static PageRouteBuilder slideForward(Widget page, {String? routeName}) {
    return PageRouteBuilder(
      settings: RouteSettings(name: routeName),
      pageBuilder: (_, animation, __) => page,
      transitionsBuilder: (_, animation, __, child) {
        const begin = Offset(0.12, 0.0); // subtle slide
        const end = Offset.zero;
        final tween = Tween(begin: begin, end: end).chain(CurveTween(curve: Curves.easeOutCubic));
        final fadeTween = Tween<double>(begin: 0.0, end: 1.0).chain(CurveTween(curve: Curves.easeOut));

        return SlideTransition(
          position: animation.drive(tween),
          child: FadeTransition(opacity: animation.drive(fadeTween), child: child),
        );
      },
      transitionDuration: const Duration(milliseconds: 350),
      reverseTransitionDuration: const Duration(milliseconds: 250),
    );
  }

  /// Scale + fade for dialogs and modals.
  static PageRouteBuilder scaleUp(Widget page, {String? routeName}) {
    return PageRouteBuilder(
      settings: RouteSettings(name: routeName),
      pageBuilder: (_, animation, __) => page,
      transitionsBuilder: (_, animation, __, child) {
        final scaleTween = Tween<double>(begin: 0.95, end: 1.0).chain(CurveTween(curve: Curves.easeOutBack));
        final fadeTween = Tween<double>(begin: 0.0, end: 1.0).chain(CurveTween(curve: Curves.easeOut));

        return FadeTransition(
          opacity: animation.drive(fadeTween),
          child: ScaleTransition(scale: animation.drive(scaleTween), child: child),
        );
      },
      transitionDuration: const Duration(milliseconds: 400),
      reverseTransitionDuration: const Duration(milliseconds: 300),
    );
  }

  /// Simple fade — used by default in routes.dart.
  static PageRouteBuilder fade(Widget page, {String? routeName}) {
    return PageRouteBuilder(
      settings: RouteSettings(name: routeName),
      pageBuilder: (_, animation, __) => page,
      transitionsBuilder: (_, animation, __, child) {
        return FadeTransition(opacity: animation.drive(CurveTween(curve: Curves.easeInOut)), child: child);
      },
      transitionDuration: const Duration(milliseconds: 300),
    );
  }
}
