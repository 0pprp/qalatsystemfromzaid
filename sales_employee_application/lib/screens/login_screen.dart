import 'package:sales_employee_application/widgets/company_loader.dart';
import 'package:flutter/material.dart';
import 'package:sales_employee_application/config/app_env.dart';
import 'package:sales_employee_application/config/company_branches.dart';
import 'package:sales_employee_application/core/update/app_update_gate.dart';
import 'package:sales_employee_application/services/api_client.dart';
import 'package:sales_employee_application/services/device_handover.dart';
import 'package:sales_employee_application/services/session.dart';
import 'package:sales_employee_application/utils/app_theme.dart';

class LoginScreen extends StatefulWidget {
  const LoginScreen({super.key});

  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  final _userCtrl = TextEditingController();
  final _passCtrl = TextEditingController();
  bool _loading = false;
  bool _loadingBranches = false;
  String? _branchesError;
  List<CompanyBranch> _branches = [];
  CompanyBranch? _selectedBranch;

  @override
  void initState() {
    super.initState();
    if (AppEnv.isProduction) {
      _loadBranches();
    }
  }

  @override
  void dispose() {
    _userCtrl.dispose();
    _passCtrl.dispose();
    super.dispose();
  }

  Future<void> _loadBranches() async {
    setState(() {
      _loadingBranches = true;
      _branchesError = null;
    });
    try {
      final rows = await CompanyBranches.fetchSalesBranches();
      CompanyBranch? selected;
      final saved = Session.branchValue;
      if (saved != null) {
        for (final branch in rows) {
          if ('${branch.value}' == saved) {
            selected = branch;
            break;
          }
        }
      }
      selected ??= rows.isEmpty ? null : rows.first;
      if (!mounted) return;
      setState(() {
        _branches = rows;
        _selectedBranch = selected;
        _loadingBranches = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _loadingBranches = false;
        _branchesError = 'تعذر جلب قائمة الفروع';
      });
    }
  }

