import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:webview_flutter/webview_flutter.dart';

void main() {
  WidgetsFlutterBinding.ensureInitialized();
  SystemChrome.setPreferredOrientations([DeviceOrientation.portraitUp]);
  SystemChrome.setSystemUIOverlayStyle(const SystemUiOverlayStyle(
    statusBarColor: Color(0xFF1e5799),
    statusBarIconBrightness: Brightness.light,
  ));
  runApp(const ShortCompanyApp());
}

class ShortCompanyApp extends StatefulWidget {
  const ShortCompanyApp({super.key});

  @override
  State<ShortCompanyApp> createState() => _ShortCompanyAppState();
}

class _ShortCompanyAppState extends State<ShortCompanyApp> {
  late final WebViewController _controller;
  bool _isLoading = true;
  String? _loadError;

  @override
  void initState() {
    super.initState();
    _controller = WebViewController()
      ..setJavaScriptMode(JavaScriptMode.unrestricted)
      ..setBackgroundColor(const Color(0xFF1e5799))
      ..setNavigationDelegate(NavigationDelegate(
        onPageFinished: (_) async {
          if (mounted) {
            setState(() {
              _isLoading = false;
              _loadError = null;
            });
          }
          // F26: allow pinch-zoom / user scaling (removed maximum-scale=1 / user-scalable=no).
          await _controller.runJavaScript('''
            var meta = document.querySelector('meta[name="viewport"]');
            if (!meta) {
              meta = document.createElement('meta');
              meta.name = 'viewport';
              document.head.appendChild(meta);
            }
            meta.content = 'width=device-width, initial-scale=1.0';
          ''');
        },
        onWebResourceError: (error) {
          if (!mounted) return;
          setState(() {
            _isLoading = false;
            _loadError = error.description.isNotEmpty
                ? error.description
                : 'تعذر تحميل الصفحة';
          });
        },
      ));
    _controller.loadRequest(
      Uri.parse('http://shortnewadmin.alsaaeidy.com/#/login'),
    );
  }

  Future<void> _retry() async {
    setState(() {
      _isLoading = true;
      _loadError = null;
    });
    await _controller.loadRequest(
        Uri.parse('http://shortnewadmin.alsaaeidy.com/#/login'));
  }

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'تطبيق المدير',
      debugShowCheckedModeBanner: false,
      theme: ThemeData(primarySwatch: Colors.blue),
      home: Scaffold(
        body: SafeArea(
          child: Stack(
            children: [
              WebViewWidget(controller: _controller),
              if (_loadError != null)
                Container(
                  color: const Color(0xFF1e5799),
                  width: double.infinity,
                  padding: const EdgeInsets.all(24),
                  child: Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      const Icon(Icons.wifi_off, color: Colors.white, size: 48),
                      const SizedBox(height: 16),
                      Text(
                        _loadError!,
                        textAlign: TextAlign.center,
                        textDirection: TextDirection.rtl,
                        style: const TextStyle(color: Colors.white, fontSize: 16),
                      ),
                      const SizedBox(height: 24),
                      ElevatedButton(
                        onPressed: _retry,
                        child: const Text('إعادة المحاولة'),
                      ),
                    ],
                  ),
                )
              else if (_isLoading)
                Container(
                  color: const Color(0xFF1e5799),
                  child: Center(
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Container(
                          width: 140,
                          height: 140,
                          decoration: BoxDecoration(
                            color: Colors.white,
                            shape: BoxShape.circle,
                            boxShadow: [
                              BoxShadow(
                                color: Colors.black.withAlpha(51),
                                blurRadius: 20,
                                offset: const Offset(0, 10),
                              ),
                            ],
                          ),
                          child: ClipOval(
                            child: Padding(
                              padding: const EdgeInsets.all(14),
                              child: Image.asset(
                                'assets/logo.png',
                                fit: BoxFit.contain,
                                errorBuilder: (_, __, ___) => const Icon(
                                    Icons.shield_outlined,
                                    size: 70,
                                    color: Color(0xFF1e5799)),
                              ),
                            ),
                          ),
                        ),
                        const SizedBox(height: 32),
                        const Text('قلعة الضمان',
                            style: TextStyle(
                                color: Colors.white,
                                fontSize: 28,
                                fontWeight: FontWeight.bold)),
                        const SizedBox(height: 8),
                        const Text('لأنظمة الإدارة',
                            style:
                                TextStyle(color: Colors.white70, fontSize: 16)),
                        const SizedBox(height: 40),
                        const SizedBox(
                          width: 28,
                          height: 28,
                          child: CircularProgressIndicator(
                              strokeWidth: 2.5, color: Colors.white),
                        ),
                      ],
                    ),
                  ),
                ),
            ],
          ),
        ),
      ),
    );
  }
}
