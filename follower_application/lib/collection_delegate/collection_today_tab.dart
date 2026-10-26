import 'dart:convert';

import 'package:follower_application/config/app_env.dart';
import 'package:follower_application/utils/AppTheme.dart';
import 'package:follower_application/utils/Formatters.dart';
import 'package:follower_application/utils/display_name.dart';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';

class CollectionTodayTab extends StatefulWidget {
  final int listId;
  final String listName;

  const CollectionTodayTab({
    super.key,
    required this.listId,
    required this.listName,
  });

  @override
  State<CollectionTodayTab> createState() => CollectionTodayTabState();
}

class CollectionTodayTabState extends State<CollectionTodayTab> {
  bool _loading = true;
  String _filter = 'الكل'; // الكل | سدد | لم يسدد
  List<Map<String, dynamic>> _rows = [];

  @override
  void initState() {
    super.initState();
    reload();
  }

  Future<void> reload() async {
    setState(() => _loading = true);
    try {
      final prefs = await SharedPreferences.getInstance();
      final asyncId = prefs.getString('AsyncId') ?? '';
      final link =
          AppEnv.apiBase(fallback: prefs.getString('LinkDelegate') ?? '');
      final uri = Uri.parse('${link}Followers/Collection/Customers')
          .replace(queryParameters: {
        'asyncId': asyncId,
        'listId': '${widget.listId}',
      });
      final res = await http.get(uri).timeout(const Duration(seconds: 60));
      if (res.statusCode != 200) throw Exception('load');
      final data = json.decode(res.body) as Map;
      final raw = data['items'] ?? data['Items'] ?? [];
      if (mounted) {
        setState(() {
          _rows = (raw as List)
              .map((e) => Map<String, dynamic>.from(e as Map))
              .toList();
          _loading = false;
        });
      }
    } catch (_) {
      if (mounted) setState(() => _loading = false);
    }
  }

  List<Map<String, dynamic>> get _visible {
    if (_filter == 'سدد') {
      return _rows
          .where((r) => r['paidToday'] == true || r['PaidToday'] == true)
          .toList();
    }
    if (_filter == 'لم يسدد') {
      return _rows
          .where((r) => !(r['paidToday'] == true || r['PaidToday'] == true))
          .toList();
    }
    return _rows;
  }

  double _d(dynamic v) {
    if (v is num) return v.toDouble();
    return double.tryParse('$v') ?? 0;
  }

  @override
  Widget build(BuildContext context) {
    final rows = _visible;
    final paidCount = _rows
        .where((r) => r['paidToday'] == true || r['PaidToday'] == true)
        .length;
    return Column(
      children: [
        Padding(
          padding: const EdgeInsets.all(12),
          child: Row(
            children: [
              for (final f in ['الكل', 'سدد', 'لم يسدد']) ...[
                Expanded(
                  child: Padding(
                    padding: const EdgeInsets.symmetric(horizontal: 4),
                    child: ChoiceChip(
                      label: Text(f, style: const TextStyle(fontFamily: 'Cairo')),
                      selected: _filter == f,
                      onSelected: (_) => setState(() => _filter = f),
                      selectedColor: AppTheme.primaryColor.withValues(alpha: 0.2),
                    ),
                  ),
                ),
              ],
            ],
          ),
        ),
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: 16),
          child: Align(
            alignment: Alignment.centerRight,
            child: Text(
              'مسدد اليوم: $paidCount / ${_rows.length}',
              style: const TextStyle(fontFamily: 'Cairo', color: Colors.grey),
            ),
          ),
        ),
        Expanded(
          child: _loading
              ? const Center(child: CircularProgressIndicator())
              : RefreshIndicator(
                  onRefresh: reload,
                  child: ListView.separated(
                    padding: const EdgeInsets.all(12),
                    itemCount: rows.length,
                    separatorBuilder: (_, __) => const NameListSeparator(),
                    itemBuilder: (_, i) {
                      final r = rows[i];
                      final paid =
                          r['paidToday'] == true || r['PaidToday'] == true;
                      return ListTile(
                          title: PersonNameText(
                              '${r['customerName'] ?? r['CustomerName'] ?? ''}'),
                          subtitle: Text(
                            'الباقي: ${Formatters.formatNumber(_d(r['amountRemaining'] ?? r['AmountRemaining']))}',
                            style: const TextStyle(fontFamily: 'Cairo'),
                          ),
                          trailing: Icon(
                            paid ? Icons.check_circle : Icons.pending,
                            color: paid ? Colors.green : Colors.orange,
                          ),
                        );
                    },
                  ),
                ),
        ),
      ],
    );
  }
}
