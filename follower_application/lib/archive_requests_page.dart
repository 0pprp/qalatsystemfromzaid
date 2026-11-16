import 'dart:async';
import 'dart:convert';

import 'package:follower_application/config/app_env.dart';
import 'package:follower_application/ui/app_safe_scaffold.dart';
import 'package:follower_application/utils/AppTheme.dart';
import 'package:follower_application/utils/display_name.dart';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';

/// أرشيف الزبائن: request the paperwork of any customer on the follower's lists
/// (a note is mandatory when the customer is not finished), then confirm receipt.
class ArchiveRequestsPage extends StatefulWidget {
  const ArchiveRequestsPage({super.key});

  @override
  State<ArchiveRequestsPage> createState() => _ArchiveRequestsPageState();
}

class ArchiveStatus {
  static const requested = 'Requested';
  static const sent = 'Sent';
  static const received = 'Received';

  static String label(String status) => switch (status) {
        requested => 'بانتظار التجهيز',
        sent => 'أُرسل إليك',
        received => 'تم الاستلام',
        _ => status,
      };

  static Color color(String status) => switch (status) {
        requested => const Color(0xFFE09B00),
        sent => const Color(0xFF1E6FD9),
        received => AppTheme.primaryColor,
        _ => AppTheme.subtitleColor,
      };
}

