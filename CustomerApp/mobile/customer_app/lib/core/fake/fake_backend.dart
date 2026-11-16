import 'dart:math';

import 'package:dio/dio.dart';

import 'fake_data.dart';

class _FakeError implements Exception {
  _FakeError(this.status, this.code, this.message, [this.extra = const {}]);

  final int status;
  final String code;
  final String message;
  final Map<String, dynamic> extra;
}

class _FakeAgent {
  _FakeAgent({
    required this.id,
    required this.username,
    required this.password,
    required this.phone,
    required this.firstName,
    required this.fatherName,
    required this.grandfatherName,
    this.surname,
  });

  final int id;
  final String username;
  final String password;
  final String phone;
  final String firstName;
  final String fatherName;
  final String grandfatherName;
  final String? surname;
  final DateTime createdAt = DateTime.now().toUtc().subtract(const Duration(days: 90));
  final List<Map<String, dynamic>> orders = [];
  final List<Map<String, dynamic>> notifications = [];

  /// Today's «دوام» sessions: {id, startedAt, endedAt, endedBy} (ISO strings) and their points.
  final List<Map<String, dynamic>> sessions = [];
  final List<Map<String, dynamic>> points = [];
  bool routeSeeded = false;
}

/// In-memory stand-in for the API, used by the demo build (USE_FAKE_API) and the tests.
///
/// Demo accounts (password `123456`):
/// - `agent1` — علي حسين محمد الكعبي, orders at every stage.
/// - `agent2` — زينب كاظم جواد.
class FakeBackend extends Interceptor {
  FakeBackend({this.latency = const Duration(milliseconds: 550)}) {
    _seedAgent(_FakeAgent(
      id: 42,
      username: 'agent1',
      password: '123456',
      phone: '07701234567',
      firstName: 'علي',
      fatherName: 'حسين',
      grandfatherName: 'محمد',
      surname: 'الكعبي',
    ));
    _seedAgent(_FakeAgent(
      id: 43,
      username: 'agent2',
      password: '123456',
      phone: '07801234567',
      firstName: 'زينب',
      fatherName: 'كاظم',
      grandfatherName: 'جواد',
    ));
  }

  final Duration latency;
  final _rnd = Random(7);
  final Map<String, _FakeAgent> _agents = {}; // username -> agent
  final Map<String, String> _tokens = {}; // access/refresh token -> username
  final Map<String, Map<String, dynamic>> _idempotency = {};
  int _orderSeq = 80;
  int _idSeq = 1000;

  /// «المحلات المحتملة», shared by all agents. Sample pins are added around the first area the map shows.
  final List<Map<String, dynamic>> _prospects = [];
  bool _prospectsSeeded = false;

  @override
  Future<void> onRequest(RequestOptions options, RequestInterceptorHandler handler) async {
    await Future<void>.delayed(latency);
    try {
      final (status, body) = _handle(options);
      handler.resolve(Response(requestOptions: options, statusCode: status, data: body));
    } on _FakeError catch (e) {
      handler.reject(
        DioException(
          requestOptions: options,
          type: DioExceptionType.badResponse,
          response: Response(
            requestOptions: options,
            statusCode: e.status,
            data: {'error': e.message, 'code': e.code, ...e.extra},
          ),
        ),
      );
    }
  }

  // ---------------------------------------------------------------- routing

