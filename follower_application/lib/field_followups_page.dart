import 'dart:convert';

import 'package:follower_application/config/app_env.dart';
import 'package:follower_application/ui/app_safe_scaffold.dart';
import 'package:follower_application/utils/AppTheme.dart';
import 'package:follower_application/utils/display_name.dart';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';

class FieldFollowupsPage extends StatefulWidget {
  const FieldFollowupsPage({super.key});

  @override
  State<FieldFollowupsPage> createState() => _FieldFollowupsPageState();
}

class _FieldFollowupsPageState extends State<FieldFollowupsPage> {
  bool _loading = true;
  String _error = '';
  List<Map<String, dynamic>> _items = [];

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<Map<String, String>> _session() async {
    final prefs = await SharedPreferences.getInstance();
    return {
      'link': AppEnv.apiBase(fallback: prefs.getString('LinkDelegate') ?? ''),
      'asyncId': prefs.getString('AsyncId') ?? '',
    };
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = '';
    });
    try {
      final s = await _session();
      final uri = Uri.parse('${s['link']}Followers/FieldFollowups')
          .replace(queryParameters: {'asyncId': s['asyncId']});
      final res = await http.get(uri).timeout(const Duration(seconds: 45));
      if (res.statusCode != 200) {
        throw Exception('http ${res.statusCode}');
      }
      final data = json.decode(res.body);
      final raw = data is Map ? (data['items'] ?? data['Items'] ?? []) : data;
      final list = (raw as List)
          .map((e) => Map<String, dynamic>.from(e as Map))
          .toList();
      if (mounted) {
        setState(() {
          _items = list;
          _loading = false;
        });
      }
    } catch (_) {
      if (mounted) {
        setState(() {
          _loading = false;
          _error = 'تعذر تحميل المتابعة الميدانية';
        });
      }
    }
  }

  Map<int, List<Map<String, dynamic>>> get _byList {
    final map = <int, List<Map<String, dynamic>>>{};
    for (final item in _items) {
      final listId = int.tryParse('${item['listId'] ?? item['ListId'] ?? 0}') ?? 0;
      map.putIfAbsent(listId, () => []).add(item);
    }
    return map;
  }

  String _listTitle(List<Map<String, dynamic>> rows) {
    if (rows.isEmpty) return 'قائمة';
    return '${rows.first['listName'] ?? rows.first['ListName'] ?? 'قائمة'}';
  }

  Future<void> _complete(Map<String, dynamic> item) async {
    var draft = '';
    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('تمت المتابعة', style: TextStyle(fontFamily: 'Cairo')),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(
              '${item['customerName'] ?? item['CustomerName'] ?? ''}',
              style: const TextStyle(fontFamily: 'Cairo', fontWeight: FontWeight.bold),
            ),
            const SizedBox(height: 12),
            TextField(
              maxLines: 3,
              onChanged: (v) => draft = v,
              decoration: const InputDecoration(
                labelText: 'ملاحظة إجبارية',
                border: OutlineInputBorder(),
              ),
              style: const TextStyle(fontFamily: 'Cairo'),
            ),
          ],
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx, false),
            child: const Text('إلغاء', style: TextStyle(fontFamily: 'Cairo')),
          ),
          ElevatedButton(
            onPressed: () => Navigator.pop(ctx, true),
            child: const Text('اعتماد', style: TextStyle(fontFamily: 'Cairo')),
          ),
        ],
      ),
    );
    if (ok != true) return;
    final note = draft.trim();
    if (note.isEmpty) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('الملاحظة إجبارية', style: TextStyle(fontFamily: 'Cairo')),
          ),
        );
      }
      return;
    }

    try {
      final s = await _session();
      final id = int.tryParse('${item['id'] ?? item['Id'] ?? 0}') ?? 0;
      final uri = Uri.parse('${s['link']}Followers/FieldFollowups/$id/complete');
      final res = await http
          .post(
            uri,
            headers: {'Content-Type': 'application/json'},
            body: json.encode({'asyncId': s['asyncId'], 'note': note}),
          )
          .timeout(const Duration(seconds: 45));
      if (res.statusCode != 200) {
        final msg = () {
          try {
            final j = json.decode(res.body);
            return '${j['message'] ?? j['Message'] ?? res.body}';
          } catch (_) {
            return 'تعذر الإتمام';
          }
        }();
        throw Exception(msg);
      }
      await _load();
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('تم اعتماد المتابعة', style: TextStyle(fontFamily: 'Cairo')),
          ),
        );
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('$e', style: const TextStyle(fontFamily: 'Cairo')),
          ),
        );
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final groups = _byList.entries.toList()
      ..sort((a, b) => _listTitle(a.value).compareTo(_listTitle(b.value)));

    return Directionality(
      textDirection: TextDirection.rtl,
      child: AppSafeScaffold(
        appBar: AppBar(
          title: const Text('المتابعة الميدانية', style: TextStyle(fontFamily: 'Cairo')),
          backgroundColor: AppTheme.primaryColor,
          actions: [
            IconButton(onPressed: _load, icon: const Icon(Icons.refresh)),
          ],
        ),
        body: _loading
            ? const Center(child: CircularProgressIndicator())
            : _error.isNotEmpty
                ? Center(child: Text(_error, style: const TextStyle(fontFamily: 'Cairo')))
                : Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      Container(
                        margin: const EdgeInsets.fromLTRB(12, 12, 12, 0),
                        padding: const EdgeInsets.all(12),
                        decoration: BoxDecoration(
                          color: AppTheme.primaryColor.withValues(alpha: 0.08),
                          borderRadius: BorderRadius.circular(12),
                        ),
                        child: const Text(
                          'فائدة القسم: تجميع الأسماء التي تحتاج زيارة ميدانية من المسددين/غير المسددين، '
                          'ثم اعتماد «تمت المتابعة» مع ملاحظة تُحفظ في ملف الزبون.',
                          style: TextStyle(fontFamily: 'Cairo', fontSize: 12, height: 1.5),
                        ),
                      ),
                      Expanded(
                        child: groups.isEmpty
                            ? const Center(
                                child: Text(
                                  'لا توجد أسماء في المتابعة الميدانية',
                                  style: TextStyle(fontFamily: 'Cairo', color: Colors.grey),
                                ),
                              )
                            : RefreshIndicator(
                                onRefresh: _load,
                                child: ListView.separated(
                                  padding: const EdgeInsets.all(12),
                                  itemCount: groups.length,
                                  separatorBuilder: (_, __) =>
                                      const SizedBox(height: 12),
                                  itemBuilder: (context, i) {
                                    final rows = groups[i].value;
                                    return Container(
                                      decoration: BoxDecoration(
                                        color: Theme.of(context).cardColor,
                                        borderRadius: BorderRadius.circular(16),
                                        border: Border.all(
                                          color: AppTheme.primaryColor
                                              .withValues(alpha: 0.25),
                                        ),
                                      ),
                                      padding: const EdgeInsets.all(12),
                                      child: Column(
                                        crossAxisAlignment:
                                            CrossAxisAlignment.stretch,
                                        children: [
                                          Text(
                                            '${_listTitle(rows)} — ${rows.length}',
                                            style: const TextStyle(
                                              fontFamily: 'Cairo',
                                              fontWeight: FontWeight.bold,
                                              fontSize: 16,
                                            ),
                                          ),
                                          const SizedBox(height: 10),
                                          ...withNameSeparators(rows.map((item) {
                                            final name =
                                                '${item['customerName'] ?? item['CustomerName'] ?? ''}';
                                            final phone =
                                                '${item['phoneNumber'] ?? item['PhoneNumber'] ?? ''}';
                                            return Padding(
                                              padding: const EdgeInsets.symmetric(
                                                  vertical: 4),
                                              child: Row(
                                                children: [
                                                  Expanded(
                                                    child: Column(
                                                      crossAxisAlignment:
                                                          CrossAxisAlignment
                                                              .start,
                                                      children: [
                                                        PersonNameText(name),
                                                        if (phone.isNotEmpty)
                                                          Text(
                                                            phone,
                                                            style:
                                                                const TextStyle(
                                                              fontFamily:
                                                                  'Cairo',
                                                              color:
                                                                  Colors.grey,
                                                              fontSize: 12,
                                                            ),
                                                          ),
                                                      ],
                                                    ),
                                                  ),
                                                  ElevatedButton(
                                                    onPressed: () =>
                                                        _complete(item),
                                                    style: ElevatedButton
                                                        .styleFrom(
                                                      backgroundColor: Colors
                                                          .green.shade700,
                                                      foregroundColor:
                                                          Colors.white,
                                                    ),
                                                    child: const Text(
                                                      'تمت المتابعة',
                                                      style: TextStyle(
                                                          fontFamily: 'Cairo',
                                                          fontSize: 12),
                                                    ),
                                                  ),
                                                ],
                                              ),
                                            );
                                          })),
                                        ],
                                      ),
                                    );
                                  },
                                ),
                              ),
                      ),
                    ],
                  ),
      ),
    );
  }
}
