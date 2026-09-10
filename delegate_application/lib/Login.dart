import 'package:delegate_application/config/app_env.dart';
import 'package:delegate_application/config/login_city_catalog.dart';
import 'package:delegate_application/services/delegate_data_refresh_service.dart';
import 'package:delegate_application/ui/app_safe_scaffold.dart';
import 'package:delegate_application/utils/AppTheme.dart';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';
import 'dart:convert';

class Login extends StatefulWidget {
  const Login({super.key});

  @override
  _LoginState createState() => _LoginState();
}

class _LoginState extends State<Login> {
  String? _selectedGovernorate;
  String _password = "";
  // ignore: unused_field
  final _formKey = GlobalKey<FormState>();
  bool _isLoading = false;
  bool _isLoadingCities = true;
  String? _cityError;

  List<Map<String, String>> cityData = [];

  @override
  void initState() {
    super.initState();
    _fetchCityData();
  }

  /// Demo: local نجف - DEMO only (never calls GetHaider).
  /// Production: GetHaider + env-separated SharedPreferences cache.
  Future<void> _fetchCityData() async {
    if (AppEnv.isDemo) {
      if (!mounted) return;
      setState(() {
        cityData = LoginCityCatalog.demoCities();
        _isLoadingCities = false;
        _cityError = null;
        _selectedGovernorate = cityData.first['name'];
      });
      return;
    }

    try {
      final response = await http
          .get(Uri.parse(LoginCityCatalog.productionCityApiUrl))
          .timeout(const Duration(seconds: 10));

      if (!mounted) return;

      if (response.statusCode == 200) {
        final prefs = await SharedPreferences.getInstance();
        await prefs.setString(
            LoginCityCatalog.productionCacheKey, response.body);

        setState(() {
          cityData = LoginCityCatalog.parseCityJson(response.body);
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

  /// Production-only cache load. Never used for demo branch of [_fetchCityData].
  Future<void> _loadCachedCities() async {
    if (AppEnv.isDemo) {
      if (!mounted) return;
      setState(() {
        cityData = LoginCityCatalog.demoCities();
        _isLoadingCities = false;
        _cityError = null;
        _selectedGovernorate = cityData.first['name'];
      });
      return;
    }

    try {
      final prefs = await SharedPreferences.getInstance();
      final String? cachedJson = LoginCityCatalog.resolveCacheJson(
        envName: AppEnv.name,
        envSpecificCache: prefs.getString(LoginCityCatalog.productionCacheKey),
        legacyCache: prefs.getString(LoginCityCatalog.legacyCacheKey),
      );

      if (cachedJson != null && cachedJson.isNotEmpty) {
        if (mounted) {
          setState(() {
            cityData = LoginCityCatalog.parseCityJson(cachedJson);
            _isLoadingCities = false;
            _cityError = null;
          });
        }
      } else {
        if (mounted) {
          setState(() {
            _isLoadingCities = false;
            _cityError = 'تعذر تحميل الفروع. تأكد من اتصالك بالإنترنت.';
          });
        }
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

  Future<void> _saveLoginData(
      String delegateId, String asyncId, String apiUrl) async {
    SharedPreferences prefs = await SharedPreferences.getInstance();
    await prefs.setString("DelegateID", delegateId);
    await prefs.setString("AsyncId", asyncId);
    await prefs.setString("LinkDelegate", apiUrl);
    // Fire-and-forget master-data refresh after successful login.
    // ignore: unawaited_futures
    DelegateDataRefreshService.instance.refreshIfPossible();
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
      String apiUrl = "";
      for (var city in cityData) {
        if (city['name'] == _selectedGovernorate) {
          apiUrl = city['link']!;
          break;
        }
      }

      // Force Demo API base so Sync/requests always hit the demo host.
      if (AppEnv.isDemo) {
        apiUrl = AppEnv.demoApiBaseUrl;
      }

      if (apiUrl.isEmpty) {
        _showErrorDialog("لم يتم العثور على الرابط للمحافظة المختارة");
        setState(() {
          _isLoading = false;
        });
        return;
      }

      var uri =
          Uri.parse('${apiUrl}Delegates/GetDelegateLogin/asyncId=$_password/');

      var response = await http.get(
        uri,
        headers: {"Content-Type": "application/json"},
      ).timeout(const Duration(seconds: 10));

      if (!mounted) return;

      if (response.statusCode == 200) {
        try {
          var data = json.decode(response.body);
          var delegateId = data['delegateId'] ?? data['DelegateId'];
          var asyncId = data['asyncId'] ?? data['AsyncId'];

          if (data != null &&
              delegateId != null &&
              int.parse(delegateId.toString()) > 0) {
            await _saveLoginData(
              delegateId.toString(),
              asyncId?.toString() ?? _password,
              apiUrl,
            );
            if (!mounted) return;
            Navigator.pushReplacementNamed(context, '/HomePage');
          } else {
            _showErrorDialog("الرمز الذي أدخلته غير صحيح");
          }
        } catch (e) {
          _showErrorDialog("يوجد خلل في معالجة بيانات السيرفر");
        }
      } else if (response.statusCode == 401) {
        _showErrorDialog("الرمز الذي أدخلته غير صحيح");
      } else {
        _showErrorDialog("الرمز الذي أدخلته غير صحيح");
      }
    } catch (e) {
      if (mounted) {
        if (e.toString().contains("SocketException")) {
          _showErrorDialog("لا يوجد اتصال بالإنترنت أو السيرفر غير متاح");
        } else if (e.toString().contains("TimeoutException")) {
          _showErrorDialog("انتهى وقت محاولة الاتصال، يرجى المحاولة مرة أخرى");
        } else {
          _showErrorDialog("حدث خطأ غير متوقع في الاتصال");
        }
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
                Positioned(
                  bottom: -30,
                  left: -30,
                  child: Container(
                    width: 150,
                    height: 150,
                    decoration: BoxDecoration(
                      color: AppTheme.secondaryColor.withValues(alpha: 0.05),
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
                            height: height * 0.18,
                          ),
                        ),
                        const SizedBox(height: 20),
                        const Text(
                          'تسجيل الدخول',
                          style: TextStyle(
                            fontFamily: 'Cairo',
                            fontSize: 28,
                            fontWeight: FontWeight.bold,
                            color: AppTheme.primaryColor,
                          ),
                        ),
                        const SizedBox(height: 10),
                        Text(
                          'أهلا بك مجددا، يرجى تسجيل الدخول للمتابعة',
                          style: TextStyle(
                              fontFamily: 'Cairo', color: Colors.grey[600]),
                        ),
                        SizedBox(height: height * 0.05),
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
                                    child: Row(
                                      mainAxisAlignment:
                                          MainAxisAlignment.center,
                                      children: [
                                        SizedBox(
                                          width: 20,
                                          height: 20,
                                          child: CircularProgressIndicator(
                                            strokeWidth: 2,
                                            color: AppTheme.primaryColor,
                                          ),
                                        ),
                                        SizedBox(width: 10),
                                        Text(
                                          "جاري تحميل الفروع...",
                                          style: TextStyle(
                                            fontFamily: 'Cairo',
                                            color: Colors.grey,
                                          ),
                                        ),
                                      ],
                                    ),
                                  ),
                                )
                              : _cityError != null
                                  ? Padding(
                                      padding: const EdgeInsets.all(16.0),
                                      child: Center(
                                        child: Column(
                                          children: [
                                            const Icon(Icons.cloud_off,
                                                color: Colors.red, size: 32),
                                            const SizedBox(height: 8),
                                            Text(
                                              _cityError!,
                                              textAlign: TextAlign.center,
                                              style: const TextStyle(
                                                fontFamily: 'Cairo',
                                                color: Colors.red,
                                                fontSize: 14,
                                              ),
                                            ),
                                            const SizedBox(height: 12),
                                            OutlinedButton.icon(
                                              onPressed: () {
                                                setState(() {
                                                  _isLoadingCities = true;
                                                  _cityError = null;
                                                });
                                                _fetchCityData();
                                              },
                                              icon: const Icon(Icons.refresh,
                                                  size: 18),
                                              label: const Text("إعادة المحاولة",
                                                  style: TextStyle(
                                                      fontFamily: 'Cairo')),
                                            ),
                                          ],
                                        ),
                                      ),
                                    )
                                  : cityData.isEmpty
                                      ? const Padding(
                                          padding: EdgeInsets.all(16.0),
                                          child: Center(
                                            child: Text(
                                              "لا توجد فروع متاحة حالياً",
                                              style: TextStyle(
                                                fontFamily: 'Cairo',
                                                color: Colors.grey,
                                              ),
                                            ),
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
                                            hintText: "اختر المحافظة",
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
                                                    textAlign: TextAlign.right,
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
                              elevation: 5,
                              shadowColor:
                                  AppTheme.primaryColor.withValues(alpha: 0.4),
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