  (int, dynamic) _handle(RequestOptions o) {
    final path = o.path.replaceAll(RegExp(r'^/+'), '').split('?').first;
    final seg = path.split('/');
    final method = o.method.toUpperCase();
    final q = o.queryParameters;
    final body = o.data is Map ? Map<String, dynamic>.from(o.data as Map) : <String, dynamic>{};

    // Public
    if (method == 'GET' && path == 'branches') return (200, fakeBranches);
    if (method == 'GET' && path == 'app/config') {
      return (200, {
        'minSupportedVersion': '1.0.0',
        'latestVersion': '1.0.0',
        'supportPhone': '07801112233',
        'supportWhatsapp': '9647801112233',
        'apkUrl': 'https://store.qalaa.iq/download',
      });
    }

    // Auth
    if (method == 'POST' && path == 'auth/login') return (200, _login(body));
    if (method == 'POST' && path == 'auth/refresh') {
      final username = _tokens[body['refreshToken']];
      if (username == null) throw _FakeError(401, 'unauthorized', 'انتهت الجلسة. سجّل الدخول من جديد.');
      return (200, _session(_agents[username]!));
    }

    final user = _auth(o);
    if (method == 'POST' && path == 'auth/logout') return (204, null);

    // Account
    if (path == 'me' && method == 'GET') return (200, _me(user));
    if (path == 'me/overview' && method == 'GET') return (200, _overview(user));
    if (path == 'me/devices') return (204, null);
    if (seg.length == 3 && seg[0] == 'me' && seg[1] == 'devices') return (204, null);

    // Orders
    if (path == 'orders/shop-photo' && method == 'POST') {
      // The demo keeps the photo on the phone; the app sends its path in this header.
      final local = o.headers['X-Local-Path']?.toString();
      return (200, {'url': local == null ? 'asset:assets/demo/banner1.jpg' : 'file://$local'});
    }
    if (path == 'orders' && method == 'POST') {
      final key = o.headers['Idempotency-Key']?.toString() ?? '';
      if (key.isNotEmpty && _idempotency.containsKey(key)) return (201, _idempotency[key]);
      final order = _createOrder(user, body);
      if (key.isNotEmpty) _idempotency[key] = order;
      return (201, order);
    }
    if (path == 'orders/counts') {
      final counts = <String, int>{for (final s in _statusLabels.keys) s: 0};
      for (final order in user.orders) {
        counts[order['status'] as String] = (counts[order['status']] ?? 0) + 1;
      }
      return (200, {...counts, 'all': user.orders.length});
    }
    if (path == 'orders' && method == 'GET') {
      final statuses = (q['status']?.toString() ?? '').split(',').where((s) => s.isNotEmpty).toSet();
      final list = user.orders.where((e) => statuses.isEmpty || statuses.contains(e['status'])).toList();
      return (200, _page(list.map(_orderSummary).toList(), q));
    }
    if (seg.length == 2 && seg[0] == 'orders' && method == 'GET') return (200, _order(user, int.parse(seg[1])));
    if (seg.length == 3 && seg[0] == 'orders' && seg[2] == 'cancel') {
      final order = _order(user, int.parse(seg[1]));
      if (order['status'] != 'New' && order['status'] != 'UnderReview') {
        throw _FakeError(409, 'conflict', 'لا يمكن إلغاء الطلب بعد تحويله إلى مندوب المبيعات.');
      }
      _setStatus(order, 'Cancelled', note: body['reason']?.toString() ?? 'ألغاه المسوّق');
      return (200, order);
    }

    // Work day
    if (path == 'work/today') {
      final lat = double.tryParse('${q['nearLat'] ?? ''}');
      final lng = double.tryParse('${q['nearLng'] ?? ''}');
      if (lat != null && lng != null) _seedMorningRoute(user, lat, lng);
      return (200, _workDay(user));
    }
    if (path == 'work/start' && method == 'POST') {
      if (!_isWorking(user)) {
        user.sessions.add({'id': _idSeq++, 'startedAt': _iso(_now), 'endedAt': null, 'endedBy': null});
      }
      _addPoint(user, body['latitude'], body['longitude'], _now);
      return (200, _workDay(user));
    }
    if (path == 'work/end' && method == 'POST') {
      _addPoint(user, body['latitude'], body['longitude'], _now);
      for (final s in user.sessions.where((s) => s['endedAt'] == null)) {
        s['endedAt'] = _iso(_now);
        s['endedBy'] = 'user';
      }
      return (200, _workDay(user));
    }
    if (path == 'work/locations' && method == 'POST') {
      if (!_isWorking(user)) return (200, {'accepted': 0, 'isWorking': false});
      var accepted = 0;
      for (final p in (body['points'] as List? ?? const [])) {
        if (p is! Map) continue;
        if (_addPoint(user, p['latitude'], p['longitude'], DateTime.tryParse('${p['recordedAt']}') ?? _now)) accepted++;
      }
      return (200, {'accepted': accepted, 'isWorking': true});
    }

    // Prospects
    if (path == 'prospects' && method == 'GET') {
      final minLat = double.tryParse('${q['minLat']}') ?? -90;
      final maxLat = double.tryParse('${q['maxLat']}') ?? 90;
      final minLng = double.tryParse('${q['minLng']}') ?? -180;
      final maxLng = double.tryParse('${q['maxLng']}') ?? 180;
      _seedProspects((minLat + maxLat) / 2, (minLng + maxLng) / 2);
      return (
        200,
        [
          for (final p in _prospects.reversed)
            if ((p['latitude'] as double) >= minLat &&
                (p['latitude'] as double) <= maxLat &&
                (p['longitude'] as double) >= minLng &&
                (p['longitude'] as double) <= maxLng)
              _prospectFor(user, p),
        ]
      );
    }
    if (path == 'prospects/mine' && method == 'GET') {
      final interest = int.tryParse('${q['interest'] ?? ''}');
      final search = (q['q']?.toString() ?? '').trim();
      // Like the server: Iraq days, both ends included.
      String iraqDay(Object? iso) => (DateTime.tryParse('$iso') ?? DateTime(2000)).toUtc().add(const Duration(hours: 3)).toIso8601String().substring(0, 10);
      final from = q['from']?.toString();
      final to = q['to']?.toString();
      final mine = [
        for (final p in _prospects.reversed)
          if (p['createdByUserId'] == user.id &&
              (interest == null || p['interest'] == interest) &&
              (from == null || iraqDay(p['createdAt']).compareTo(from) >= 0) &&
              (to == null || iraqDay(p['createdAt']).compareTo(to) <= 0) &&
              (search.isEmpty || '${p['shopName']} ${p['description'] ?? ''} ${p['phone'] ?? ''}'.contains(search)))
            _prospectFor(user, p),
      ];
      return (200, _page(mine, q));
    }
    if (seg.length == 2 && seg[0] == 'prospects' && method == 'GET') {
      final p = _prospects.firstWhere((x) => '${x['id']}' == seg[1], orElse: () => throw _FakeError(404, 'not_found', 'الموقع غير موجود'));
      return (200, _prospectFor(user, p));
    }
    if (path == 'prospects' && method == 'POST') {
      final p = _validProspect(body)
        ..['id'] = _idSeq++
        ..['createdByUserId'] = user.id
        ..['createdByName'] = '${user.firstName} ${user.fatherName} ${user.grandfatherName}'
        ..['createdAt'] = _iso(_now);
      _prospects.add(p);
      return (200, _prospectFor(user, p));
    }
    if (seg.length == 2 && seg[0] == 'prospects' && (method == 'PUT' || method == 'DELETE')) {
      final id = int.parse(seg[1]);
      final p = _prospects.firstWhere((x) => x['id'] == id, orElse: () => throw _FakeError(404, 'not_found', 'الموقع غير موجود'));
      if (p['createdByUserId'] != user.id) throw _FakeError(403, 'not_owner', 'يمكنك تعديل المواقع التي أضفتها فقط');
      if (method == 'DELETE') {
        _prospects.remove(p);
        return (204, null);
      }
      p.addAll(_validProspect(body));
      return (200, _prospectFor(user, p));
    }

    // Notifications
    if (path == 'notifications' && method == 'GET') {
      final page = _page(user.notifications, q);
      page['unreadCount'] = user.notifications.where((n) => n['readAt'] == null).length;
      return (200, page);
    }
    if (path == 'notifications/read-all') {
      for (final n in user.notifications) {
        n['readAt'] ??= _iso(_now);
      }
      return (204, null);
    }
    if (seg.length == 3 && seg[0] == 'notifications' && seg[2] == 'read') {
      for (final n in user.notifications) {
        if (n['id'].toString() == seg[1]) n['readAt'] ??= _iso(_now);
      }
      return (204, null);
    }

    throw _FakeError(404, 'not_found', 'المسار غير موجود في الوضع التجريبي: $method $path');
  }

