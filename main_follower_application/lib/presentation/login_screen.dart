import 'package:flutter/material.dart';
import 'package:main_follower_application/core/config/app_env.dart';
import 'package:main_follower_application/core/update/app_update_gate.dart';
import 'package:main_follower_application/core/update/app_version.dart';
import 'package:main_follower_application/data/auth_repository.dart';
import 'package:main_follower_application/presentation/shift_gate_screen.dart';

class LoginScreen extends StatefulWidget {
  const LoginScreen({super.key});

  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  final _user = TextEditingController();
  final _pass = TextEditingController();
  bool _busy = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) async {
      await AppVersion.ensureLoaded();
      if (mounted) {
        setState(() {});
        AppUpdateGate.checkAndPrompt(context);
      }
    });
  }

  Future<void> _submit() async {
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await AuthRepository.login(userName: _user.text, password: _pass.text);
      if (!mounted) return;
      Navigator.of(context).pushReplacement(
        MaterialPageRoute(builder: (_) => const ShiftGateScreen()),
      );
    } catch (e) {
      setState(() => _error = e.toString());
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  void dispose() {
    _user.dispose();
    _pass.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const SizedBox(height: 24),
              Image.asset(
                'assets/icons/logo_last.png',
                height: 88,
                errorBuilder: (_, __, ___) => const Icon(
                  Icons.account_balance,
                  size: 72,
                  color: Color(0xFF0F3D3E),
                ),
              ),
              const SizedBox(height: 16),
              Text(
                'المتابع الرئيسي',
                textAlign: TextAlign.center,
                style: Theme.of(context).textTheme.headlineMedium?.copyWith(
                      fontWeight: FontWeight.bold,
                      color: const Color(0xFF0F3D3E),
                    ),
              ),
              const SizedBox(height: 8),
              Text(
                'مهام اللجان متعددة المحافظات',
                textAlign: TextAlign.center,
                style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                      color: Colors.black54,
                    ),
              ),
              const SizedBox(height: 40),
              TextField(
                controller: _user,
                decoration: const InputDecoration(
                  labelText: 'اسم المستخدم',
                  border: OutlineInputBorder(),
                ),
                textInputAction: TextInputAction.next,
              ),
              const SizedBox(height: 12),
              TextField(
                controller: _pass,
                obscureText: true,
                decoration: const InputDecoration(
                  labelText: 'كلمة المرور',
                  border: OutlineInputBorder(),
                ),
                onSubmitted: (_) => _submit(),
              ),
              if (_error != null) ...[
                const SizedBox(height: 12),
                Text(_error!, style: const TextStyle(color: Colors.red)),
              ],
              const SizedBox(height: 24),
              FilledButton(
                onPressed: _busy ? null : _submit,
                style: FilledButton.styleFrom(
                  backgroundColor: const Color(0xFF0F3D3E),
                  padding: const EdgeInsets.symmetric(vertical: 14),
                ),
                child: _busy
                    ? const SizedBox(
                        height: 20,
                        width: 20,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : const Text('دخول'),
              ),
              const SizedBox(height: 16),
              Text(
                'الإصدار ${AppVersion.display} • بيئة ${AppEnv.isDemo ? 'تجريبي' : 'إنتاج'}',
                textAlign: TextAlign.center,
                style: const TextStyle(color: Colors.black45, fontSize: 12),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
