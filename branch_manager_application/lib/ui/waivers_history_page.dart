import 'package:branch_manager_application/core/api.dart';
import 'package:branch_manager_application/core/session.dart';
import 'package:branch_manager_application/theme/bm_theme.dart';
import 'package:flutter/material.dart';
import 'package:intl/intl.dart' hide TextDirection;

class WaiversHistoryPage extends StatefulWidget {
  const WaiversHistoryPage({super.key});

  @override
  State<WaiversHistoryPage> createState() => _WaiversHistoryPageState();
}

class _WaiversHistoryPageState extends State<WaiversHistoryPage>
    with SingleTickerProviderStateMixin {
  late final TabController _tabs;
  final _money = NumberFormat('#,##0.##', 'en');

  static const _statuses = [
    (null, 'الكل'),
    ('Pending', 'قيد الانتظار'),
    ('Rejected', 'المرفوضين'),
    ('Approved', 'الموافق عليهم'),
  ];

  @override
  void initState() {
    super.initState();
    _tabs = TabController(length: _statuses.length, vsync: this);
  }

  @override
  void dispose() {
    _tabs.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Directionality(
      textDirection: TextDirection.rtl,
      child: Column(
        children: [
          TabBar(
            controller: _tabs,
            isScrollable: true,
            labelColor: BmTheme.primary,
            unselectedLabelColor: BmTheme.muted,
            labelStyle: const TextStyle(fontFamily: 'Cairo', fontWeight: FontWeight.bold),
            tabs: [for (final s in _statuses) Tab(text: s.$2)],
          ),
          Expanded(
            child: TabBarView(
              controller: _tabs,
              children: [
                for (final s in _statuses)
                  _WaiverList(status: s.$1, money: _money),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _WaiverList extends StatefulWidget {
  const _WaiverList({required this.status, required this.money});
  final String? status;
  final NumberFormat money;

  @override
  State<_WaiverList> createState() => _WaiverListState();
}

class _WaiverListState extends State<_WaiverList> {
  bool _loading = true;
  String? _error;
  List<Map<String, dynamic>> _items = [];

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
      final data = await BmApi.instance.listWaivers(status: widget.status);
      final raw = data['items'] ?? data['Items'] ?? [];
      final list = raw is List ? raw : <dynamic>[];
      if (!mounted) return;
      setState(() {
        _items = list.map((e) => Map<String, dynamic>.from(e as Map)).toList();
        _loading = false;
      });
    } catch (e) {
      if (!mounted) return;
      if (e is BmApiException && e.statusCode == 401) {
        await BmSession.instance.clear();
      }
      setState(() {
        _loading = false;
        _error = '$e';
      });
    }
  }

  String _statusAr(String? s) {
    switch ((s ?? '').trim()) {
      case 'Pending':
        return 'قيد الانتظار';
      case 'Approved':
        return 'موافق عليه';
      case 'Rejected':
        return 'مرفوض';
      default:
        return s ?? '-';
    }
  }

  Color _statusColor(String? s) {
    switch ((s ?? '').trim()) {
      case 'Pending':
        return BmTheme.accent;
      case 'Approved':
        return Colors.green.shade700;
      case 'Rejected':
        return BmTheme.danger;
      default:
        return BmTheme.muted;
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) return const Center(child: CircularProgressIndicator());
    if (_error != null) {
      return Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(_error!, textAlign: TextAlign.center,
                style: const TextStyle(fontFamily: 'Cairo', color: BmTheme.danger)),
            const SizedBox(height: 12),
            FilledButton(onPressed: _load, child: const Text('إعادة المحاولة')),
          ],
        ),
      );
    }
    if (_items.isEmpty) {
      return const Center(
        child: Text('لا توجد طلبات', style: TextStyle(fontFamily: 'Cairo', color: BmTheme.muted)),
      );
    }

    return RefreshIndicator(
      onRefresh: _load,
      child: ListView.separated(
        padding: const EdgeInsets.fromLTRB(16, 12, 16, 24),
        itemCount: _items.length,
        separatorBuilder: (_, _) => const SizedBox(height: 10),
        itemBuilder: (ctx, i) {
          final row = _items[i];
          final name = '${row['customerName'] ?? row['CustomerName'] ?? 'زبون'}';
          final note = '${row['note'] ?? row['Note'] ?? ''}';
          final status = '${row['status'] ?? row['Status'] ?? ''}';
          final amount = row['approvedAmount'] ?? row['ApprovedAmount'];
          final amountNum = amount is num ? amount.toDouble() : double.tryParse('$amount');

          return Container(
            padding: const EdgeInsets.all(14),
            decoration: BoxDecoration(
              color: BmTheme.card,
              borderRadius: BorderRadius.circular(14),
              border: Border.all(color: BmTheme.primary.withValues(alpha: 0.08)),
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Expanded(
                      child: Text(
                        name,
                        style: const TextStyle(
                          fontFamily: 'Cairo',
                          fontWeight: FontWeight.bold,
                          fontSize: 15,
                        ),
                      ),
                    ),
                    Text(
                      _statusAr(status),
                      style: TextStyle(
                        fontFamily: 'Cairo',
                        fontWeight: FontWeight.bold,
                        color: _statusColor(status),
                        fontSize: 12,
                      ),
                    ),
                  ],
                ),
                if (note.isNotEmpty) ...[
                  const SizedBox(height: 8),
                  Text(note, style: const TextStyle(fontFamily: 'Cairo', height: 1.45)),
                ],
                if (amountNum != null && amountNum > 0) ...[
                  const SizedBox(height: 6),
                  Text(
                    'مبلغ التنازل: ${widget.money.format(amountNum)}',
                    style: const TextStyle(
                      fontFamily: 'Cairo',
                      fontWeight: FontWeight.w700,
                      color: Colors.green,
                    ),
                  ),
                ],
              ],
            ),
          );
        },
      ),
    );
  }
}
