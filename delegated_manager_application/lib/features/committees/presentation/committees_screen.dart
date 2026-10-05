import 'dart:convert';

import 'package:delegated_manager_application/core/config/app_env.dart';
import 'package:delegated_manager_application/core/config/province_catalog.dart';
import 'package:delegated_manager_application/core/network/api_client.dart';
import 'package:delegated_manager_application/core/utils/json_read.dart';
import 'package:delegated_manager_application/features/committees/presentation/main_follower_live_map.dart';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;

/// Phase 1: create committee (2 main followers) + mission (cities).
class CommitteesScreen extends StatefulWidget {
  const CommitteesScreen({super.key, this.tabController});

  /// When hosted in HomeShell the parent owns the TabBar in the top AppBar.
  final TabController? tabController;

  @override
  State<CommitteesScreen> createState() => CommitteesScreenState();
}

class CommitteesScreenState extends State<CommitteesScreen>
    with SingleTickerProviderStateMixin {
  final _name = TextEditingController(text: 'لجنة 1');
  final _note = TextEditingController();
  final _mapKey = GlobalKey<MainFollowerLiveMapState>();
  TabController? _ownedTabs;
  TabController get _tabs => widget.tabController ?? _ownedTabs!;
  List<Map<String, dynamic>> _followers = [];
  String? _m1;
  String? _m2;
  List<Map<String, dynamic>> _missions = [];
  List<ProvinceOption> _provinces = [];
  final Set<String> _selectedCities = {};
  String? _error;
  bool _busy = false;

  @override
  void initState() {
    super.initState();
    if (widget.tabController == null) {
      _ownedTabs = TabController(length: 2, vsync: this);
    }
    _provinces = ProvinceCatalog.forEnv(AppEnv.name);
    if (AppEnv.isDemo && _provinces.isNotEmpty) {
      _selectedCities.add(_provinces.first.value);
    }
    reload();
  }

  Future<void> reload() async {
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await _ensureProvinces();
      final followersRaw =
          await ApiClient.get('delegated-manager/committees/main-followers');
      final missionsRaw =
          await ApiClient.get('delegated-manager/committees/missions');
      if (!mounted) return;
      final followers = followersRaw is List ? followersRaw : const [];
      final missions = missionsRaw is List ? missionsRaw : const [];
      setState(() {
        _followers = followers
            .whereType<Map>()
            .map((e) => Map<String, dynamic>.from(e))
            .toList();
        _missions = missions
            .whereType<Map>()
            .map((e) => Map<String, dynamic>.from(e))
            .toList();
        _m1 ??= _followers.isNotEmpty
            ? JsonRead.text(_followers.first['userName'])
            : null;
        _m2 ??= _followers.length > 1
            ? JsonRead.text(_followers[1]['userName'])
            : null;
      });
      _mapKey.currentState?.reload();
    } catch (e) {
      if (mounted) setState(() => _error = e.toString());
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _ensureProvinces() async {
    if (!AppEnv.isProduction) {
      final local = ProvinceCatalog.forEnv(AppEnv.name);
      if (!mounted) return;
      setState(() {
        _provinces = local;
        if (AppEnv.isDemo &&
            local.isNotEmpty &&
            _selectedCities.isEmpty) {
          _selectedCities.add(local.first.value);
        }
      });
      return;
    }
    if (_provinces.isNotEmpty) return;
    try {
      final response = await http
          .get(
            Uri.parse(ProvinceCatalog.getAdminUrl),
            headers: const {'Accept': 'application/json'},
          )
          .timeout(ApiClient.timeout);
      if (response.statusCode < 200 || response.statusCode >= 300) {
        throw ApiException('تعذر تحميل المحافظات (${response.statusCode})');
      }
      final decoded = jsonDecode(response.body);
      final list = decoded is List ? decoded : const [];
      final mapped = ProvinceCatalog.fromGetAdmin(list);
      if (!mounted) return;
      setState(() => _provinces = mapped);
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _error = e is ApiException ? e.message : 'تعذر تحميل قائمة المحافظات';
      });
    }
  }

  Future<void> _create() async {
    if (_selectedCities.isEmpty) {
      setState(() => _error = 'اختر محافظة واحدة على الأقل');
      return;
    }
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final committeeRaw =
          await ApiClient.post('delegated-manager/committees', body: {
        'name': _name.text.trim(),
        'memberUserNames': [_m1, _m2],
      });
      final committee = committeeRaw is Map
          ? Map<String, dynamic>.from(committeeRaw)
          : <String, dynamic>{};
      final id = JsonRead.number(committee['id']);
      await ApiClient.post('delegated-manager/committees/missions', body: {
        'committeeId': id,
        'cityValues': _selectedCities.toList()..sort(),
        'note': _note.text.trim(),
      });
      await reload();
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('تم إنشاء اللجنة والمهمة')),
        );
      }
    } catch (e) {
      if (mounted) setState(() => _error = e.toString());
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  String _cityLabel(String value) {
    for (final p in _provinces) {
      if (p.value == value) return p.name;
    }
    return value;
  }

  @override
  void dispose() {
    _ownedTabs?.dispose();
    _name.dispose();
    _note.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final names = _followers
        .map((f) => JsonRead.text(f['userName']))
        .where((e) => e.isNotEmpty)
        .toList();
    return Scaffold(
      body: TabBarView(
        controller: _tabs,
        children: [
          ListView(
            padding: const EdgeInsets.fromLTRB(16, 16, 16, 32),
            children: [
              if (_error != null)
                Padding(
                  padding: const EdgeInsets.only(bottom: 12),
                  child: Text(_error!, style: const TextStyle(color: Colors.red)),
                ),
              TextField(
                controller: _name,
                decoration: const InputDecoration(
                  labelText: 'اسم اللجنة',
                  border: OutlineInputBorder(),
                ),
              ),
              const SizedBox(height: 12),
              DropdownButtonFormField<String>(
                value: names.contains(_m1) ? _m1 : null,
                decoration: const InputDecoration(
                  labelText: 'متابع رئيسي 1',
                  border: OutlineInputBorder(),
                ),
                items: names
                    .map((n) => DropdownMenuItem(value: n, child: Text(n)))
                    .toList(),
                onChanged: (v) => setState(() => _m1 = v),
              ),
              const SizedBox(height: 12),
              DropdownButtonFormField<String>(
                value: names.contains(_m2) ? _m2 : null,
                decoration: const InputDecoration(
                  labelText: 'متابع رئيسي 2',
                  border: OutlineInputBorder(),
                ),
                items: names
                    .map((n) => DropdownMenuItem(value: n, child: Text(n)))
                    .toList(),
                onChanged: (v) => setState(() => _m2 = v),
              ),
              const SizedBox(height: 12),
              Text('المحافظات', style: Theme.of(context).textTheme.titleSmall),
              const SizedBox(height: 8),
              if (_provinces.isEmpty)
                const Text('لا توجد محافظات متاحة')
              else
                ..._provinces.map((p) {
                  final selected = _selectedCities.contains(p.value);
                  return CheckboxListTile(
                    dense: true,
                    contentPadding: EdgeInsets.zero,
                    title: Text(p.name),
                    value: selected,
                    onChanged: (v) {
                      setState(() {
                        if (v == true) {
                          _selectedCities.add(p.value);
                        } else {
                          _selectedCities.remove(p.value);
                        }
                      });
                    },
                  );
                }),
              const SizedBox(height: 12),
              TextField(
                controller: _note,
                decoration: const InputDecoration(
                  labelText: 'ملاحظة التكليف',
                  border: OutlineInputBorder(),
                ),
              ),
              const SizedBox(height: 16),
              FilledButton(
                onPressed: _busy ? null : _create,
                child: const Text('إنشاء لجنة + مهمة'),
              ),
              const SizedBox(height: 24),
              Text('المهام', style: Theme.of(context).textTheme.titleMedium),
              const SizedBox(height: 8),
              if (_missions.isEmpty) const Text('لا توجد مهام بعد'),
              ..._missions.map((m) {
                final rawCities = (m['cityValues'] as List?) ?? const [];
                final cities = rawCities
                    .map((c) => _cityLabel(c.toString()))
                    .where((e) => e.isNotEmpty)
                    .join('، ');
                final id = JsonRead.number(m['id']);
                return ListTile(
                  title: Text(JsonRead.text(m['committeeName'])),
                  subtitle: Text('${JsonRead.text(m['status'])} — $cities'),
                  trailing: const Icon(Icons.insights_outlined),
                  onTap: id <= 0 ? null : () => _showSummary(id),
                );
              }),
            ],
          ),
          MainFollowerLiveMap(key: _mapKey),
        ],
      ),
    );
  }

  Future<void> _showSummary(num missionId) async {
    try {
      final raw = await ApiClient.get(
        'delegated-manager/committees/missions/${missionId.toInt()}/summary',
      );
      if (!mounted) return;
      final m =
          raw is Map ? Map<String, dynamic>.from(raw) : <String, dynamic>{};
      await showDialog<void>(
        context: context,
        builder: (ctx) => AlertDialog(
          title: Text(JsonRead.text(m['committeeName']).isEmpty
              ? 'ملخص المهمة'
              : JsonRead.text(m['committeeName'])),
          content: Text(
            [
              'تقييمات المبيعات: ${JsonRead.number(m['saleReviewCount'])}',
              'متوسط المبيعات: ${m['avgSaleScore'] ?? '—'}',
              'تقييمات الموظفين: ${JsonRead.number(m['staffReviewCount'])}',
              'متوسط الموظفين: ${m['avgStaffScore'] ?? '—'}',
            ].join('\n'),
          ),
          actions: [
            TextButton(
                onPressed: () => Navigator.pop(ctx),
                child: const Text('إغلاق')),
          ],
        ),
      );
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context)
            .showSnackBar(SnackBar(content: Text('$e')));
      }
    }
  }
}
