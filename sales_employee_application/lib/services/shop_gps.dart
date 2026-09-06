import 'dart:io';

import 'package:flutter/foundation.dart';
import 'package:permission_handler/permission_handler.dart';
import 'package:sales_employee_application/services/geo_fix.dart';
import 'package:sales_employee_application/services/location.dart';
import 'package:sales_employee_application/tracking/tracking_channel.dart';

class ShopGps {
  ShopGps._();

  @visibleForTesting
  static GeoFix? debugFix;

  static bool isValid(double? latitude, double? longitude) {
    if (latitude == null || longitude == null) return false;
    if (latitude.isNaN || longitude.isNaN) return false;
    return latitude >= -90 && latitude <= 90 && longitude >= -180 && longitude <= 180;
  }

  static Future<GeoFix> capture() async {
    final debug = debugFix;
    if (debug != null && isValid(debug.latitude, debug.longitude)) {
      return debug;
    }

    final status = await Permission.locationWhenInUse.request();
    if (!status.isGranted && !status.isLimited) {
      throw Exception('يجب السماح بصلاحية الموقع لتحديد موقع المحل.');
    }

    GeoFix? fix;
    if (!kIsWeb && Platform.isAndroid) {
      fix = await TrackingChannel.currentFix();
    } else if (!kIsWeb && Platform.isWindows) {
      fix = await readLocation();
    } else {
      fix = await TrackingChannel.currentFix() ?? await readLocation();
    }

    if (fix == null || !isValid(fix.latitude, fix.longitude)) {
      throw Exception('تعذر تحديد الموقع. تأكد أن GPS يعمل ثم أعد المحاولة.');
    }
    return fix;
  }
}
