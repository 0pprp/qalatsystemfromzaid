import 'dart:convert';

import 'package:follower_application/config/app_env.dart';
import 'package:follower_application/legal_followup_detail_page.dart';
import 'package:follower_application/ui/app_safe_scaffold.dart';
import 'package:follower_application/utils/AppTheme.dart';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';

class LegalFollowupsPage extends StatefulWidget {
  const LegalFollowupsPage({super.key});

  @override
  State<LegalFollowupsPage> createState() => _LegalFollowupsPageState();
}

class _LegalFollowupsPageState extends State<LegalFollowupsPage>
    with SingleTickerProviderStateMixin {
  late final TabController _tabs;
  bool _loading = true;
  String _error = '';
  List<Map<String, dynamic>> _rows = [];

  @override
  void initState() {
    super.initState();
    _tabs = TabController(length: 2, vsync: this);
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

  String get _status => _tabs.index == 1 ? 'Completed' : 'InProgress';

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = '';
    });
    try {
      final prefs = await SharedPreferences.getInstance();
      final asyncId = prefs.getString('AsyncId') ?? '';
      final link = AppEnv.apiBase(fallback: prefs.getString('LinkDelegate') ?? '');
      final uri = Uri.parse('${link}Followers/LegalFollowups')
          .replace(queryParameters: {
        'asyncId': asyncId,
        'status': _status,
      });
      final res = await http.get(uri).timeout(const Duration(seconds: 30));
      if (res.statusCode != 200) {
        setState(() {
          _loading = false;
          _error = 'تعذر تحميل المتابعات';
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

  @override
  Widget build(BuildContext context) {
    return AppSafeScaffold(
      appBar: AppBar(
        title: const Text('الزبائن القديمة', style: TextStyle(fontFamily: 'Cairo')),
        backgroundColor: AppTheme.primaryColor,
        bottom: TabBar(
          controller: _tabs,
          tabs: const [
            Tab(child: Text('قيد العمل', style: TextStyle(fontFamily: 'Cairo'))),
            Tab(child: Text('تمت المتابعة', style: TextStyle(fontFamily: 'Cairo'))),
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
                      child: Text('لا توجد متابعات',
                          style: TextStyle(fontFamily: 'Cairo')))
                  : RefreshIndicator(
                      onRefresh: _load,
                      child: ListView.separated(
                        padding: const EdgeInsets.all(12),
                        itemCount: _rows.length,
                        separatorBuilder: (_, __) => const SizedBox(height: 8),
                        itemBuilder: (context, i) {
                          final row = _rows[i];
                          final name =
                              '${row['customerName'] ?? row['CustomerName'] ?? ''}';
                          final phone =
                              '${row['customerPhone'] ?? row['CustomerPhone'] ?? ''}';
                          final status =
                              '${row['statusLabel'] ?? row['StatusLabel'] ?? ''}';
                          return Card(
                            child: ListTile(
                              title: Text(name,
                                  style: const TextStyle(
                                      fontFamily: 'Cairo',
                                      fontWeight: FontWeight.bold)),
                              subtitle: Text('$phone\n$status',
                                  style:
                                      const TextStyle(fontFamily: 'Cairo')),
                              isThreeLine: true,
                              trailing: const Icon(Icons.chevron_left),
                              onTap: () async {
                                final id = int.tryParse(
                                        '${row['id'] ?? row['Id'] ?? 0}') ??
                                    0;
                                if (id <= 0) return;
                                await Navigator.push(
                                  context,
                                  MaterialPageRoute(
                                    builder: (_) =>
                                        LegalFollowupDetailPage(caseId: id),
                                  ),
                                );
                                _load();
                              },
                            ),
                          );
                        },
                      ),
                    ),
    );
  }
}
