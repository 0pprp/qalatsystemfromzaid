import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/config/app_config.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/widgets/common.dart';
import '../data/auth_controller.dart';
import '../data/auth_repository.dart';

/// Agents sign in with the username and password the administration gave them. There is no sign-up.
class LoginScreen extends ConsumerStatefulWidget {
  const LoginScreen({super.key, this.from});

  /// Route to open after signing in.
  final String? from;

  @override
  ConsumerState<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends ConsumerState<LoginScreen> {
  final _username = TextEditingController(text: AppConfig.useFakeApi ? 'agent1' : '');
  final _password = TextEditingController(text: AppConfig.useFakeApi ? '123456' : '');
  final _passwordFocus = FocusNode();
  bool _obscure = true;
  bool _loading = false;
  String? _error;

  @override
  void dispose() {
    _username.dispose();
    _password.dispose();
    _passwordFocus.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    FocusScope.of(context).unfocus();
    if (_username.text.trim().isEmpty || _password.text.isEmpty) {
      setState(() => _error = 'أدخل اسم المستخدم وكلمة المرور');
      return;
    }
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final (tokens, me) = await ref.read(authRepositoryProvider).login(username: _username.text, password: _password.text);
      await ref.read(authControllerProvider.notifier).completeLogin(tokens, me);
      if (!mounted) return;
      final from = widget.from;
      context.go(from != null && from.startsWith('/') && !from.startsWith('/login') ? from : '/new');
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _error = e.message;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Scaffold(
      backgroundColor: Colors.white,
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.fromLTRB(24, 24, 24, 32),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 440),
              child: AutofillGroup(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Center(child: Image.asset('assets/images/logo.png', cacheWidth: 500, height: 130)),
                    const SizedBox(height: 18),
                    Text('تطبيق المسوّقين', textAlign: TextAlign.center, style: theme.textTheme.headlineSmall),
                    const SizedBox(height: 6),
                    const Text(
                      'سجّل الدخول باسم المستخدم وكلمة المرور التي استلمتها من الإدارة.',
                      textAlign: TextAlign.center,
                      style: TextStyle(color: AppColors.textMuted, height: 1.6),
                    ),
                    const SizedBox(height: 28),
                    TextField(
                      controller: _username,
                      // Arabic or English usernames.
                      keyboardType: TextInputType.text,
                      autocorrect: false,
                      enableSuggestions: false,
                      textInputAction: TextInputAction.next,
                      autofillHints: const [AutofillHints.username],
                      decoration: const InputDecoration(labelText: 'اسم المستخدم', prefixIcon: Icon(Icons.person_outline_rounded)),
                      onSubmitted: (_) => _passwordFocus.requestFocus(),
                      onChanged: (_) => setState(() => _error = null),
                    ),
                    const SizedBox(height: 14),
                    TextField(
                      controller: _password,
                      focusNode: _passwordFocus,
                      obscureText: _obscure,
                      textDirection: TextDirection.ltr,
                      textInputAction: TextInputAction.done,
                      autofillHints: const [AutofillHints.password],
                      decoration: InputDecoration(
                        labelText: 'كلمة المرور',
                        prefixIcon: const Icon(Icons.lock_outline_rounded),
                        suffixIcon: IconButton(
                          tooltip: _obscure ? 'إظهار' : 'إخفاء',
                          icon: Icon(_obscure ? Icons.visibility_outlined : Icons.visibility_off_outlined),
                          onPressed: () => setState(() => _obscure = !_obscure),
                        ),
                      ),
                      onSubmitted: (_) => _submit(),
                      onChanged: (_) => setState(() => _error = null),
                    ),
                    if (_error != null)
                      Padding(
                        padding: const EdgeInsets.only(top: 12),
                        child: NoticeBanner(message: _error!, icon: Icons.error_outline_rounded, color: AppColors.danger),
                      ),
                    const SizedBox(height: 22),
                    LoadingButton(label: 'تسجيل الدخول', icon: Icons.login_rounded, loading: _loading, onPressed: _submit),
                    const SizedBox(height: 18),
                    const Text(
                      'نسيت كلمة المرور؟ راجع الإدارة لتعيين كلمة مرور جديدة.',
                      textAlign: TextAlign.center,
                      style: TextStyle(color: AppColors.textMuted, fontSize: 12.5),
                    ),
                    if (AppConfig.useFakeApi)
                      const Padding(
                        padding: EdgeInsets.only(top: 18),
                        child: NoticeBanner(
                          message: 'نسخة تجريبية: اسم المستخدم agent1 وكلمة المرور 123456',
                          icon: Icons.science_outlined,
                          color: AppColors.info,
                        ),
                      ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
