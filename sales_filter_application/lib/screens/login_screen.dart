import 'package:flutter/material.dart';
import 'package:sales_filter_application/config/app_env.dart';
import 'package:sales_filter_application/screens/home_screen.dart';
import 'package:sales_filter_application/services/api_client.dart';
import 'package:sales_filter_application/services/session.dart';
import 'package:sales_filter_application/theme/app_theme.dart';

class LoginScreen extends StatefulWidget {
  const LoginScreen({super.key});

  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  final _user = TextEditingController();
  final _pass = TextEditingController();
  bool _loading = false;

  @override
  void dispose() {
    _user.dispose();
    _pass.dispose();
    super.dispose();
  }

  Future<void> _login() async {
    if (_user.text.trim().isEmpty || _pass.text.isEmpty) {
      _toast('اسم المستخدم وكلمة المرور مطلوبان');
      return;
    }
    setState(() => _loading = true);
    try {
      final data = await ApiClient.loginSalesFilter(
        userName: _user.text.trim(),
        password: _pass.text,
      );
      final type = '${data['userType'] ?? data['UserType'] ?? ''}';
      if (type != 'موظف فلترة المبيعات') {
        throw ApiException('هذا الحساب ليس موظف فلترة المبيعات');
      }
      final token = '${data['token'] ?? data['Token'] ?? ''}';
      if (token.isEmpty) throw ApiException('تعذر الحصول على رمز الدخول');
      await Session.save(
        tokenValue: token,
        base: AppEnv.apiBase(),
        name: '${data['userName'] ?? data['UserName'] ?? _user.text.trim()}',
        type: type,
        homeCityValue: '${data['homeCityValue'] ?? data['cityValue'] ?? ''}',
      );
      if (!mounted) return;
      Navigator.of(context).pushReplacement(MaterialPageRoute(builder: (_) => const HomeScreen()));
    } catch (e) {
      _toast(e.toString());
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  void _toast(String m) {
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(m, style: const TextStyle(fontFamily: 'Cairo'))));
  }

  @override
  Widget build(BuildContext context) {
    return Directionality(
      textDirection: TextDirection.rtl,
      child: Scaffold(
        body: SafeArea(
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                const SizedBox(height: 24),
                const Text(
                  'فلترة المبيعات',
                  textAlign: TextAlign.center,
                  style: TextStyle(fontFamily: 'Cairo', fontSize: 26, fontWeight: FontWeight.bold, color: AppTheme.primary),
                ),
                const SizedBox(height: 8),
                const Text(
                  'سجّل الدخول بحساب موظف فلترة المبيعات',
                  textAlign: TextAlign.center,
                  style: TextStyle(fontFamily: 'Cairo', color: Colors.black54),
                ),
                const SizedBox(height: 32),
                TextField(controller: _user, decoration: const InputDecoration(labelText: 'اسم المستخدم', border: OutlineInputBorder())),
                const SizedBox(height: 12),
                TextField(controller: _pass, obscureText: true, decoration: const InputDecoration(labelText: 'كلمة المرور', border: OutlineInputBorder())),
                const SizedBox(height: 20),
                ElevatedButton(
                  onPressed: _loading ? null : _login,
                  style: ElevatedButton.styleFrom(
                    backgroundColor: AppTheme.primary,
                    foregroundColor: Colors.white,
                    padding: const EdgeInsets.symmetric(vertical: 14),
                  ),
                  child: Text(_loading ? '...' : 'دخول', style: const TextStyle(fontFamily: 'Cairo', fontWeight: FontWeight.bold)),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
