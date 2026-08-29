import 'package:follower_application/utils/AppTheme.dart';
import 'package:follower_application/AsyncIdChecker.dart';
import 'package:follower_application/LocalLabApi.dart';
import 'package:follower_application/config/app_env.dart';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'dart:convert';
import 'package:shared_preferences/shared_preferences.dart';

class Login extends StatefulWidget {
  const Login({super.key});

  @override
  _LoginState createState() => _LoginState();
}

class _LoginState extends State<Login> {
  String? _selectedGovernorate;
  String _password = "";
  bool _isLoading = false;
  bool _isLoadingCities = true;
  String? _cityError;

  List<Map<String, String>> cityData = [];

  static const String _cityApiUrl = 'http://defaultdata.alsaaeidy.com/GetHaider';
  static const String _cityCacheKey = 'cached_city_data';

  @override
  void initState() {
    super.initState();
    _fetchCityData();
  }

  Future<void> _fetchCityData() async {
    if (AppEnv.isDemo) {
      if (!mounted) return;
      setState(() {
        cityData = [
          {
            'name': 'الناصرية',
            'link': AppEnv.demoApiBaseUrl,
            'number': '',
          },
        ];
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
    if (_selectedGovernorate == null ||
        _selectedGovernorate!.isEmpty ||
        _password.isEmpty) {
      _showErrorDialog("لا يمكن ترك أي شيء فارغ");
      return;
    }

    setState(() {
      _isLoading = true;
    });

    try {
      String productionUrl = "";
      for (var city in cityData) {
        if (city['name'] == _selectedGovernorate) {
          productionUrl = city['link']!;
          break;
        }
      }

      if (productionUrl.isEmpty && !LocalLabApi.enabled && !AppEnv.isDemo) {
        _showErrorDialog("لم يتم العثور على الرابط للمحافظة المختارة");
        return;
      }

      final password = _password.trim();
      final bases = AppEnv.isDemo
          ? [AppEnv.demoApiBaseUrl]
          : (LocalLabApi.enabled ? LocalLabApi.bases() : [productionUrl]);

      http.Response? response;
      String apiUrl = productionUrl;

      for (final base in bases) {
        try {
          final uri = Uri.parse(
              '${base}Delegates/GetDelegateLogin/asyncId=$password');
          final candidate = await http.get(
            uri,
            headers: {"Content-Type": "application/json"},
          ).timeout(const Duration(seconds: 8));
          apiUrl = base;
          response = candidate;
          if (candidate.statusCode == 200) {
            break;
          }
        } catch (_) {}
      }

      if (!mounted) return;

      if (AppEnv.isDemo) {
        apiUrl = AppEnv.demoApiBaseUrl;
      }

      if (response == null) {
        _showErrorDialog(
            AppEnv.isDemo
                ? "لا يوجد اتصال بالإنترنت أو السيرفر غير متاح"
                : "تعذر الاتصال بالسيرفر المحلي على المنفذ 5080. تأكد أن الجهاز على نفس الواي فاي.");
        return;
      }

      if (response.statusCode == 200) {
        var data = json.decode(response.body);
        var delegateId = data['delegateId'] ??
            data['DelegateId'] ??
            data['delegateID'];
        var asyncId = data['asyncId'] ??
            data['AsyncId'] ??
            data['asyncID'] ??
            password;
        var delegateName = data['delegateName'] ?? data['DelegateName'] ?? '';

        if (delegateId != null && int.parse(delegateId.toString()) > 0) {
          final listsUri = Uri.parse('${AppEnv.apiBase(fallback: apiUrl)}Followers/Lists').replace(
            queryParameters: {'asyncId': asyncId.toString()},
          );
          final listsRes = await http.get(listsUri).timeout(const Duration(seconds: 15));
          if (!mounted) return;

          if (listsRes.statusCode != 200) {
            _showErrorDialog("تعذر جلب القوائم المرتبطة");
            return;
          }

          final lists = json.decode(listsRes.body);
          if (lists is! List || lists.isEmpty) {
            _showErrorDialog("هذا الحساب ليست لديه قوائم مرتبطة. تطبيق المتابع للقوائم المرتبطة فقط.");
            return;
          }

          await AsyncIdChecker.login(
            asyncId: asyncId.toString(),
            linkDelegate: AppEnv.apiBase(fallback: apiUrl),
            delegateId: delegateId.toString(),
            delegateName: delegateName.toString(),
          );
          if (!mounted) return;
          Navigator.pushReplacementNamed(context, '/HomePage');
        } else {
          _showErrorDialog("الرمز الذي أدخلته غير صحيح");
        }
      } else {
        _showErrorDialog("الرمز الذي أدخلته غير صحيح");
      }
    } catch (e) {
      if (mounted) {
        _showErrorDialog("لا يوجد اتصال بالإنترنت أو السيرفر غير متاح");
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
        title: const Text("خطأ", style: TextStyle(fontFamily: 'Cairo')),
        content: Text(message, style: const TextStyle(fontFamily: 'Cairo')),
        actions: <Widget>[
          TextButton(
            child: const Text("موافق", style: TextStyle(fontFamily: 'Cairo')),
            onPressed: () {
              Navigator.of(ctx).pop();
            },
          )
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
        backgroundColor: Theme.of(context).scaffoldBackgroundColor,
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
                        Hero(
                          tag: 'logo',
                          child: Image.asset(
                            'assets/icons/LogoCompany.png',
                            height: height * 0.16,
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
                          'اختر المحافظة ثم أدخل رمز المتابع',
                          style: TextStyle(
                              fontFamily: 'Cairo', color: Colors.grey[600]),
                        ),
                        SizedBox(height: height * 0.04),
                        Container(
                          decoration: BoxDecoration(
                              color: Theme.of(context).cardColor,
                              borderRadius: BorderRadius.circular(15),
                              boxShadow: [
                                BoxShadow(
                                    color: Colors.black.withValues(alpha: 0.05),
                                    blurRadius: 10,
                                    offset: const Offset(0, 5))
                              ]),
                          child: _isLoadingCities
                              ? const Padding(
                                  padding: EdgeInsets.all(16.0),
                                  child: Center(
                                    child: Text(
                                      "جاري تحميل الفروع...",
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
                                            child: const Text("إعادة المحاولة",
                                                style: TextStyle(
                                                    fontFamily: 'Cairo')),
                                          ),
                                        ],
                                      ),
                                    )
                                  : DropdownButtonFormField<String>(
                                      decoration: InputDecoration(
                                        contentPadding:
                                            const EdgeInsets.symmetric(
                                                horizontal: 20, vertical: 5),
                                        border: OutlineInputBorder(
                                            borderRadius:
                                                BorderRadius.circular(15),
                                            borderSide: BorderSide.none),
                                        prefixIcon: const Icon(
                                            Icons.location_city,
                                            color: AppTheme.primaryColor),
                                      ),
                                      alignment: Alignment.centerRight,
                                      value: _selectedGovernorate,
                                      hint: const Text("اختر المحافظة",
                                          style: TextStyle(
                                              fontFamily: 'Cairo')),
                                      items: cityData.map((city) {
                                        return DropdownMenuItem(
                                          value: city['name'],
                                          child: Align(
                                            alignment: Alignment.centerRight,
                                            child: Text(city['name']!,
                                                style: const TextStyle(
                                                    fontFamily: 'Cairo')),
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
                        const SizedBox(height: 20),
                        Container(
                          decoration: BoxDecoration(
                              color: Theme.of(context).cardColor,
                              borderRadius: BorderRadius.circular(15),
                              boxShadow: [
                                BoxShadow(
                                    color: Colors.black.withValues(alpha: 0.05),
                                    blurRadius: 10,
                                    offset: const Offset(0, 5))
                              ]),
                          child: TextField(
                            style: const TextStyle(fontFamily: 'Cairo'),
                            obscureText: true,
                            textInputAction: TextInputAction.done,
                            onSubmitted: (_) => _login(),
                            decoration: InputDecoration(
                                hintText: 'كلمة السر',
                                hintStyle: const TextStyle(fontFamily: 'Cairo'),
                                filled: true,
                                fillColor: Theme.of(context).cardColor,
                                border: OutlineInputBorder(
                                  borderRadius: BorderRadius.circular(15),
                                  borderSide: BorderSide.none,
                                ),
                                prefixIcon: const Icon(Icons.lock_outline,
                                    color: AppTheme.primaryColor),
                                contentPadding: const EdgeInsets.symmetric(
                                    horizontal: 20, vertical: 15)),
                            onChanged: (value) {
                              _password = value;
                            },
                          ),
                        ),
                        const SizedBox(height: 40),
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
