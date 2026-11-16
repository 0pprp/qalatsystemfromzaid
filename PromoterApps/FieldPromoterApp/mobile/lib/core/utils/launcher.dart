import 'package:flutter/material.dart';
import 'package:url_launcher/url_launcher.dart';

import '../widgets/state_views.dart';

abstract final class Launch {
  static Future<void> url(BuildContext context, String? url, {bool external = true}) async {
    if (url == null || url.isEmpty) return;
    final uri = Uri.tryParse(url);
    if (uri == null) return;
    final ok = await launchUrl(uri, mode: external ? LaunchMode.externalApplication : LaunchMode.inAppBrowserView)
        .catchError((_) => false);
    if (!ok && context.mounted) context.showSnack('تعذّر فتح الرابط', error: true);
  }

  static Future<void> call(BuildContext context, String? phone) async {
    if (phone == null || phone.isEmpty) return;
    final ok = await launchUrl(Uri(scheme: 'tel', path: phone)).catchError((_) => false);
    if (!ok && context.mounted) context.showSnack('تعذّر إجراء الاتصال', error: true);
  }

  /// [number] in international form without "+", e.g. `9647801112233`.
  static Future<void> whatsapp(BuildContext context, String? number, {String? text}) async {
    if (number == null || number.isEmpty) return;
    final digits = number.replaceAll(RegExp(r'[^\d]'), '');
    final uri = Uri.https('wa.me', '/$digits', text == null ? null : {'text': text});
    final ok = await launchUrl(uri, mode: LaunchMode.externalApplication).catchError((_) => false);
    if (!ok && context.mounted) context.showSnack('تعذّر فتح واتساب', error: true);
  }
}
