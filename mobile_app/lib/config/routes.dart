import 'package:flutter/material.dart';
import '../screens/login/login_screen.dart';
import '../screens/home_shell.dart';
import '../screens/customers/customers_list_screen.dart';
import '../screens/customers/customers_archived_screen.dart';
import '../screens/customers/customers_stopped_date_screen.dart';
import '../screens/payments/payments_total_screen.dart';
import '../screens/payments/payments_week_screen.dart';
import '../screens/payments/payments_month_screen.dart';
import '../screens/payments/payments_date_tracking_screen.dart';
import '../screens/payments/payment_requests_screen.dart';
import '../screens/sales/sales_list_screen.dart';
import '../screens/statistics/agents_statistics_screen.dart';
import '../screens/statistics/no_statistics_screen.dart';
import '../screens/delegates/delegates_list_screen.dart';
import '../screens/inventory/warehouses_list_screen.dart';
import '../screens/inventory/items_list_screen.dart';
import '../screens/inventory/suppliers_list_screen.dart';
import '../screens/inventory/purchases_list_screen.dart';
import '../screens/finance/cash_registers_screen.dart';
import '../screens/finance/cash_deposits_screen.dart';
import '../screens/finance/cash_withdrawals_screen.dart';
import '../screens/finance/cash_transfers_screen.dart';
import '../screens/settings/employees_list_screen.dart';
import '../screens/settings/users_list_screen.dart';
import '../screens/settings/permission_list_screen.dart';
import '../screens/settings/backup_database_screen.dart';
import '../screens/settings/expense_items_screen.dart';
import '../screens/daily_followup/daily_followup_screen.dart';
import '../screens/daily_followup/daily_followup_print_screen.dart';

class AppRoutes {
  static Route<dynamic> generateRoute(RouteSettings settings) {
    switch (settings.name) {
      case '/login':
        return _buildRoute(const LoginScreen(), settings);
      case '/dashboard':
        return _buildRoute(const HomeShell(), settings);
      case '/customers-list':
        return _buildRoute(const CustomersListScreen(), settings);
      case '/customers-archived':
        return _buildRoute(const CustomersArchivedScreen(), settings);
      case '/customers-stopped-date':
        return _buildRoute(const CustomersStoppedDateScreen(), settings);
      case '/payments-total':
        return _buildRoute(const PaymentsTotalScreen(), settings);
      case '/payments-week':
        return _buildRoute(const PaymentsWeekScreen(), settings);
      case '/payments-month':
        return _buildRoute(const PaymentsMonthScreen(), settings);
      case '/payments-date-tracking':
        return _buildRoute(const PaymentsDateTrackingScreen(), settings);
      case '/payment-requests':
        return _buildRoute(const PaymentRequestsScreen(), settings);
      case '/sales-list':
        return _buildRoute(const SalesListScreen(), settings);
      case '/agents-statistics':
        return _buildRoute(const AgentsStatisticsScreen(), settings);
      case '/no-statistics':
        return _buildRoute(const NoStatisticsScreen(), settings);
      case '/delegates-list':
        return _buildRoute(const DelegatesListScreen(), settings);
      case '/warehouses-list':
        return _buildRoute(const WarehousesListScreen(), settings);
      case '/items-list':
        return _buildRoute(const ItemsListScreen(), settings);
      case '/suppliers-list':
        return _buildRoute(const SuppliersListScreen(), settings);
      case '/purchases-list':
        return _buildRoute(const PurchasesListScreen(), settings);
      case '/cash-registers':
        return _buildRoute(const CashRegistersScreen(), settings);
      case '/cash-deposits':
        return _buildRoute(const CashDepositsScreen(), settings);
      case '/cash-withdrawals':
        return _buildRoute(const CashWithdrawalsScreen(), settings);
      case '/cash-transfers':
        return _buildRoute(const CashTransfersScreen(), settings);
      case '/employees-list':
        return _buildRoute(const EmployeesListScreen(), settings);
      case '/users-list':
        return _buildRoute(const UsersListScreen(), settings);
      case '/permission-list':
        return _buildRoute(const PermissionListScreen(), settings);
      case '/backup-database':
        return _buildRoute(const BackupDatabaseScreen(), settings);
      case '/expense-items':
        return _buildRoute(const ExpenseItemsScreen(), settings);
      case '/daily-followup':
        return _buildRoute(const DailyFollowupScreen(), settings);
      case '/daily-followup-print':
        final args = settings.arguments as Map<String, dynamic>? ?? {};
        return _buildRoute(DailyFollowupPrintScreen(queryParams: args), settings);
      default:
        return _buildRoute(const LoginScreen(), settings);
    }
  }

  static PageRouteBuilder _buildRoute(Widget page, RouteSettings settings) {
    return PageRouteBuilder(
      settings: settings,
      pageBuilder: (context, animation, secondaryAnimation) => page,
      transitionsBuilder: (context, animation, secondaryAnimation, child) {
        // Slide + fade for professional navigation feel
        const begin = Offset(0.08, 0.0);
        const end = Offset.zero;
        final tween = Tween(begin: begin, end: end).chain(CurveTween(curve: Curves.easeOutCubic));
        final fadeTween = Tween<double>(begin: 0.0, end: 1.0).chain(CurveTween(curve: Curves.easeOut));
        return SlideTransition(
          position: animation.drive(tween),
          child: FadeTransition(opacity: animation.drive(fadeTween), child: child),
        );
      },
      transitionDuration: const Duration(milliseconds: 350),
      reverseTransitionDuration: const Duration(milliseconds: 280),
    );
  }
}
