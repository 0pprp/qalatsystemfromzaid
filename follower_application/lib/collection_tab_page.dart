import 'package:follower_application/AsyncIdChecker.dart';
import 'package:follower_application/collection_delegate/collection_delegate_shell.dart';
import 'package:follower_application/config/app_env.dart';
import 'package:follower_application/utils/AppTheme.dart';
import 'package:follower_application/utils/display_name.dart';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';
import 'dart:convert';

class CollectionTabPage extends StatefulWidget {
  const CollectionTabPage({super.key});

  @override
  State<CollectionTabPage> createState() => _CollectionTabPageState();
}

class _CollectionTabPageState extends State<CollectionTabPage> {
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
    };
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = '';
    });
    try {
      final s = await _session();
      final uri = Uri.parse('${s['link']}Followers/CollectionLists')
          .replace(queryParameters: {'asyncId': s['asyncId']});
      final res = await http.get(uri).timeout(const Duration(seconds: 30));
      if (res.statusCode == 401) {
        await AsyncIdChecker.logout();
        if (mounted) Navigator.pushReplacementNamed(context, '/Login');
        return;
      }
      if (res.statusCode != 200) throw Exception('collection');
      final data = json.decode(res.body) as Map;
      final raw = data['items'] ?? data['Items'] ?? [];
      final items = (raw as List).map((e) {
        final m = Map<String, dynamic>.from(e as Map);
        return {
          'listId': int.tryParse('${m['listId'] ?? m['ListId'] ?? 0}') ?? 0,
          'listName': '${m['listName'] ?? m['ListName'] ?? 'قائمة'}',
          'enabled': m['followerCollectionEnabled'] == true ||
              m['FollowerCollectionEnabled'] == true,
        };
      }).where((e) => (e['listId'] as int) > 0).toList();
      if (mounted) {
        setState(() {
          _items = items;
          _loading = false;
        });
      }
    } catch (_) {
      if (mounted) {
        setState(() {
          _loading = false;
          _error = 'تعذر تحميل قوائم الجباية';
        });
      }
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
            'الجباية',
            style: TextStyle(
              fontFamily: 'Cairo',
              fontWeight: FontWeight.bold,
              fontSize: 20,
            ),
          ),
          const SizedBox(height: 8),
          const Text(
            'ادخل فقط على القوائم المفعّلة من المحاسب الرئيسي',
            style: TextStyle(fontFamily: 'Cairo', color: Colors.grey),
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
            const Text('لا توجد قوائم',
                style: TextStyle(fontFamily: 'Cairo', color: Colors.grey))
          else
            ...withNameSeparators(_items.map((item) {
              final enabled = item['enabled'] as bool;
              return Material(
                color: Theme.of(context).cardColor,
                borderRadius: BorderRadius.circular(14),
                child: InkWell(
                  borderRadius: BorderRadius.circular(14),
                  onTap: enabled
                      ? () {
                          Navigator.push(
                            context,
                            MaterialPageRoute(
                              builder: (_) => CollectionDelegateShell(
                                listId: item['listId'] as int,
                                listName: item['listName'] as String,
                              ),
                            ),
                          );
                        }
                      : () {
                          ScaffoldMessenger.of(context).showSnackBar(
                            const SnackBar(
                              content: Text(
                                'هذه القائمة غير فعّالة للجباية',
                                style: TextStyle(fontFamily: 'Cairo'),
                              ),
                            ),
                          );
                        },
                  child: Padding(
                    padding: const EdgeInsets.all(16),
                    child: Row(
                      children: [
                        Expanded(
                          child: PersonNameText(
                            item['listName'] as String,
                          ),
                        ),
                        Container(
                          padding: const EdgeInsets.symmetric(
                              horizontal: 10, vertical: 4),
                          decoration: BoxDecoration(
                            color: enabled
                                ? Colors.green.shade50
                                : Colors.grey.shade200,
                            borderRadius: BorderRadius.circular(20),
                          ),
                          child: Text(
                            enabled ? 'فعال' : 'غير فعال',
                            style: TextStyle(
                              fontFamily: 'Cairo',
                              fontWeight: FontWeight.bold,
                              color: enabled
                                  ? Colors.green.shade800
                                  : Colors.grey.shade700,
                            ),
                          ),
                        ),
                        if (enabled) ...[
                          const SizedBox(width: 8),
                          const Icon(Icons.chevron_left),
                        ],
                      ],
                    ),
                  ),
                ),
              );
            })),
        ],
      ),
    );
  }
}
