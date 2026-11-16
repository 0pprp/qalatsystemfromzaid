import 'dart:async';
import 'dart:convert';

import 'package:follower_application/config/app_env.dart';
import 'package:geolocator/geolocator.dart';
import 'package:http/http.dart' as http;
import 'package:http_parser/http_parser.dart';
import 'package:image_picker/image_picker.dart';
import 'package:shared_preferences/shared_preferences.dart';

/// Shop location + shop photo taken by the follower (متابع).
class ShopCaptureList {
  ShopCaptureList(this.listId, this.listName, this.customerCount, this.withLocation, this.withPhoto);

  final int listId;
  final String listName;
  final int customerCount;
  final int withLocation;
  final int withPhoto;

  factory ShopCaptureList.fromJson(Map<String, dynamic> j) => ShopCaptureList(
        _int(j['listId']),
        '${j['listName'] ?? ''}',
        _int(j['customerCount']),
        _int(j['withLocation']),
        _int(j['withPhoto']),
      );
}

class ShopCaptureCustomer {
  ShopCaptureCustomer({
    required this.customerId,
    required this.customerName,
    this.shopName,
    this.phoneNumber,
    this.address,
    this.followerLat,
    this.followerLng,
    this.followerAccuracy,
    this.salesLat,
    this.salesLng,
    this.hasPhoto = false,
    this.photoAt,
  });

  final int customerId;
  final String customerName;
  final String? shopName;
  final String? phoneNumber;
  final String? address;
  double? followerLat;
  double? followerLng;
  double? followerAccuracy;
  final double? salesLat;
  final double? salesLng;
  bool hasPhoto;
  String? photoAt;

  bool get hasFollowerLocation => followerLat != null && followerLng != null;
  bool get hasSalesLocation => salesLat != null && salesLng != null;

  factory ShopCaptureCustomer.fromJson(Map<String, dynamic> j) => ShopCaptureCustomer(
        customerId: _int(j['customerId']),
        customerName: '${j['customerName'] ?? ''}'.trim(),
        shopName: _str(j['shopName']),
        phoneNumber: _str(j['phoneNumber']),
        address: _str(j['address']),
        followerLat: _dbl(j['followerLatitude']),
        followerLng: _dbl(j['followerLongitude']),
        followerAccuracy: _dbl(j['followerAccuracyM']),
        salesLat: _dbl(j['salesLatitude']),
        salesLng: _dbl(j['salesLongitude']),
        hasPhoto: j['hasPhoto'] == true,
        photoAt: _str(j['photoAtUtc']),
      );
}

class CapturedPoint {
  CapturedPoint(this.latitude, this.longitude, this.accuracy);

  final double latitude;
  final double longitude;
  final double accuracy;
}

int _int(dynamic v) => int.tryParse('${v ?? 0}') ?? 0;
double? _dbl(dynamic v) => v == null ? null : double.tryParse('$v');
String? _str(dynamic v) {
  final s = v?.toString().trim();
  return s == null || s.isEmpty ? null : s;
}

class ShopCaptureApi {
  static Future<({String link, String asyncId})> _session() async {
    final prefs = await SharedPreferences.getInstance();
    return (
      link: AppEnv.apiBase(fallback: prefs.getString('LinkDelegate') ?? ''),
      asyncId: prefs.getString('AsyncId') ?? '',
    );
  }

  static String _message(http.Response res, String fallback) {
    try {
      final j = json.decode(res.body);
      final m = j is Map ? (j['message'] ?? j['Message']) : null;
      if (m != null && '$m'.trim().isNotEmpty) return '$m';
    } catch (_) {}
    return fallback;
  }

  static Future<List<ShopCaptureList>> lists() async {
    final s = await _session();
    final uri = Uri.parse('${s.link}Followers/ShopCapture/Lists').replace(queryParameters: {'asyncId': s.asyncId});
    final res = await http.get(uri).timeout(const Duration(seconds: 60));
    if (res.statusCode != 200) throw Exception(_message(res, 'تعذر تحميل القوائم'));
    final items = (json.decode(res.body)['items'] as List?) ?? const [];
    return items.map((e) => ShopCaptureList.fromJson(Map<String, dynamic>.from(e as Map))).toList();
  }

