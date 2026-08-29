import 'package:shared_preferences/shared_preferences.dart';
import 'package:dio/dio.dart';
import 'dart:convert';

class ApiService {
  static final ApiService _instance = ApiService._internal();
  factory ApiService() => _instance;

  late Dio _dio;
  String? _baseUrl;
  String? _token;

  ApiService._internal() {
    _dio = Dio(BaseOptions(
      connectTimeout: const Duration(seconds: 15),
      receiveTimeout: const Duration(seconds: 30),
      sendTimeout: const Duration(seconds: 15),
    ));
    _dio.interceptors.add(LogInterceptor(
      requestBody: true,
      responseBody: true,
      error: true,
    ));
  }

  String get baseUrl => _baseUrl ?? '';

  Future<void> init() async {
    final prefs = await SharedPreferences.getInstance();
    _baseUrl = prefs.getString('apiUrl');
    _token = prefs.getString('token');
    if (_baseUrl != null && !_baseUrl!.endsWith('/')) {
      _baseUrl = '$_baseUrl/';
    }
  }

  void setBaseUrl(String url) {
    _baseUrl = url.endsWith('/') ? url : '$url/';
    SharedPreferences.getInstance().then((p) => p.setString('apiUrl', _baseUrl!));
  }

  void setToken(String token) {
    _token = token;
    SharedPreferences.getInstance().then((p) => p.setString('token', token));
  }

  void clearAuth() {
    _token = null;
    SharedPreferences.getInstance().then((p) {
      p.remove('token');
      p.remove('apiUrl');
      p.remove('userName');
      p.remove('userID');
      p.remove('fullName');
    });
  }

  Map<String, String> get _headers {
    final headers = <String, String>{
      'Content-Type': 'application/json',
    };
    if (_token != null) {
      headers['Authorization'] = 'Bearer $_token';
    }
    return headers;
  }

  String _buildUrl(String path) {
    return '$_baseUrl$path';
  }

  Future<Response> get(String path, {Map<String, dynamic>? queryParams}) async {
    return _dio.get(
      _buildUrl(path),
      queryParameters: queryParams,
      options: Options(headers: _headers),
    );
  }

  Future<Response> post(String path, {dynamic data}) async {
    return _dio.post(
      _buildUrl(path),
      data: data,
      options: Options(headers: _headers),
    );
  }

  Future<Response> put(String path, {dynamic data}) async {
    return _dio.put(
      _buildUrl(path),
      data: data,
      options: Options(headers: _headers),
    );
  }

  Future<Response> delete(String path) async {
    return _dio.delete(
      _buildUrl(path),
      options: Options(headers: _headers),
    );
  }
}
