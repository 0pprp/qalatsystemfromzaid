import 'package:delegated_manager_application/core/network/api_client.dart';
import 'package:delegated_manager_application/core/theme/app_theme.dart';
import 'package:delegated_manager_application/core/utils/date_format.dart';
import 'package:flutter/material.dart';

/// Customer profile opened from the ratings board (same data as المدير / مدير الفرع).
class CustomerProfileScreen extends StatefulWidget {
  const CustomerProfileScreen({
    super.key,
    required this.cityValue,
    required this.customerId,
    this.initialName,
    this.seed,
  });

  final String cityValue;
  final int customerId;
  final String? initialName;
  final Map<String, dynamic>? seed;

  @override
  State<CustomerProfileScreen> createState() => _CustomerProfileScreenState();
}

class _CustomerProfileScreenState extends State<CustomerProfileScreen> {
  final _noteCtrl = TextEditingController();

  bool _loading = true;
  bool _saving = false;
  String? _error;
  Map<String, dynamic>? _customer;
  List<Map<String, dynamic>> _notes = [];

  @override
  void initState() {
    super.initState();
    _customer = widget.seed == null
        ? null
        : Map<String, dynamic>.from(widget.seed!);
    _load();
  }

  @override
  void dispose() {
    _noteCtrl.dispose();
    super.dispose();
  }

  String _s(Map? m, List<String> keys) {
    if (m == null) return '';
    for (final k in keys) {
      final v = m[k];
      if (v != null && '$v'.trim().isNotEmpty) return '$v'.trim();
    }
    return '';
  }

  double _d(Map? m, List<String> keys) {
    if (m == null) return 0;
    for (final k in keys) {
      final v = m[k];
      if (v is num) return v.toDouble();
      final p = double.tryParse('$v');
      if (p != null) return p;
    }
    return 0;
  }

