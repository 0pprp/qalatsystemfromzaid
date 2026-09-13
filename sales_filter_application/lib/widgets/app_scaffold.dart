import 'package:flutter/material.dart';

/// Body inset that clears Android system navigation / gesture bars even when
/// [MediaQuery.padding].bottom is 0 (edge-to-edge), by flooring SafeArea with
/// [MediaQuery.viewPadding].
class AppSafeBody extends StatelessWidget {
  const AppSafeBody({
    super.key,
    this.top = true,
    this.bottom = true,
    this.left = true,
    this.right = true,
    required this.child,
  });

  final bool top;
  final bool bottom;
  final bool left;
  final bool right;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    final view = MediaQuery.viewPaddingOf(context);
    return SafeArea(
      top: top,
      bottom: bottom,
      left: left,
      right: right,
      minimum: EdgeInsets.only(
        top: top ? view.top : 0,
        bottom: bottom ? view.bottom : 0,
        left: left ? view.left : 0,
        right: right ? view.right : 0,
      ),
      child: child,
    );
  }
}

/// Shared scaffold for filter app screens: RTL + bottom (and optional top) safe insets.
class FilterScaffold extends StatelessWidget {
  const FilterScaffold({
    super.key,
    this.appBar,
    required this.body,
    this.floatingActionButton,
    this.resizeToAvoidBottomInset = true,
  });

  final PreferredSizeWidget? appBar;
  final Widget body;
  final Widget? floatingActionButton;
  final bool resizeToAvoidBottomInset;

  @override
  Widget build(BuildContext context) {
    return Directionality(
      textDirection: TextDirection.rtl,
      child: Scaffold(
        appBar: appBar,
        floatingActionButton: floatingActionButton,
        resizeToAvoidBottomInset: resizeToAvoidBottomInset,
        body: AppSafeBody(
          // AppBar already clears the status bar; login-style screens need top inset.
          top: appBar == null,
          bottom: true,
          child: body,
        ),
      ),
    );
  }
}
