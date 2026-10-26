import 'package:delegated_manager_application/core/network/api_client.dart';
import 'package:delegated_manager_application/core/theme/app_theme.dart';
import 'package:delegated_manager_application/core/utils/json_read.dart';
import 'package:flutter/material.dart';
import 'package:intl/intl.dart' hide TextDirection;

class BranchVisitsScreen extends StatefulWidget {
  const BranchVisitsScreen({super.key});

  @override
  State<BranchVisitsScreen> createState() => BranchVisitsScreenState();
}

class BranchVisitsScreenState extends State<BranchVisitsScreen> {
  bool _loading = true;
  String? _error;
  List<Map<String, dynamic>> _rows = [];

  @override
  void initState() {
    super.initState();
    reload();
  }

  Future<void> reload() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final json = await ApiClient.get('delegated-manager/branch-visits?take=100');
      final list = JsonRead.list(json);
      if (!mounted) return;
      setState(() {
        _rows = list;
        _loading = false;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _error = '$e';
      });
    }
  }

  String _fmt(dynamic v) {
    final d = DateTime.tryParse('$v')?.toLocal();
    if (d == null) return '—';
    return DateFormat('yyyy/MM/dd hh:mm a', 'en').format(d);
  }

  @override
  Widget build(BuildContext context) {
    if (_loading && _rows.isEmpty) {
      return const Center(child: CircularProgressIndicator());
    }
    if (_error != null && _rows.isEmpty) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(_error!,
                  textAlign: TextAlign.center,
                  style: const TextStyle(fontFamily: 'Cairo', color: Colors.red)),
              const SizedBox(height: 12),
              FilledButton(
                onPressed: reload,
                style: FilledButton.styleFrom(backgroundColor: AppColors.primary),
                child: const Text('إعادة المحاولة',
                    style: TextStyle(fontFamily: 'Cairo')),
              ),
            ],
          ),
        ),
      );
    }
    if (_rows.isEmpty) {
      return const Center(
        child: Text('لا توجد زيارات مسجّلة',
            style: TextStyle(fontFamily: 'Cairo', color: Colors.grey)),
      );
    }
    return RefreshIndicator(
      onRefresh: reload,
      child: ListView.separated(
        padding: const EdgeInsets.all(16),
        itemCount: _rows.length,
        separatorBuilder: (_, __) => const SizedBox(height: 10),
        itemBuilder: (context, i) {
          final row = _rows[i];
          final city = JsonRead.text(row['cityName'] ?? row['cityValue']);
          final status = JsonRead.text(row['status']);
          final members = JsonRead.list(row['members']);
          final ratings = JsonRead.list(row['delegateRatings']);
          final notes = JsonRead.text(row['notes']);
          return Card(
            child: Padding(
              padding: const EdgeInsets.all(14),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Row(
                    children: [
                      Expanded(
                        child: Text(
                          city.isEmpty ? 'محافظة' : city,
                          style: const TextStyle(
                              fontFamily: 'Cairo',
                              fontWeight: FontWeight.bold,
                              fontSize: 16),
                        ),
                      ),
                      Chip(
                        label: Text(
                          status == 'Open' ? 'مفتوحة' : 'مغلقة',
                          style: const TextStyle(fontFamily: 'Cairo', fontSize: 12),
                        ),
                      ),
                    ],
                  ),
                  Text(
                    JsonRead.text(row['openedByDisplayName']),
                    style: const TextStyle(fontFamily: 'Cairo', color: Colors.grey),
                  ),
                  const SizedBox(height: 6),
                  Text('الوصول: ${_fmt(row['arrivedAtUtc'])}',
                      style: const TextStyle(fontFamily: 'Cairo', fontSize: 13)),
                  Text('المغادرة: ${_fmt(row['departedAtUtc'])}',
                      style: const TextStyle(fontFamily: 'Cairo', fontSize: 13)),
                  const SizedBox(height: 6),
                  Text(
                    'الأعضاء: ${members.map((m) => JsonRead.text(JsonRead.map(m)['displayName'])).where((s) => s.isNotEmpty).join('، ')}',
                    style: const TextStyle(fontFamily: 'Cairo', fontSize: 13),
                  ),
                  if (ratings.isNotEmpty) ...[
                    const SizedBox(height: 6),
                    ...ratings.map((r) {
                      final m = JsonRead.map(r);
                      return Text(
                        '${JsonRead.text(m['delegateName'])}: ${m['score']}/10',
                        style: const TextStyle(fontFamily: 'Cairo', fontSize: 13),
                      );
                    }),
                  ],
                  if (notes.isNotEmpty) ...[
                    const SizedBox(height: 8),
                    Text(notes,
                        style: const TextStyle(fontFamily: 'Cairo', fontSize: 13)),
                  ],
                ],
              ),
            ),
          );
        },
      ),
    );
  }
}
