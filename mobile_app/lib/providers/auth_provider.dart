import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';
import '../services/api_service.dart';

class AuthProvider extends ChangeNotifier {
  final ApiService _api = ApiService();

  bool _isAuthenticated = false;
  bool _isLoading = false;
  String? _userName;
  int? _userID;
  String? _fullName;
  ThemeMode _themeMode = ThemeMode.light;
  String? _errorMessage;

  bool get isAuthenticated => _isAuthenticated;
  bool get isLoading => _isLoading;
  String? get userName => _userName;
  int? get userID => _userID;
  String? get fullName => _fullName;
  ThemeMode get themeMode => _themeMode;
  String? get errorMessage => _errorMessage;

  Future<void> tryAutoLogin() async {
    await _api.init();
    final prefs = await SharedPreferences.getInstance();
    _isAuthenticated = prefs.containsKey('token');
    _userName = prefs.getString('userName');
    _userID = prefs.getInt('userID');
    _fullName = prefs.getString('fullName');
    final theme = prefs.getString('themeMode');
    _themeMode = theme == 'dark' ? ThemeMode.dark : ThemeMode.light;
    notifyListeners();
  }

  Future<bool> login(String userName, String password, String apiUrl, String cityName) async {
    _isLoading = true;
    _errorMessage = null;
    notifyListeners();

    try {
      final url = apiUrl.endsWith('/') ? apiUrl : '$apiUrl/';
      _api.setBaseUrl(url);

      final response = await _api.post(
        'Users/Users_LoginEmployee',
        data: {
          'userName': userName,
          'password': password,
        },
      );

      final data = response.data;
      if (data is Map && data.containsKey('message')) {
        _errorMessage = data['message'];
        _isLoading = false;
        notifyListeners();
        return false;
      }

      final token = data['token'] ?? '';
      if (token.toString().isEmpty) {
        _errorMessage = 'فشل تسجيل الدخول';
        _isLoading = false;
        notifyListeners();
        return false;
      }

      _token = token.toString();
      _userName = userName;
      _userID = data['userID'];
      _fullName = data['fullName'] ?? userName;
      _isAuthenticated = true;

      final prefs = await SharedPreferences.getInstance();
      prefs.setString('token', _token!);
      prefs.setString('apiUrl', url);
      prefs.setString('userName', userName);
      if (_userID != null) prefs.setInt('userID', _userID!);
      if (_fullName != null) prefs.setString('fullName', _fullName!);
      prefs.setString('province', cityName);

      _isLoading = false;
      notifyListeners();
      return true;
    } catch (e) {
      _errorMessage = 'خطأ في الاتصال بالخادم';
      _isLoading = false;
      notifyListeners();
      return false;
    }
  }

  String? _token;
  String? get token => _token;

  void setTokenDirect(String token, String url, String cityName) {
    _token = token;
    _api.setBaseUrl(url);
    _api.setToken(token);
    _isAuthenticated = true;
    final prefs = SharedPreferences.getInstance();
    prefs.then((p) {
      p.setString('token', token);
      p.setString('apiUrl', url);
      p.setString('province', cityName);
    });
    notifyListeners();
  }

  void toggleTheme() {
    _themeMode = _themeMode == ThemeMode.light ? ThemeMode.dark : ThemeMode.light;
    SharedPreferences.getInstance().then((p) => p.setString('themeMode', _themeMode == ThemeMode.dark ? 'dark' : 'light'));
    notifyListeners();
  }

  void logout() async {
    _api.clearAuth();
    _isAuthenticated = false;
    _userName = null;
    _userID = null;
    _fullName = null;
    _token = null;
    notifyListeners();
  }
}
