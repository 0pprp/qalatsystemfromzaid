import 'package:branch_manager_application/core/api.dart';
import 'package:branch_manager_application/core/session.dart';
import 'package:branch_manager_application/theme/bm_theme.dart';
import 'package:flutter/material.dart';
import 'package:intl/intl.dart' hide TextDirection;

/// Payments by list (delegate) + date range — summary + payer names.
class PaymentsPage extends StatefulWidget {
  const PaymentsPage({super.key});

  @override
  State<PaymentsPage> createState() => _PaymentsPageState();
}

class _PaymentsPageState extends State<PaymentsPage> {
  final _money = NumberFormat('#,##0', 'en');
  final _df = DateFormat('yyyy-MM-dd');

  late DateTime _from;
  late DateTime _to;
  int _delegateId = 0;

  bool _loadingLists = true;
  bool _loading = false;
  String? _error;
  List<Map<String, dynamic>> _lists = [];
  List<Map<String, dynamic>> _rows = [];

  @override
  void initState() {
    super.initState();
    final now = DateTime.now();
    _to = DateTime(now.year, now.month, now.day);
    _from = _to.subtract(const Duration(days: 6));
    _loadLists();
  }

  String _s(Map? m, List<String> keys) {
    if (m == null) return '';
    for (final k in keys) {
      final v = m[k];
      if (v != null && '$v'.trim().isNotEmpty) return '$v'.trim();
    }
    return '';
  }

  double _d(Map m, List<String> keys) {
    for (final k in keys) {
      final v = m[k];
      if (v is num) return v.toDouble();
      final p = double.tryParse('$v');
      if (p != null) return p;
    }
    return 0;
  }

  Future<void> _loadLists() async {
    setState(() {
      _loadingLists = true;
      _error = null;
    });
    try {
      final cat = await BmApi.instance.statsCatalog();
      if (!mounted) return;
      final lists = (cat['lists'] ?? cat['Lists']);
      final parsed = lists is List
          ? lists.map((e) => Map<String, dynamic>.from(e as Map)).toList()
          : <Map<String, dynamic>>[];
      setState(() {
        _lists = parsed;
        _loadingLists = false;
        if (_delegateId == 0 && parsed.isNotEmpty) {
          _delegateId = int.tryParse(_s(parsed.first, ['id', 'Id'])) ?? 0;
        }
      });
      if (_delegateId > 0) await _load();
    } catch (e) {
      if (!mounted) return;
      if (e is BmApiException && e.statusCode == 401) {
        await BmSession.instance.clear();
      }
      setState(() {
        _loadingLists = false;
        _error = '$e';
      });
    }
  }

