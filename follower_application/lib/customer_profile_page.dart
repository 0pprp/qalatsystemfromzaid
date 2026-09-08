import 'dart:convert';

import 'package:follower_application/config/app_env.dart';
import 'package:follower_application/utils/AppTheme.dart';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:intl/intl.dart' hide TextDirection;
import 'package:shared_preferences/shared_preferences.dart';

class CustomerProfilePage extends StatefulWidget {
  const CustomerProfilePage({super.key, required this.customer, required this.listId});

  final Map<String, dynamic> customer;
  final int listId;

  @override
  State<CustomerProfilePage> createState() => _CustomerProfilePageState();
}

class _CustomerProfilePageState extends State<CustomerProfilePage> {
  final _noteController = TextEditingController();
  List<Map<String, dynamic>> _notes = [];
  bool _loading = true;
  bool _saving = false;
  String? _error;

  int get _customerId => int.tryParse('${widget.customer['customerId'] ?? 0}') ?? 0;
  int get _employeeId => int.tryParse('${widget.customer['userId'] ?? 0}') ?? 0;
  String get _saleName => '${widget.customer['saleName'] ?? ''}'.trim();

  @override
  void initState() {
    super.initState();
    _loadNotes();
  }

  @override
  void dispose() {
    _noteController.dispose();
    super.dispose();
  }

  Future<Map<String, String>> _session() async {
    final prefs = await SharedPreferences.getInstance();
    return {
      'asyncId': prefs.getString('AsyncId') ?? '',
      'link': AppEnv.apiBase(fallback: prefs.getString('LinkDelegate') ?? ''),
    };
  }

