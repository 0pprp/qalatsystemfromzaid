/// Tolerant JSON readers: the backend is built in parallel, so models must not crash
/// on a missing or slightly differently typed field.
typedef Json = Map<String, dynamic>;

int asInt(Object? v, [int fallback = 0]) {
  if (v is int) return v;
  if (v is num) return v.toInt();
  if (v is String) return int.tryParse(v) ?? double.tryParse(v)?.toInt() ?? fallback;
  return fallback;
}

int? asIntOrNull(Object? v) {
  if (v == null) return null;
  if (v is int) return v;
  if (v is num) return v.toInt();
  if (v is String) return int.tryParse(v);
  return null;
}

double? asDoubleOrNull(Object? v) {
  if (v is num) return v.toDouble();
  if (v is String) return double.tryParse(v);
  return null;
}

String asString(Object? v, [String fallback = '']) => v == null ? fallback : v.toString();

String? asStringOrNull(Object? v) {
  if (v == null) return null;
  final s = v.toString();
  return s.isEmpty ? null : s;
}

bool asBool(Object? v, [bool fallback = false]) {
  if (v is bool) return v;
  if (v is num) return v != 0;
  if (v is String) return v.toLowerCase() == 'true' || v == '1';
  return fallback;
}

/// Server times are UTC. A value without a zone ("2026-11-06T14:25:00") is read as UTC too, never as the phone's time.
DateTime? asDate(Object? v) {
  if (v == null) return null;
  if (v is DateTime) return v;
  final text = v.toString().trim();
  if (text.isEmpty) return null;
  final hasZone = RegExp(r'(Z|[+-]\d{2}:?\d{2})$', caseSensitive: false).hasMatch(text);
  final hasTime = text.contains('T') || text.contains(' ');
  return DateTime.tryParse(hasTime && !hasZone ? '${text}Z' : text);
}

Json asJson(Object? v) => v is Map ? Map<String, dynamic>.from(v) : <String, dynamic>{};

List<T> asList<T>(Object? v, T Function(Json json) parse) {
  if (v is! List) return <T>[];
  return v.whereType<Map>().map((e) => parse(Map<String, dynamic>.from(e))).toList();
}

/// Paged response envelope: `{ items, total, page, pageSize }`.
class Paged<T> {
  const Paged({
    required this.items,
    required this.total,
    required this.page,
    required this.pageSize,
    this.extra = const {},
  });

  final List<T> items;
  final int total;
  final int page;
  final int pageSize;

  /// Any extra top-level field (e.g. `unreadCount` in notifications).
  final Json extra;

  bool get hasMore => page * pageSize < total;

  factory Paged.fromJson(Object? data, T Function(Json json) parse) {
    final json = asJson(data);
    final items = asList(json['items'], parse);
    return Paged(
      items: items,
      total: asInt(json['total'], items.length),
      page: asInt(json['page'], 1),
      pageSize: asInt(json['pageSize'], 20),
      extra: json,
    );
  }
}
