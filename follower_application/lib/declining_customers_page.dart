import 'dart:convert';

import 'package:follower_application/AsyncIdChecker.dart';
import 'package:follower_application/config/app_env.dart';
import 'package:follower_application/utils/AppTheme.dart';
import 'package:follower_application/utils/display_name.dart';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';

class DecliningCustomersPage extends StatefulWidget {
  const DecliningCustomersPage({super.key});

  @override
  State<DecliningCustomersPage> createState() => _DecliningCustomersPageState();
}

class _DecliningCustomersPageState extends State<DecliningCustomersPage> {
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
      'asyncId': prefs.getString('AsyncId') ?? '',
      'link': AppEnv.apiBase(fallback: prefs.getString('LinkDelegate') ?? ''),
      'name': prefs.getString('DelegateName') ?? 'مسؤول اللجنة',
    };
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = '';
    });
    try {
      final s = await _session();
      final link = (s['link'] ?? '').trim();
      final asyncId = (s['asyncId'] ?? '').trim();
      if (link.isEmpty || asyncId.isEmpty) {
        throw Exception('جلسة غير مكتملة — أعد تسجيل الدخول');
      }
      final uri = Uri.parse('${link}Followers/DecliningCustomers')
          .replace(queryParameters: {'asyncId': asyncId});
      final res = await http.get(uri).timeout(const Duration(seconds: 90));
      if (res.statusCode == 401) {
        await AsyncIdChecker.logout();
        if (mounted) Navigator.pushReplacementNamed(context, '/Login');
        return;
      }
      if (res.statusCode != 200) {
        String msg = 'تعذر تحميل الزبائن المتدنية (${res.statusCode})';
        try {
          final body = json.decode(res.body);
          if (body is Map) {
            final m = '${body['message'] ?? body['Message'] ?? ''}'.trim();
            if (m.isNotEmpty) msg = m;
          }
        } catch (_) {}
        throw Exception(msg);
      }
      final decoded = json.decode(res.body);
      final data = decoded is Map ? decoded : <String, dynamic>{};
      final raw = data['items'] ?? data['Items'] ?? [];
      final items = (raw is List)
          ? raw
              .whereType<Map>()
              .map((e) => Map<String, dynamic>.from(e))
              .toList()
          : <Map<String, dynamic>>[];
      if (mounted) {
        setState(() {
          _items = items;
          _loading = false;
          _error = '';
        });
      }
    } catch (e) {
      if (mounted) {
        setState(() {
          _loading = false;
          // Empty declining list is OK; only show real failures.
          final msg = e.toString().replaceFirst('Exception: ', '');
          _error = msg.contains('Timeout')
              ? 'انتهت مهلة التحميل — اسحب للتحديث'
              : (msg.isEmpty ? 'تعذر تحميل الزبائن المتدنية' : msg);
        });
      }
    }
  }

  Future<void> _followUp(Map<String, dynamic> row) async {
    final customerId =
        int.tryParse('${row['customerId'] ?? row['CustomerId'] ?? 0}') ?? 0;
    final listId = int.tryParse('${row['listId'] ?? row['ListId'] ?? 0}') ?? 0;
    final name = '${row['customerName'] ?? row['CustomerName'] ?? ''}';
    final from = '${row['fromLabel'] ?? row['FromLabel'] ?? ''}';
    final to = '${row['toLabel'] ?? row['ToLabel'] ?? ''}';
    if (customerId <= 0 || listId <= 0) return;

    var draft = 'متابعة: هبوط التقييم من $from إلى $to — تم التواصل مع الزبون.';
    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text('متابعة — $name',
            style: const TextStyle(fontFamily: 'Cairo')),
        content: TextField(
          maxLines: 4,
          controller: TextEditingController(text: draft),
          onChanged: (v) => draft = v,
          decoration: const InputDecoration(
            hintText: 'نص المتابعة',
            hintStyle: TextStyle(fontFamily: 'Cairo'),
          ),
          style: const TextStyle(fontFamily: 'Cairo'),
        ),
        actions: [
          TextButton(
              onPressed: () => Navigator.pop(ctx, false),
              child: const Text('إلغاء', style: TextStyle(fontFamily: 'Cairo'))),
          ElevatedButton(
              onPressed: () => Navigator.pop(ctx, true),
              child: const Text('حفظ', style: TextStyle(fontFamily: 'Cairo'))),
        ],
      ),
    );
    final text = draft.trim();
    if (!mounted || ok != true || text.isEmpty) return;

    try {
      final s = await _session();
      final note = text.startsWith('متابعة:') || text.startsWith('[متابعة]')
          ? text
          : 'متابعة: $text';
      final uri =
          Uri.parse('${s['link']}Followers/Customers/$customerId/notes');
      final res = await http
          .post(
            uri,
            headers: {'Content-Type': 'application/json'},
            body: json.encode({
              'asyncId': s['asyncId'],
              'listId': listId,
              'noteText': note,
            }),
          )
          .timeout(const Duration(seconds: 20));
      if (!mounted) return;
      final msg = res.statusCode == 200
          ? 'تم تسجيل المتابعة على بروفايل الزبون'
          : 'تعذر حفظ المتابعة';
      ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(msg, style: const TextStyle(fontFamily: 'Cairo'))));
      if (res.statusCode == 200) {
        Navigator.pushNamed(context, '/CustomerProfile', arguments: {
          'customer': {
            'customerId': customerId,
            'customerName': name,
            'phoneNumber': '${row['phoneNumber'] ?? row['PhoneNumber'] ?? ''}',
          },
          'listId': listId,
        });
      }
    } catch (_) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(
          content: Text('تعذر الاتصال', style: TextStyle(fontFamily: 'Cairo'))));
    }
  }

  @override
  Widget build(BuildContext context) {
    return RefreshIndicator(
      onRefresh: _load,
      color: AppTheme.primaryColor,
      child: ListView(
        padding: const EdgeInsets.fromLTRB(16, 16, 16, 100),
        children: [
          const Text(
            'زبائن متدنية',
            style: TextStyle(
              fontFamily: 'Cairo',
              fontWeight: FontWeight.bold,
              fontSize: 20,
            ),
          ),
          const SizedBox(height: 8),
          const Text(
            'هبوط درجتين خلال أسبوع (ممتاز→ضعيف، جيد→متلكئ، ضعيف→سيء)',
            style: TextStyle(fontFamily: 'Cairo', color: Colors.grey, fontSize: 13),
          ),
          const SizedBox(height: 16),
          if (_loading)
            const Padding(
              padding: EdgeInsets.all(40),
              child: Center(child: CircularProgressIndicator()),
            )
          else if (_error.isNotEmpty)
            Text(_error,
                style: const TextStyle(fontFamily: 'Cairo', color: Colors.red))
          else if (_items.isEmpty)
            const Padding(
              padding: EdgeInsets.all(30),
              child: Center(
                child: Text('لا توجد زبائن متدنية حالياً',
                    style: TextStyle(fontFamily: 'Cairo', color: Colors.grey)),
              ),
            )
          else
            ...withNameSeparators(_items.map((row) {
              final name = '${row['customerName'] ?? row['CustomerName'] ?? ''}';
              final from = '${row['fromLabel'] ?? row['FromLabel'] ?? ''}';
              final to = '${row['toLabel'] ?? row['ToLabel'] ?? ''}';
              final listName = '${row['listName'] ?? row['ListName'] ?? ''}';
              final days = row['daysBetween'] ?? row['DaysBetween'] ?? '';
              return Material(
                color: Theme.of(context).cardColor,
                borderRadius: BorderRadius.circular(14),
                child: Padding(
                  padding: const EdgeInsets.all(14),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      PersonNameText(name),
                      const SizedBox(height: 4),
                      Text(
                        '$from → $to'
                        '${listName.isEmpty ? '' : ' · $listName'}'
                        '${days == '' ? '' : ' · خلال $days يوم'}',
                        style: const TextStyle(
                            fontFamily: 'Cairo', color: Colors.grey),
                      ),
                      const SizedBox(height: 10),
                      Align(
                        alignment: Alignment.centerLeft,
                        child: ElevatedButton.icon(
                          onPressed: () => _followUp(row),
                          icon: const Icon(Icons.note_alt_outlined, size: 18),
                          label: const Text('متابعة',
                              style: TextStyle(fontFamily: 'Cairo')),
                          style: ElevatedButton.styleFrom(
                            backgroundColor: AppTheme.primaryColor,
                            foregroundColor: Colors.white,
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
              );
            })),
        ],
      ),
    );
  }
}
