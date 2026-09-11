import 'package:flutter/material.dart';

/// Shared safe-area + scaffold helpers (no double padding).
class AppInsets {
  /// Extra scroll space so content clears the bottom navigation bar.
  static const double bottomNavContentClearance = 72;

  static EdgeInsets scrollPadding(
    BuildContext context, {
    double horizontal = 20,
    double top = 20,
    double extraBottom = bottomNavContentClearance,
  }) {
    final bottomSafe = MediaQuery.viewPaddingOf(context).bottom;
    return EdgeInsets.fromLTRB(
      horizontal,
      top,
      horizontal,
      extraBottom + (bottomSafe > 0 ? 0 : 8),
    );
  }
}

/// Scaffold that applies SafeArea once, correctly with AppBar / bottom bar.
class AppSafeScaffold extends StatelessWidget {
  const AppSafeScaffold({
    super.key,
    required this.body,
    this.appBar,
    this.floatingActionButton,
    this.floatingActionButtonLocation,
    this.bottomNavigationBar,
    this.backgroundColor,
    this.extendBody = false,
    this.resizeToAvoidBottomInset = true,
    /// When true and [appBar] is null, pads status bar / notch.
    this.safeTop = true,
    /// When true and [bottomNavigationBar] is null, pads system nav bar.
    this.safeBottom = true,
    this.wrapBottomNavSafeArea = true,
  });

  final Widget body;
  final PreferredSizeWidget? appBar;
  final Widget? floatingActionButton;
  final FloatingActionButtonLocation? floatingActionButtonLocation;
  final Widget? bottomNavigationBar;
  final Color? backgroundColor;
  final bool extendBody;
  final bool resizeToAvoidBottomInset;
  final bool safeTop;
  final bool safeBottom;
  final bool wrapBottomNavSafeArea;

  @override
  Widget build(BuildContext context) {
    final hasAppBar = appBar != null;
    final hasBottomNav = bottomNavigationBar != null;

    Widget? nav = bottomNavigationBar;
    if (hasBottomNav && wrapBottomNavSafeArea) {
      nav = SafeArea(
        top: false,
        maintainBottomViewPadding: true,
        child: bottomNavigationBar!,
      );
    }

    return Scaffold(
      backgroundColor: backgroundColor,
      appBar: appBar,
      extendBody: extendBody,
      resizeToAvoidBottomInset: resizeToAvoidBottomInset,
      floatingActionButton: floatingActionButton,
      floatingActionButtonLocation: floatingActionButtonLocation,
      bottomNavigationBar: nav,
      body: SafeArea(
        top: safeTop && !hasAppBar,
        bottom: safeBottom && !hasBottomNav,
        child: body,
      ),
    );
  }
}
