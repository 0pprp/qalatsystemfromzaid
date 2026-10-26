import 'package:follower_application/follower_payers_split.dart';
import 'package:follower_application/follower_unpaid_days_split.dart';
import 'package:follower_application/tracking/follower_shift_debug.dart';
import 'package:follower_application/tracking/shift_gate_coordinator.dart';
import 'package:follower_application/tracking/shift_tracking_controller.dart';
import 'package:follower_application/tracking/work_shift.dart';
import 'package:follower_application/ui/app_safe_scaffold.dart';
import 'package:follower_application/ui/day_rating_gate.dart';
import 'package:follower_application/ui/follower_complaint_sheet.dart';
import 'package:follower_application/core/update/app_update_gate.dart';
import 'package:follower_application/utils/AppTheme.dart';
import 'package:follower_application/utils/Formatters.dart';
import 'package:follower_application/utils/display_name.dart';
import 'package:follower_application/utils/iraq_datetime.dart';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';
import 'package:follower_application/AsyncIdChecker.dart';
import 'package:follower_application/config/app_env.dart';
import 'package:follower_application/main.dart';
import 'package:follower_application/services/follower_tracking_repository.dart';
import 'package:follower_application/tutorial_questions_page.dart';
import 'dart:convert';


class HomePage extends StatefulWidget {
  const HomePage({super.key});

  @override
  State<HomePage> createState() => _HomePageState();
}

class _HomePageState extends State<HomePage> {
  bool _loadingLists = true;
  bool _loadingFollow = false;
  bool _shiftBusy = false;
  String _error = '';
  String _shiftError = '';
  String _delegateName = '';
  String _showType = 'المسددين';
  DateTime _paymentDate = DateTime.now().subtract(const Duration(days: 1));
  WorkShift? _activeShift;
  late final ShiftTrackingController _tracking;

  List<Map<String, dynamic>> _lists = [];
  int? _selectedChildId;
  List<Map<String, dynamic>> _customers = [];
  /// customerId -> last field-followup note
  final Map<int, String> _latestFollowupNotes = {};
  final TextEditingController _searchController = TextEditingController();
  String _searchQuery = '';
  final GlobalKey<ScaffoldState> _scaffoldKey = GlobalKey<ScaffoldState>();

  @override
  void initState() {
    super.initState();
    _tracking = TrackingRuntime.instance ??=
        ShiftTrackingController(repository: ApiFollowerTrackingRepository());
    _boot();
  }

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  Future<void> _boot() async {
    final dest = await ShiftGateCoordinator(tracking: _tracking)
        .resolve(attachIfActive: true);
    if (!mounted) return;
    if (dest != ShiftGateDestination.home) {
      Navigator.pushNamedAndRemoveUntil(
        context,
        ShiftGateCoordinator.routeFor(dest),
        (r) => false,
      );
      return;
    }
    ShiftGateCoordinator.markHomeOk();
    if (mounted) {
      setState(() {
        _activeShift = _tracking.activeShift;
      });
    }
    await _loadLists();
    if (mounted) {
      await AppUpdateGate.checkAndPrompt(context);
    }
  }

  Future<void> _startShift() async {
    setState(() {
      _shiftBusy = true;
      _shiftError = '';
    });
    try {
      final shift = await _tracking.startShiftFlow();
      if (!mounted) return;
      setState(() {
        _shiftBusy = false;
        _activeShift = shift ?? _tracking.activeShift;
        _shiftError = shift == null ? (_tracking.lastError ?? FollowerShiftDebug.generic) : '';
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _shiftBusy = false;
        _shiftError = FollowerShiftDebug.apiFailure(e);
      });
    }
  }

