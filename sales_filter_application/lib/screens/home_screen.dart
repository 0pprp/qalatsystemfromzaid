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

class _HomeScreenState extends State<HomeScreen> with SingleTickerProviderStateMixin {
  late final FilterRepository _repo = widget.repository ?? FilterRepository();
  late final TabController _tabs = TabController(length: FilterStatuses.tabs.length, vsync: this);
  List<FilterCity> _cities = [];
  String? _city;
  bool _loadingCities = true;
  bool _loadingList = false;
  String? _error;
  List<FilterRequest> _items = [];

  @override
  void initState() {
    super.initState();
    _tabs.addListener(() {
      if (!_tabs.indexIsChanging) _loadList();
    });
    _bootstrap();
  }

  @override
  void dispose() {
    _tabs.dispose();
    super.dispose();
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
        // Single city: auto-select. Multiple: force explicit choice.
        _city = cities.length == 1 ? cities.first.cityValue : null;
        _loadingCities = false;
      });
      await _loadList();
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

  Future<void> _loadList() async {
    if (_city == null || _city!.isEmpty) {
      setState(() => _items = []);
      return;
    }
    setState(() {
      _loadingList = true;
      _error = null;
    });
    try {
      final status = FilterStatuses.tabs[_tabs.index].$1;
      final rows = await _repo.list(status: status, cityValue: _city);
      if (!mounted) return;
      setState(() {
        _items = rows;
        _loadingList = false;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _loadingList = false;
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

  Future<void> _open(FilterRequest row) async {
    final city = _city;
    if (city == null || city.isEmpty) return;
    final changed = await Navigator.of(context).push<bool>(
      MaterialPageRoute(
        builder: (_) => DetailScreen(
          requestId: row.id,
          cityValue: city,
          repository: _repo,
          dialer: widget.dialer ?? _defaultDial,
        ),
      ),
    );
    if (changed == true) await _loadList();
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
          title: const Text('فلترة المبيعات', style: TextStyle(fontFamily: 'Cairo')),
          actions: [
            IconButton(onPressed: _loadList, icon: const Icon(Icons.refresh)),
            IconButton(onPressed: _logout, icon: const Icon(Icons.logout)),
          ],
          bottom: TabBar(
            controller: _tabs,
            isScrollable: true,
            tabs: [for (final t in FilterStatuses.tabs) Tab(text: t.$2)],
          ),
        ),
        body: SafeArea(
          child: Column(
            children: [
              Padding(
                padding: const EdgeInsets.fromLTRB(16, 12, 16, 8),
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
                          await _loadList();
                        },
                ),
              ),
              if (_error != null)
                Padding(
                  padding: const EdgeInsets.symmetric(horizontal: 16),
                  child: Text(_error!, style: const TextStyle(fontFamily: 'Cairo', color: Colors.red)),
                ),
              Expanded(
                child: _loadingCities || _loadingList
                    ? const Center(child: CircularProgressIndicator())
                    : _items.isEmpty
                        ? const Center(child: Text('لا توجد طلبات', style: TextStyle(fontFamily: 'Cairo', color: Colors.grey)))
                        : ListView.builder(
                            padding: const EdgeInsets.all(12),
                            itemCount: _items.length,
                            itemBuilder: (_, i) => RequestCard(
                              request: _items[i],
                              onTap: () => _open(_items[i]),
                              onDial: (phone) async {
                                final cleaned = phone.replaceAll(RegExp(r'[^\d+]'), '');
                                if (cleaned.isEmpty) return;
                                final ok = await (widget.dialer ?? _defaultDial)(cleaned);
                                if (!ok && mounted) {
                                  ScaffoldMessenger.of(context).showSnackBar(
                                    const SnackBar(content: Text('تعذر فتح تطبيق الاتصال', style: TextStyle(fontFamily: 'Cairo'))),
                                  );
                                }
                              },
                            ),
                          ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