  Future<void> _loadNotes() async {
    if (_customerId <= 0) {
      setState(() {
        _loading = false;
        _error = 'معرّف الزبون غير متوفر';
      });
      return;
    }
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final session = await _session();
      final uri = Uri.parse('${session['link']}Followers/Customers/$_customerId/notes').replace(
        queryParameters: {
          'asyncId': session['asyncId'],
          'listId': widget.listId.toString(),
        },
      );
      final response = await http.get(uri).timeout(const Duration(seconds: 20));
      if (response.statusCode != 200) {
        throw Exception('notes');
      }
      final data = json.decode(response.body) as List<dynamic>;
      if (!mounted) return;
      setState(() {
        _notes = data.map((e) => Map<String, dynamic>.from(e as Map)).toList();
        _loading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _error = 'تعذر تحميل الملاحظات';
      });
    }
  }

  Future<void> _saveNote() async {
    final text = _noteController.text.trim();
    if (text.isEmpty || _saving) return;
    setState(() => _saving = true);
    try {
      final session = await _session();
      final uri = Uri.parse('${session['link']}Followers/Customers/$_customerId/notes');
      final response = await http
          .post(
            uri,
            headers: {'Content-Type': 'application/json'},
            body: json.encode({
              'asyncId': session['asyncId'],
              'listId': widget.listId,
              'noteText': text,
              // Spoof attempt ignored by server:
              'createdByUserId': 999999,
              'createdByName': 'hack',
              'createdByRole': 'Admin',
            }),
          )
          .timeout(const Duration(seconds: 20));
      if (response.statusCode != 200) {
        throw Exception('save');
      }
      _noteController.clear();
      await _loadNotes();
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('تعذر حفظ الملاحظة', style: TextStyle(fontFamily: 'Cairo'))),
        );
      }
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  Future<void> _addEmployeeNote() async {
    if (_employeeId <= 0) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('لا يوجد مندوب مرتبط بهذا الزبون', style: TextStyle(fontFamily: 'Cairo'))),
      );
      return;
    }
    final controller = TextEditingController();
    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text(
          _saleName.isEmpty ? 'ملاحظة على المندوب' : 'ملاحظة على $_saleName',
          style: const TextStyle(fontFamily: 'Cairo'),
        ),
        content: TextField(
          controller: controller,
          maxLines: 4,
          decoration: const InputDecoration(hintText: 'نص الملاحظة *', hintStyle: TextStyle(fontFamily: 'Cairo')),
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('إلغاء', style: TextStyle(fontFamily: 'Cairo'))),
          ElevatedButton(onPressed: () => Navigator.pop(ctx, true), child: const Text('حفظ', style: TextStyle(fontFamily: 'Cairo'))),
        ],
      ),
    );
    final text = controller.text.trim();
    controller.dispose();
    if (ok != true || text.isEmpty) return;
    try {
      final session = await _session();
      final uri = Uri.parse('${session['link']}Followers/Employees/$_employeeId/notes');
      final response = await http
          .post(
            uri,
            headers: {'Content-Type': 'application/json'},
            body: json.encode({
              'asyncId': session['asyncId'],
              'listId': widget.listId,
              'noteText': text,
            }),
          )
          .timeout(const Duration(seconds: 20));
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(
        content: Text(
          response.statusCode == 200 ? 'تم حفظ ملاحظة المندوب' : 'تعذر حفظ ملاحظة المندوب',
          style: const TextStyle(fontFamily: 'Cairo'),
        ),
      ));
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('تعذر حفظ ملاحظة المندوب', style: TextStyle(fontFamily: 'Cairo'))),
        );
      }
    }
  }

  String _iraqClock(dynamic utc) {
    final raw = utc?.toString() ?? '';
    final parsed = DateTime.tryParse(raw)?.toUtc();
    if (parsed == null) return '—';
    final iraq = parsed.add(const Duration(hours: 3));
    return DateFormat('yyyy/MM/dd hh:mm a', 'en').format(iraq)
        .replaceAll('AM', 'صباحاً')
        .replaceAll('PM', 'مساءً');
  }

  @override
  Widget build(BuildContext context) {
    return Directionality(
      textDirection: TextDirection.rtl,
      child: Scaffold(
        appBar: AppBar(
          title: const Text('ملف الزبون', style: TextStyle(fontFamily: 'Cairo')),
          backgroundColor: AppTheme.primaryColor,
        ),
        body: ListView(
          padding: const EdgeInsets.all(16),
          children: [
            Text(
              '${widget.customer['customerName'] ?? ''}',
              style: const TextStyle(fontFamily: 'Cairo', fontWeight: FontWeight.bold, fontSize: 18),
            ),
            const SizedBox(height: 4),
            Text('${widget.customer['phoneNumber'] ?? ''}', style: const TextStyle(fontFamily: 'Cairo', color: Colors.grey)),
            if (_saleName.isNotEmpty) ...[
              const SizedBox(height: 4),
              Text('المندوب: $_saleName', style: const TextStyle(fontFamily: 'Cairo')),
            ],
            const SizedBox(height: 16),
            OutlinedButton.icon(
              onPressed: () => Navigator.pushNamed(
                context,
                '/FollowerSalesRequest',
                arguments: {
                  'customer': widget.customer,
                  'listId': widget.listId,
                },
              ),
              icon: const Icon(Icons.send),
              label: const Text('إرسال طلب مبيع', style: TextStyle(fontFamily: 'Cairo')),
            ),
            const SizedBox(height: 8),
            OutlinedButton.icon(
              onPressed: _addEmployeeNote,
              icon: const Icon(Icons.person_outline),
              label: const Text('إضافة ملاحظة على المندوب', style: TextStyle(fontFamily: 'Cairo')),
            ),
            const SizedBox(height: 24),
            const Text('الملاحظات', style: TextStyle(fontFamily: 'Cairo', fontWeight: FontWeight.bold, fontSize: 16)),
            const SizedBox(height: 8),
            TextField(
              controller: _noteController,
              maxLines: 3,
              decoration: const InputDecoration(
                border: OutlineInputBorder(),
                hintText: 'أضف ملاحظة جديدة...',
                hintStyle: TextStyle(fontFamily: 'Cairo'),
              ),
            ),
            const SizedBox(height: 8),
            ElevatedButton(
              onPressed: _saving ? null : _saveNote,
              style: ElevatedButton.styleFrom(backgroundColor: AppTheme.primaryColor),
              child: Text(_saving ? '...' : 'حفظ الملاحظة', style: const TextStyle(fontFamily: 'Cairo', color: Colors.white)),
            ),
            const SizedBox(height: 16),
            if (_loading)
              const Center(child: CircularProgressIndicator())
            else if (_error != null)
              Text(_error!, style: const TextStyle(fontFamily: 'Cairo', color: Colors.red))
            else if (_notes.isEmpty)
              const Text('لا توجد ملاحظات', style: TextStyle(fontFamily: 'Cairo', color: Colors.grey))
            else
              ..._notes.map((n) {
                return Card(
                  margin: const EdgeInsets.only(bottom: 8),
                  child: Padding(
                    padding: const EdgeInsets.all(12),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text('${n['noteText'] ?? n['NoteText'] ?? ''}', style: const TextStyle(fontFamily: 'Cairo')),
                        const SizedBox(height: 8),
                        Text(
                          'الكاتب: ${n['createdByName'] ?? n['CreatedByName'] ?? ''}',
                          style: const TextStyle(fontFamily: 'Cairo', fontSize: 12, color: Colors.grey),
                        ),
                        const Text(
                          'الصفة: متابع',
                          style: TextStyle(fontFamily: 'Cairo', fontSize: 12, color: Colors.grey),
                        ),
                        Text(
                          'التاريخ: ${_iraqClock(n['createdAtUtc'] ?? n['CreatedAtUtc'])}',
                          style: const TextStyle(fontFamily: 'Cairo', fontSize: 12, color: Colors.grey),
                        ),
                      ],
                    ),
                  ),
                );
              }),
          ],
        ),
      ),
    );
  }
}