  Future<void> _endShift() async {
    setState(() {
      _shiftBusy = true;
      _shiftError = '';
    });
    try {
      final rated = await ensureDayRatingsBeforeEndShift(context);
      if (!rated) {
        if (mounted) {
          setState(() {
            _shiftBusy = false;
            _shiftError = 'يجب تقييم مناديب الجباية قبل إنهاء الدوام';
          });
        }
        return;
      }
      await _tracking.endShiftFlow();
      if (!mounted) return;
      setState(() {
        _shiftBusy = false;
        _activeShift = null;
      });
      Navigator.pushNamedAndRemoveUntil(context, '/StartShift', (r) => false);
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _shiftBusy = false;
        _shiftError = FollowerShiftDebug.apiFailure(e);
      });
    }
  }

  Future<Map<String, String>> _session() async {
    final prefs = await SharedPreferences.getInstance();
    return {
      'asyncId': prefs.getString('AsyncId') ?? '',
      'link': AppEnv.apiBase(fallback: prefs.getString('LinkDelegate') ?? ''),
      'name': prefs.getString('DelegateName') ?? '',
    };
  }

  Future<void> _loadLists() async {
    setState(() {
      _loadingLists = true;
      _error = '';
    });

    try {
      final session = await _session();
      _delegateName = session['name'] ?? '';
      final uri = Uri.parse('${session['link']}Followers/Lists').replace(
        queryParameters: {'asyncId': session['asyncId']},
      );
      final response = await http.get(uri).timeout(const Duration(seconds: 20));

      if (response.statusCode == 401) {
        await AsyncIdChecker.logout();
        if (mounted) Navigator.pushReplacementNamed(context, '/Login');
        return;
      }
      if (response.statusCode != 200) {
        throw Exception('lists');
      }

      final data = json.decode(response.body) as List<dynamic>;
      final lists = data.map((item) {
        final id = item['delegateId'] ?? item['DelegateId'] ?? item['delegateID'];
        return {
          'id': int.tryParse(id.toString()) ?? 0,
          'name': (item['delegateName'] ?? item['DelegateName'] ?? 'قائمة').toString(),
        };
      }).where((item) => (item['id'] as int) > 0).toList();

      final prefs = await SharedPreferences.getInstance();
      final savedChild = int.tryParse(prefs.getString('SelectedChildId') ?? '');
      int? selected = lists.any((item) => item['id'] == savedChild)
          ? savedChild
          : (lists.isNotEmpty ? lists.first['id'] as int : null);

      if (mounted) {
        setState(() {
          _lists = lists;
          _selectedChildId = selected;
          _loadingLists = false;
        });
      }

      if (selected != null) {
        await _loadFollow();
      }
    } catch (e) {
      if (mounted) {
        setState(() {
          _loadingLists = false;
          _error = 'تعذر جلب القوائم المرتبطة';
        });
      }
    }
  }

  Future<void> _loadFollow() async {
    if (_selectedChildId == null) return;

    setState(() {
      _loadingFollow = true;
      _error = '';
    });

    try {
      final session = await _session();
      final date = IraqDateTime.formatYmd(_paymentDate);
      final uri = Uri.parse('${session['link']}Followers/Daily').replace(
        queryParameters: {
          'asyncId': session['asyncId'],
          'childId': _selectedChildId.toString(),
          'date': date,
          'showType': _showType,
        },
      );
      final response = await http.get(uri).timeout(const Duration(seconds: 60));

      if (response.statusCode == 401) {
        await AsyncIdChecker.logout();
        if (mounted) Navigator.pushReplacementNamed(context, '/Login');
        return;
      }
      if (response.statusCode == 403) {
        if (mounted) {
          setState(() {
            _customers = [];
            _loadingFollow = false;
            _error = 'هذه القائمة غير مرتبطة بحسابك';
          });
        }
        return;
      }
      if (response.statusCode != 200) {
        throw Exception('follow');
      }

      final data = json.decode(response.body) as List<dynamic>;
      var customers = data.map((item) {
        return {
          'customerId': int.tryParse('${item['customerID'] ?? item['CustomerID'] ?? item['customerId'] ?? 0}') ?? 0,
          'userId': int.tryParse('${item['userID'] ?? item['UserID'] ?? item['userId'] ?? 0}') ?? 0,
          'saleName': item['saleName'] ?? item['SaleName'] ?? '',
          'cityName': item['cityName'] ?? item['CityName'] ?? '',
          'address': item['address'] ?? item['Address'] ?? '',
          'customerName': item['customerName'] ?? item['CustomerName'] ?? '',
          'phoneNumber': item['phoneNumber'] ?? item['PhoneNumber'] ?? '',
          'amountReceipt': _toDouble(item['amountReceipt'] ?? item['AmountReceipt']),
          'amountDaySales': _toDouble(item['amountDaySales'] ?? item['AmountDaySales']),
          'amountRemaining': _toDouble(item['amountRemaining'] ?? item['AmountRemaining']),
          'amountTotalSales': _toDouble(item['amountTotalSales'] ?? item['AmountTotalSales']),
          'countReceiptDevice': _toInt(item['countReceiptDevice'] ?? item['CountReceiptDevice']),
          'numberOfDayDevice': _toInt(item['numberOfDayDevice'] ?? item['NumberOfDayDevice']),
          'numberOfDayPayment': _toInt(item['numberOfDayPayment'] ?? item['NumberOfDayPayment']),
        };
      }).toList();

      if (_showType == 'الغير مسددين') {
        customers = customers
            .where((item) => (item['amountRemaining'] as double) > 0)
            .toList();
      }

      if (mounted) {
        setState(() {
          _customers = customers;
          _loadingFollow = false;
        });
      }
      await _loadLatestFollowupNotes();
    } catch (e) {
      if (mounted) {
        setState(() {
          _loadingFollow = false;
          _error = 'تعذر جلب المسددين / غير المسددين';
        });
      }
    }
  }

  Future<void> _loadLatestFollowupNotes() async {
    if (_selectedChildId == null) return;
    try {
      final session = await _session();
      final uri = Uri.parse('${session['link']}Followers/FieldFollowups/latest-notes')
          .replace(queryParameters: {
        'asyncId': session['asyncId'],
        'listId': '${_selectedChildId}',
      });
      final res = await http.get(uri).timeout(const Duration(seconds: 30));
      if (res.statusCode != 200) return;
      final data = json.decode(res.body);
      final raw = data is Map ? (data['items'] ?? data['Items'] ?? []) : [];
      final map = <int, String>{};
      for (final e in (raw as List)) {
        if (e is! Map) continue;
        final id = int.tryParse('${e['customerId'] ?? e['CustomerId'] ?? 0}') ?? 0;
        final note = '${e['note'] ?? e['Note'] ?? ''}'.trim();
        if (id > 0 && note.isNotEmpty) map[id] = note;
      }
      if (mounted) {
        setState(() {
          _latestFollowupNotes
            ..clear()
            ..addAll(map);
        });
      }
    } catch (_) {
      // optional enrichment
    }
  }

  double _toDouble(dynamic value) {
    if (value == null) return 0;
    if (value is num) return value.toDouble();
    return double.tryParse(value.toString()) ?? 0;
  }

  int _toInt(dynamic value) {
    if (value == null) return 0;
    if (value is int) return value;
    if (value is num) return value.toInt();
    return int.tryParse(value.toString()) ?? 0;
  }

  Future<void> _pickDate() async {
    final picked = await showDatePicker(
      context: context,
      initialDate: _paymentDate,
      firstDate: DateTime(2020),
      lastDate: DateTime.now(),
    );
    if (picked != null) {
      setState(() {
        _paymentDate = picked;
      });
      await _loadFollow();
    }
  }

  Future<void> _onSelectList(int? id) async {
    if (id == null) return;
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString('SelectedChildId', id.toString());
    setState(() {
      _selectedChildId = id;
    });
    await _loadFollow();
  }

  Future<void> _addListDelegateNote() async {
    if (_selectedChildId == null) return;
    final list = _lists.cast<Map<String, dynamic>?>().firstWhere(
          (item) => item?['id'] == _selectedChildId,
          orElse: () => null,
        );
    final listName = list?['name']?.toString() ?? 'القائمة';
    // Avoid TextEditingController dispose races with dialog TextField (_dependents.isEmpty).
    var draft = '';
    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text('ملاحظة على مندوب $listName', style: const TextStyle(fontFamily: 'Cairo')),
        content: TextField(
          maxLines: 4,
          onChanged: (v) => draft = v,
          decoration: const InputDecoration(hintText: 'نص الملاحظة *', hintStyle: TextStyle(fontFamily: 'Cairo')),
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('إلغاء', style: TextStyle(fontFamily: 'Cairo'))),
          ElevatedButton(onPressed: () => Navigator.pop(ctx, true), child: const Text('حفظ', style: TextStyle(fontFamily: 'Cairo'))),
        ],
      ),
    );
    final text = draft.trim();
    if (!mounted || ok != true || text.isEmpty) return;
    try {
      final session = await _session();
      final delegateId = _selectedChildId!;
      final uri = Uri.parse('${session['link']}Followers/Delegates/$delegateId/notes');
      final response = await http
          .post(
            uri,
            headers: {'Content-Type': 'application/json'},
            body: json.encode({
              'asyncId': session['asyncId'],
              'listId': delegateId,
              'noteText': text,
            }),
          )
          .timeout(const Duration(seconds: 20));
      if (!mounted) return;
      final msg = response.statusCode == 200
          ? 'تم حفظ ملاحظة مندوب القائمة'
          : response.statusCode == 404
              ? '404: مسار ملاحظات المندوب غير موجود على السيرفر'
              : 'تعذر الحفظ (HTTP ${response.statusCode})';
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(msg, style: const TextStyle(fontFamily: 'Cairo'))));
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('تعذر حفظ ملاحظة المندوب', style: TextStyle(fontFamily: 'Cairo'))),
        );
      }
    }
  }

  Future<void> _openTutorialVideo() async {
    if (!mounted) return;
    await Navigator.of(context).push(
      MaterialPageRoute(builder: (_) => const TutorialQuestionsPage()),
    );
  }

  Future<void> _logout() async {
    await AsyncIdChecker.logout();
    if (mounted) Navigator.pushReplacementNamed(context, '/Login');
  }

  String _selectedListName() {
    final list = _lists.cast<Map<String, dynamic>?>().firstWhere(
          (item) => item?['id'] == _selectedChildId,
          orElse: () => null,
        );
    return list?['name']?.toString() ?? 'قائمة';
  }

  Future<void> _moveToFieldFollowup(Map<String, dynamic> item) async {
    if (_selectedChildId == null) return;
    final customerId = item['customerId'] as int? ?? 0;
    if (customerId <= 0) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('بيانات الزبون غير كاملة', style: TextStyle(fontFamily: 'Cairo'))),
      );
      return;
    }
    try {
      final session = await _session();
      final uri = Uri.parse('${session['link']}Followers/FieldFollowups');
      final response = await http
          .post(
            uri,
            headers: {'Content-Type': 'application/json'},
            body: json.encode({
              'asyncId': session['asyncId'],
              'listId': _selectedChildId,
              'listName': _selectedListName(),
              'customerId': customerId,
              'customerName': item['customerName']?.toString() ?? '',
              'phoneNumber': item['phoneNumber']?.toString() ?? '',
            }),
          )
          .timeout(const Duration(seconds: 30));
      if (!mounted) return;
      final msg = response.statusCode == 200
          ? 'تم الإرسال إلى المتابعة الميدانية'
          : response.statusCode == 404
              ? '404: مسار المتابعة الميدانية غير موجود على السيرفر'
              : 'تعذر الإرسال (HTTP ${response.statusCode})';
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(msg, style: const TextStyle(fontFamily: 'Cairo'))),
      );
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('تعذر الإرسال إلى المتابعة الميدانية', style: TextStyle(fontFamily: 'Cairo')),
          ),
        );
      }
    }
  }

  Future<void> _recordFieldFollowupDone(Map<String, dynamic> item) async {
    if (_selectedChildId == null) return;
    final customerId = item['customerId'] as int? ?? 0;
    if (customerId <= 0) return;

    var draft = '';
    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('تمت المتابعة', style: TextStyle(fontFamily: 'Cairo')),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(
              '${item['customerName'] ?? ''}',
              style: const TextStyle(fontFamily: 'Cairo', fontWeight: FontWeight.bold, fontSize: 16),
            ),
            const SizedBox(height: 12),
            TextField(
              maxLines: 3,
              onChanged: (v) => draft = v,
              decoration: const InputDecoration(
                labelText: 'ملاحظة إجبارية',
                border: OutlineInputBorder(),
              ),
              style: const TextStyle(fontFamily: 'Cairo'),
            ),
          ],
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx, false),
            child: const Text('إلغاء', style: TextStyle(fontFamily: 'Cairo')),
          ),
          ElevatedButton(
            onPressed: () => Navigator.pop(ctx, true),
            child: const Text('اعتماد', style: TextStyle(fontFamily: 'Cairo')),
          ),
        ],
      ),
    );
    if (ok != true) return;
    final note = draft.trim();
    if (note.isEmpty) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('الملاحظة إجبارية', style: TextStyle(fontFamily: 'Cairo'))),
        );
      }
      return;
    }

    try {
      final session = await _session();
      final uri = Uri.parse('${session['link']}Followers/FieldFollowups/record-complete');
      final res = await http
          .post(
            uri,
            headers: {'Content-Type': 'application/json'},
            body: json.encode({
              'asyncId': session['asyncId'],
              'listId': _selectedChildId,
              'listName': _selectedListName(),
              'customerId': customerId,
              'customerName': item['customerName']?.toString() ?? '',
              'phoneNumber': item['phoneNumber']?.toString() ?? '',
              'note': note,
            }),
          )
          .timeout(const Duration(seconds: 45));
      if (!mounted) return;
      if (res.statusCode != 200) {
        final msg = () {
          try {
            final j = json.decode(res.body);
            return '${j['message'] ?? j['Message'] ?? 'تعذر الاعتماد'}';
          } catch (_) {
            return 'تعذر الاعتماد (HTTP ${res.statusCode})';
          }
        }();
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(msg, style: const TextStyle(fontFamily: 'Cairo'))),
        );
        return;
      }
      setState(() => _latestFollowupNotes[customerId] = note);
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('تم تسجيل المتابعة في الملف', style: TextStyle(fontFamily: 'Cairo'))),
      );
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('تعذر تسجيل المتابعة', style: TextStyle(fontFamily: 'Cairo'))),
        );
      }
    }
  }

  double get _totalReceipt =>
      _customers.fold(0.0, (sum, item) => sum + (item['amountReceipt'] as double));

  double get _totalDay =>
      _customers.fold(0.0, (sum, item) => sum + (item['amountDaySales'] as double));

  List<Map<String, dynamic>> get _visibleCustomers {
    final query = _searchQuery.trim();
    if (query.isEmpty) return _customers;
    return _customers.where((item) {
      final name = item['customerName'].toString();
      final phone = item['phoneNumber'].toString();
      return name.contains(query) || phone.contains(query);
    }).toList();
  }

  List<Map<String, dynamic>> get _underDueCustomers =>
      FollowerPayersSplit.underDue(_visibleCustomers);

  List<Map<String, dynamic>> get _correctPaidCustomers =>
      FollowerPayersSplit.correct(_visibleCustomers);

  List<Map<String, dynamic>> get _unpaidDays1to3 =>
      FollowerUnpaidDaysSplit.days1to3(_visibleCustomers);

  List<Map<String, dynamic>> get _unpaidDays4to7 =>
      FollowerUnpaidDaysSplit.days4to7(_visibleCustomers);

  List<Map<String, dynamic>> get _unpaidDays8to14 =>
      FollowerUnpaidDaysSplit.days8to14(_visibleCustomers);

  List<Map<String, dynamic>> get _unpaidDays15Plus =>
      FollowerUnpaidDaysSplit.days15Plus(_visibleCustomers);

  @override
  Widget build(BuildContext context) {
    return Directionality(
      textDirection: TextDirection.rtl,
      child: AppSafeScaffold(
        scaffoldKey: _scaffoldKey,
        backgroundColor: AppTheme.backgroundColor,
        drawer: Drawer(
          child: SafeArea(
            child: ListView(
              padding: EdgeInsets.zero,
              children: [
                DrawerHeader(
                  decoration: BoxDecoration(color: AppTheme.primaryColor),
                  child: const Align(
                    alignment: Alignment.bottomRight,
                    child: Text(
                      'القائمة',
                      style: TextStyle(
                        fontFamily: 'Cairo',
                        color: Colors.white,
                        fontSize: 22,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                  ),
                ),
                ListTile(
                  leading: const Icon(Icons.point_of_sale),
                  title: const Text('المبيعات', style: TextStyle(fontFamily: 'Cairo')),
                  onTap: () {
                    Navigator.pop(context);
                    Navigator.pushNamed(context, '/PendingSales');
                  },
                ),
                ListTile(
                  leading: const Icon(Icons.bar_chart),
                  title: const Text('الإحصائيات', style: TextStyle(fontFamily: 'Cairo')),
                  onTap: () {
                    Navigator.pop(context);
                    Navigator.pushNamed(context, '/ListStats');
                  },
                ),
                ListTile(
                  leading: const Icon(Icons.gavel),
                  title: const Text('الزبائن القديمة', style: TextStyle(fontFamily: 'Cairo')),
                  onTap: () {
                    Navigator.pop(context);
                    Navigator.pushNamed(context, '/LegalFollowups');
                  },
                ),
                ListTile(
                  leading: const Icon(Icons.report_gmailerrorred_outlined),
                  title: const Text('إرسال شكوى', style: TextStyle(fontFamily: 'Cairo')),
                  onTap: () {
                    Navigator.pop(context);
                    FollowerComplaintSheet.show(context);
                  },
                ),
              ],
            ),
          ),
        ),
        body: RefreshIndicator(
            onRefresh: _loadFollow,
            color: AppTheme.primaryColor,
            child: ListView(
              padding: AppInsets.scrollPadding(context),
              children: [
                Row(
                  children: [
                    IconButton(
                      tooltip: 'القائمة',
                      onPressed: () => _scaffoldKey.currentState?.openDrawer(),
                      icon: const Icon(Icons.menu),
                    ),
                    IconButton(
                      onPressed: () {
                        MyApp.themeNotifier.value =
                            MyApp.themeNotifier.value == ThemeMode.light
                                ? ThemeMode.dark
                                : ThemeMode.light;
                        setState(() {});
                      },
                      icon: Icon(
                        MyApp.themeNotifier.value == ThemeMode.light
                            ? Icons.dark_mode
                            : Icons.light_mode,
                      ),
                    ),
                    const Spacer(),
                    Column(
                      crossAxisAlignment: CrossAxisAlignment.end,
                      children: [
                        const Text(
                          'تطبيق مسؤول اللجنة',
                          style: TextStyle(
                            fontFamily: 'Cairo',
                            fontWeight: FontWeight.bold,
                            fontSize: 16,
                          ),
                        ),
                  Text(
                    _delegateName.isEmpty ? '—' : _delegateName,
                    style: const TextStyle(
                      fontFamily: 'Cairo',
                      fontSize: 13,
                      fontWeight: FontWeight.w600,
                      color: Colors.grey,
                    ),
                  ),
                      ],
                    ),
                    const SizedBox(width: 10),
                    InkWell(
                      onTap: _logout,
                      child: const CircleAvatar(
                        radius: 22,
                        backgroundColor: AppTheme.primaryColor,
                        child: Icon(Icons.person, color: Colors.white),
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 20),
                _card(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      Text(
                        _activeShift != null && _activeShift!.isActive
                            ? 'الدوام فعال — #${_activeShift!.shiftId}'
                            : 'الدوام غير فعال',
                        style: const TextStyle(
                          fontFamily: 'Cairo',
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                      if (_shiftError.isNotEmpty) ...[
                        const SizedBox(height: 6),
                        Text(
                          _shiftError,
                          style: const TextStyle(
                            fontFamily: 'Cairo',
                            color: Colors.red,
                            fontSize: 12,
                          ),
                        ),
                      ],
                      const SizedBox(height: 10),
                      Row(
                        children: [
                          Expanded(
                            child: ElevatedButton.icon(
                              onPressed: _shiftBusy || (_activeShift?.isActive ?? false)
                                  ? null
                                  : _startShift,
                              icon: _shiftBusy
                                  ? const SizedBox(
                                      width: 16,
                                      height: 16,
                                      child: CircularProgressIndicator(strokeWidth: 2),
                                    )
                                  : const Icon(Icons.play_arrow, size: 18),
                              label: const Text(
                                'بدء الدوام',
                                style: TextStyle(fontFamily: 'Cairo', fontSize: 13),
                              ),
                              style: ElevatedButton.styleFrom(
                                backgroundColor: Colors.green.shade700,
                                foregroundColor: Colors.white,
                              ),
                            ),
                          ),
                          const SizedBox(width: 8),
                          Expanded(
                            child: OutlinedButton.icon(
                              onPressed: _shiftBusy || !(_activeShift?.isActive ?? false)
                                  ? null
                                  : _endShift,
                              icon: const Icon(Icons.stop, size: 18),
                              label: const Text(
                                'إنهاء الدوام',
                                style: TextStyle(fontFamily: 'Cairo', fontSize: 13),
                              ),
                            ),
                          ),
                        ],
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: 12),
                if (_loadingLists)
                  const Center(
                      child: Padding(
                    padding: EdgeInsets.all(40),
                    child: CircularProgressIndicator(color: AppTheme.primaryColor),
                  ))
                else ...[
                  if (_lists.isEmpty)
                    _card(
                      child: const Padding(
                        padding: EdgeInsets.symmetric(vertical: 16),
                        child: Text(
                          'لا توجد قوائم مخصصة لك حاليًا',
                          textAlign: TextAlign.center,
                          style: TextStyle(
                            fontFamily: 'Cairo',
                            fontWeight: FontWeight.bold,
                            color: Colors.grey,
                          ),
                        ),
                      ),
                    )
                  else ...[
                  _card(
                    child: DropdownButtonFormField<int>(
                      value: _selectedChildId,
                      decoration: const InputDecoration(
                        labelText: 'القائمة المرتبطة',
                        border: InputBorder.none,
                      ),
                      items: _lists
                          .map((item) => DropdownMenuItem<int>(
                                value: item['id'] as int,
                                child: Text(
                                  item['name'] as String,
                                  style: const TextStyle(fontFamily: 'Cairo'),
                                ),
                              ))
                          .toList(),
                      onChanged: _onSelectList,
                    ),
                  ),
                  const SizedBox(height: 12),
                  Row(
                    children: [
                      Expanded(
                        child: ElevatedButton.icon(
                          onPressed: _selectedChildId == null
                              ? null
                              : () => Navigator.pushNamed(
                                    context,
                                    '/FollowerSalesRequest',
                                    arguments: {'listId': _selectedChildId},
                                  ),
                          icon: const Icon(Icons.add_shopping_cart, size: 18),
                          label: const Text('طلب مبيع جديد', style: TextStyle(fontFamily: 'Cairo', fontSize: 13)),
                          style: ElevatedButton.styleFrom(
                            backgroundColor: AppTheme.primaryColor,
                            foregroundColor: Colors.white,
                          ),
                        ),
                      ),
                      const SizedBox(width: 8),
                      Expanded(
                        child: OutlinedButton.icon(
                          onPressed: _selectedChildId == null ? null : _addListDelegateNote,
                          icon: const Icon(Icons.note_alt_outlined, size: 18),
                          label: const Text('ملاحظة المندوب', style: TextStyle(fontFamily: 'Cairo', fontSize: 13)),
                        ),
                      ),
                    ],
                  ),
                  ],
                  if (_lists.isNotEmpty) ...[
                  const SizedBox(height: 12),
                  _card(
                    child: Row(
                      children: [
                        Expanded(
                          child: InkWell(
                            onTap: _pickDate,
                            child: Padding(
                              padding: const EdgeInsets.symmetric(vertical: 8),
                              child: Text(
                                'التاريخ: ${IraqDateTime.formatYmd(_paymentDate)}',
                                style: const TextStyle(
                                    fontFamily: 'Cairo',
                                    fontWeight: FontWeight.bold),
                              ),
                            ),
                          ),
                        ),
                        IconButton(
                          onPressed: _pickDate,
                          icon: const Icon(Icons.calendar_month,
                              color: AppTheme.primaryColor),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: 12),
                  SizedBox(
                    width: double.infinity,
                    child: ElevatedButton.icon(
                      onPressed: _openTutorialVideo,
                      icon: const Icon(Icons.play_circle_filled_rounded,
                          color: Colors.white, size: 28),
                      label: const Text(
                        'مشاهدة الفيديو التعليمي',
                        style: TextStyle(
                          fontFamily: 'Cairo',
                          color: Colors.white,
                          fontWeight: FontWeight.bold,
                          fontSize: 16,
                        ),
                      ),
                      style: ElevatedButton.styleFrom(
                        backgroundColor: const Color(0xFFFFC107),
                        padding: const EdgeInsets.symmetric(vertical: 14),
                        shape: RoundedRectangleBorder(
                            borderRadius: BorderRadius.circular(12)),
                      ),
                    ),
                  ),
                  const SizedBox(height: 12),
                  Row(
                    children: [
                      Expanded(child: _typeChip('المسددين')),
                      const SizedBox(width: 10),
                      Expanded(child: _typeChip('الغير مسددين')),
                    ],
                  ),
                  const SizedBox(height: 12),
                  _card(
                    child: TextField(
                      controller: _searchController,
                      textInputAction: TextInputAction.search,
                      decoration: InputDecoration(
                        hintText: 'بحث بالاسم أو رقم الهاتف',
                        hintStyle: const TextStyle(fontFamily: 'Cairo'),
                        border: InputBorder.none,
                        prefixIcon: const Icon(Icons.search,
                            color: AppTheme.primaryColor),
                        suffixIcon: _searchQuery.isEmpty
                            ? null
                            : IconButton(
                                onPressed: () {
                                  _searchController.clear();
                                  setState(() {
                                    _searchQuery = '';
                                  });
                                },
                                icon: const Icon(Icons.close),
                              ),
                      ),
                      onChanged: (value) {
                        setState(() {
                          _searchQuery = value;
                        });
                      },
                    ),
                  ),
                  const SizedBox(height: 16),
                  Row(
                    children: [
                      Expanded(
                        child: _statBox(
                          _showType == 'المسددين' ? 'عدد المسددين' : 'عدد غير المسددين',
                          '${_customers.length}',
                        ),
                      ),
                      const SizedBox(width: 10),
                      Expanded(
                        child: _statBox(
                          _showType == 'المسددين' ? 'الواصل' : 'مجموع الأقساط',
                          '${Formatters.formatNumber(_showType == 'المسددين' ? _totalReceipt : _totalDay)} دع',
                        ),
                      ),
                    ],
                  ),
                  if (_showType == 'المسددين' && !_loadingFollow && _customers.isNotEmpty) ...[
                    const SizedBox(height: 12),
                    Row(
                      children: [
                        Expanded(
                          child: _statBox(
                            'دون القسط المستحق',
                            '${_underDueCustomers.length}',
                          ),
                        ),
                        const SizedBox(width: 10),
                        Expanded(
                          child: _statBox(
                            'مسدد بشكل صحيح',
                            '${_correctPaidCustomers.length}',
                          ),
                        ),
                      ],
                    ),
                  ],
                  const SizedBox(height: 16),
                  if (_error.isNotEmpty)
                    Text(_error,
                        style: const TextStyle(
                            fontFamily: 'Cairo', color: Colors.red)),
                  if (_loadingFollow)
                    const Padding(
                      padding: EdgeInsets.all(30),
                      child: Center(
                          child: CircularProgressIndicator(
                              color: AppTheme.primaryColor)),
                    )
                  else if (_customers.isEmpty && _error.isEmpty)
                    const Padding(
                      padding: EdgeInsets.all(30),
                      child: Center(
                        child: Text(
                          'لا توجد بيانات لهذا اليوم',
                          style: TextStyle(fontFamily: 'Cairo', color: Colors.grey),
                        ),
                      ),
                    )
                  else if (_visibleCustomers.isEmpty)
                    const Padding(
                      padding: EdgeInsets.all(30),
                      child: Center(
                        child: Text(
                          'لا يوجد اسم مطابق للبحث',
                          style: TextStyle(fontFamily: 'Cairo', color: Colors.grey),
                        ),
                      ),
                    )
                  else if (_showType == 'المسددين') ...[
                    _payersSection(
                      title: 'المسددين دون القسط المستحق',
                      subtitle: 'مسدد أقل من القسط المستحق',
                      accent: Colors.orange.shade700,
                      rows: _underDueCustomers,
                    ),
                    const SizedBox(height: 14),
                    _payersSection(
                      title: 'الزبائن المسددين بشكل صحيح',
                      subtitle: 'مسدد يساوي أو أكثر من القسط المستحق',
                      accent: Colors.green.shade700,
                      rows: _correctPaidCustomers,
                    ),
                  ] else if (_showType == 'الغير مسددين') ...[
                    _payersSection(
                      title: 'غير المسددين من يوم الى 3 ايام',
                      subtitle: 'بدون تسديد 1–3 أيام',
                      accent: Colors.blue.shade700,
                      rows: _unpaidDays1to3,
                    ),
                    const SizedBox(height: 14),
                    _payersSection(
                      title: 'من 4 ايام الى 7 ايام',
                      subtitle: 'بدون تسديد 4–7 أيام',
                      accent: Colors.teal.shade700,
                      rows: _unpaidDays4to7,
                    ),
                    const SizedBox(height: 14),
                    _payersSection(
                      title: 'من 8 ايام الى 14 يوم',
                      subtitle: 'بدون تسديد 8–14 يوم',
                      accent: Colors.deepOrange.shade700,
                      rows: _unpaidDays8to14,
                    ),
                    const SizedBox(height: 14),
                    _payersSection(
                      title: '15 يوم واكثر',
                      subtitle: 'بدون تسديد 15 يوم فأكثر',
                      accent: Colors.red.shade700,
                      rows: _unpaidDays15Plus,
                    ),
                  ] else
                    ...withNameSeparators(
                        _visibleCustomers.map(_customerCard)),
                  ],
                  const SizedBox(height: 20),
                  TextButton.icon(
                    onPressed: _logout,
                    icon: const Icon(Icons.logout, color: Colors.red),
                    label: const Text('تسجيل الخروج',
                        style: TextStyle(fontFamily: 'Cairo', color: Colors.red)),
                  ),
                ],
              ],
            ),
          ),
        ),
    );
  }

  Widget _typeChip(String type) {
    final selected = _showType == type;
    return InkWell(
      onTap: () async {
        setState(() {
          _showType = type;
        });
        await _loadFollow();
      },
      child: Container(
        padding: const EdgeInsets.symmetric(vertical: 12),
        decoration: BoxDecoration(
          color: selected ? AppTheme.primaryColor : Colors.white,
          borderRadius: BorderRadius.circular(14),
          border: Border.all(color: AppTheme.primaryColor),
        ),
        child: Text(
          type,
          textAlign: TextAlign.center,
          style: TextStyle(
            fontFamily: 'Cairo',
            fontWeight: FontWeight.bold,
            color: selected ? Colors.white : AppTheme.primaryColor,
          ),
        ),
      ),
    );
  }

  Widget _card({required Widget child}) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 4),
      decoration: BoxDecoration(
        color: Theme.of(context).cardColor,
        borderRadius: BorderRadius.circular(16),
      ),
      child: child,
    );
  }

  Widget _statBox(String title, String value) {
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Theme.of(context).cardColor,
        borderRadius: BorderRadius.circular(16),
      ),
      child: Column(
        children: [
          Text(title,
              style: const TextStyle(fontFamily: 'Cairo', color: Colors.grey)),
          const SizedBox(height: 6),
          Text(value,
              style: const TextStyle(
                  fontFamily: 'Cairo',
                  fontWeight: FontWeight.bold,
                  fontSize: 16)),
        ],
      ),
    );
  }

  Widget _payersSection({
    required String title,
    required String subtitle,
    required Color accent,
    required List<Map<String, dynamic>> rows,
  }) {
    return Container(
      decoration: BoxDecoration(
        color: Theme.of(context).cardColor,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: accent.withValues(alpha: 0.35)),
      ),
      padding: const EdgeInsets.fromLTRB(12, 12, 12, 8),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            children: [
              Container(
                width: 8,
                height: 28,
                decoration: BoxDecoration(
                  color: accent,
                  borderRadius: BorderRadius.circular(4),
                ),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      title,
                      style: TextStyle(
                        fontFamily: 'Cairo',
                        fontWeight: FontWeight.bold,
                        fontSize: 15,
                        color: accent,
                      ),
                    ),
                    Text(
                      '$subtitle — ${rows.length}',
                      style: const TextStyle(
                        fontFamily: 'Cairo',
                        fontSize: 11,
                        color: Colors.grey,
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
          const SizedBox(height: 10),
          if (rows.isEmpty)
            const Padding(
              padding: EdgeInsets.symmetric(vertical: 18),
              child: Center(
                child: Text(
                  'لا يوجد زبائن في هذا القسم',
                  style: TextStyle(fontFamily: 'Cairo', color: Colors.grey),
                ),
              ),
            )
          else
            ...withNameSeparators(rows.map(_customerCard)),
        ],
      ),
    );
  }

  Widget _customerCard(Map<String, dynamic> item) {
    final remaining = Formatters.formatNumber(item['amountRemaining']);
    final due = Formatters.formatNumber(item['amountDaySales']);
    final paid = Formatters.formatNumber(item['amountReceipt']);
    final paymentDays = item['numberOfDayDevice'] as int;
    final unpaidDays = FollowerUnpaidDaysSplit.daysSincePayment(item);

    return Container(
      decoration: BoxDecoration(
        color: Theme.of(context).cardColor,
        borderRadius: BorderRadius.circular(16),
      ),
      child: InkWell(
        borderRadius: BorderRadius.circular(16),
        onTap: () {
          if (_selectedChildId == null) return;
          Navigator.pushNamed(
            context,
            '/CustomerProfile',
            arguments: {
              'customer': item,
              'listId': _selectedChildId,
            },
          );
        },
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              PersonNameText(item['customerName'].toString()),
              const SizedBox(height: 4),
              Text(
                item['phoneNumber'].toString().isEmpty
                    ? 'لا يوجد هاتف'
                    : item['phoneNumber'].toString(),
                style: const TextStyle(fontFamily: 'Cairo', color: Colors.grey),
              ),
              if ('${item['saleName'] ?? ''}'.trim().isNotEmpty) ...[
                const SizedBox(height: 4),
                Text(
                  'المندوب: ${item['saleName']}',
                  style: const TextStyle(fontFamily: 'Cairo', fontSize: 12, color: Colors.grey),
                ),
              ],
              const SizedBox(height: 10),
              if (_showType == 'الغير مسددين') ...[
                _amountRow('أيام بدون تسديد', '$unpaidDays', Colors.red.shade700),
                const SizedBox(height: 4),
              ],
              _amountRow('القسط المستحق', '$due دع', AppTheme.primaryColor),
              const SizedBox(height: 4),
              if (_showType == 'المسددين') ...[
                _amountRow('القسط المدفوع', '$paid دع', Colors.green.shade700),
                const SizedBox(height: 4),
              ],
              _amountRow('عدد أيام التسديدات', '$paymentDays', AppTheme.textColor),
              const SizedBox(height: 4),
              _amountRow('الباقي', '$remaining دع', Colors.orange),
              if ((_latestFollowupNotes[item['customerId'] as int? ?? 0] ?? '').isNotEmpty) ...[
                const SizedBox(height: 8),
                Text(
                  'آخر متابعة: ${_latestFollowupNotes[item['customerId'] as int? ?? 0]}',
                  style: TextStyle(
                    fontFamily: 'Cairo',
                    fontSize: 12,
                    color: Colors.blueGrey.shade700,
                    height: 1.4,
                  ),
                ),
              ],
              const SizedBox(height: 10),
              Wrap(
                spacing: 8,
                runSpacing: 8,
                children: [
                  OutlinedButton(
                    onPressed: () => _moveToFieldFollowup(item),
                    child: const Text(
                      'إرسال للمتابعة الميدانية',
                      style: TextStyle(fontFamily: 'Cairo', fontSize: 12),
                    ),
                  ),
                  FilledButton(
                    style: FilledButton.styleFrom(backgroundColor: AppTheme.primaryColor),
                    onPressed: () => _recordFieldFollowupDone(item),
                    child: const Text(
                      'تمت المتابعة',
                      style: TextStyle(fontFamily: 'Cairo', fontSize: 12),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 6),
              const Text(
                'اضغط على الكارت لفتح الملف / الملاحظات',
                style: TextStyle(fontFamily: 'Cairo', fontSize: 11, color: Colors.grey),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _amountRow(String label, String value, Color valueColor) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: [
        Text(label, style: const TextStyle(fontFamily: 'Cairo', color: Colors.grey)),
        Text(
          value,
          style: TextStyle(
            fontFamily: 'Cairo',
            fontWeight: FontWeight.bold,
            color: valueColor,
          ),
        ),
      ],
    );
  }
}
