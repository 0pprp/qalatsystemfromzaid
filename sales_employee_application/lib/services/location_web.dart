// ignore_for_file: avoid_web_libraries_in_flutter, deprecated_member_use
import 'dart:html';

import 'package:sales_employee_application/services/geo_fix.dart';

Future<GeoFix?> readLocation() async {
  try {
    final pos = await window.navigator.geolocation.getCurrentPosition();
    final coords = pos.coords;
    if (coords == null) return null;
    final lat = coords.latitude;
    final lng = coords.longitude;
    if (lat == null || lng == null) return null;
    return GeoFix(lat.toDouble(), lng.toDouble(), coords.accuracy?.toDouble());
  } catch (_) {
    return null;
  }
}