  Future<void> _load() async {
    if (_delegateId <= 0) {
      setState(() {
        _rows = [];
        _error = 'اختر قائمة أولاً';
      });
      return;
    }
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final rows = await BmApi.instance.listPayments(
        from: _from,
        to: _to,
        delegateId: _delegateId,
      );
      if (!mounted) return;
      setState(() {
        _rows = rows;
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

  Future<void> _pickFrom() async {
    final d = await showDatePicker(
      context: context,
      initialDate: _from,
      firstDate: DateTime(2020),
      lastDate: _to,
    );
    if (d == null) return;
    setState(() => _from = DateTime(d.year, d.month, d.day));
    await _load();
  }

  Future<void> _pickTo() async {
    final d = await showDatePicker(
      context: context,
      initialDate: _to,
      firstDate: _from,
      lastDate: DateTime.now().add(const Duration(days: 1)),
    );
    if (d == null) return;
    setState(() => _to = DateTime(d.year, d.month, d.day));
    await _load();
  }

  double get _totalAmount =>
      _rows.fold<double>(0, (a, r) => a + _d(r, ['amountDenar', 'AmountDenar']));

  int get _count => _rows.length;

  @override
  Widget build(BuildContext context) {
    return RefreshIndicator(
      onRefresh: _load,
      child: ListView(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.fromLTRB(16, 12, 16, 24),
        children: [
          Container(
            padding: const EdgeInsets.all(14),
            decoration: BoxDecoration(
              gradient: const LinearGradient(
                colors: [Color(0xFF0F5C5C), Color(0xFF1A7A6D)],
              ),
              borderRadius: BorderRadius.circular(18),
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Text(
                  'التسديدات',
                  style: TextStyle(
                    fontFamily: 'Cairo',
                    color: Colors.white,
                    fontWeight: FontWeight.bold,
                    fontSize: 18,
                  ),
                ),
                const SizedBox(height: 6),
                Text(
                  'اختر القائمة والفترة لعرض أسماء المسددين.',
                  style: TextStyle(
                    fontFamily: 'Cairo',
                    color: Colors.white.withValues(alpha: 0.9),
                    fontSize: 12,
                  ),
                ),
                const SizedBox(height: 12),
                Row(
                  children: [
                    Expanded(
                      child: _sumChip(
                        _money.format(_totalAmount),
                        'مجموع المبالغ (دع)',
                      ),
                    ),
                    const SizedBox(width: 8),
                    Expanded(
                      child: _sumChip('$_count', 'عدد التسديدات'),
                    ),
                  ],
                ),
              ],
            ),
          ),
          const SizedBox(height: 12),
          if (_loadingLists)
            const Center(child: Padding(
              padding: EdgeInsets.all(24),
              child: CircularProgressIndicator(),
            ))
          else ...[
            DropdownButtonFormField<int>(
              value: _lists.any(
                      (e) => (int.tryParse(_s(e, ['id', 'Id'])) ?? -1) == _delegateId)
                  ? _delegateId
                  : null,
              decoration: const InputDecoration(labelText: 'القائمة'),
              items: _lists
                  .map(
                    (e) => DropdownMenuItem(
                      value: int.tryParse(_s(e, ['id', 'Id'])) ?? 0,
                      child: Text(
                        _s(e, ['name', 'Name']),
                        style: const TextStyle(fontFamily: 'Cairo'),
                      ),
                    ),
                  )
                  .toList(),
              onChanged: (v) {
                if (v == null) return;
                setState(() => _delegateId = v);
                _load();
              },
            ),
            const SizedBox(height: 10),
            Row(
              children: [
                Expanded(
                  child: OutlinedButton(
                    onPressed: _pickFrom,
                    child: Text('من ${_df.format(_from)}',
                        style: const TextStyle(fontFamily: 'Cairo', fontSize: 12)),
                  ),
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: OutlinedButton(
                    onPressed: _pickTo,
                    child: Text('إلى ${_df.format(_to)}',
                        style: const TextStyle(fontFamily: 'Cairo', fontSize: 12)),
                  ),
                ),
                IconButton(
                  onPressed: _loading ? null : _load,
                  icon: const Icon(Icons.refresh),
                ),
              ],
            ),
          ],
          const SizedBox(height: 12),
          if (_error != null)
            Text(_error!,
                style: const TextStyle(fontFamily: 'Cairo', color: BmTheme.danger))
          else if (_loading)
            const Padding(
              padding: EdgeInsets.all(32),
              child: Center(child: CircularProgressIndicator()),
            )
          else if (_rows.isEmpty)
            const Padding(
              padding: EdgeInsets.all(32),
              child: Center(
                child: Text(
                  'لا تسديدات في هذه الفترة لهذه القائمة',
                  style: TextStyle(fontFamily: 'Cairo', color: BmTheme.muted),
                ),
              ),
            )
          else
            ..._rows.map((r) {
              final name = _s(r, ['customerName', 'CustomerName']);
              final phone = _s(r, ['phoneNumber', 'PhoneNumber']);
              final amount = _d(r, ['amountDenar', 'AmountDenar']);
              final date = _s(r, ['paymentDate', 'PaymentDate']);
              final shortDate = date.length >= 10 ? date.substring(0, 10) : date;
              return Padding(
                padding: const EdgeInsets.only(bottom: 10),
                child: Material(
                  color: BmTheme.card,
                  borderRadius: BorderRadius.circular(16),
                  child: Container(
                    padding: const EdgeInsets.all(14),
                    decoration: BoxDecoration(
                      borderRadius: BorderRadius.circular(16),
                      border: Border.all(
                        color: BmTheme.primary.withValues(alpha: 0.08),
                      ),
                    ),
                    child: Row(
                      children: [
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(
                                name.isEmpty ? '—' : name,
                                style: const TextStyle(
                                  fontFamily: 'Cairo',
                                  fontWeight: FontWeight.bold,
                                  fontSize: 15,
                                ),
                              ),
                              if (phone.isNotEmpty)
                                Text(phone,
                                    style: const TextStyle(
                                        fontFamily: 'Cairo',
                                        color: BmTheme.muted,
                                        fontSize: 12)),
                              if (shortDate.isNotEmpty)
                                Text(shortDate,
                                    style: const TextStyle(
                                        fontFamily: 'Cairo',
                                        color: BmTheme.muted,
                                        fontSize: 12)),
                            ],
                          ),
                        ),
                        Text(
                          '${_money.format(amount)} دع',
                          style: const TextStyle(
                            fontFamily: 'Cairo',
                            fontWeight: FontWeight.bold,
                            color: BmTheme.primary,
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
              );
            }),
        ],
      ),
    );
  }

  Widget _sumChip(String value, String label) {
    return Container(
      padding: const EdgeInsets.symmetric(vertical: 8, horizontal: 10),
      decoration: BoxDecoration(
        color: Colors.white.withValues(alpha: 0.14),
        borderRadius: BorderRadius.circular(12),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(value,
              style: const TextStyle(
                  fontFamily: 'Cairo',
                  color: Colors.white,
                  fontWeight: FontWeight.bold,
                  fontSize: 15)),
          Text(label,
              style: TextStyle(
                  fontFamily: 'Cairo',
                  color: Colors.white.withValues(alpha: 0.8),
                  fontSize: 11)),
        ],
      ),
    );
  }
}