  // ---------------------------------------------------------------- helpers

  String _iso(DateTime d) => d.toUtc().toIso8601String();
  DateTime get _now => DateTime.now().toUtc();

  Map<String, dynamic> _page(List<Map<String, dynamic>> all, Map<String, dynamic> q) {
    final page = int.tryParse('${q['page'] ?? 1}') ?? 1;
    final size = int.tryParse('${q['pageSize'] ?? 20}') ?? 20;
    final start = (page - 1) * size;
    final items = start >= all.length ? <Map<String, dynamic>>[] : all.sublist(start, min(start + size, all.length));
    return {'items': items, 'total': all.length, 'page': page, 'pageSize': size};
  }

  _FakeAgent _auth(RequestOptions o) {
    final header = o.headers['Authorization']?.toString() ?? '';
    final token = header.startsWith('Bearer ') ? header.substring(7) : '';
    final username = _tokens[token];
    final user = username == null ? null : _agents[username];
    if (user == null) throw _FakeError(401, 'unauthorized', 'يجب تسجيل الدخول.');
    return user;
  }

  String _cityName(int cv) =>
      fakeBranches.firstWhere((b) => b['cityValue'] == cv, orElse: () => fakeBranches.first)['name'] as String;

  // ---------------------------------------------------------------- auth

  Map<String, dynamic> _login(Map<String, dynamic> body) {
    final username = (body['username']?.toString() ?? '').trim().toLowerCase();
    final user = _agents[username];
    if (user == null || user.password != body['password']) {
      throw _FakeError(401, 'invalid_credentials', 'اسم المستخدم أو كلمة المرور غير صحيحة');
    }
    return _session(user);
  }

