import 'package:branch_manager_application/config/app_env.dart';
import 'package:branch_manager_application/core/api.dart';
import 'package:branch_manager_application/theme/bm_theme.dart';
import 'package:flutter/material.dart';

class LoginPage extends StatefulWidget {
  const LoginPage({super.key, required this.onLoggedIn});

  final VoidCallback onLoggedIn;

  @override
  State<LoginPage> createState() => _LoginPageState();
}

class _LoginPageState extends State<LoginPage> {
  final _userCtrl = TextEditingController();
  final _passCtrl = TextEditingController();
  bool _obscure = true;
  bool _loading = false;
  bool _loadingCities = true;
  String? _error;
  List<Map<String, dynamic>> _cities = [];
  Map<String, dynamic>? _selectedCity;

  @override
  void initState() {
    super.initState();
    _loadCities();
  }

  @override
  void dispose() {
    _userCtrl.dispose();
    _passCtrl.dispose();
    super.dispose();
  }

  Future<void> _loadCities() async {
    setState(() {
      _loadingCities = true;
      _error = null;
    });
    try {
      final cities = await BmApi.instance.fetchCities();
      if (!mounted) return;
      setState(() {
        _cities = cities;
        _selectedCity = cities.isNotEmpty ? cities.first : null;
        _loadingCities = false;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _loadingCities = false;
        _error = '$e';
      });
    }
  }

  String _cityName(Map<String, dynamic> c) =>
      '${c['name'] ?? c['Name'] ?? c['cityName'] ?? c['CityName'] ?? 'فرع'}';

  String _cityLink(Map<String, dynamic> c) {
    if (AppEnv.isDemo) return AppEnv.demoApiBaseUrl;
    final link = '${c['link'] ?? c['Link'] ?? c['api'] ?? ''}'.trim();
    return link.isEmpty ? AppEnv.demoApiBaseUrl : link;
  }

  Future<void> _submit() async {
    final city = _selectedCity;
    final user = _userCtrl.text.trim();
    final pass = _passCtrl.text;
    if (city == null) {
      setState(() => _error = 'اختر المحافظة');
      return;
    }
    if (user.isEmpty || pass.isEmpty) {
      setState(() => _error = 'أدخل اسم المستخدم وكلمة المرور');
      return;
    }
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      await BmApi.instance.login(
        apiBase: _cityLink(city),
        cityName: _cityName(city),
        userName: user,
        password: pass,
      );
      if (!mounted) return;
      widget.onLoggedIn();
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _error = '$e';
      });
      return;
    }
    if (mounted) setState(() => _loading = false);
  }

  @override
  Widget build(BuildContext context) {
    return Directionality(
      textDirection: TextDirection.rtl,
      child: Scaffold(
        body: Container(
          decoration: const BoxDecoration(
            gradient: LinearGradient(
              begin: Alignment.topRight,
              end: Alignment.bottomLeft,
              colors: [
                Color(0xFF0A3F3F),
                Color(0xFF0F5C5C),
                Color(0xFF1A7A6D),
              ],
            ),
          ),
          child: SafeArea(
            child: Center(
              child: SingleChildScrollView(
                padding: const EdgeInsets.all(20),
                child: ConstrainedBox(
                  constraints: const BoxConstraints(maxWidth: 420),
                  child: Column(
                    children: [
                      const SizedBox(height: 18),
                      Image.asset(
                        'assets/icons/logo_last.png',
                        height: 96,
                        fit: BoxFit.contain,
                      ),
                      const SizedBox(height: 18),
                      const Text(
                        'مدير الفرع',
                        style: TextStyle(
                          fontFamily: 'Cairo',
                          color: Colors.white,
                          fontSize: 34,
                          fontWeight: FontWeight.bold,
                          height: 1.2,
                        ),
                      ),
                      const SizedBox(height: 8),
                      Text(
                        'قرارات الضعفاء ومتابعة الفرع',
                        style: TextStyle(
                          fontFamily: 'Cairo',
                          color: Colors.white.withValues(alpha: 0.85),
                          fontSize: 14,
                        ),
                      ),
                      const SizedBox(height: 28),
                      Container(
                        padding: const EdgeInsets.all(22),
                        decoration: BoxDecoration(
                          color: Colors.white,
                          borderRadius: BorderRadius.circular(24),
                          boxShadow: [
                            BoxShadow(
                              color: Colors.black.withValues(alpha: 0.18),
                              blurRadius: 28,
                              offset: const Offset(0, 14),
                            ),
                          ],
                        ),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.stretch,
                          children: [
                            if (_loadingCities)
                              const Padding(
                                padding: EdgeInsets.all(24),
                                child: Center(child: CircularProgressIndicator()),
                              )
                            else ...[
                              DropdownButtonFormField<Map<String, dynamic>>(
                                value: _selectedCity,
                                decoration: const InputDecoration(labelText: 'المحافظة / الفرع'),
                                items: _cities
                                    .map(
                                      (c) => DropdownMenuItem(
                                        value: c,
                                        child: Text(
                                          _cityName(c),
                                          style: const TextStyle(fontFamily: 'Cairo'),
                                        ),
                                      ),
                                    )
                                    .toList(),
                                onChanged: _loading
                                    ? null
                                    : (v) => setState(() => _selectedCity = v),
                              ),
                              const SizedBox(height: 14),
                              TextField(
                                controller: _userCtrl,
                                enabled: !_loading,
                                textInputAction: TextInputAction.next,
                                decoration: const InputDecoration(
                                  labelText: 'اسم المستخدم',
                                  prefixIcon: Icon(Icons.person_outline),
                                ),
                                style: const TextStyle(fontFamily: 'Cairo'),
                              ),
                              const SizedBox(height: 14),
                              TextField(
                                controller: _passCtrl,
                                enabled: !_loading,
                                obscureText: _obscure,
                                onSubmitted: (_) => _submit(),
                                decoration: InputDecoration(
                                  labelText: 'كلمة المرور',
                                  prefixIcon: const Icon(Icons.lock_outline),
                                  suffixIcon: IconButton(
                                    onPressed: () =>
                                        setState(() => _obscure = !_obscure),
                                    icon: Icon(
                                      _obscure
                                          ? Icons.visibility_outlined
                                          : Icons.visibility_off_outlined,
                                    ),
                                  ),
                                ),
                                style: const TextStyle(fontFamily: 'Cairo'),
                              ),
                              if (_error != null) ...[
                                const SizedBox(height: 12),
                                Text(
                                  _error!,
                                  style: const TextStyle(
                                    fontFamily: 'Cairo',
                                    color: BmTheme.danger,
                                    height: 1.4,
                                  ),
                                ),
                              ],
                              const SizedBox(height: 18),
                              FilledButton(
                                onPressed: _loading ? null : _submit,
                                child: _loading
                                    ? const SizedBox(
                                        width: 22,
                                        height: 22,
                                        child: CircularProgressIndicator(
                                          strokeWidth: 2,
                                          color: Colors.white,
                                        ),
                                      )
                                    : const Text('دخول آمن'),
                              ),
                            ],
                          ],
                        ),
                      ),
                      const SizedBox(height: 18),
                      Text(
                        AppEnv.isDemo
                            ? 'بيئة تجريبي النجف'
                            : 'الحساب يُنشأ من المستخدمين بواسطة المحاسب الرئيسي',
                        textAlign: TextAlign.center,
                        style: TextStyle(
                          fontFamily: 'Cairo',
                          color: Colors.white.withValues(alpha: 0.75),
                          fontSize: 12,
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
