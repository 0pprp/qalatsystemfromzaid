import 'dart:convert';

import 'package:follower_application/config/app_env.dart';
import 'package:follower_application/ui/app_safe_scaffold.dart';
import 'package:follower_application/utils/AppTheme.dart';
import 'package:follower_application/utils/display_name.dart';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';

/// Inbox of sales requests originated by this follower.
/// Tabs: الكل / قيد العمل / مرفوض / تم البيع
class FollowerSalesRequestsPage extends StatefulWidget {
  const FollowerSalesRequestsPage({super.key});

  @override
  State<FollowerSalesRequestsPage> createState() =>
      _FollowerSalesRequestsPageState();
}

class _FollowerSalesRequestsPageState extends State<FollowerSalesRequestsPage>
    with SingleTickerProviderStateMixin {
  late final TabController _tabs;
  bool _loading = true;
  String _error = '';
  List<Map<String, dynamic>> _rows = [];

  static const _tabKeys = ['all', 'inProgress', 'rejected', 'completed'];

  @override
  void initState() {
    super.initState();
    _tabs = TabController(length: _tabKeys.length, vsync: this);
    _tabs.addListener(() {
      if (!_tabs.indexIsChanging) _load();
    });
    _load();
  }

  @override
  void dispose() {
    _tabs.dispose();
    super.dispose();
  }

  String get _tab => _tabKeys[_tabs.index.clamp(0, _tabKeys.length - 1)];

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = '';
    });
    try {
      final prefs = await SharedPreferences.getInstance();
      final asyncId = prefs.getString('AsyncId') ?? '';
      final link =
          AppEnv.apiBase(fallback: prefs.getString('LinkDelegate') ?? '');
      final uri = Uri.parse('${link}Followers/SalesRequests').replace(
        queryParameters: {
          'asyncId': asyncId,
          'tab': _tab,
        },
      );
      final res = await http.get(uri).timeout(const Duration(seconds: 30));
      if (res.statusCode != 200) {
        if (!mounted) return;
        setState(() {
          _loading = false;
          _error = 'تعذر تحميل طلبات البيع';
          _rows = [];
        });
        return;
      }
      final decoded = json.decode(res.body);
      final list = decoded is List
          ? decoded
              .whereType<Map>()
              .map((e) => Map<String, dynamic>.from(e))
              .toList()
          : <Map<String, dynamic>>[];
      if (!mounted) return;
      setState(() {
        _rows = list;
        _loading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _error = 'تعذر الاتصال بالخادم';
        _rows = [];
      });
    }
  }

  String _str(Map<String, dynamic> row, List<String> keys) {
    for (final k in keys) {
      final v = row[k];
      if (v != null && '$v'.trim().isNotEmpty) return '$v'.trim();
    }
    return '';
  }

  String _fmtDate(String raw) {
    if (raw.isEmpty) return '';
    final dt = DateTime.tryParse(raw);
    if (dt == null) return raw;
    final local = dt.toLocal();
    String two(int n) => n.toString().padLeft(2, '0');
    return '${two(local.day)}/${two(local.month)}/${local.year}';
  }

  @override
  Widget build(BuildContext context) {
    return AppSafeScaffold(
      appBar: AppBar(
        title: const Text('طلبات البيع',
            style: TextStyle(fontFamily: 'Cairo', color: Colors.white)),
        backgroundColor: AppTheme.primaryColor,
        foregroundColor: Colors.white,
        bottom: AppTheme.primaryAppBarTabBar(
          controller: _tabs,
          isScrollable: true,
          tabs: const [
            Tab(text: 'الكل'),
            Tab(text: 'قيد العمل'),
            Tab(text: 'مرفوض'),
            Tab(text: 'تم البيع'),
          ],
        ),
        actions: [
          IconButton(onPressed: _load, icon: const Icon(Icons.refresh)),
        ],
      ),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : _error.isNotEmpty
              ? Center(
                  child: Text(_error,
                      style: const TextStyle(fontFamily: 'Cairo')))
              : _rows.isEmpty
                  ? const Center(
                      child: Text('لا توجد طلبات',
                          style: TextStyle(fontFamily: 'Cairo')))
                  : RefreshIndicator(
                      onRefresh: _load,
                      child: ListView.separated(
                        padding: const EdgeInsets.all(12),
                        itemCount: _rows.length,
                        separatorBuilder: (_, __) => const NameListSeparator(),
                        itemBuilder: (context, i) {
                          final row = _rows[i];
                          final name = _str(row, ['customerName', 'CustomerName']);
                          final phone =
                              _str(row, ['customerPhone', 'CustomerPhone']);
                          final status = _str(
                              row, ['statusLabel', 'StatusLabel', 'status', 'Status']);
                          final emp = _str(
                              row, ['targetEmployeeName', 'TargetEmployeeName']);
                          final created = _fmtDate(
                              _str(row, ['createdAtUtc', 'CreatedAtUtc']));
                          final reason = _str(
                              row, ['rejectionReason', 'RejectionReason']);
                          final pending =
                              _str(row, ['pendingNote', 'PendingNote']);

                          final lines = <String>[
                            if (phone.isNotEmpty) phone,
                            if (status.isNotEmpty) status,
                            if (emp.isNotEmpty) 'الموظف: $emp',
                            if (created.isNotEmpty) 'تاريخ الطلب: $created',
                            if (reason.isNotEmpty) 'سبب الرفض: $reason',
                            if (pending.isNotEmpty && reason.isEmpty)
                              'ملاحظة: $pending',
                          ];

                          return Card(
                            child: ListTile(
                              title: PersonNameText(name.isEmpty ? '—' : name),
                              subtitle: Text(
                                lines.join('\n'),
                                style: const TextStyle(fontFamily: 'Cairo'),
                              ),
                              isThreeLine: lines.length >= 2,
                            ),
                          );
                        },
                      ),
                    ),
    );
  }
}
