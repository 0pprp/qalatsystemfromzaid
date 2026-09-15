import 'package:delegated_manager_application/core/auth/session.dart';
import 'package:delegated_manager_application/core/network/api_client.dart';
import 'package:delegated_manager_application/core/theme/app_theme.dart';
import 'package:delegated_manager_application/core/widgets/state_views.dart';
import 'package:delegated_manager_application/features/dashboard/data/dashboard_repository.dart';
import 'package:delegated_manager_application/features/dashboard/domain/dashboard_summary.dart';
import 'package:flutter/material.dart';

class DashboardScreen extends StatefulWidget {
  const DashboardScreen({super.key, required this.onOpenTab});

  /// Tab index requested from a dashboard card (1 = inbox, 2 = exceptions).
  final void Function(int index) onOpenTab;

  @override
  State<DashboardScreen> createState() => DashboardScreenState();
}

class DashboardScreenState extends State<DashboardScreen> {
  DashboardSummary? _summary;
  bool _loading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    reload();
  }

  Future<void> reload() async {
    setState(() {
      _loading = _summary == null;
      _error = null;
    });
    try {
      final summary = await DashboardRepository.load();
      if (!mounted) return;
      setState(() {
        _summary = summary;
        _loading = false;
      });
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() {
        _error = e.message;
        _loading = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('الرئيسية'),
        actions: [
          IconButton(
            tooltip: 'تحديث',
            onPressed: reload,
            icon: const Icon(Icons.refresh),
          ),
        ],
      ),
      body: SafeArea(
        child: Builder(
          builder: (context) {
            if (_loading) return const LoadingView();
            final summary = _summary;
            if (summary == null) {
              return ErrorView(
                message: _error ?? 'تعذر تحميل البيانات',
                onRetry: reload,
              );
            }
            return RefreshIndicator(
              onRefresh: reload,
              child: ListView(
                padding: const EdgeInsets.all(AppSpacing.md),
                children: [
                  _Greeting(userName: Session.userName ?? 'المدير المفوض'),
                  if (_error != null) ...[
                    const SizedBox(height: AppSpacing.sm),
                    Text(
                      _error!,
                      style: const TextStyle(
                          color: AppColors.rejected, fontSize: 13),
                    ),
                  ],
                  const SizedBox(height: AppSpacing.md),
                  _MetricCard(
                    title: 'رسائل وشكاوى غير مقروءة',
                    value: summary.unreadComplaints,
                    icon: Icons.mark_email_unread_outlined,
                    color: AppColors.accent,
                    onTap: () => widget.onOpenTab(1),
                  ),
                  const SizedBox(height: AppSpacing.sm),
                  _MetricCard(
                    title: 'طلبات استثناء قيد الانتظار',
                    value: summary.pendingExceptions,
                    icon: Icons.pending_actions_outlined,
                    color: AppColors.pending,
                    onTap: () => widget.onOpenTab(2),
                  ),
                  const SizedBox(height: AppSpacing.md),
                  SectionCard(
                    title: 'ملخص طلبات الاستثناء',
                    children: [
                      InfoRow(
                        label: ExceptionStatuses.arabic(
                            ExceptionStatuses.approved),
                        value: summary.approvedExceptions.toString(),
                      ),
                      InfoRow(
                        label: ExceptionStatuses.arabic(
                            ExceptionStatuses.rejected),
                        value: summary.rejectedExceptions.toString(),
                      ),
                      InfoRow(
                        label: ExceptionStatuses.arabic(
                            ExceptionStatuses.cancelled),
                        value: summary.cancelledExceptions.toString(),
                      ),
                      InfoRow(
                        label: 'الإجمالي',
                        value: summary.totalExceptions.toString(),
                      ),
                    ],
                  ),
                ],
              ),
            );
          },
        ),
      ),
    );
  }
}

class _Greeting extends StatelessWidget {
  const _Greeting({required this.userName});

  final String userName;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.md),
        child: Row(
          children: [
            const CircleAvatar(
              backgroundColor: AppColors.primary,
              child: Icon(Icons.person_outline, color: Colors.white),
            ),
            const SizedBox(width: AppSpacing.sm),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    userName,
                    style: const TextStyle(
                      fontSize: 16,
                      fontWeight: FontWeight.bold,
                      color: AppColors.text,
                    ),
                  ),
                  const Text(
                    'مدير مفوض • كل المحافظات',
                    style: TextStyle(color: AppColors.muted, fontSize: 13),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _MetricCard extends StatelessWidget {
  const _MetricCard({
    required this.title,
    required this.value,
    required this.icon,
    required this.color,
    required this.onTap,
  });

  final String title;
  final int value;
  final IconData icon;
  final Color color;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: InkWell(
        borderRadius: BorderRadius.circular(AppRadius.md),
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.all(AppSpacing.md),
          child: Row(
            children: [
              Container(
                padding: const EdgeInsets.all(AppSpacing.sm),
                decoration: BoxDecoration(
                  color: color.withValues(alpha: 0.12),
                  borderRadius: BorderRadius.circular(AppRadius.sm),
                ),
                child: Icon(icon, color: color, size: 26),
              ),
              const SizedBox(width: AppSpacing.sm),
              Expanded(
                child: Text(
                  title,
                  style: const TextStyle(
                      fontSize: 15,
                      fontWeight: FontWeight.w600,
                      color: AppColors.text),
                ),
              ),
              Text(
                value.toString(),
                style: TextStyle(
                  fontSize: 24,
                  fontWeight: FontWeight.bold,
                  color: color,
                ),
              ),
              const Icon(Icons.chevron_left, color: AppColors.muted),
            ],
          ),
        ),
      ),
    );
  }
}