  static Future<List<ShopCaptureCustomer>> customers(int listId) async {
    final s = await _session();
    final uri = Uri.parse('${s.link}Followers/ShopCapture/Lists/$listId/Customers')
        .replace(queryParameters: {'asyncId': s.asyncId});
    final res = await http.get(uri).timeout(const Duration(seconds: 60));
    if (res.statusCode != 200) throw Exception(_message(res, 'تعذر تحميل الزبائن'));
    final items = (json.decode(res.body)['items'] as List?) ?? const [];
    return items.map((e) => ShopCaptureCustomer.fromJson(Map<String, dynamic>.from(e as Map))).toList();
  }

  static Future<void> saveLocation(int listId, int customerId, CapturedPoint p) async {
    final s = await _session();
    final res = await http
        .put(
          Uri.parse('${s.link}Followers/ShopCapture/Customers/$customerId/Location'),
          headers: {'Content-Type': 'application/json'},
          body: json.encode({
            'asyncId': s.asyncId,
            'listId': listId,
            'latitude': p.latitude,
            'longitude': p.longitude,
            'accuracy': p.accuracy,
          }),
        )
        .timeout(const Duration(seconds: 30));
    if (res.statusCode != 200) throw Exception(_message(res, 'تعذر حفظ الموقع'));
  }

  static Future<void> uploadPhoto(int listId, int customerId, XFile photo) async {
    final s = await _session();
    final req = http.MultipartRequest('PUT', Uri.parse('${s.link}Followers/ShopCapture/Customers/$customerId/Photo'))
      ..fields['asyncId'] = s.asyncId
      ..fields['listId'] = '$listId'
      ..files.add(http.MultipartFile.fromBytes(
        'photo',
        await photo.readAsBytes(),
        filename: 'shop_$customerId.jpg',
        contentType: MediaType('image', 'jpeg'),
      ));
    final res = await http.Response.fromStream(await req.send().timeout(const Duration(seconds: 90)));
    if (res.statusCode != 200) throw Exception(_message(res, 'تعذر رفع الصورة'));
  }

  /// Image URL (cache-busted so an edited photo shows at once).
  static Future<String> photoUrl(int listId, int customerId, {String? version}) async {
    final s = await _session();
    return Uri.parse('${s.link}Followers/ShopCapture/Customers/$customerId/Photo').replace(queryParameters: {
      'asyncId': s.asyncId,
      'listId': '$listId',
      'v': version ?? '${DateTime.now().millisecondsSinceEpoch}',
    }).toString();
  }

  /// A fresh high-accuracy GPS fix taken now — never the phone's cached last position.
  static Future<CapturedPoint> captureLocation() async {
    if (!await Geolocator.isLocationServiceEnabled()) {
      throw Exception('شغّل خدمة الموقع (GPS) في الهاتف');
    }
    var perm = await Geolocator.checkPermission();
    if (perm == LocationPermission.denied) {
      perm = await Geolocator.requestPermission();
    }
    if (perm == LocationPermission.denied || perm == LocationPermission.deniedForever) {
      throw Exception('اسمح للتطبيق باستخدام الموقع');
    }
    try {
      final pos = await Geolocator.getCurrentPosition(
        locationSettings: const LocationSettings(
          accuracy: LocationAccuracy.best,
          timeLimit: Duration(seconds: 25),
        ),
      );
      return CapturedPoint(pos.latitude, pos.longitude, pos.accuracy);
    } on TimeoutException {
      throw Exception('تعذر تحديد الموقع، اخرج لمكان مفتوح وحاول مرة أخرى');
    }
  }

  /// Camera photo, resized/compressed so the upload stays small.
  static Future<XFile?> takePhoto() => ImagePicker().pickImage(
        source: ImageSource.camera,
        maxWidth: 1280,
        maxHeight: 1280,
        imageQuality: 70,
      );
}