  Map<String, dynamic> _session(_FakeAgent u) {
    final access = 'acc-${_idSeq++}';
    final refresh = 'ref-${_idSeq++}';
    _tokens[access] = u.username;
    _tokens[refresh] = u.username;
    return {
      'accessToken': access,
      'accessTokenExpiresAt': _iso(_now.add(const Duration(hours: 12))),
      'refreshToken': refresh,
      'user': _me(u),
    };
  }

  Map<String, dynamic> _me(_FakeAgent u) => {
        'id': u.id,
        'username': u.username,
        'phone': u.phone,
        'firstName': u.firstName,
        'fatherName': u.fatherName,
        'grandfatherName': u.grandfatherName,
        'surname': u.surname,
        'fullName': [u.firstName, u.fatherName, u.grandfatherName, u.surname ?? ''].where((s) => s.isNotEmpty).join(' '),
        'createdAt': _iso(u.createdAt),
      };

  // ---------------------------------------------------------------- seed

  static const _customers = [
    ('مصطفى', 'جاسم', 'عبد', '07812345678', 'أسواق النور', 'مواد غذائية'),
    ('حسن', 'علي', 'كاظم', '07723456789', 'صيدلية الشفاء', 'صيدلية'),
    ('كرار', 'حيدر', 'سالم', '07834567890', 'محلات الأمير', 'ملابس'),
    ('سجاد', 'محمد', 'جبار', '07745678901', 'مخبز الرافدين', 'مخبز'),
    ('مرتضى', 'عباس', 'هادي', '07856789012', 'بقالة الهدى', 'مواد غذائية'),
    ('أحمد', 'صالح', 'مهدي', '07767890123', 'كهربائيات الفرات', 'أدوات كهربائية'),
    ('علي', 'رحيم', 'ناصر', '07878901234', 'عطارة الزهراء', 'عطارة'),
  ];

  void _seedAgent(_FakeAgent u) {
    _agents[u.username] = u;
    // (item, quantity, status, purchase type, days ago, sale total, paid %)
    final seeds = <(String, int, String, String, int, int, int)>[
      ('ثلاجة سامسونج 18 قدم', 1, 'Transferred', 'Cash', 0, 0, 0),
      ('مبردة هواء 6000', 2, 'FieldInspection', 'DailyInstallments', 3, 0, 0),
      ('غسالة إل جي 9 كغم', 1, 'Sold', 'DailyInstallments', 25, 780000, 35),
      ('شاشة 55 بوصة', 1, 'Sold', 'DailyInstallments', 70, 950000, 78),
      ('طباخ 5 عيون', 1, 'Finished', 'DailyInstallments', 130, 420000, 100),
      ('مكيف 1.5 طن', 1, 'Finished', 'Cash', 40, 610000, 100),
      ('مجمدة عمودية', 1, 'NotSold', 'DailyInstallments', 18, 0, 0),
    ];
    for (var i = 0; i < seeds.length; i++) {
      final (itemName, qty, status, type, daysAgo, saleTotal, paidPercent) = seeds[i];
      final c = _customers[i];
      final order = _buildOrder(
        user: u,
        cityValue: 1 + i % 3,
        purchaseType: type,
        customer: {
          'firstName': c.$1,
          'fatherName': c.$2,
          'grandfatherName': c.$3,
          'surname': null,
          'phone': c.$4,
          'secondaryPhone': null,
        },
        address: {
          'area': ['حي الأنصار', 'حي الأمير', 'حي الحسين', 'حي العسكري'][i % 4],
          'details': 'شارع ${10 + i * 3} قرب الجامع',
          'nearestLandmark': null,
          'latitude': 32.0256 + i * 0.004,
          'longitude': 44.3269 - i * 0.003,
        },
        shop: {'name': c.$5, 'businessType': c.$6, 'address': null, 'ownsShop': i.isEven},
        items: [
          {'name': itemName, 'quantity': qty},
        ],
        notes: null,
        shopPhotoUrl: 'asset:assets/demo/p${20 + i % 6}.jpg',
        createdAt: _now.subtract(Duration(days: daysAgo, hours: 2)),
      );
      final path = ['Transferred', 'Transferred', 'FieldInspection', 'Sold', 'Finished'];
      final reach = switch (status) {
        'New' => -1,
        'Transferred' => 1,
        'FieldInspection' => 2,
        'Sold' => 3,
        'Finished' => 4,
        _ => 2, // NotSold: inspected first
      };
      for (var k = 1; k <= reach; k++) {
        _setStatus(order, path[k], at: _now.subtract(Duration(days: max(0, daysAgo - k * 2), hours: 1)));
      }
      if (status == 'NotSold') {
        _setStatus(order, 'NotSold', at: _now.subtract(Duration(days: daysAgo - 5)));
        order['publicReason'] = 'لم يتم إتمام البيع بعد الكشف الميداني.';
      }
      if (status == 'Sold' || status == 'Finished') {
        final total = saleTotal;
        final paid = (total * paidPercent / 100).round();
        final soldAt = _now.subtract(Duration(days: max(0, daysAgo - 6)));
        order['sale'] = {
          'saleId': 50000 + order['id'],
          'total': total,
          'paid': paid,
          'remaining': total - paid,
          'paymentsCount': type == 'Cash' ? 1 : max(1, (paidPercent * 1.2).round()),
          'lastPaymentAt': _iso(_now.subtract(Duration(days: status == 'Finished' ? daysAgo - 100 : 1))),
          'progressPercent': paidPercent,
          'soldAt': _iso(soldAt),
          'finishedAt': status == 'Finished' ? _iso(_now.subtract(Duration(days: max(1, daysAgo - 100)))) : null,
        };
      }
      u.orders.add(order);
    }
    u.orders.sort((a, b) => (b['createdAt'] as String).compareTo(a['createdAt'] as String));

    final sold = u.orders.firstWhere((o) => o['status'] == 'Sold');
    final finished = u.orders.firstWhere((o) => o['status'] == 'Finished');
    final notes = <(String, String, String, Map<String, dynamic>, int, bool)>[
      ('Order', 'تم البيع بنجاح 🎉', 'تحوّل طلبك رقم ${sold['orderNumber']} إلى مبيعة. سنتابع تسديدها حتى الاكتمال.', {'orderId': sold['id']}, 6, false),
      ('Order', 'اكتمل تسديد المبيعة ✅', 'المبيعة رقم ${finished['orderNumber']} مسددة بالكامل.', {'orderId': finished['id']}, 10, true),
      ('System', 'مرحباً بك في تطبيق المسوّقين', 'ابدأ الدوام من «الرئيسية» وأرسل طلب البيع عند إتمام كل بيع.', {}, 60, true),
    ];
    var k = 0;
    for (final (type, title, text, data, daysAgo, read) in notes) {
      k++;
      u.notifications.add({
        'id': u.id * 100 + k,
        'type': type,
        'title': title,
        'body': text,
        'data': data,
        'readAt': read ? _iso(_now.subtract(Duration(days: daysAgo))) : null,
        'createdAt': _iso(_now.subtract(Duration(days: daysAgo, hours: k))),
      });
    }
  }

