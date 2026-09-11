import 'package:follower_application/utils/AppTheme.dart';
import 'package:follower_application/AsyncIdChecker.dart';
import 'package:follower_application/LocalLabApi.dart';
import 'package:follower_application/config/app_env.dart';
import 'package:follower_application/tracking/shift_gate_coordinator.dart';
import 'package:follower_application/ui/app_safe_scaffold.dart';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'dart:convert';
import 'package:shared_preferences/shared_preferences.dart';

/// Builds POST Followers/Login URI — password never goes in the query string.
Uri followerLoginUri(String apiBase) {
  final base = apiBase.endsWith('/') ? apiBase : '$apiBase/';
  return Uri.parse('${base}Followers/Login');
}

class Login extends StatefulWidget {
  const Login({super.key});

  @override
  State<Login> createState() => _LoginState();
}

class _LoginState extends State<Login> {
  String? _selectedGovernorate;
  final TextEditingController _userNameController = TextEditingController();
  final TextEditingController _passwordController = TextEditingController();
  bool _isLoading = false;
  bool _isLoadingCities = true;
  bool _obscurePassword = true;
  String? _cityError;

  List<Map<String, String>> cityData = [];

  static const String _cityApiUrl = 'http://defaultdata.alsaaeidy.com/GetHaider';
  static const String _cityCacheKey = 'cached_city_data';

  @override
  void initState() {
    super.initState();
    _fetchCityData();
  }

  @override
  void dispose() {
    _userNameController.dispose();
    _passwordController.dispose();
    super.dispose();
  }

  Future<void> _fetchCityData() async {
    if (AppEnv.isDemo || AppEnv.isLocal) {
      if (!mounted) return;
      setState(() {
        cityData = [
          {
            'name': AppEnv.isLocal ? 'الناصرية' : 'النجف - DEMO',
            'link': AppEnv.apiBase(),
            'number': '',
          },
        ];
        _selectedGovernorate = cityData.first['name'];
        _isLoadingCities = false;
        _cityError = null;
      });
      return;
    }

    try {
      final response = await http
          .get(Uri.parse(_cityApiUrl))
          .timeout(const Duration(seconds: 10));

      if (!mounted) return;

      if (response.statusCode == 200) {
        final List<dynamic> data = json.decode(response.body);
        final prefs = await SharedPreferences.getInstance();
        await prefs.setString(_cityCacheKey, response.body);

        setState(() {
          cityData = data.map((item) {
            return {
              'name': item['name']?.toString() ?? '',
              'link': item['link']?.toString() ?? '',
              'number': item['number']?.toString() ?? '',
            };
          }).toList();
          _isLoadingCities = false;
          _cityError = null;
        });
      } else {
        await _loadCachedCities();
      }
    } catch (e) {
      if (mounted) {
        await _loadCachedCities();
      }
    }
  }

  Future<void> _loadCachedCities() async {
    try {
      final prefs = await SharedPreferences.getInstance();
      final String? cachedJson = prefs.getString(_cityCacheKey);

      if (cachedJson != null && cachedJson.isNotEmpty) {
        final List<dynamic> data = json.decode(cachedJson);
        if (mounted) {
          setState(() {
            cityData = data.map((item) {
              return {
                'name': item['name']?.toString() ?? '',
                'link': item['link']?.toString() ?? '',
                'number': item['number']?.toString() ?? '',
              };
            }).toList();
            _isLoadingCities = false;
            _cityError = null;
          });
        }
      } else if (mounted) {
        setState(() {
          _isLoadingCities = false;
          _cityError = 'تعذر تحميل الفروع. تأكد من اتصالك بالإنترنت.';
        });
      }
    } catch (e) {
      if (mounted) {
        setState(() {
          _isLoadingCities = false;
          _cityError = 'تعذر تحميل الفروع. تأكد من اتصالك بالإنترنت.';
        });
      }
    }
  }

