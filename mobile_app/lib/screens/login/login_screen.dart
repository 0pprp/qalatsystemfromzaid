import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:dio/dio.dart';
import '../../config/theme.dart';
import '../../providers/auth_provider.dart';
import '../../services/api_service.dart';
import '../../animations/widget_animations.dart';

class LoginScreen extends StatefulWidget {
  const LoginScreen({super.key});

  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> with SingleTickerProviderStateMixin {
  final _formKey = GlobalKey<FormState>();
  final _userNameCtrl = TextEditingController();
  final _passwordCtrl = TextEditingController();
  bool _obscurePassword = true;
  bool _isLoadingCities = true;
  String? _citiesError;
  List<Map<String, String>> _cities = [];
  Map<String, String>? _selectedCity;
  bool _shakeForm = false;

  late AnimationController _logoAnim;
  late Animation<double> _logoScale;
  late Animation<double> _logoOpacity;

  @override
  void initState() {
    super.initState();
    _logoAnim = AnimationController(vsync: this, duration: const Duration(milliseconds: 800));
    _logoScale = Tween<double>(begin: 0.8, end: 1.0).animate(CurvedAnimation(parent: _logoAnim, curve: Curves.elasticOut));
    _logoOpacity = Tween<double>(begin: 0.0, end: 1.0).animate(CurvedAnimation(parent: _logoAnim, curve: Curves.easeOut));
    _fetchCities();
    _logoAnim.forward();
  }

  @override
  void dispose() {
    _userNameCtrl.dispose();
    _passwordCtrl.dispose();
    _logoAnim.dispose();
    super.dispose();
  }

  Future<void> _fetchCities() async {
    try {
      final dio = Dio(BaseOptions(
        connectTimeout: const Duration(seconds: 10),
        receiveTimeout: const Duration(seconds: 10),
      ));
      final response = await dio.get('http://defaultdata.alsaaeidy.com/GetEmployee');
      if (response.data is List) {
        setState(() {
          _cities = (response.data as List).map<Map<String, String>>((item) {
            return {
              'name': item['name']?.toString() ?? '',
              'link': item['link']?.toString() ?? '',
              'database': item['database']?.toString() ?? '',
            };
          }).toList();
          _isLoadingCities = false;
          _citiesError = null;
        });
      } else {
        setState(() {
          _citiesError = 'تنسيق بيانات غير متوقع من الخادم';
          _isLoadingCities = false;
        });
      }
    } catch (e) {
      setState(() {
        _citiesError = 'فشل تحميل المحافظات - تحقق من الاتصال بالإنترنت';
        _isLoadingCities = false;
      });
    }
  }

  Future<void> _login() async {
    if (!_formKey.currentState!.validate()) {
      _triggerShake();
      return;
    }
    if (_selectedCity == null) {
      _triggerShake();
      _showSnack('يجب اختيار المحافظة');
      return;
    }

    final auth = context.read<AuthProvider>();
    final success = await auth.login(
      _userNameCtrl.text.trim(),
      _passwordCtrl.text.trim(),
      _selectedCity!['link']!,
      _selectedCity!['name']!,
    );

    if (success && mounted) {
      Navigator.pushReplacementNamed(context, '/dashboard');
    } else if (mounted) {
      _triggerShake();
      _showSnack(auth.errorMessage ?? 'فشل تسجيل الدخول');
    }
  }

  void _triggerShake() {
    setState(() => _shakeForm = true);
    Future.delayed(const Duration(milliseconds: 600), () {
      if (mounted) setState(() => _shakeForm = false);
    });
  }

  void _showSnack(String msg) {
    final scheme = Theme.of(context).colorScheme;
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(msg),
        backgroundColor: scheme.error,
        behavior: SnackBarBehavior.floating,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final auth = context.watch<AuthProvider>();
    final scheme = Theme.of(context).colorScheme;
    final isDark = Theme.of(context).brightness == Brightness.dark;

    return Scaffold(
      body: SafeArea(
        child: SingleChildScrollView(
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: Form(
              key: _formKey,
              child: ShakeWidget(
                shake: _shakeForm,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    const SizedBox(height: 40),

                    // ── Animated Logo ──
                    AnimatedBuilder(
                      animation: _logoAnim,
                      builder: (_, __) => Opacity(
                        opacity: _logoOpacity.value,
                        child: Transform.scale(
                          scale: _logoScale.value,
                          child: Center(
                            child: Container(
                              width: 110,
                              height: 110,
                              decoration: BoxDecoration(
                                color: isDark ? scheme.surface : Colors.white,
                                borderRadius: BorderRadius.circular(25),
                                boxShadow: [
                                  BoxShadow(
                                    color: scheme.primary.withAlpha(51),
                                    blurRadius: 20,
                                    offset: const Offset(0, 10),
                                  ),
                                ],
                              ),
                              child: ClipRRect(
                                borderRadius: BorderRadius.circular(25),
                                child: Image.asset('assets/icons/logo.png', fit: BoxFit.contain),
                              ),
                            ),
                          ),
                        ),
                      ),
                    ),

                    const SizedBox(height: 32),

                    // ── Title ──
                    Text('تطبيق الإدارة',
                      textAlign: TextAlign.center,
                      style: TextStyle(fontSize: 28, fontWeight: FontWeight.bold, color: scheme.onSurface),
                    ),
                    const SizedBox(height: 8),
                    Text('نظام إدارة المبيعات والمخازن',
                      textAlign: TextAlign.center,
                      style: TextStyle(fontSize: 14, color: scheme.onSurface.withAlpha(128)),
                    ),
                    const SizedBox(height: 40),

                    // ── Governorate Dropdown ──
                    Text('المحافظة',
                      style: TextStyle(fontSize: 14, fontWeight: FontWeight.w600, color: scheme.onSurface.withAlpha(200)),
                    ),
                    const SizedBox(height: 8),
                    Container(
                      decoration: BoxDecoration(
                        borderRadius: BorderRadius.circular(12),
                        color: scheme.surface,
                        border: Border.all(color: scheme.outline),
                      ),
                      child: _isLoadingCities
                          ? const Padding(
                              padding: EdgeInsets.all(16),
                              child: Center(child: CircularProgressIndicator(strokeWidth: 2)),
                            )
                          : _citiesError != null
                              ? Padding(
                                  padding: const EdgeInsets.all(16),
                                  child: Row(
                                    children: [
                                      Icon(Icons.error_outline, color: scheme.error),
                                      const SizedBox(width: 8),
                                      Expanded(child: Text(_citiesError!, style: TextStyle(color: scheme.error, fontSize: 13))),
                                      TextButton(onPressed: _fetchCities, child: const Text('إعادة')),
                                    ],
                                  ),
                                )
                              : DropdownButtonFormField<Map<String, String>>(
                                  value: _selectedCity,
                                  decoration: const InputDecoration(
                                    hintText: 'اختر المحافظة',
                                    border: InputBorder.none,
                                    contentPadding: EdgeInsets.symmetric(horizontal: 16),
                                  ),
                                  items: _cities.map((city) {
                                    return DropdownMenuItem(value: city, child: Text(city['name']!));
                                  }).toList(),
                                  onChanged: (v) => setState(() => _selectedCity = v),
                                ),
                    ),
                    const SizedBox(height: 16),

                    // ── Username ──
                    Text('اسم المستخدم',
                      style: TextStyle(fontSize: 14, fontWeight: FontWeight.w600, color: scheme.onSurface.withAlpha(200)),
                    ),
                    const SizedBox(height: 8),
                    TextFormField(
                      controller: _userNameCtrl,
                      textDirection: TextDirection.ltr,
                      decoration: const InputDecoration(hintText: 'اسم المستخدم', prefixIcon: Icon(Icons.person_outline)),
                      validator: (v) => v == null || v.isEmpty ? 'مطلوب' : null,
                    ),
                    const SizedBox(height: 16),

                    // ── Password ──
                    Text('كلمة المرور',
                      style: TextStyle(fontSize: 14, fontWeight: FontWeight.w600, color: scheme.onSurface.withAlpha(200)),
                    ),
                    const SizedBox(height: 8),
                    TextFormField(
                      controller: _passwordCtrl,
                      textDirection: TextDirection.ltr,
                      obscureText: _obscurePassword,
                      decoration: InputDecoration(
                        hintText: 'كلمة المرور',
                        prefixIcon: const Icon(Icons.lock_outline),
                        suffixIcon: IconButton(
                          icon: Icon(_obscurePassword ? Icons.visibility_off : Icons.visibility),
                          onPressed: () => setState(() => _obscurePassword = !_obscurePassword),
                        ),
                      ),
                      validator: (v) => v == null || v.isEmpty ? 'مطلوب' : null,
                    ),
                    const SizedBox(height: 32),

                    // ── Login Button ──
                    SizedBox(
                      height: 52,
                      child: ElevatedButton(
                        onPressed: auth.isLoading ? null : _login,
                        style: ElevatedButton.styleFrom(
                          backgroundColor: scheme.primary,
                          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
                        ),
                        child: auth.isLoading
                            ? const BouncingDots(dotSize: 6, color: Colors.white)
                            : const Text('تسجيل الدخول', style: TextStyle(fontSize: 16, fontWeight: FontWeight.w600)),
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