  static const _statusLabels = {
    'New': 'طلب جديد',
    'UnderReview': 'قيد المعالجة',
    'Denied': 'مرفوض',
    'Transferred': 'في قسم المبيعات',
    'FieldInspection': 'قيد الكشف الميداني',
    'Sold': 'تم البيع - قيد التسديد',
    'Finished': 'مكتمل التسديد',
    'NotSold': 'لم يتم البيع',
    'Cancelled': 'ملغى',
  };

  // ---------------------------------------------------------------- orders

  Map<String, dynamic> _buildOrder({
    required _FakeAgent user,
    required int cityValue,
    required String purchaseType,
    required Map<String, dynamic> customer,
    required Map<String, dynamic> address,
    required Map<String, dynamic>? shop,
    required List items,
    required String? notes,
    required String? shopPhotoUrl,
    DateTime? createdAt,
  }) {
    final id = ++_orderSeq;
    final created = createdAt ?? _now;
    final lines = [
      for (final i in items) {'name': '${i['name']}'.trim(), 'quantity': i['quantity'] as int? ?? 1},
    ];
    return {
      'id': id,
      'orderNumber': 'QO-${created.year % 100}${created.month.toString().padLeft(2, '0')}-${id.toString().padLeft(6, '0')}',
      'status': 'New',
      'statusLabel': _statusLabels['New'],
      'purchaseType': purchaseType,
      'customerName': [customer['firstName'], customer['fatherName'], customer['grandfatherName']].join(' '),
      'cityName': _cityName(cityValue),
      'itemsSummary': lines.map((l) => (l['quantity'] as int) > 1 ? '${l['name']} ×${l['quantity']}' : l['name']).join('، '),
      'createdAt': _iso(created),
      'updatedAt': _iso(created),
      'customer': customer,
      'address': address,
      'shop': shop,
      'notes': notes,
      'items': lines,
      'publicReason': null,
      'timeline': [
        {'status': 'New', 'statusLabel': _statusLabels['New'], 'at': _iso(created), 'note': null},
      ],
      'canCancel': true,
      'shopPhotoUrl': shopPhotoUrl,
      'sale': null,
    };
  }

