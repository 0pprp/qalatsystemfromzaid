import 'package:flutter/material.dart';
import 'package:sales_employee_application/config/app_env.dart';
import 'package:sales_employee_application/services/api_client.dart';
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

  @override
  void dispose() {
    _userCtrl.dispose();
    _passCtrl.dispose();
    super.dispose();
  }

  Future<void> _login() async {
    if (_userCtrl.text.trim().isEmpty || _passCtrl.text.isEmpty) {
      _alert('لا يمكن ترك أي شيء فارغ');
      return;
    }
    setState(() => _loading = true);
    try {
      if (AppEnv.useMockSalesRepository) {
        await Session.saveLogin({
          'token': 'mock-token',
          'userName': _userCtrl.text.trim(),
          'cityName': AppEnv.loginCityLabel(),
          'userId': '1',
          'userType': 'موظف مبيعات',
        });
        if (!mounted) return;
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
      await Session.saveLogin({
        ...payload,
        'token': token,
        'userName': _userCtrl.text.trim(),
        'cityName': AppEnv.loginCityLabel(),
      });
      if (!mounted) return;
      Navigator.pushReplacementNamed(context, '/shift');
    } on ApiException catch (e) {
      _alert(e.message);
    } catch (_) {
      _alert('لا يوجد اتصال بالإنترنت أو السيرفر غير متاح');
    } finally {
      if (mounted) setState(() => _loading = false);
    }
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

  @override
  Widget build(BuildContext context) {
    final height = MediaQuery.of(context).size.height;
    return Directionality(
      textDirection: TextDirection.rtl,
      child: Scaffold(
        body: SingleChildScrollView(
          child: SizedBox(
            height: height,
            child: Stack(
              children: [
                Positioned(
                  top: -50,
                  right: -50,
                  child: Container(
                    width: 200,
                    height: 200,
                    decoration: BoxDecoration(
                      color: AppTheme.primaryColor.withValues(alpha: 0.05),
                      shape: BoxShape.circle,
                    ),
                  ),
                ),
                Center(
                  child: Padding(
                    padding: const EdgeInsets.symmetric(horizontal: 25),
                    child: Column(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        Image.asset('assets/icons/LogoCompany.png',
                            height: height * 0.14),
                        const SizedBox(height: 16),
                        const Text(
                          'تطبيق موظف المبيعات',
                          style: TextStyle(
                            fontSize: 24,
                            fontWeight: FontWeight.bold,
                            color: AppTheme.primaryColor,
                          ),
                        ),
                        const SizedBox(height: 8),
                        Text(
                          'فرع النجف التجريبي — المحافظة ثابتة من الحساب',
                          style: TextStyle(color: Colors.grey[600]),
                        ),
                        const Padding(
                          padding: EdgeInsets.only(top: 6),
                          child: Text(
                            'النجف - DEMO',
                            style: TextStyle(
                                color: AppTheme.secondaryColor, fontSize: 12),
                          ),
                        ),
                        SizedBox(height: height * 0.04),
                        TextField(
                          controller: _userCtrl,
                          textInputAction: TextInputAction.next,
                          decoration: const InputDecoration(
                            hintText: 'اسم المستخدم',
                            prefixIcon: Icon(Icons.person_outline,
                                color: AppTheme.primaryColor),
                          ),
                        ),
                        const SizedBox(height: 16),
                        TextField(
                          controller: _passCtrl,
                          obscureText: true,
                          textInputAction: TextInputAction.done,
                          onSubmitted: (_) => _login(),
                          decoration: const InputDecoration(
                            hintText: 'كلمة المرور',
                            prefixIcon: Icon(Icons.lock_outline,
                                color: AppTheme.primaryColor),
                          ),
                        ),
                        const SizedBox(height: 36),
                        SizedBox(
                          width: double.infinity,
                          height: 55,
                          child: ElevatedButton(
                            onPressed: _loading ? null : _login,
                            child: _loading
                                ? const CircularProgressIndicator(color: Colors.white)
                                : const Text('تسجيل الدخول'),
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
