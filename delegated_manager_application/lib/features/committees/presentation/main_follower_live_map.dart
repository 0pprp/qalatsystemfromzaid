import 'dart:async';

import 'package:delegated_manager_application/core/network/api_client.dart';
import 'package:delegated_manager_application/core/utils/json_read.dart';
import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:latlong2/latlong.dart';

class MfLiveRow {
  MfLiveRow({
    required this.userName,
    required this.displayName,
    required this.shiftStatus,
    required this.locationStatus,
    this.latitude,
    this.longitude,
    this.lastAt,
  });

  final String userName;
  final String displayName;
  final String shiftStatus;
  final String locationStatus;
  final double? latitude;
  final double? longitude;
  final String? lastAt;

  bool get hasPoint => latitude != null && longitude != null;

  factory MfLiveRow.fromJson(Map<String, dynamic> json) {
    double? asD(dynamic v) {
      if (v is num) return v.toDouble();
      return double.tryParse(v?.toString() ?? '');
    }

    return MfLiveRow(
      userName: JsonRead.text(json['userName']),
      displayName: JsonRead.text(json['displayName']).isEmpty
          ? JsonRead.text(json['userName'])
          : JsonRead.text(json['displayName']),
      shiftStatus: JsonRead.text(json['shiftStatus']),
      locationStatus: JsonRead.text(json['locationStatus']),
      latitude: asD(json['lastLatitude']),
      longitude: asD(json['lastLongitude']),
      lastAt: JsonRead.optionalText(json['lastLocationAtUtc']),
    );
  }
}

/// Live map of main followers for the committees tab.
class MainFollowerLiveMap extends StatefulWidget {
  const MainFollowerLiveMap({super.key});

  @override
  State<MainFollowerLiveMap> createState() => MainFollowerLiveMapState();
}

class MainFollowerLiveMapState extends State<MainFollowerLiveMap> {
  final _map = MapController();
  List<MfLiveRow> _rows = [];
  List<LatLng> _trail = [];
  String? _selected;
  String? _error;
  Timer? _poll;

  @override
  void initState() {
    super.initState();
    reload();
    _poll = Timer.periodic(const Duration(seconds: 30), (_) => reload());
  }

  @override
  void dispose() {
    _poll?.cancel();
    super.dispose();
  }

  Future<void> reload() async {
    try {
      final raw = await ApiClient.get('delegated-manager/committees/main-followers/live');
      if (!mounted) return;
      final list = raw is List ? raw : const [];
      setState(() {
        _rows = list
            .whereType<Map>()
            .map((e) => MfLiveRow.fromJson(Map<String, dynamic>.from(e)))
            .toList();
        _error = null;
      });
      if (_selected != null) await _loadTrail(_selected!);
    } catch (e) {
      if (mounted) setState(() => _error = e.toString());
    }
  }

  Future<void> _loadTrail(String userName) async {
    try {
      final raw = await ApiClient.get(
        'delegated-manager/committees/main-followers/${Uri.encodeComponent(userName)}/trail',
      );
      if (!mounted) return;
      final list = raw is List ? raw : const [];
      final points = <LatLng>[];
      for (final row in list.whereType<Map>()) {
        final lat = row['latitude'] ?? row['Latitude'];
        final lng = row['longitude'] ?? row['Longitude'];
        final la = lat is num ? lat.toDouble() : double.tryParse('$lat');
        final lo = lng is num ? lng.toDouble() : double.tryParse('$lng');
        if (la != null && lo != null) points.add(LatLng(la, lo));
      }
      setState(() {
        _selected = userName;
        _trail = points;
      });
      if (points.isNotEmpty) {
        _map.move(points.last, 12);
      }
    } catch (e) {
      if (mounted) setState(() => _error = e.toString());
    }
  }

  Color _color(String status) {
    switch (status) {
      case 'Live':
        return const Color(0xFF16A34A);
      case 'Stale':
        return const Color(0xFFEA580C);
      case 'NoShift':
      case 'Closed':
        return const Color(0xFF64748B);
      default:
        return const Color(0xFF0F3D3E);
    }
  }

  String _statusAr(String s) => switch (s) {
        'Live' => 'مباشر',
        'Stale' => 'متأخر',
        'NoLocation' => 'بدون موقع',
        'NoShift' => 'بدون دوام',
        'Offline' => 'غير متصل',
        'Active' => 'دوام نشط',
        'Closed' => 'منتهٍ',
        _ => s,
      };

  @override
  Widget build(BuildContext context) {
    final center = _rows.where((r) => r.hasPoint).isNotEmpty
        ? LatLng(_rows.firstWhere((r) => r.hasPoint).latitude!,
            _rows.firstWhere((r) => r.hasPoint).longitude!)
        : const LatLng(32.0, 44.35); // Iraq center-ish (Najaf area)

    return Column(
      children: [
        if (_error != null)
          Padding(
            padding: const EdgeInsets.all(8),
            child: Text(_error!, style: const TextStyle(color: Colors.red, fontSize: 12)),
          ),
        Expanded(
          child: FlutterMap(
            mapController: _map,
            options: MapOptions(
              initialCenter: center,
              initialZoom: 10,
            ),
            children: [
              TileLayer(
                urlTemplate: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
                userAgentPackageName: 'delegated_manager_application',
              ),
              if (_trail.length >= 2)
                PolylineLayer(
                  polylines: [
                    Polyline(
                      points: _trail,
                      color: const Color(0xFF0F3D3E),
                      strokeWidth: 3,
                    ),
                  ],
                ),
              MarkerLayer(
                markers: [
                  for (final r in _rows.where((e) => e.hasPoint))
                    Marker(
                      point: LatLng(r.latitude!, r.longitude!),
                      width: 40,
                      height: 40,
                      child: GestureDetector(
                        onTap: () => _loadTrail(r.userName),
                        child: Icon(
                          Icons.location_on,
                          color: _color(r.locationStatus),
                          size: 36,
                        ),
                      ),
                    ),
                ],
              ),
            ],
          ),
        ),
        SizedBox(
          height: 140,
          child: ListView.separated(
            padding: const EdgeInsets.all(8),
            itemCount: _rows.length,
            separatorBuilder: (_, __) => const Divider(height: 1),
            itemBuilder: (context, i) {
              final r = _rows[i];
              final selected = _selected == r.userName;
              return ListTile(
                dense: true,
                selected: selected,
                leading: Icon(Icons.circle, size: 14, color: _color(r.locationStatus)),
                title: Text(r.displayName),
                subtitle: Text(
                  '${_statusAr(r.shiftStatus)} · ${_statusAr(r.locationStatus)}'
                  '${r.lastAt != null ? '\n${r.lastAt}' : ''}',
                ),
                onTap: r.hasPoint ? () => _loadTrail(r.userName) : null,
              );
            },
          ),
        ),
      ],
    );
  }
}
