import 'package:flutter/material.dart';
import 'package:sales_employee_application/data/archive_api.dart';
import 'package:sales_employee_application/data/archive_models.dart';
import 'package:sales_employee_application/screens/archive_bundle_screen.dart';
import 'package:sales_employee_application/services/api_client.dart';
import 'package:sales_employee_application/utils/app_theme.dart';

class ArchiveScreen extends StatefulWidget {
  const ArchiveScreen({super.key});

  @override
  State<ArchiveScreen> createState() => ArchiveScreenState();
}

class ArchiveScreenState extends State<ArchiveScreen> {
  bool _loading = true;
  bool _showSentAndApproved = false;
  String? _error;
  List<ArchiveBundle> _bundles = const [];

  @override
  void initState() {
    super.initState();
    reload();
  }

  Future<void> reload() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final rows = await ArchiveApi.list();
      if (!mounted) return;
      setState(() {
        _bundles = rows;
        _loading = false;
      });
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() {
        _error = e.message;
        _loading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _error = 'تعذر تحميل الأرشيف';
        _loading = false;
      });
    }
  }

  List<ArchiveBundle> get _visible {
    if (_showSentAndApproved) return _bundles;
    return _bundles.where((b) => archiveShownWhenHidingSentAndApproved(b.phase)).toList();
  }

  Future<void> _open(ArchiveBundle bundle) async {
    final changed = await Navigator.of(context).push<bool>(
      MaterialPageRoute(builder: (_) => ArchiveBundleScreen(bundleId: bundle.bundleId)),
    );
    if (changed == true) {
      await reload();
    }
  }

  @override
  Widget build(BuildContext context) {
    final visible = _visible;
    return Scaffold(
      appBar: AppBar(
        title: const Text('الارشيف'),
        actions: [
          IconButton(
            tooltip: 'تحديث',
            onPressed: _loading ? null : reload,
            icon: const Icon(Icons.refresh),
          ),
        ],
      ),
      body: Column(
        children: [
          SwitchListTile(
            value: _showSentAndApproved,
            activeColor: AppColors.darkGreen,
            title: const Text('إظهار الحزم المرسلة والمعتمدة'),
            subtitle: Text(
              _showSentAndApproved
                  ? 'تظهر الحزم التي أُرسلت ولم تصل بعد، والحزم المعتمدة.'
                  : 'الحزم المرسلة والتي لم تصل بعد، والحزم المعتمدة، مخفية.',
            ),
            onChanged: (value) => setState(() => _showSentAndApproved = value),
          ),
          const Divider(height: 1),
          Expanded(child: _body(visible)),
        ],
      ),
    );
  }

  Widget _body(List<ArchiveBundle> visible) {
    if (_loading) {
      return const Center(child: CircularProgressIndicator(color: AppColors.darkGreen));
    }
    if (_error != null) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(_error!, textAlign: TextAlign.center),
              const SizedBox(height: 12),
              FilledButton(onPressed: reload, child: const Text('إعادة المحاولة')),
            ],
          ),
        ),
      );
    }
    if (visible.isEmpty) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Text(
            _bundles.isEmpty
                ? 'لا توجد مبيعات في الأرشيف بعد. تُضاف كل عملية بيع ناجحة تلقائياً إلى حزمة الفترة.'
                : 'لا توجد حزم ظاهرة. فعّل إظهار الحزم المرسلة والمعتمدة لرؤية البقية.',
            textAlign: TextAlign.center,
            style: const TextStyle(color: AppColors.muted),
          ),
        ),
      );
    }
    return RefreshIndicator(
      color: AppColors.darkGreen,
      onRefresh: reload,
      child: ListView.separated(
        padding: const EdgeInsets.all(16),
        itemCount: visible.length,
        separatorBuilder: (_, __) => const SizedBox(height: 12),
        itemBuilder: (context, index) {
          final bundle = visible[index];
          return _BundleCard(bundle: bundle, onTap: () => _open(bundle));
        },
      ),
    );
  }
}

class _BundleCard extends StatelessWidget {
  const _BundleCard({required this.bundle, required this.onTap});

  final ArchiveBundle bundle;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final phase = bundle.phase;
    final color = switch (phase) {
      'Ready' => AppColors.darkGreen,
      'NeedsFix' => AppColors.danger,
      'InTransit' => const Color(0xFF8A5A00),
      'Approved' => AppColors.lightGreen,
      _ => AppColors.muted,
    };
    return Material(
      color: Colors.white,
      borderRadius: BorderRadius.circular(AppRadius.md),
      child: InkWell(
        borderRadius: BorderRadius.circular(AppRadius.md),
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Expanded(
                    child: Text(
                      'حزمة ${bundle.bundleNumber}',
                      style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w700),
                    ),
                  ),
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                    decoration: BoxDecoration(
                      color: color.withValues(alpha: 0.12),
                      borderRadius: BorderRadius.circular(20),
                    ),
                    child: Text(
                      archivePhaseLabel(phase),
                      style: TextStyle(color: color, fontWeight: FontWeight.w700, fontSize: 12),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 6),
              Text(bundle.periodLabel, style: const TextStyle(color: AppColors.muted)),
              const SizedBox(height: 8),
              Text('${bundle.itemCount} عملية بيع'),
              if (phase == 'NeedsFix')
                Padding(
                  padding: const EdgeInsets.only(top: 8),
                  child: Text(
                    'معتمد ${bundle.approvedCount} · فيه مشكلة ${bundle.refusedCount}',
                    style: const TextStyle(color: AppColors.danger, fontWeight: FontWeight.w600),
                  ),
                ),
              if (phase == 'Open')
                Padding(
                  padding: const EdgeInsets.only(top: 8),
                  child: Text(
                    'يمكن الإرسال بعد انتهاء ${formatArchiveDate(bundle.periodEnd)}',
                    style: const TextStyle(color: AppColors.muted),
                  ),
                ),
              if ((bundle.intermediateName ?? '').trim().isNotEmpty)
                Padding(
                  padding: const EdgeInsets.only(top: 8),
                  child: Text('الوسيط: ${bundle.intermediateName} · ${bundle.intermediatePhone ?? ''}'),
                ),
            ],
          ),
        ),
      ),
    );
  }
}
