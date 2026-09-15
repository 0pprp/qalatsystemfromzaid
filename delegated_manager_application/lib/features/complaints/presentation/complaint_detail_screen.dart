import 'package:delegated_manager_application/core/network/api_client.dart';
import 'package:delegated_manager_application/core/theme/app_theme.dart';
import 'package:delegated_manager_application/core/utils/date_format.dart';
import 'package:delegated_manager_application/core/widgets/state_views.dart';
import 'package:delegated_manager_application/features/complaints/data/complaints_repository.dart';
import 'package:delegated_manager_application/features/complaints/domain/complaint.dart';
import 'package:flutter/material.dart';

class ComplaintDetailScreen extends StatefulWidget {
  const ComplaintDetailScreen({super.key, required this.complaintId});

  final String complaintId;

  @override
  State<ComplaintDetailScreen> createState() => _ComplaintDetailScreenState();
}

class _ComplaintDetailScreenState extends State<ComplaintDetailScreen> {
  ComplaintDetail? _detail;
  bool _loading = true;
  bool _marking = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final detail = await ComplaintsRepository.detail(widget.complaintId);
      if (!mounted) return;
      setState(() {
        _detail = detail;
        _loading = false;
      });
      if (detail.summary.isUnread) await _markRead(silent: true);
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() {
        _error = e.message;
        _loading = false;
      });
    }
  }

  Future<void> _markRead({bool silent = false}) async {
    if (_marking) return;
    setState(() => _marking = true);
    try {
      final updated = await ComplaintsRepository.markRead(widget.complaintId);
      if (!mounted) return;
      setState(() => _detail = updated);
    } on ApiException catch (e) {
      if (!mounted || silent) return;
      ScaffoldMessenger.of(context)
          .showSnackBar(SnackBar(content: Text(e.message)));
    } finally {
      if (mounted) setState(() => _marking = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final detail = _detail;
    return Scaffold(
      appBar: AppBar(title: const Text('تفاصيل الرسالة')),
      body: SafeArea(
        child: _loading
            ? const LoadingView()
            : detail == null
                ? ErrorView(
                    message: _error ?? 'تعذر تحميل الرسالة', onRetry: _load)
                : ListView(
                    padding: const EdgeInsets.all(AppSpacing.md),
                    children: [
                      Card(
                        child: Padding(
                          padding: const EdgeInsets.all(AppSpacing.md),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Row(
                                children: [
                                  Expanded(
                                    child: Text(
                                      detail.summary.subject,
                                      style: const TextStyle(
                                        fontSize: 17,
                                        fontWeight: FontWeight.bold,
                                        color: AppColors.text,
                                      ),
                                    ),
                                  ),
                                  StatusChip(
                                    label: ComplaintStatuses.arabic(
                                        detail.summary.status),
                                    color: detail.summary.isUnread
                                        ? AppColors.accent
                                        : AppColors.approved,
                                  ),
                                ],
                              ),
                              const Divider(height: AppSpacing.lg),
                              InfoRow(
                                label: 'المرسل',
                                value: detail.summary.senderDisplayName,
                              ),
                              InfoRow(
                                label: 'الصفة',
                                value: detail.summary.senderRole ?? '-',
                              ),
                              InfoRow(
                                label: 'المحافظة',
                                value: detail.summary.cityName ?? 'غير محددة',
                              ),
                              InfoRow(
                                label: 'التطبيق',
                                value: detail.summary.sourceApp ?? '-',
                              ),
                              InfoRow(
                                label: 'النوع',
                                value: detail.summary.sourceType ?? '-',
                              ),
                              InfoRow(
                                label: 'تاريخ الإرسال',
                                value:
                                    AppDate.format(detail.summary.createdAtUtc),
                              ),
                              InfoRow(
                                label: 'تاريخ القراءة',
                                value: AppDate.format(detail.summary.readAtUtc),
                              ),
                            ],
                          ),
                        ),
                      ),
                      const SizedBox(height: AppSpacing.md),
                      SectionCard(
                        title: 'نص الرسالة',
                        children: [
                          Text(
                            detail.body,
                            style: const TextStyle(
                                fontSize: 15,
                                height: 1.6,
                                color: AppColors.text),
                          ),
                        ],
                      ),
                      if ((detail.metadataJson ?? '').isNotEmpty) ...[
                        const SizedBox(height: AppSpacing.md),
                        SectionCard(
                          title: 'بيانات إضافية',
                          children: [
                            Text(
                              detail.metadataJson!,
                              style: const TextStyle(
                                  fontSize: 12, color: AppColors.muted),
                            ),
                          ],
                        ),
                      ],
                      const SizedBox(height: AppSpacing.md),
                      if (detail.summary.isUnread)
                        FilledButton.icon(
                          onPressed: _marking ? null : () => _markRead(),
                          icon: const Icon(Icons.mark_email_read_outlined),
                          label: const Text('تحديد كمقروءة'),
                        ),
                    ],
                  ),
      ),
    );
  }
}