  Future<void> _login() async {
    final userName = _userNameController.text.trim();
    final password = _passwordController.text;
    if (_selectedGovernorate == null ||
        _selectedGovernorate!.isEmpty ||
        userName.isEmpty ||
        password.isEmpty) {
      _showErrorDialog('لا يمكن ترك أي شيء فارغ');
      return;
    }

    setState(() {
      _isLoading = true;
    });

    try {
      String productionUrl = '';
      for (var city in cityData) {
        if (city['name'] == _selectedGovernorate) {
          productionUrl = city['link']!;
          break;
        }
      }

      if (productionUrl.isEmpty &&
          !LocalLabApi.enabled &&
          !AppEnv.isDemo &&
          !AppEnv.isLocal) {
        _showErrorDialog('لم يتم العثور على الرابط للمحافظة المختارة');
        return;
      }

      final bases = AppEnv.isDemo
          ? [AppEnv.demoApiBaseUrl]
          : AppEnv.isLocal
              ? [AppEnv.localApiBaseUrl]
              : (LocalLabApi.enabled ? LocalLabApi.bases() : [productionUrl]);

      http.Response? response;
      String apiUrl = productionUrl;

      for (final base in bases) {
        try {
          final uri = followerLoginUri(base);
          final candidate = await http
              .post(
                uri,
                headers: const {'Content-Type': 'application/json'},
                body: json.encode({
                  'userName': userName,
                  'password': password,
                }),
              )
              .timeout(const Duration(seconds: 8));
          apiUrl = base;
          response = candidate;
          if (candidate.statusCode == 200 ||
              candidate.statusCode == 401 ||
              candidate.statusCode == 403) {
            break;
          }
        } catch (_) {}
      }

      if (!mounted) return;

      if (AppEnv.isDemo || AppEnv.isLocal) {
        apiUrl = AppEnv.apiBase();
      }

      if (response == null) {
        _showErrorDialog(
          AppEnv.isDemo
              ? 'لا يوجد اتصال بالإنترنت أو السيرفر غير متاح'
              : 'تعذر الاتصال بالسيرفر. تأكد من الشبكة أو المنفذ المحلي.',
        );
        return;
      }

      String serverMessage = '';
      try {
        final body = json.decode(response.body);
        if (body is Map && body['message'] != null) {
          serverMessage = body['message'].toString();
        }
      } catch (_) {}

      if (response.statusCode == 401) {
        _showErrorDialog(serverMessage.isNotEmpty
            ? serverMessage
            : 'اسم المستخدم أو كلمة المرور غير صحيحة');
        return;
      }

      if (response.statusCode == 403) {
        _showErrorDialog(serverMessage.isNotEmpty
            ? serverMessage
            : 'هذا الحساب غير مخول لتطبيق المتابع');
        return;
      }

      if (response.statusCode == 200) {
        final data = json.decode(response.body);
        final userId = data['userId'] ?? data['delegateId'] ?? data['DelegateId'];
        final asyncId = data['asyncId'] ?? data['AsyncId'] ?? data['asyncID'];
        final displayName = data['userName'] ??
            data['delegateName'] ??
            data['DelegateName'] ??
            userName;

        if (userId == null ||
            int.tryParse(userId.toString()) == null ||
            int.parse(userId.toString()) <= 0 ||
            asyncId == null ||
            asyncId.toString().trim().isEmpty) {
          _showErrorDialog('فشل تسجيل الدخول — استجابة غير مكتملة من السيرفر');
          return;
        }

        final link = AppEnv.apiBase(fallback: apiUrl);

        // Prefetch lists; empty list is allowed (not a login failure).
        try {
          final listsUri = Uri.parse('${link}Followers/Lists').replace(
            queryParameters: {'asyncId': asyncId.toString()},
          );
          final listsRes =
              await http.get(listsUri).timeout(const Duration(seconds: 15));
          if (!mounted) return;
          if (listsRes.statusCode == 401 || listsRes.statusCode == 403) {
            _showErrorDialog(serverMessage.isNotEmpty
                ? serverMessage
                : 'هذا الحساب غير مخول لتطبيق المتابع');
            return;
          }
        } catch (_) {
          // Lists prefetch failure should not block login; HomePage reloads.
        }

        await AsyncIdChecker.login(
          asyncId: asyncId.toString(),
          linkDelegate: link,
          delegateId: userId.toString(),
          delegateName: displayName.toString(),
          userId: userId.toString(),
          cityId: data['cityId']?.toString(),
          cityName: data['cityName']?.toString() ?? _selectedGovernorate,
        );
        if (!mounted) return;
        final dest =
            await ShiftGateCoordinator().resolve(attachIfActive: true);
        if (!mounted) return;
        Navigator.pushNamedAndRemoveUntil(
          context,
          ShiftGateCoordinator.routeFor(dest),
          (r) => false,
        );
      } else {
        _showErrorDialog(serverMessage.isNotEmpty
            ? serverMessage
            : 'تعذر تسجيل الدخول (${response.statusCode})');
      }
    } catch (e) {
      if (mounted) {
        _showErrorDialog('لا يوجد اتصال بالإنترنت أو السيرفر غير متاح');
      }
    } finally {
      if (mounted) {
        setState(() {
          _isLoading = false;
        });
      }
    }
  }