  Map<String, dynamic> _createOrder(_FakeAgent user, Map<String, dynamic> body) {
    final customer = Map<String, dynamic>.from(body['customer'] as Map? ?? {});
    final address = Map<String, dynamic>.from(body['address'] as Map? ?? {});
    final fields = <String, String>{};
    // Like the real API: one full-name field (three names at least) or the name parts.
    final full = (customer['fullName']?.toString() ?? '').trim().split(RegExp(r'\s+')).where((w) => w.isNotEmpty).toList();
    if (customer.containsKey('fullName')) {
      if (full.length < 3) fields['customer.fullName'] = 'اكتب اسم الزبون الثلاثي على الأقل';
      customer
        ..['firstName'] = full.isNotEmpty ? full[0] : ''
        ..['fatherName'] = full.length > 1 ? full[1] : ''
        ..['grandfatherName'] = full.length > 2 ? full[2] : ''
        ..['surname'] = full.length > 3 ? full.skip(3).join(' ') : null;
    }
    if ((customer['phone']?.toString() ?? '').trim().isEmpty) fields['customer.phone'] = 'مطلوب';
    if ((address['details']?.toString() ?? '').trim().length < 3) fields['address.details'] = 'أدخل عنوان الزبون';
    // Shop photo and GPS location are optional.
    final photo = body['shopPhotoUrl']?.toString() ?? '';
    final items = [
      for (final i in body['items'] as List? ?? const [])
        if ('${(i as Map)['name'] ?? ''}'.trim().isNotEmpty) i,
    ];
    if (items.isEmpty) fields['items'] = 'اختر المنتج';
    if (fields.isNotEmpty) throw _FakeError(400, 'validation', 'يرجى التحقق من البيانات المدخلة.', {'fields': fields});
    final phone = customer['phone']?.toString() ?? '';
    for (final o in user.orders) {
      final open = ['New', 'UnderReview', 'Transferred', 'FieldInspection'].contains(o['status']);
      final samePhone = (o['customer'] as Map)['phone'] == phone;
      final sameItem = (o['items'] as List).any((l) => items.any((i) => '${i['name']}'.trim() == l['name']));
      if (open && samePhone && sameItem) {
        throw _FakeError(409, 'duplicate_open_order', 'يوجد طلب مفتوح لنفس الزبون ونفس المادة (رقم ${o['orderNumber']})',
            {'orderNumber': o['orderNumber']});
      }
    }
    final order = _buildOrder(
      user: user,
      cityValue: body['cityValue'] as int,
      purchaseType: body['purchaseType'] as String? ?? 'DailyInstallments',
      customer: customer,
      address: address,
      shop: body['shop'] == null ? null : Map<String, dynamic>.from(body['shop'] as Map),
      items: items,
      notes: body['notes']?.toString(),
      shopPhotoUrl: photo.isEmpty ? null : photo,
    );
    user.orders.insert(0, order);
    // Like the real API: sent straight to «طلبات البيع» (غير المسندة) in the company system.
    _setStatus(order, 'Transferred');
    user.notifications.insert(0, {
      'id': _idSeq++,
      'type': 'Order',
      'title': 'تم إرسال الطلب إلى قسم المبيعات',
      'body': 'طلب البيع رقم ${order['orderNumber']} وصل إلى قسم المبيعات وسيتم إسناده لموظف لإكمال البيع.',
      'data': {'orderId': order['id']},
      'readAt': null,
      'createdAt': _iso(_now),
    });
    return order;
  }

  Map<String, dynamic> _orderSummary(Map<String, dynamic> o) => {
        for (final k in [
          'id', 'orderNumber', 'status', 'statusLabel', 'purchaseType', 'customerName', 'cityName', //
          'itemsSummary', 'createdAt', 'updatedAt', 'sale',
        ])
          k: o[k],
      };

  /// «الرئيسية».
  Map<String, dynamic> _overview(_FakeAgent u) {
    final local = DateTime.now();
    final dayStart = DateTime(local.year, local.month, local.day).toUtc();
    final monthStart = DateTime(local.year, local.month).toUtc();
    DateTime at(Map<String, dynamic> o, String k) => DateTime.tryParse('${o[k]}') ?? DateTime(2000);
    int count(bool Function(Map<String, dynamic>) f) => u.orders.where(f).length;
    const open = ['New', 'UnderReview', 'Transferred', 'FieldInspection'];
    return {
      'ordersToday': count((o) => !at(o, 'createdAt').isBefore(dayStart)),
      'ordersThisMonth': count((o) => !at(o, 'createdAt').isBefore(monthStart)),
      'soldThisMonth': count((o) =>
          (o['status'] == 'Sold' || o['status'] == 'Finished') &&
          !(DateTime.tryParse('${(o['sale'] as Map?)?['soldAt']}') ?? DateTime(2000)).isBefore(monthStart)),
      'openOrders': count((o) => open.contains(o['status'])),
      'waitingPayment': count((o) => o['status'] == 'Sold'),
      'fullyPaid': count((o) => o['status'] == 'Finished'),
      'notSoldThisMonth': count((o) => (o['status'] == 'NotSold' || o['status'] == 'Denied') && !at(o, 'updatedAt').isBefore(monthStart)),
      'pinsThisMonth': _prospects.where((p) => p['createdByUserId'] == u.id && !at(p, 'createdAt').isBefore(monthStart)).length,
      'recentOrders': u.orders.take(5).map(_orderSummary).toList(),
      'pinsTotal': _prospects.where((p) => p['createdByUserId'] == u.id).length,
      // Demo: a daily target of 3 shops.
      'pinsByInterest': [
        for (var l = 5; l >= 1; l--)
          {
            'interest': l,
            'label': _interestLabels[l],
            'count': _prospects.where((p) => p['createdByUserId'] == u.id && p['interest'] == l).length,
          },
      ],
    };
  }