  String _money(double v) {
    final n = v.round();
    final s = n.abs().toString();
    final buf = StringBuffer();
    for (var i = 0; i < s.length; i++) {
      final fromEnd = s.length - i;
      buf.write(s[i]);
      if (fromEnd > 1 && fromEnd % 3 == 1) buf.write(',');
    }
    return n < 0 ? '-$buf' : buf.toString();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final results = await Future.wait([
        ApiClient.get(
          'delegated-manager/customers/${widget.cityValue}/${widget.customerId}/profile',
        ),
        ApiClient.get(
          'delegated-manager/customers/${widget.cityValue}/${widget.customerId}/notes',
        ),
      ]);
      if (!mounted) return;
      final info = results[0] is Map
          ? Map<String, dynamic>.from(results[0] as Map)
          : <String, dynamic>{};
      final notesRaw = results[1];
      final notes = notesRaw is List
          ? notesRaw
              .whereType<Map>()
              .map((e) => Map<String, dynamic>.from(e))
              .toList()
          : <Map<String, dynamic>>[];
      setState(() {
        _customer = {...?widget.seed, ...info};
        _notes = notes;
        _loading = false;
      });
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _error = e.message;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _error = '$e';
      });
    }
  }

  Future<void> _submitNote() async {
    final text = _noteCtrl.text.trim();
    if (text.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('اكتب الملاحظة أولاً')),
      );
      return;
    }
    setState(() => _saving = true);
    try {
      await ApiClient.post(
        'delegated-manager/customers/${widget.cityValue}/notes',
        body: {
          'customerId': widget.customerId,
          'noteText': text,
        },
      );
      if (!mounted) return;
      _noteCtrl.clear();
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('تمت إضافة الملاحظة')),
      );
      await _load();
    } on ApiException catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(e.message)),
      );
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('$e')),
      );
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final name = _s(_customer, ['customerName', 'CustomerName']);
    final title =
        name.isNotEmpty ? name : (widget.initialName ?? 'ملف الزبون');
    final delegate = _s(_customer, ['delegateName', 'DelegateName']);
    final phone = _s(_customer, ['phone', 'Phone', 'phoneNumber', 'PhoneNumber']);
    final total = _d(_customer, [
      'amountTotalSales',
      'AmountTotalSales',
      'amountTotal',
      'AmountTotal',
    ]);
    final receipts = _d(_customer, [
      'receiptsTotal',
      'ReceiptsTotal',
      'amountPaid',
      'AmountPaid',
    ]);
    final remaining = _d(_customer, ['amountRemaining', 'AmountRemaining']);
    final day = _d(_customer, ['amountDaySales', 'AmountDaySales']);

    return Scaffold(
      appBar: AppBar(
        title: Text(title),
        actions: [
          IconButton(
            tooltip: 'تحديث',
            onPressed: _loading ? null : _load,
            icon: const Icon(Icons.refresh),
          ),
        ],
      ),
      body: _loading && _customer == null
          ? const Center(child: CircularProgressIndicator())
          : _error != null && _customer == null
              ? Center(
                  child: Padding(
                    padding: const EdgeInsets.all(24),
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Text(
                          _error!,
                          textAlign: TextAlign.center,
                          style: const TextStyle(color: AppColors.rejected),
                        ),
                        const SizedBox(height: 12),
                        FilledButton(
                          onPressed: _load,
                          child: const Text('إعادة'),
                        ),
                      ],
                    ),
                  ),
                )
              : RefreshIndicator(
                  onRefresh: _load,
                  child: ListView(
                    padding: const EdgeInsets.fromLTRB(16, 12, 16, 32),
                    children: [
                      if (_loading) const LinearProgressIndicator(minHeight: 2),
                      Text(
                        delegate.isEmpty
                            ? 'مندوب غير محدد'
                            : 'المندوب: $delegate',
                        style: const TextStyle(color: AppColors.muted),
                      ),
                      if (phone.isNotEmpty) ...[
                        const SizedBox(height: 4),
                        Text(phone,
                            style: const TextStyle(color: AppColors.muted)),
                      ],
                      const SizedBox(height: 12),
                      Row(
                        children: [
                          Expanded(
                            child: _StatTile(
                              label: 'سعر البيع',
                              value: _money(total),
                            ),
                          ),
                          const SizedBox(width: 8),
                          Expanded(
                            child: _StatTile(
                              label: 'الواصل',
                              value: _money(receipts),
                            ),
                          ),
                        ],
                      ),
                      const SizedBox(height: 8),
                      Row(
                        children: [
                          Expanded(
                            child: _StatTile(
                              label: 'الباقي',
                              value: _money(remaining),
                            ),
                          ),
                          const SizedBox(width: 8),
                          Expanded(
                            child: _StatTile(
                              label: 'القسط',
                              value: _money(day),
                            ),
                          ),
                        ],
                      ),
                      const SizedBox(height: 20),
                      const Text(
                        'إضافة ملاحظة',
                        style: TextStyle(
                          fontWeight: FontWeight.bold,
                          fontSize: 16,
                        ),
                      ),
                      const SizedBox(height: 8),
                      TextField(
                        controller: _noteCtrl,
                        minLines: 3,
                        maxLines: 5,
                        decoration: const InputDecoration(
                          hintText: 'اكتب ملاحظة تظهر في ملف الزبون…',
                        ),
                      ),
                      const SizedBox(height: 10),
                      FilledButton.icon(
                        onPressed: _saving ? null : _submitNote,
                        icon: _saving
                            ? const SizedBox(
                                width: 18,
                                height: 18,
                                child: CircularProgressIndicator(
                                  strokeWidth: 2,
                                  color: Colors.white,
                                ),
                              )
                            : const Icon(Icons.note_add_outlined),
                        label: Text(_saving ? 'جارٍ الحفظ…' : 'حفظ الملاحظة'),
                      ),
                      const SizedBox(height: 24),
                      Row(
                        children: [
                          const Text(
                            'سجل الملاحظات',
                            style: TextStyle(
                              fontWeight: FontWeight.bold,
                              fontSize: 16,
                            ),
                          ),
                          const Spacer(),
                          Text(
                            '${_notes.length}',
                            style: const TextStyle(color: AppColors.muted),
                          ),
                        ],
                      ),
                      const SizedBox(height: 10),
                      if (_notes.isEmpty)
                        const Padding(
                          padding: EdgeInsets.symmetric(vertical: 24),
                          child: Text(
                            'لا توجد ملاحظات بعد',
                            textAlign: TextAlign.center,
                            style: TextStyle(color: AppColors.muted),
                          ),
                        )
                      else
                        ..._notes.map((n) {
                          final text = _s(n, ['noteText', 'NoteText']);
                          final author = _s(n, [
                            'createdByName',
                            'CreatedByName',
                            'userName',
                            'UserName',
                          ]);
                          final at = AppDate.parseUtc(
                            n['createdAtUtc'] ??
                                n['CreatedAtUtc'] ??
                                n['createdDate'] ??
                                n['CreatedDate'],
                          );
                          return Container(
                            margin: const EdgeInsets.only(bottom: 10),
                            padding: const EdgeInsets.all(14),
                            decoration: BoxDecoration(
                              color: AppColors.card,
                              borderRadius: BorderRadius.circular(16),
                              border: Border.all(
                                color: AppColors.primary.withValues(alpha: 0.12),
                              ),
                            ),
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.stretch,
                              children: [
                                Row(
                                  children: [
                                    const Icon(Icons.chat_bubble_outline,
                                        size: 18, color: AppColors.primary),
                                    const SizedBox(width: 8),
                                    Expanded(
                                      child: Text(
                                        author.isEmpty ? '—' : author,
                                        style: const TextStyle(
                                          fontSize: 12,
                                          color: AppColors.muted,
                                        ),
                                      ),
                                    ),
                                    Text(
                                      AppDate.format(at),
                                      style: const TextStyle(
                                        fontSize: 11,
                                        color: AppColors.muted,
                                      ),
                                    ),
                                  ],
                                ),
                                const SizedBox(height: 8),
                                Text(
                                  text.isEmpty ? '—' : text,
                                  style: const TextStyle(height: 1.45),
                                ),
                              ],
                            ),
                          );
                        }),
                    ],
                  ),
                ),
    );
  }
}

class _StatTile extends StatelessWidget {
  const _StatTile({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: AppColors.card,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: AppColors.primary.withValues(alpha: 0.08)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label,
              style: const TextStyle(fontSize: 12, color: AppColors.muted)),
          const SizedBox(height: 4),
          Text(
            '$value دع',
            style: const TextStyle(
              fontWeight: FontWeight.bold,
              fontSize: 15,
            ),
          ),
        ],
      ),
    );
  }
}