  Future<void> _login() async {
    if (_userCtrl.text.trim().isEmpty || _passCtrl.text.isEmpty) {
      _alert('لا يمكن ترك أي شيء فارغ');
      return;
    }
    if (AppEnv.isProduction && _selectedBranch == null) {
      _alert('اختر الفرع أولاً');
      return;
    }
    setState(() => _loading = true);
    try {
      await _kickPreviousUserIfNeeded(_userCtrl.text.trim());
      if (AppEnv.isProduction) {
        await Session.saveBranch(_selectedBranch!.toJson());
      } else if (AppEnv.isDemo) {
        await Session.saveBranch({
          'name': AppEnv.loginCityLabel(),
          'value': 'DatabaseCompanyNajaf_DEMO',
          'database': 'DatabaseCompanyNajaf_DEMO',
          'link': AppEnv.apiBase(),
        });
      }
      if (AppEnv.useMockSalesRepository) {
        await Session.saveLogin({
          'token': 'mock-token',
          'userName': _userCtrl.text.trim(),
          'cityName': _selectedBranch?.name ?? AppEnv.loginCityLabel(),
          'userId': '1',
          'userType': 'موظف مبيعات',
        });
        if (!mounted) return;
        final canContinueMock = await AppUpdateGate.checkAndPrompt(context);
        if (!canContinueMock || !mounted) return;
        Navigator.pushReplacementNamed(context, '/shift');
        return;
      }
      final data = await ApiClient.post('Users/Users_LoginEmployee', body: {
        'userName': _userCtrl.text.trim(),
        'password': _passCtrl.text,
      });
      if (data is! Map) {
        _alert('استجابة غير متوقعة من السيرفر');
        return;
      }
      final payload = Map<String, dynamic>.from(data);
      final token = '${payload['token'] ?? payload['Token'] ?? ''}';
      if (token.isEmpty) {
        _alert(payload['message']?.toString() ?? 'فشل تسجيل الدخول');
        return;
      }
      final userType = '${payload['userType'] ?? payload['UserType'] ?? ''}'.trim();
      if (userType != 'موظف مبيعات') {
        _alert(
          userType.isEmpty
              ? 'هذا التطبيق مخصص لحسابات «موظف مبيعات» فقط.'
              : 'نوع حسابك «$userType» غير مسموح في تطبيق موظف المبيعات.\nاستخدم حساباً نوعه «موظف مبيعات».',
        );
        return;
      }
      await _kickPreviousUserIfNeeded(_userCtrl.text.trim(), payload);
      await Session.saveLogin({
        ...payload,
        'token': token,
        'userName': _userCtrl.text.trim(),
        'userType': userType,
        'cityName': _selectedBranch?.name ?? AppEnv.loginCityLabel(),
      });
      if (!mounted) return;
      final canContinue = await AppUpdateGate.checkAndPrompt(context);
      if (!canContinue || !mounted) return;
      Navigator.pushReplacementNamed(context, '/shift');
    } on ApiException catch (e) {
      _alert(e.message);
    } catch (_) {
      _alert('لا يوجد اتصال بالإنترنت أو السيرفر غير متاح');
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _kickPreviousUserIfNeeded(
    String userName, [
    Map<String, dynamic>? payload,
  ]) async {
    if (!Session.isLoggedIn) return;
    final incoming = <String, dynamic>{
      ...?payload,
      'userName': userName,
    };
    if (Session.isSameLoggedInUser(incoming)) return;
    await DeviceHandover.kickPreviousUser();
  }

  void _alert(String message) {
    showDialog(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('تنبيه'),
        content: Text(message),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx), child: const Text('موافق')),
        ],
      ),
    );
  }

  Widget _branchSection() {
    if (AppEnv.isDemo) {
      return Column(
        children: [
          Text(
            'فرع النجف التجريبي — المحافظة ثابتة من الحساب',
            style: TextStyle(color: Colors.grey[600]),
          ),
          const Padding(
            padding: EdgeInsets.only(top: 6),
            child: Text(
              'النجف - DEMO',
              style: TextStyle(color: AppTheme.secondaryColor, fontSize: 12),
            ),
          ),
        ],
      );
    }
    if (AppEnv.isLocal) {
      return Text(
        'محلي — 127.0.0.1:5280',
        style: TextStyle(color: Colors.grey[600]),
      );
    }
    if (_loadingBranches) {
      return const Padding(
        padding: EdgeInsets.symmetric(vertical: 8),
        child: CompanyLoader(size: 32),
      );
    }
    if (_branchesError != null) {
      return Column(
        children: [
          Text(_branchesError!, style: const TextStyle(color: Colors.red)),
          TextButton(onPressed: _loadBranches, child: const Text('إعادة المحاولة')),
        ],
      );
    }
    return InputDecorator(
      decoration: const InputDecoration(
        hintText: 'اختر الفرع',
        prefixIcon: Icon(Icons.location_city_outlined, color: AppTheme.primaryColor),
      ),
      child: DropdownButtonHideUnderline(
        child: DropdownButton<CompanyBranch>(
          isExpanded: true,
          value: _selectedBranch,
          items: [
            for (final branch in _branches)
              DropdownMenuItem(
                value: branch,
                child: Text(branch.name),
              ),
          ],
          onChanged: (value) => setState(() => _selectedBranch = value),
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: SafeArea(
        child: LayoutBuilder(builder: (context, constraints) {
          return SingleChildScrollView(
            padding: const EdgeInsets.all(24),
            child: ConstrainedBox(
              constraints: BoxConstraints(minHeight: (constraints.maxHeight - 48).clamp(0, double.infinity)),
              child: Center(
                child: ConstrainedBox(
                  constraints: const BoxConstraints(maxWidth: 440),
                  child: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.stretch, children: [
                    Center(child: Image.asset('assets/icons/LogoCompany.png', height: 120, fit: BoxFit.contain)),
                    const SizedBox(height: 24),
                    const Text('أهلًا بك', textAlign: TextAlign.center,
                      style: TextStyle(fontSize: 30, fontWeight: FontWeight.w700, color: AppTheme.textColor)),
                    const SizedBox(height: 6),
                    const Text('سجّل دخولك إلى تطبيق موظف المبيعات', textAlign: TextAlign.center,
                      style: TextStyle(fontSize: 14, color: AppTheme.subtitleColor)),
                    const SizedBox(height: 32),
                    const Text('الفرع', style: TextStyle(fontWeight: FontWeight.w600)),
                    const SizedBox(height: 8),
                    _branchSection(),
                    const SizedBox(height: 20),
                    TextField(
                      controller: _userCtrl,
                      textInputAction: TextInputAction.next,
                      autofillHints: const [AutofillHints.username],
                      decoration: const InputDecoration(labelText: 'اسم المستخدم', prefixIcon: Icon(Icons.person_outline)),
                    ),
                    const SizedBox(height: 20),
                    TextField(
                      controller: _passCtrl,
                      obscureText: true,
                      textInputAction: TextInputAction.done,
                      autofillHints: const [AutofillHints.password],
                      onSubmitted: (_) { if (!_loading) _login(); },
                      decoration: const InputDecoration(labelText: 'كلمة المرور', prefixIcon: Icon(Icons.lock_outline)),
                    ),
                    const SizedBox(height: 28),
                    ElevatedButton(
                      onPressed: _loading ? null : _login,
                      child: _loading ? const CompanyLoader(size: 30) : const Text('تسجيل الدخول'),
                    ),
                    const SizedBox(height: 24),
                    const Text('شركة قلعة الضمان', textAlign: TextAlign.center,
                        style: TextStyle(color: AppTheme.subtitleColor, fontSize: 12)),
                  ]),
                ),
              ),
            ),
          );
        }),
      ),
    );
  }
}