  Map<String, dynamic> _order(_FakeAgent u, int id) =>
      u.orders.firstWhere((o) => o['id'] == id, orElse: () => throw _FakeError(404, 'not_found', 'الطلب غير موجود.'));

  void _setStatus(Map<String, dynamic> order, String status, {String? note, DateTime? at}) {
    final when = _iso(at ?? _now);
    order['status'] = status;
    order['statusLabel'] = _statusLabels[status];
    order['canCancel'] = status == 'New' || status == 'UnderReview';
    order['updatedAt'] = when;
    (order['timeline'] as List).add({'status': status, 'statusLabel': _statusLabels[status], 'at': when, 'note': note});
  }

  // ---------------------------------------------------------------- prospects

  static const _interestLabels = ['', 'غير مهتم إطلاقاً', 'قليل الاهتمام', 'مهتم نوعاً ما', 'مهتم', 'مهتم جداً'];

  Map<String, dynamic> _validProspect(Map<String, dynamic> b) {
    final errors = <String, String>{};
    final name = (b['shopName'] ?? '').toString().trim();
    if (name.length < 2) errors['shopName'] = 'أدخل اسم المحل';
    final interest = b['interest'] is num ? (b['interest'] as num).toInt() : int.tryParse('${b['interest']}');
    if (interest == null || interest < 1 || interest > 5) errors['interest'] = 'اختر مدى اهتمام صاحب المحل';
    final lat = (b['latitude'] as num?)?.toDouble();
    final lng = (b['longitude'] as num?)?.toDouble();
    if (lat == null || lng == null) errors['latitude'] = 'حدد الموقع على الخريطة';
    if (errors.isNotEmpty) throw _FakeError(400, 'validation', 'تحقق من الحقول', {'fields': errors});
    double? n(Object? v) => v is num ? v.toDouble() : null;
    return {
      'latitude': lat,
      'longitude': lng,
      'shopName': name,
      'description': (b['description']?.toString().trim().isEmpty ?? true) ? null : b['description'].toString().trim(),
      'interest': interest,
      'phone': (b['phone']?.toString().isEmpty ?? true) ? null : b['phone'].toString(),
      'goodsValue': b['goodsValue'] is num ? (b['goodsValue'] as num).toInt() : null,
      'lengthM': n(b['lengthM']),
      'widthM': n(b['widthM']),
      'agentLatitude': n(b['agentLatitude']),
      'agentLongitude': n(b['agentLongitude']),
    };
  }

  Map<String, dynamic> _prospectFor(_FakeAgent viewer, Map<String, dynamic> p) {
    final l = p['lengthM'] as double?;
    final w = p['widthM'] as double?;
    return {
      ...p,
      'interestLabel': _interestLabels[p['interest'] as int],
      'areaM2': l != null && w != null ? double.parse((l * w).toStringAsFixed(2)) : null,
      'isMine': p['createdByUserId'] == viewer.id,
      'updatedAt': p['createdAt'],
    };
  }