class _ArchiveRequestsPageState extends State<ArchiveRequestsPage>
    with SingleTickerProviderStateMixin {
  late final TabController _tabs;
  bool _loading = true;
  String _error = '';
  String _search = '';
  List<Map<String, dynamic>> _finished = [];
  List<Map<String, dynamic>> _mine = [];
  final Set<int> _busy = {};
  Timer? _debounce;
  bool _searching = false;
  bool _onlyFinished = true;

  @override
  void initState() {
    super.initState();
    _tabs = TabController(length: 2, vsync: this);
    _load();
  }

  @override
  void dispose() {
    _debounce?.cancel();
    _tabs.dispose();
    super.dispose();
  }

  Future<({String link, String asyncId})> _session() async {
    final prefs = await SharedPreferences.getInstance();
    return (
      link: AppEnv.apiBase(fallback: prefs.getString('LinkDelegate') ?? ''),
      asyncId: prefs.getString('AsyncId') ?? '',
    );
  }

  List<Map<String, dynamic>> _list(String body) {
    final decoded = json.decode(body);
    return decoded is List
        ? decoded
            .whereType<Map>()
            .map((e) => Map<String, dynamic>.from(e))
            .toList()
        : <Map<String, dynamic>>[];
  }

  String _message(http.Response res, String fallback) {
    try {
      final body = json.decode(res.body);
      if (body is Map && body['message'] != null) return '${body['message']}';
    } catch (_) {}
    return fallback;
  }

  Future<http.Response> _getFinished(({String link, String asyncId}) s) {
    final q = _search.trim();
    return http
        .get(Uri.parse('${s.link}Followers/ArchiveRequests/${_onlyFinished ? 'FinishedCustomers' : 'Customers'}')
            .replace(queryParameters: {
          'asyncId': s.asyncId,
          if (q.isNotEmpty) 'q': q,
        }))
        .timeout(const Duration(seconds: 60));
  }

  void _onSearch(String value) {
    _search = value;
    _debounce?.cancel();
    _debounce = Timer(const Duration(milliseconds: 450), _reloadFinished);
  }

  Future<void> _reloadFinished() async {
    setState(() => _searching = true);
    try {
      final res = await _getFinished(await _session());
      if (!mounted) return;
      if (res.statusCode == 200) setState(() => _finished = _list(res.body));
    } catch (_) {
      // Keep the previous list on a transient error.
    } finally {
      if (mounted) setState(() => _searching = false);
    }
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = '';
    });
    try {
      final s = await _session();
      final q = {'asyncId': s.asyncId};
      final results = await Future.wait([
        _getFinished(s),
        http
            .get(Uri.parse('${s.link}Followers/ArchiveRequests')
                .replace(queryParameters: q))
            .timeout(const Duration(seconds: 30)),
      ]);
      if (!mounted) return;
      final bad = results.where((r) => r.statusCode != 200).toList();
      if (bad.isNotEmpty) {
        setState(() {
          _loading = false;
          _error = _message(bad.first, 'تعذر تحميل الأرشيف');
        });
        return;
      }
      setState(() {
        _finished = _list(results[0].body);
        _mine = _list(results[1].body);
        _loading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _error = 'تعذر الاتصال بالخادم';
      });
    }
  }

  void _toast(String text, {bool error = false}) {
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(
      content: Text(text, style: const TextStyle(fontFamily: 'Cairo')),
      backgroundColor: error ? Colors.red.shade700 : AppTheme.primaryColor,
    ));
  }

  Future<String?> _askNote(Map<String, dynamic> customer) {
    final finished = customer['isFinished'] != false;
    final controller = TextEditingController();
    return showDialog<String>(
      context: context,
      builder: (ctx) => Directionality(
        textDirection: TextDirection.rtl,
        child: StatefulBuilder(
          builder: (ctx, setLocal) {
            final ok = finished || controller.text.trim().length >= 3;
            return AlertDialog(
              title: Text('طلب أرشيف ${_str(customer['customerName'])}',
                  style: const TextStyle(fontFamily: 'Cairo', fontSize: 16)),
              content: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  if (!finished)
                    Padding(
                      padding: const EdgeInsets.only(bottom: 8),
                      child: Text(
                        'الزبون غير منتهٍ — المتبقي ${_money(customer['remaining'])}\nاكتب سبب الطلب (إلزامي)',
                        style: const TextStyle(fontFamily: 'Cairo', color: Colors.red),
                      ),
                    ),
                  TextField(
                    controller: controller,
                    autofocus: !finished,
                    maxLength: 500,
                    minLines: 2,
                    maxLines: 4,
                    onChanged: (_) => setLocal(() {}),
                    style: const TextStyle(fontFamily: 'Cairo'),
                    decoration: InputDecoration(
                      labelText: finished ? 'ملاحظة (اختياري)' : 'السبب',
                      labelStyle: const TextStyle(fontFamily: 'Cairo'),
                    ),
                  ),
                ],
              ),
              actions: [
                TextButton(
                  onPressed: () => Navigator.pop(ctx),
                  child: const Text('إلغاء', style: TextStyle(fontFamily: 'Cairo')),
                ),
                FilledButton(
                  onPressed: ok ? () => Navigator.pop(ctx, controller.text.trim()) : null,
                  child: const Text('إرسال الطلب', style: TextStyle(fontFamily: 'Cairo')),
                ),
              ],
            );
          },
        ),
      ),
    );
  }

  Future<void> _request(Map<String, dynamic> customer) async {
    final id = _int(customer['customerId']);
    if (id <= 0 || _busy.contains(id)) return;
    final note = await _askNote(customer);
    if (note == null || !mounted) return;
    setState(() => _busy.add(id));
    try {
      final s = await _session();
      final res = await http
          .post(
            Uri.parse('${s.link}Followers/ArchiveRequests'),
            headers: const {'Content-Type': 'application/json'},
            body: json.encode({'asyncId': s.asyncId, 'customerId': id, 'note': note}),
          )
          .timeout(const Duration(seconds: 30));
      if (!mounted) return;
      if (res.statusCode == 200) {
        _toast('تم طلب أرشيف ${_str(customer['customerName'])}');
        await _load();
      } else {
        _toast(_message(res, 'تعذر إرسال الطلب'), error: true);
      }
    } catch (_) {
      if (mounted) _toast('تعذر الاتصال بالخادم', error: true);
    } finally {
      if (mounted) setState(() => _busy.remove(id));
    }
  }

  Future<void> _confirmReceived(Map<String, dynamic> row) async {
    final id = _int(row['id']);
    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) => Directionality(
        textDirection: TextDirection.rtl,
        child: AlertDialog(
          title: const Text('تأكيد الاستلام',
              style: TextStyle(fontFamily: 'Cairo')),
          content: Text(
            'هل استلمت أرشيف ${_str(row['customerName'])}؟\nسيُغلق الطلب بعد التأكيد.',
            style: const TextStyle(fontFamily: 'Cairo'),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(ctx, false),
              child: const Text('إلغاء', style: TextStyle(fontFamily: 'Cairo')),
            ),
            FilledButton(
              onPressed: () => Navigator.pop(ctx, true),
              child: const Text('نعم، استلمت',
                  style: TextStyle(fontFamily: 'Cairo')),
            ),
          ],
        ),
      ),
    );
    if (ok != true || !mounted) return;
    setState(() => _busy.add(-id));
    try {
      final s = await _session();
      final res = await http
          .post(
            Uri.parse('${s.link}Followers/ArchiveRequests/$id/Received'),
            headers: const {'Content-Type': 'application/json'},
            body: json.encode({'asyncId': s.asyncId}),
          )
          .timeout(const Duration(seconds: 30));
      if (!mounted) return;
      if (res.statusCode == 200) {
        _toast('تم إغلاق الطلب');
        await _load();
      } else {
        _toast(_message(res, 'تعذر تأكيد الاستلام'), error: true);
      }
    } catch (_) {
      if (mounted) _toast('تعذر الاتصال بالخادم', error: true);
    } finally {
      if (mounted) setState(() => _busy.remove(-id));
    }
  }

  static int _int(Object? v) => int.tryParse('${v ?? ''}') ?? 0;

  static String _money(Object? v) {
    final n = (num.tryParse('${v ?? 0}') ?? 0).round();
    final digits = n.abs().toString().replaceAllMapped(
        RegExp(r'\B(?=(\d{3})+(?!\d))'), (_) => ',');
    return '${n < 0 ? '-' : ''}$digits د.ع';
  }
  static String _str(Object? v) => v == null ? '' : '$v'.trim();

  static String _date(Object? raw) {
    final text = _str(raw);
    if (text.isEmpty) return '';
    final hasZone = text.endsWith('Z') || text.contains('+');
    final dt = DateTime.tryParse(text.length > 10 && !hasZone ? '${text}Z' : text);
    if (dt == null) return text;
    final local = text.length > 10 ? dt.toUtc().add(const Duration(hours: 3)) : dt;
    String two(int n) => n.toString().padLeft(2, '0');
    return '${two(local.day)}/${two(local.month)}/${local.year}';
  }

  int get _awaitingReceipt =>
      _mine.where((r) => _str(r['status']) == ArchiveStatus.sent).length;

  @override
  Widget build(BuildContext context) {
    return Directionality(
      textDirection: TextDirection.rtl,
      child: AppSafeScaffold(
        backgroundColor: AppTheme.backgroundColor,
        appBar: AppBar(
          title: const Text('أرشيف الزبائن',
              style: TextStyle(fontFamily: 'Cairo', color: Colors.white)),
          backgroundColor: AppTheme.primaryColor,
          foregroundColor: Colors.white,
          bottom: AppTheme.primaryAppBarTabBar(
            controller: _tabs,
            tabs: [
              const Tab(text: 'الزبائن'),
              Tab(
                child: Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    const Text('طلباتي'),
                    if (_awaitingReceipt > 0) ...[
                      const SizedBox(width: 6),
                      _CountBadge(_awaitingReceipt),
                    ],
                  ],
                ),
              ),
            ],
          ),
          actions: [
            IconButton(onPressed: _load, icon: const Icon(Icons.refresh)),
          ],
        ),
        body: _loading
            ? const Center(child: CircularProgressIndicator())
            : _error.isNotEmpty
                ? _Empty(icon: Icons.cloud_off, text: _error, onRetry: _load)
                : TabBarView(
                    controller: _tabs,
                    children: [_finishedTab(), _mineTab()],
                  ),
      ),
    );
  }

  Widget _finishedTab() {
    final q = _search.trim();
    final rows = _finished;
    return Column(
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(12, 12, 12, 4),
          child: TextField(
            onChanged: _onSearch,
            style: const TextStyle(fontFamily: 'Cairo'),
            decoration: InputDecoration(
              hintText: 'بحث بالاسم أو الهاتف',
              hintStyle: const TextStyle(fontFamily: 'Cairo'),
              prefixIcon: const Icon(Icons.search),
              suffixIcon: _searching
                  ? const Padding(
                      padding: EdgeInsets.all(12),
                      child: SizedBox(
                          width: 16,
                          height: 16,
                          child: CircularProgressIndicator(strokeWidth: 2)),
                    )
                  : null,
              isDense: true,
              filled: true,
              fillColor: Colors.white,
              border: OutlineInputBorder(
                borderRadius: BorderRadius.circular(12),
                borderSide: BorderSide.none,
              ),
            ),
          ),
        ),
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: 12),
          child: Row(
            children: [
              FilterChip(
                label: const Text('المنتهون فقط', style: TextStyle(fontFamily: 'Cairo')),
                selected: _onlyFinished,
                onSelected: (v) {
                  setState(() => _onlyFinished = v);
                  _reloadFinished();
                },
              ),
            ],
          ),
        ),
        Expanded(
          child: RefreshIndicator(
            onRefresh: _load,
            child: rows.isEmpty
                ? _Empty(
                    icon: Icons.inventory_2_outlined,
                    text: q.isEmpty
                        ? (_onlyFinished ? 'لا يوجد زبائن منتهون في قوائمك' : 'لا يوجد زبائن في قوائمك')
                        : 'لا توجد نتائج')
                : ListView.builder(
                    padding: const EdgeInsets.fromLTRB(12, 4, 12, 24),
                    itemCount: rows.length,
                    itemBuilder: (_, i) => _finishedRow(rows[i]),
                  ),
          ),
        ),
      ],
    );
  }

  Widget _finishedRow(Map<String, dynamic> row) {
    final id = _int(row['customerId']);
    final status = _str(row['requestStatus']);
    final finished = row['isFinished'] != false;
    final sub = [
      if (_str(row['listName']).isNotEmpty) _str(row['listName']),
      if (finished && _date(row['finishedAt']).isNotEmpty) 'انتهى ${_date(row['finishedAt'])}',
      if (!finished) 'المتبقي ${_money(row['remaining'])}',
    ].join(' · ');
    return Card(
      margin: const EdgeInsets.only(bottom: 8),
      elevation: 0,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
      child: ListTile(
        title: PersonNameText(_str(row['customerName']).isEmpty
            ? '—'
            : _str(row['customerName'])),
        subtitle: Text(sub,
            style: const TextStyle(
                fontFamily: 'Cairo', color: AppTheme.subtitleColor)),
        trailing: status.isNotEmpty
            ? _StatusChip(status)
            : FilledButton.tonal(
                onPressed: _busy.contains(id) ? null : () => _request(row),
                child: _busy.contains(id)
                    ? const SizedBox(
                        width: 16,
                        height: 16,
                        child: CircularProgressIndicator(strokeWidth: 2))
                    : const Text('طلب الأرشيف',
                        style: TextStyle(fontFamily: 'Cairo')),
              ),
      ),
    );
  }

  Widget _mineTab() {
    return RefreshIndicator(
      onRefresh: _load,
      child: _mine.isEmpty
          ? const _Empty(
              icon: Icons.inbox_outlined,
              text: 'لم تطلب أي أرشيف بعد\nاطلبه من تبويب «الزبائن»')
          : ListView.builder(
              padding: const EdgeInsets.fromLTRB(12, 12, 12, 24),
              itemCount: _mine.length,
              itemBuilder: (_, i) => _mineRow(_mine[i]),
            ),
    );
  }

  Widget _mineRow(Map<String, dynamic> row) {
    final status = _str(row['status']);
    final id = _int(row['id']);
    final lines = <String>[
      'طُلب ${_date(row['requestedAtUtc'])}'
          '${_date(row['sentAtUtc']).isNotEmpty ? ' · أُرسل ${_date(row['sentAtUtc'])}' : ''}'
          '${_date(row['receivedAtUtc']).isNotEmpty ? ' · استُلم ${_date(row['receivedAtUtc'])}' : ''}',
      if (_str(row['followerNote']).isNotEmpty) 'ملاحظتك: ${_str(row['followerNote'])}',
      if (_str(row['sendNote']).isNotEmpty) 'ملاحظة: ${_str(row['sendNote'])}',
    ];
    final isSent = status == ArchiveStatus.sent;
    return Card(
      margin: const EdgeInsets.only(bottom: 8),
      elevation: 0,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(12),
        side: isSent
            ? BorderSide(color: ArchiveStatus.color(status), width: 1.2)
            : BorderSide.none,
      ),
      child: Padding(
        padding: const EdgeInsets.fromLTRB(16, 12, 16, 12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Expanded(
                  child: PersonNameText(_str(row['customerName']).isEmpty
                      ? '—'
                      : _str(row['customerName'])),
                ),
                _StatusChip(status),
              ],
            ),
            const SizedBox(height: 4),
            Text(lines.join('\n'),
                style: const TextStyle(
                    fontFamily: 'Cairo',
                    fontSize: 13,
                    color: AppTheme.subtitleColor)),
            if (isSent) ...[
              const SizedBox(height: 10),
              SizedBox(
                width: double.infinity,
                child: FilledButton.icon(
                  onPressed:
                      _busy.contains(-id) ? null : () => _confirmReceived(row),
                  icon: const Icon(Icons.check_circle_outline),
                  label: const Text('استلمت الأرشيف',
                      style: TextStyle(fontFamily: 'Cairo')),
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class _StatusChip extends StatelessWidget {
  const _StatusChip(this.status);
  final String status;

  @override
  Widget build(BuildContext context) {
    final color = ArchiveStatus.color(status);
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(20),
      ),
      child: Text(ArchiveStatus.label(status),
          style: TextStyle(
              fontFamily: 'Cairo',
              fontSize: 12,
              fontWeight: FontWeight.bold,
              color: color)),
    );
  }
}

class _CountBadge extends StatelessWidget {
  const _CountBadge(this.count);
  final int count;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 7, vertical: 1),
      decoration: BoxDecoration(
          color: Colors.white, borderRadius: BorderRadius.circular(10)),
      child: Text('$count',
          style: const TextStyle(
              fontSize: 12,
              fontWeight: FontWeight.bold,
              color: AppTheme.primaryColor)),
    );
  }
}

class _Empty extends StatelessWidget {
  const _Empty({required this.icon, required this.text, this.onRetry});
  final IconData icon;
  final String text;
  final VoidCallback? onRetry;

  @override
  Widget build(BuildContext context) {
    // ListView so pull-to-refresh still works on an empty tab.
    return ListView(
      physics: const AlwaysScrollableScrollPhysics(),
      children: [
        const SizedBox(height: 80),
        Icon(icon, size: 48, color: AppTheme.subtitleColor),
        const SizedBox(height: 12),
        Text(text,
            textAlign: TextAlign.center,
            style: const TextStyle(
                fontFamily: 'Cairo', color: AppTheme.subtitleColor)),
        if (onRetry != null)
          Center(
            child: TextButton(
              onPressed: onRetry,
              child: const Text('إعادة المحاولة',
                  style: TextStyle(fontFamily: 'Cairo')),
            ),
          ),
      ],
    );
  }
}
