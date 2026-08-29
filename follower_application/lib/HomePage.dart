import 'dart:convert';
import 'package:follower_application/config/app_env.dart';
import 'package:follower_application/AsyncIdChecker.dart';
import 'package:follower_application/main.dart';
import 'package:follower_application/utils/AppTheme.dart';
import 'package:follower_application/utils/Formatters.dart';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:intl/intl.dart' hide TextDirection;
import 'package:shared_preferences/shared_preferences.dart';

class HomePage extends StatefulWidget {
  const HomePage({super.key});

  @override
  State<HomePage> createState() => _HomePageState();
}

class _HomePageState extends State<HomePage> {
  bool _loadingLists = true;
  bool _loadingFollow = false;
  String _error = '';
  String _delegateName = '';
  String _showType = 'المسددين';
  DateTime _paymentDate = DateTime.now().subtract(const Duration(days: 1));

  List<Map<String, dynamic>> _lists = [];
  int? _selectedChildId;
  List<Map<String, dynamic>> _customers = [];

  @override
  void initState() {
    super.initState();
    _boot();
  }

  Future<void> _boot() async {
    final loggedIn = await AsyncIdChecker.isLoggedIn();
    if (!loggedIn) {
      if (mounted) Navigator.pushReplacementNamed(context, '/Login');
      return;
    }
    await _loadLists();
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
      final date = DateFormat('yyyy-MM-dd').format(_paymentDate);
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
      final customers = data.map((item) {
        return {
          'customerName': item['customerName'] ?? item['CustomerName'] ?? '',
          'phoneNumber': item['phoneNumber'] ?? item['PhoneNumber'] ?? '',
          'amountReceipt': _toDouble(item['amountReceipt'] ?? item['AmountReceipt']),
          'amountDaySales': _toDouble(item['amountDaySales'] ?? item['AmountDaySales']),
          'amountRemaining': _toDouble(item['amountRemaining'] ?? item['AmountRemaining']),
          'amountTotalSales': _toDouble(item['amountTotalSales'] ?? item['AmountTotalSales']),
        };
      }).toList();

      if (mounted) {
        setState(() {
          _customers = customers;
          _loadingFollow = false;
        });
      }
    } catch (e) {
      if (mounted) {
        setState(() {
          _loadingFollow = false;
          _error = 'تعذر جلب المسددين / غير المسددين';
        });
      }
    }
  }

  double _toDouble(dynamic value) {
    if (value == null) return 0;
    if (value is num) return value.toDouble();
    return double.tryParse(value.toString()) ?? 0;
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

  Future<void> _logout() async {
    await AsyncIdChecker.logout();
    if (mounted) Navigator.pushReplacementNamed(context, '/Login');
  }

  double get _totalReceipt =>
      _customers.fold(0.0, (sum, item) => sum + (item['amountReceipt'] as double));

  double get _totalDay =>
      _customers.fold(0.0, (sum, item) => sum + (item['amountDaySales'] as double));

  @override
  Widget build(BuildContext context) {
    return Directionality(
      textDirection: TextDirection.rtl,
      child: Scaffold(
        backgroundColor: AppTheme.backgroundColor,
        body: SafeArea(
          child: RefreshIndicator(
            onRefresh: _loadFollow,
            color: AppTheme.primaryColor,
            child: ListView(
              padding: const EdgeInsets.all(20),
              children: [
                Row(
                  children: [
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
                          'تطبيق المتابع',
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
                            fontSize: 12,
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
                if (_loadingLists)
                  const Center(
                      child: Padding(
                    padding: EdgeInsets.all(40),
                    child: CircularProgressIndicator(color: AppTheme.primaryColor),
                  ))
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
                  _card(
                    child: Row(
                      children: [
                        Expanded(
                          child: InkWell(
                            onTap: _pickDate,
                            child: Padding(
                              padding: const EdgeInsets.symmetric(vertical: 8),
                              child: Text(
                                'التاريخ: ${DateFormat('yyyy-MM-dd').format(_paymentDate)}',
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
                  Row(
                    children: [
                      Expanded(child: _typeChip('المسددين')),
                      const SizedBox(width: 10),
                      Expanded(child: _typeChip('الغير مسددين')),
                    ],
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
                  else
                    ..._customers.map(_customerCard),
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

  Widget _customerCard(Map<String, dynamic> item) {
    return Container(
      margin: const EdgeInsets.only(bottom: 10),
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Theme.of(context).cardColor,
        borderRadius: BorderRadius.circular(16),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            item['customerName'].toString(),
            style: const TextStyle(
                fontFamily: 'Cairo', fontWeight: FontWeight.bold, fontSize: 16),
          ),
          const SizedBox(height: 4),
          Text(
            item['phoneNumber'].toString().isEmpty
                ? 'لا يوجد هاتف'
                : item['phoneNumber'].toString(),
            style: const TextStyle(fontFamily: 'Cairo', color: Colors.grey),
          ),
          const SizedBox(height: 8),
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text(
                _showType == 'المسددين'
                    ? 'الواصل: ${Formatters.formatNumber(item['amountReceipt'])} دع'
                    : 'القسط: ${Formatters.formatNumber(item['amountDaySales'])} دع',
                style: const TextStyle(fontFamily: 'Cairo'),
              ),
              Text(
                'الباقي: ${Formatters.formatNumber(item['amountRemaining'])} دع',
                style: const TextStyle(fontFamily: 'Cairo', color: Colors.orange),
              ),
            ],
          ),
        ],
      ),
    );
  }
}