  /// A few shops around the first area shown: some pinned by a colleague (زينب), some by the demo agent.
  void _seedProspects(double lat, double lng) {
    if (_prospectsSeeded) return;
    _prospectsSeeded = true;
    final samples = [
      (0.0021, 0.0017, 'أسواق الرافدين', 'مواد غذائية بالجملة', 5, '07812345670', 45000000, 12.0, 6.0, 43, 1),
      (-0.0016, 0.0024, 'معرض النور للأدوات المنزلية', 'أدوات منزلية وكهربائيات', 4, '07723456781', 30000000, 9.0, 5.0, 43, 2),
      (0.0009, -0.0027, 'بقالة أبو حسن', 'بقالة صغيرة', 2, null, 6000000, 4.0, 3.0, 42, 1),
      (-0.0031, -0.0008, 'محلات الفرات للملابس', 'ملابس رجالية', 3, '07501112233', null, null, null, 43, 3),
      (0.0034, -0.0012, 'صيدلية الشفاء', null, 1, null, null, null, null, 42, 5),
    ];
    for (final (dLat, dLng, name, desc, interest, phone, goods, len, wid, by, daysAgo) in samples) {
      final agent = _agents.values.firstWhere((a) => a.id == by);
      _prospects.add({
        'id': _idSeq++,
        'latitude': double.parse((lat + dLat).toStringAsFixed(6)),
        'longitude': double.parse((lng + dLng).toStringAsFixed(6)),
        'shopName': name,
        'description': desc,
        'interest': interest,
        'phone': phone,
        'goodsValue': goods,
        'lengthM': len,
        'widthM': wid,
        'createdByUserId': by,
        'createdByName': '${agent.firstName} ${agent.fatherName} ${agent.grandfatherName}',
        'createdAt': _iso(_now.subtract(Duration(days: daysAgo, hours: 2))),
      });
    }
  }

  // ---------------------------------------------------------------- work day

  bool _isWorking(_FakeAgent u) => u.sessions.any((s) => s['endedAt'] == null);

  bool _addPoint(_FakeAgent u, Object? lat, Object? lng, DateTime at) {
    final la = lat is num ? lat.toDouble() : double.tryParse('$lat');
    final ln = lng is num ? lng.toDouble() : double.tryParse('$lng');
    if (la == null || ln == null || (la == 0 && ln == 0) || !_isWorking(u)) return false;
    u.points.add({'lat': la, 'lng': ln, 'at': _iso(at), 'accuracy': 8});
    return true;
  }

  /// A finished morning shift with a walking route around the phone's position, so the map is not empty in the demo.
  void _seedMorningRoute(_FakeAgent u, double lat, double lng) {
    if (u.routeSeeded || u.sessions.isNotEmpty) return;
    u.routeSeeded = true;
    // A 1h35 shift from 08:10, or ending 20 minutes ago when the app is opened earlier than 09:45.
    final local = DateTime.now();
    var start = DateTime(local.year, local.month, local.day, 8, 10).toUtc();
    var end = start.add(const Duration(hours: 1, minutes: 35));
    if (end.isAfter(_now.subtract(const Duration(minutes: 20)))) {
      end = _now.subtract(const Duration(minutes: 20));
      start = end.subtract(const Duration(hours: 1, minutes: 35));
    }
    u.sessions.add({'id': _idSeq++, 'startedAt': _iso(start), 'endedAt': _iso(end), 'endedBy': 'user'});
    // A loop of ~2 km through nearby streets, ending a few hundred metres from the start.
    var la = lat - 0.006;
    var ln = lng - 0.004;
    const steps = 48;
    for (var i = 0; i <= steps; i++) {
      final phase = i / steps;
      if (phase < 0.3) {
        la += 0.00028;
      } else if (phase < 0.55) {
        ln += 0.00030;
      } else if (phase < 0.8) {
        la -= 0.00018;
        ln += 0.00008;
      } else {
        ln -= 0.00022;
      }
      la += (_rnd.nextDouble() - 0.5) * 0.00006;
      ln += (_rnd.nextDouble() - 0.5) * 0.00006;
      u.points.add({
        'lat': double.parse(la.toStringAsFixed(6)),
        'lng': double.parse(ln.toStringAsFixed(6)),
        'at': _iso(start.add(Duration(seconds: (end.difference(start).inSeconds * phase).round()))),
        'accuracy': 6 + _rnd.nextInt(10),
      });
    }
  }

  Map<String, dynamic> _workDay(_FakeAgent u) {
    final sessions = [
      for (final s in u.sessions)
        {
          ...s,
          'minutes': (DateTime.tryParse('${s['endedAt']}') ?? _now).difference(DateTime.parse(s['startedAt'] as String)).inMinutes,
        },
    ];
    final current = sessions.where((s) => s['endedAt'] == null).firstOrNull;
    final local = DateTime.now();
    return {
      'date': '${local.year}-${local.month.toString().padLeft(2, '0')}-${local.day.toString().padLeft(2, '0')}',
      'isWorking': current != null,
      'currentSession': current,
      'sessions': sessions,
      'totalMinutes': sessions.fold<int>(0, (s, x) => s + (x['minutes'] as int)),
      'firstStartedAt': sessions.isEmpty ? null : sessions.first['startedAt'],
      'lastEndedAt': current == null && sessions.isNotEmpty ? sessions.last['endedAt'] : null,
      'track': u.points,
    };
  }
}
