import 'package:flutter/material.dart';
import 'package:sales_filter_application/models/filter_models.dart';
import 'package:sales_filter_application/screens/detail_screen.dart';
import 'package:sales_filter_application/screens/login_screen.dart';
import 'package:sales_filter_application/services/api_client.dart';
import 'package:sales_filter_application/services/filter_repository.dart';
import 'package:sales_filter_application/services/session.dart';
import 'package:sales_filter_application/theme/app_theme.dart';
import 'package:sales_filter_application/widgets/request_card.dart';
import 'package:url_launcher/url_launcher.dart';

class HomeScreen extends StatefulWidget {
  const HomeScreen({super.key, this.repository, this.dialer});

  @visibleForTesting
  final FilterRepository? repository;

  @visibleForTesting
  final Future<bool> Function(String tel)? dialer;

  @override
  State<HomeScreen> createState() => _HomeScreenState();
}

class _HomeScreenState extends State<HomeScreen> {
  late final FilterRepository _repo = widget.repository ?? FilterRepository();
  List<FilterCity> _cities = [];
  String? _city;
  bool _loadingCities = true;
  bool _loadingCounts = false;
  String? _error;
  Map<String, int> _counts = {};

  @override
  void initState() {
    super.initState();
    _bootstrap();
  }

  Future<void> _bootstrap() async {
    setState(() {
      _loadingCities = true;
      _error = null;
    });
    try {
      final cities = await _repo.myCities();
      if (!mounted) return;
      setState(() {
        _cities = cities;
        _city = cities.length == 1 ? cities.first.cityValue : null;
        _loadingCities = false;
      });
      await _loadCounts();
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _loadingCities = false;
        _error = e.toString();
      });
      if (e is ApiException && e.statusCode == 401) {
        await _logout();
      }
    }
  }

  Future<void> _loadCounts() async {
    if (_city == null || _city!.isEmpty) {
      setState(() => _counts = {});
      return;
    }
    setState(() {
      _loadingCounts = true;
      _error = null;
    });
    try {
      final counts = await _repo.counts(cityValue: _city!);
      if (!mounted) return;
      setState(() {
        _counts = counts;
        _loadingCounts = false;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _loadingCounts = false;
        _error = e.toString();
      });
    }
  }

  Future<void> _logout() async {
    await Session.clear();
    if (!mounted) return;
    Navigator.of(context).pushAndRemoveUntil(
      MaterialPageRoute(builder: (_) => const LoginScreen()),
      (_) => false,
    );
  }

  Future<void> _openBin(int index) async {
    final city = _city;
    if (city == null || city.isEmpty) return;
    final bin = FilterStatuses.bins[index];
    await Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => StatusListScreen(
          status: bin.status,
          title: bin.title,
          cityValue: city,
          repository: _repo,
          dialer: widget.dialer ?? _defaultDial,
        ),
      ),
    );
    await _loadCounts();
  }

  Future<bool> _defaultDial(String tel) async {
    final uri = Uri(scheme: 'tel', path: tel);
    return launchUrl(uri);
  }

  @override
  Widget build(BuildContext context) {
    return Directionality(
      textDirection: TextDirection.rtl,
      child: Scaffold(
        appBar: AppBar(
          title: const Text('تطبيق فلترة المبيعات'),
          actions: [
            IconButton(onPressed: _loadCounts, icon: const Icon(Icons.refresh)),
            IconButton(onPressed: _logout, icon: const Icon(Icons.logout)),
          ],
        ),
        body: SafeArea(
          child: Column(
            children: [
              Padding(
                padding: const EdgeInsets.fromLTRB(AppSpacing.md, AppSpacing.md, AppSpacing.md, AppSpacing.sm),
                child: DropdownButtonFormField<String>(
                  key: ValueKey('city-$_city-${_cities.length}'),
                  value: _city,
                  decoration: const InputDecoration(
                    labelText: 'المحافظة',
                    border: OutlineInputBorder(),
                  ),
                  hint: const Text('اختر المحافظة', style: TextStyle(fontFamily: 'Cairo')),
                  items: [
                    ..._cities.map(
                      (c) => DropdownMenuItem(value: c.cityValue, child: Text(c.label, style: const TextStyle(fontFamily: 'Cairo'))),
                    ),
                  ],
                  onChanged: _loadingCities || _cities.isEmpty
                      ? null
                      : (v) async {
                          setState(() => _city = v);
                          await _loadCounts();
                        },
                ),
              ),
              if (_error != null)
                Padding(
                  padding: const EdgeInsets.symmetric(horizontal: AppSpacing.md),
                  child: Text(_error!, style: const TextStyle(fontFamily: 'Cairo', color: AppColors.danger)),
                ),
              Expanded(
                child: _loadingCities || (_loadingCounts && _counts.isEmpty)
                    ? const Center(child: CircularProgressIndicator())
                    : (_city == null || _city!.isEmpty)
                        ? const Center(child: Text('اختر المحافظة أولاً', style: TextStyle(fontFamily: 'Cairo', color: AppColors.muted)))
                        : RefreshIndicator(
                            onRefresh: _loadCounts,
                            child: ListView.separated(
                              padding: const EdgeInsets.fromLTRB(AppSpacing.md, AppSpacing.sm, AppSpacing.md, AppSpacing.lg),
                              itemCount: FilterStatuses.bins.length,
                              separatorBuilder: (_, __) => const SizedBox(height: AppSpacing.md),
                              itemBuilder: (context, i) => _binCard(i),
                            ),
                          ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _binCard(int index) {
    final bin = FilterStatuses.bins[index];
    final count = _counts[bin.status] ?? 0;
    return Card(
      child: InkWell(
        onTap: () => _openBin(index),
        borderRadius: BorderRadius.circular(AppRadius.md),
        child: Container(
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(AppRadius.md),
            border: Border(right: BorderSide(color: bin.color, width: 5)),
          ),
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: AppSpacing.md, vertical: AppSpacing.lg),
            child: Row(
              children: [
                CircleAvatar(
                  radius: 26,
                  backgroundColor: bin.color.withValues(alpha: 0.12),
                  child: Icon(bin.icon, size: 30, color: bin.color),
                ),
                const SizedBox(width: AppSpacing.md),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        bin.title,
                        style: TextStyle(fontFamily: 'Cairo', fontWeight: FontWeight.w800, fontSize: 18, color: bin.color),
                      ),
                      const SizedBox(height: 4),
                      Text(
                        '$count',
                        style: const TextStyle(fontFamily: 'Cairo', fontSize: 22, fontWeight: FontWeight.w800, color: AppColors.text),
                      ),
                    ],
                  ),
                ),
                Icon(Icons.chevron_left, color: AppColors.muted.withValues(alpha: 0.8), size: 28),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class StatusListScreen extends StatefulWidget {
  const StatusListScreen({
    super.key,
    required this.status,
    required this.title,
    required this.cityValue,
    required this.repository,
    required this.dialer,
  });

  final String status;
  final String title;
  final String cityValue;
  final FilterRepository repository;
  final Future<bool> Function(String tel) dialer;

  @override
  State<StatusListScreen> createState() => _StatusListScreenState();
}

class _StatusListScreenState extends State<StatusListScreen> {
  bool _loading = true;
  String? _error;
  List<FilterRequest> _items = [];

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
      final rows = await widget.repository.list(status: widget.status, cityValue: widget.cityValue);
      if (!mounted) return;
      setState(() {
        _items = rows;
        _loading = false;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _error = e.toString();
      });
    }
  }

  Future<void> _open(FilterRequest row) async {
    final changed = await Navigator.of(context).push<bool>(
      MaterialPageRoute(
        builder: (_) => DetailScreen(
          requestId: row.id,
          cityValue: widget.cityValue,
          repository: widget.repository,
          dialer: widget.dialer,
        ),
      ),
    );
    if (changed == true) await _load();
  }

  @override
  Widget build(BuildContext context) {
    return Directionality(
      textDirection: TextDirection.rtl,
      child: Scaffold(
        appBar: AppBar(title: Text(widget.title)),
        body: SafeArea(
          child: _loading
              ? const Center(child: CircularProgressIndicator())
              : _error != null
                  ? Center(child: Text(_error!, style: const TextStyle(fontFamily: 'Cairo', color: AppColors.danger)))
                  : _items.isEmpty
                      ? const Center(child: Text('لا توجد طلبات', style: TextStyle(fontFamily: 'Cairo', color: AppColors.muted)))
                      : RefreshIndicator(
                          onRefresh: _load,
                          child: ListView.builder(
                            padding: const EdgeInsets.all(AppSpacing.md),
                            itemCount: _items.length,
                            itemBuilder: (_, i) => RequestCard(
                              request: _items[i],
                              onTap: () => _open(_items[i]),
                              onDial: (phone) async {
                                final cleaned = phone.replaceAll(RegExp(r'[^\d+]'), '');
                                if (cleaned.isEmpty) return;
                                final ok = await widget.dialer(cleaned);
                                if (!ok && mounted) {
                                  ScaffoldMessenger.of(context).showSnackBar(
                                    const SnackBar(content: Text('تعذر فتح تطبيق الاتصال', style: TextStyle(fontFamily: 'Cairo'))),
                                  );
                                }
                              },
                            ),
                          ),
                        ),
        ),
      ),
    );
  }
}