  void _showErrorDialog(String message) {
    showDialog(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('خطأ', style: TextStyle(fontFamily: 'Cairo')),
        content: Text(message, style: const TextStyle(fontFamily: 'Cairo')),
        actions: <Widget>[
          TextButton(
            child: const Text('موافق', style: TextStyle(fontFamily: 'Cairo')),
            onPressed: () {
              Navigator.of(ctx).pop();
            },
          )
        ],
      ),
    );
  }

  InputDecoration _fieldDecoration({
    required String hint,
    required IconData icon,
    Widget? suffix,
  }) {
    return InputDecoration(
      hintText: hint,
      hintStyle: const TextStyle(fontFamily: 'Cairo'),
      filled: true,
      fillColor: Theme.of(context).cardColor,
      border: OutlineInputBorder(
        borderRadius: BorderRadius.circular(15),
        borderSide: BorderSide.none,
      ),
      prefixIcon: Icon(icon, color: AppTheme.primaryColor),
      suffixIcon: suffix,
      contentPadding:
          const EdgeInsets.symmetric(horizontal: 20, vertical: 15),
    );
  }

  Widget _cardShell({required Widget child}) {
    return Container(
      decoration: BoxDecoration(
        color: Theme.of(context).cardColor,
        borderRadius: BorderRadius.circular(15),
        boxShadow: [
          BoxShadow(
            color: Colors.black.withValues(alpha: 0.05),
            blurRadius: 10,
            offset: const Offset(0, 5),
          )
        ],
      ),
      child: child,
    );
  }

  @override
  Widget build(BuildContext context) {
    final height = MediaQuery.of(context).size.height;

    return Directionality(
      textDirection: TextDirection.rtl,
      child: AppSafeScaffold(
        backgroundColor: Theme.of(context).scaffoldBackgroundColor,
        body: SingleChildScrollView(
          child: SizedBox(
            height: height - MediaQuery.viewPaddingOf(context).vertical,
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
                        Hero(
                          tag: 'logo',
                          child: Image.asset(
                            'assets/icons/LogoCompany.png',
                            height: height * 0.14,
                          ),
                        ),
                        const SizedBox(height: 16),
                        const Text(
                          'تطبيق المتابع',
                          style: TextStyle(
                            fontFamily: 'Cairo',
                            fontSize: 26,
                            fontWeight: FontWeight.bold,
                            color: AppTheme.primaryColor,
                          ),
                        ),
                        const SizedBox(height: 8),
                        Text(
                          'المحافظة · اسم المستخدم · كلمة المرور',
                          style: TextStyle(
                              fontFamily: 'Cairo', color: Colors.grey[600]),
                        ),
                        SizedBox(height: height * 0.035),
                        _cardShell(
                          child: _isLoadingCities
                              ? const Padding(
                                  padding: EdgeInsets.all(16.0),
                                  child: Center(
                                    child: Text(
                                      'جاري تحميل الفروع...',
                                      style: TextStyle(
                                        fontFamily: 'Cairo',
                                        color: Colors.grey,
                                      ),
                                    ),
                                  ),
                                )
                              : _cityError != null
                                  ? Padding(
                                      padding: const EdgeInsets.all(16.0),
                                      child: Column(
                                        children: [
                                          Text(
                                            _cityError!,
                                            textAlign: TextAlign.center,
                                            style: const TextStyle(
                                              fontFamily: 'Cairo',
                                              color: Colors.red,
                                            ),
                                          ),
                                          TextButton(
                                            onPressed: () {
                                              setState(() {
                                                _isLoadingCities = true;
                                                _cityError = null;
                                              });
                                              _fetchCityData();
                                            },
                                            child: const Text(
                                              'إعادة المحاولة',
                                              style:
                                                  TextStyle(fontFamily: 'Cairo'),
                                            ),
                                          ),
                                        ],
                                      ),
                                    )
                                  : DropdownButtonFormField<String>(
                                      decoration: _fieldDecoration(
                                        hint: 'المحافظة',
                                        icon: Icons.location_city,
                                      ),
                                      alignment: Alignment.centerRight,
                                      value: _selectedGovernorate,
                                      hint: const Text(
                                        'اختر المحافظة',
                                        style: TextStyle(fontFamily: 'Cairo'),
                                      ),
                                      items: cityData.map((city) {
                                        return DropdownMenuItem(
                                          value: city['name'],
                                          child: Align(
                                            alignment: Alignment.centerRight,
                                            child: Text(
                                              city['name']!,
                                              style: const TextStyle(
                                                  fontFamily: 'Cairo'),
                                            ),
                                          ),
                                        );
                                      }).toList(),
                                      onChanged: (val) {
                                        setState(() {
                                          _selectedGovernorate = val;
                                        });
                                      },
                                    ),
                        ),
                        const SizedBox(height: 16),
                        _cardShell(
                          child: TextField(
                            controller: _userNameController,
                            style: const TextStyle(fontFamily: 'Cairo'),
                            textInputAction: TextInputAction.next,
                            autocorrect: false,
                            enableSuggestions: false,
                            decoration: _fieldDecoration(
                              hint: 'اسم المستخدم',
                              icon: Icons.person_outline,
                            ),
                          ),
                        ),
                        const SizedBox(height: 16),
                        _cardShell(
                          child: TextField(
                            controller: _passwordController,
                            style: const TextStyle(fontFamily: 'Cairo'),
                            obscureText: _obscurePassword,
                            textInputAction: TextInputAction.done,
                            onSubmitted: (_) => _login(),
                            decoration: _fieldDecoration(
                              hint: 'كلمة المرور',
                              icon: Icons.lock_outline,
                              suffix: IconButton(
                                icon: Icon(
                                  _obscurePassword
                                      ? Icons.visibility_outlined
                                      : Icons.visibility_off_outlined,
                                  color: Colors.grey,
                                ),
                                onPressed: () {
                                  setState(() {
                                    _obscurePassword = !_obscurePassword;
                                  });
                                },
                              ),
                            ),
                          ),
                        ),
                        const SizedBox(height: 36),
                        SizedBox(
                          width: double.infinity,
                          height: 55,
                          child: ElevatedButton(
                            onPressed: _isLoading ? null : _login,
                            style: ElevatedButton.styleFrom(
                              backgroundColor: AppTheme.primaryColor,
                              shape: RoundedRectangleBorder(
                                borderRadius: BorderRadius.circular(15),
                              ),
                            ),
                            child: _isLoading
                                ? const CircularProgressIndicator(
                                    color: Colors.white)
                                : const Text(
                                    'تسجيل الدخول',
                                    style: TextStyle(
                                      fontFamily: 'Cairo',
                                      color: Colors.white,
                                      fontSize: 18,
                                      fontWeight: FontWeight.bold,
                                    ),
                                  ),
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
