import 'package:delegated_manager_application/features/authentication/presentation/login_screen.dart';
import 'package:delegated_manager_application/features/complaints/presentation/complaint_detail_screen.dart';
import 'package:delegated_manager_application/features/exceptions/presentation/exception_detail_screen.dart';
import 'package:delegated_manager_application/features/shell/presentation/home_shell.dart';
import 'package:flutter/material.dart';

class AppRoutes {
  static const String login = '/login';
  static const String home = '/home';
  static const String complaintDetail = '/complaints/detail';
  static const String exceptionDetail = '/exceptions/detail';

  /// Named routes are used for deep entry points; in-feature navigation pushes
  /// screens directly so results (e.g. a recorded decision) can flow back.
  static Route<dynamic>? generate(RouteSettings settings) {
    switch (settings.name) {
      case login:
        return MaterialPageRoute(
          settings: settings,
          builder: (_) => const LoginScreen(),
        );
      case home:
        return MaterialPageRoute(
          settings: settings,
          builder: (_) => const HomeShell(),
        );
      case complaintDetail:
        final id = settings.arguments?.toString() ?? '';
        return MaterialPageRoute(
          settings: settings,
          builder: (_) => ComplaintDetailScreen(complaintId: id),
        );
      case exceptionDetail:
        final id = settings.arguments?.toString() ?? '';
        return MaterialPageRoute(
          settings: settings,
          builder: (_) => ExceptionDetailScreen(requestId: id),
        );
    }
    return null;
  }
}
