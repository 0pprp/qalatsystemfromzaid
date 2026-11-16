import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';
import '../../../core/network/json.dart';
import '../../../core/providers.dart';

/// How interested the shop owner seemed, 1 (not at all) … 5 (very).
class InterestLevel {
  const InterestLevel(this.value, this.label, this.color);

  final int value;
  final String label;
  final Color color;

  static const all = [
    InterestLevel(1, 'غير مهتم إطلاقاً', Color(0xFFDC2626)),
    InterestLevel(2, 'قليل الاهتمام', Color(0xFFF97316)),
    InterestLevel(3, 'مهتم نوعاً ما', Color(0xFFEAB308)),
    InterestLevel(4, 'مهتم', Color(0xFF22C55E)),
    InterestLevel(5, 'مهتم جداً', Color(0xFF15803D)),
  ];

  static InterestLevel of(int value) => all.firstWhere((l) => l.value == value, orElse: () => all[2]);
}

/// «محل محتمل»: a shop an agent pinned for a later visit. Every agent sees every pin.
class Prospect {
  const Prospect({
    required this.id,
    required this.lat,
    required this.lng,
    required this.shopName,
    this.description,
    required this.interest,
    this.phone,
    this.goodsValue,
    this.lengthM,
    this.widthM,
    this.areaM2,
    required this.createdByName,
    required this.createdAt,
    this.updatedAt,
    required this.isMine,
    this.agentLat,
    this.agentLng,
  });

  final int id;
  final double lat;
  final double lng;
  final String shopName;
  final String? description;
  final int interest;
  final String? phone;
  final int? goodsValue;
  final double? lengthM;
  final double? widthM;
  final double? areaM2;
  final String createdByName;
  final DateTime createdAt;
  final DateTime? updatedAt;
  final bool isMine;

  /// Where the agent stood when pinning (null for older pins).
  final double? agentLat;
  final double? agentLng;

  InterestLevel get level => InterestLevel.of(interest);

  factory Prospect.fromJson(Json j) => Prospect(
        id: asInt(j['id']),
        lat: asDoubleOrNull(j['latitude']) ?? 0,
        lng: asDoubleOrNull(j['longitude']) ?? 0,
        shopName: asString(j['shopName']),
        description: asStringOrNull(j['description']),
        interest: asInt(j['interest'], 3),
        phone: asStringOrNull(j['phone']),
        goodsValue: asIntOrNull(j['goodsValue']),
        lengthM: asDoubleOrNull(j['lengthM']),
        widthM: asDoubleOrNull(j['widthM']),
        areaM2: asDoubleOrNull(j['areaM2']),
        createdByName: asString(j['createdByName']),
        createdAt: asDate(j['createdAt']) ?? DateTime.now().toUtc(),
        updatedAt: asDate(j['updatedAt']),
        agentLat: asDoubleOrNull(j['agentLatitude']),
        agentLng: asDoubleOrNull(j['agentLongitude']),
        isMine: asBool(j['isMine']),
      );
}

/// What the agent fills in the «إضافة محل» form (name, interest, location; the rest is kept from older pins on edit).
class ProspectInput {
  const ProspectInput({
    required this.lat,
    required this.lng,
    required this.shopName,
    this.description,
    required this.interest,
    this.phone,
    this.goodsValue,
    this.lengthM,
    this.widthM,
    this.agentLat,
    this.agentLng,
  });

  final double lat;
  final double lng;
  final String shopName;
  final String? description;
  final int interest;
  final String? phone;
  final int? goodsValue;
  final double? lengthM;
  final double? widthM;

  /// Where the agent's phone was when the shop was pinned (the pin itself may have been moved on the map).
  final double? agentLat;
  final double? agentLng;

  Json toJson() => {
        'latitude': double.parse(lat.toStringAsFixed(6)),
        'longitude': double.parse(lng.toStringAsFixed(6)),
        'shopName': shopName,
        'description': description,
        'interest': interest,
        'phone': phone,
        'goodsValue': goodsValue,
        'lengthM': lengthM,
        'widthM': widthM,
        if (agentLat != null && agentLng != null) 'agentLatitude': double.parse(agentLat!.toStringAsFixed(6)),
        if (agentLat != null && agentLng != null) 'agentLongitude': double.parse(agentLng!.toStringAsFixed(6)),
      };
}

class ProspectsRepository {
  ProspectsRepository(this._api);

  final ApiClient _api;

  /// Every agent's pins inside the visible map area.
  Future<List<Prospect>> inArea({required double minLat, required double minLng, required double maxLat, required double maxLng}) async =>
      asList(
        await _api.get('prospects', query: {
          'minLat': minLat.toStringAsFixed(5),
          'minLng': minLng.toStringAsFixed(5),
          'maxLat': maxLat.toStringAsFixed(5),
          'maxLng': maxLng.toStringAsFixed(5),
        }),
        Prospect.fromJson,
      );

  /// «محلاتي»: the agent's own pins, newest first; [interest] keeps one level, [q] searches name/type/phone.
  /// [from] / [to] are Iraq days (`yyyy-MM-dd`, both included).
  Future<Paged<Prospect>> mine({int page = 1, int? interest, String? q, String? from, String? to}) async => Paged.fromJson(
        await _api.get('prospects/mine', query: {
          'page': page,
          'interest': ?interest,
          if (q != null && q.trim().isNotEmpty) 'q': q.trim(),
          'from': ?from,
          'to': ?to,
        }),
        Prospect.fromJson,
      );

  Future<Prospect> get(int id) async => Prospect.fromJson(asJson(await _api.get('prospects/$id')));

  Future<Prospect> create(ProspectInput input) async => Prospect.fromJson(asJson(await _api.post('prospects', body: input.toJson())));

  Future<Prospect> update(int id, ProspectInput input) async => Prospect.fromJson(asJson(await _api.put('prospects/$id', body: input.toJson())));

  Future<void> delete(int id) async => _api.delete('prospects/$id');
}

final prospectsRepositoryProvider = Provider((ref) => ProspectsRepository(ref.watch(apiClientProvider)));

/// Bumped after a shop is pinned outside the «الدوام» map (e.g. from «ملخصي») so the map reloads its pins.
class ProspectsVersion extends Notifier<int> {
  @override
  int build() => 0;

  void bump() => state++;
}

final prospectsVersionProvider = NotifierProvider<ProspectsVersion, int>(ProspectsVersion.new);
