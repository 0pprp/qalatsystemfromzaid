import 'package:branch_manager_application/core/api.dart';
import 'package:branch_manager_application/core/session.dart';
import 'package:branch_manager_application/theme/bm_theme.dart';
import 'package:branch_manager_application/ui/customer_profile_page.dart';
import 'package:flutter/material.dart';
import 'package:intl/intl.dart' hide TextDirection;

/// زبائن انخفض تقييمهم من ممتاز إلى سيء/متلكئ خلال 15 يوماً.
class DecliningCustomersPage extends StatefulWidget {
  const DecliningCustomersPage({super.key});

  @override
  State<DecliningCustomersPage> createState() => _DecliningCustomersPageState();
}

class _DecliningCustomersPageState extends State<DecliningCustomersPage> {
  bool _loading = true;
  String? _error;
  List<Map<String, dynamic>> _items = [];
  final _date = DateFormat('yyyy-MM-dd');

  @override
  void initState() {
    super.initState();
    _load(refresh: true);
  }

  Future<void> _load({bool refresh = true}) async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final rows = await BmApi.instance.listDecliningCustomers(refresh: refresh);
      if (!mounted) return;
      setState(() {
        _items = rows;
        _loading = false;
      });
    } catch (e) {
      if (!mounted) return;
      if (e is BmApiException && e.statusCode == 401) {
        await BmSession.instance.clear();
      }
      setState(() {
        _loading = false;
        _error = '$e';
      });
    }
  }

  String _s(Map m, List<String> keys) {
    for (final k in keys) {
      final v = m[k];
      if (v != null && '$v'.trim().isNotEmpty) return '$v'.trim();
    }
    return '';
  }

  int _i(Map m, List<String> keys) {
    for (final k in keys) {
      final v = m[k];
      if (v is int) return v;
      final p = int.tryParse('$v');
      if (p != null) return p;
    }
    return 0;
  }

  String _fmtDate(dynamic v) {
    if (v == null) return '—';
    if (v is String) {
      final m = RegExp(r'^(\d{4}-\d{2}-\d{2})').firstMatch(v);
      if (m != null) return m.group(1)!;
      final d = DateTime.tryParse(v);
      if (d != null) return _date.format(d);
      return v;
    }
    if (v is DateTime) return _date.format(v);
    return '$v';
  }

  void _openProfile(Map<String, dynamic> row) {
    final id = _i(row, ['customerId', 'CustomerId', 'customerID']);
    if (id <= 0) return;
    Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => CustomerProfilePage(customerId: id),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Directionality(
      textDirection: TextDirection.rtl,
      child: RefreshIndicator(
        onRefresh: () => _load(refresh: true),
        child: ListView(
          padding: const EdgeInsets.fromLTRB(16, 12, 16, 24),
          children: [
            Text(
              'زبائن كان تقييمهم ممتاز ثم أصبح سيء أو متلكئ خلال 15 يوماً',
              style: TextStyle(
                fontFamily: 'Cairo',
                color: BmTheme.muted,
                fontSize: 13,
              ),
            ),
            const SizedBox(height: 12),
            if (_loading)
              const Padding(
                padding: EdgeInsets.only(top: 48),
                child: Center(child: CircularProgressIndicator()),
              )
            else if (_error != null)
              Padding(
                padding: const EdgeInsets.only(top: 24),
                child: Column(
                  children: [
                    Text(_error!, style: const TextStyle(fontFamily: 'Cairo', color: Colors.red)),
                    const SizedBox(height: 12),
                    FilledButton(
                      onPressed: () => _load(refresh: true),
                      child: const Text('إعادة المحاولة', style: TextStyle(fontFamily: 'Cairo')),
                    ),
                  ],
                ),
              )
            else if (_items.isEmpty)
              Padding(
                padding: const EdgeInsets.only(top: 48),
                child: Center(
                  child: Text(
                    'لا يوجد زبائن متدنية حالياً',
                    style: TextStyle(fontFamily: 'Cairo', color: BmTheme.muted),
                  ),
                ),
              )
            else
              ..._items.map((row) {
                final name = _s(row, ['customerName', 'CustomerName']);
                final phone = _s(row, ['phoneNumber', 'PhoneNumber']);
                final delegate = _s(row, ['delegateName', 'DelegateName']);
                final from = _s(row, ['fromLabel', 'FromLabel']);
                final to = _s(row, ['toLabel', 'ToLabel']);
                final days = _i(row, ['daysBetween', 'DaysBetween']);
                return Card(
                  margin: const EdgeInsets.only(bottom: 10),
                  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
                  child: ListTile(
                    onTap: () => _openProfile(row),
                    title: Text(
                      name.isEmpty ? 'زبون #${_i(row, ['customerId', 'CustomerId'])}' : name,
                      style: const TextStyle(fontFamily: 'Cairo', fontWeight: FontWeight.w700),
                    ),
                    subtitle: Padding(
                      padding: const EdgeInsets.only(top: 4),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            '$from → $to · خلال $days يوم',
                            style: const TextStyle(fontFamily: 'Cairo', fontSize: 13),
                          ),
                          if (phone.isNotEmpty)
                            Text(phone, style: TextStyle(fontFamily: 'Cairo', color: BmTheme.muted, fontSize: 12)),
                          if (delegate.isNotEmpty)
                            Text('القائمة: $delegate', style: TextStyle(fontFamily: 'Cairo', color: BmTheme.muted, fontSize: 12)),
                          Text(
                            'ممتاز: ${_fmtDate(row['priorExcellentDate'] ?? row['PriorExcellentDate'])}'
                            ' · الآن: ${_fmtDate(row['currentCaptureDate'] ?? row['CurrentCaptureDate'])}',
                            style: TextStyle(fontFamily: 'Cairo', color: BmTheme.muted, fontSize: 11),
                          ),
                        ],
                      ),
                    ),
                    trailing: const Icon(Icons.chevron_left),
                  ),
                );
              }),
          ],
        ),
      ),
    );
  }
}
