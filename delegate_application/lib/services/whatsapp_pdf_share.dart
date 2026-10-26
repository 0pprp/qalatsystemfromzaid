import 'dart:io';

import 'package:flutter/foundation.dart';
import 'package:flutter/services.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:url_launcher/url_launcher.dart';

/// Send a PDF into the customer's WhatsApp chat (by phone number).
///
/// WhatsApp only routes a file share (jid) to a number that already has a chat;
/// otherwise it shows the contacts picker. Numbers are remembered here once a
/// chat was opened via link and a first message was sent.
class WhatsAppPdfShare {
  static const _channel = MethodChannel('delegate_application/whatsapp_share');
  static const _readyKey = 'wa_chat_ready_phones';

  static Future<bool> isChatReady(String phone) async {
    final normalized = normalizeIraqPhone(phone);
    if (normalized.isEmpty) return false;
    final prefs = await SharedPreferences.getInstance();
    return (prefs.getStringList(_readyKey) ?? const []).contains(normalized);
  }

  static Future<void> markChatReady(String phone) async {
    final normalized = normalizeIraqPhone(phone);
    if (normalized.isEmpty) return;
    final prefs = await SharedPreferences.getInstance();
    final list = prefs.getStringList(_readyKey) ?? <String>[];
    if (list.contains(normalized)) return;
    await prefs.setStringList(_readyKey, [...list, normalized]);
  }

  static String normalizeIraqPhone(String raw) {
    var digits = raw.replaceAll(RegExp(r'[^\d]'), '');
    if (digits.startsWith('964')) return digits;
    if (digits.startsWith('0') && digits.length >= 10) {
      return '964${digits.substring(1)}';
    }
    if (digits.length == 10 && digits.startsWith('7')) {
      return '964$digits';
    }
    if (digits.length == 9 && digits.startsWith('7')) {
      return '964$digits';
    }
    return digits;
  }

  /// Opens WhatsApp for [phone] with [pdfFile] attached when possible.
  ///
  /// Returns:
  /// - `pdf` — customer chat opened with PDF ready to send
  /// - `chat` — wa.me opened (text only; attach via 📎)
  /// - `null` — failed
  static Future<String?> sendPdfToPhone({
    required String phone,
    required File pdfFile,
    String? caption,
  }) async {
    final normalized = normalizeIraqPhone(phone);
    if (normalized.isEmpty || !pdfFile.existsSync()) {
      // ignore: avoid_print
      print('WhatsAppPdfShare: bad phone/file phone="$phone" exists=${pdfFile.existsSync()}');
      return null;
    }

    // ignore: avoid_print
    print('WhatsAppPdfShare: pdf→chat $normalized path=${pdfFile.path}');

    if (Platform.isAndroid) {
      try {
        final mode = await _channel.invokeMethod<String>('sharePdfToWhatsApp', {
          'phone': normalized,
          'filePath': pdfFile.path,
          if (caption != null && caption.isNotEmpty) 'text': caption,
        });
        // ignore: avoid_print
        print('WhatsAppPdfShare: mode=$mode');
        if (mode == 'pdf' || mode == 'chat') return mode;
      } on PlatformException catch (e) {
        // ignore: avoid_print
        print('WhatsAppPdfShare: ${e.code} ${e.message}');
      } catch (e) {
        // ignore: avoid_print
        print('WhatsAppPdfShare: $e');
      }
    }

    final opened = await openCustomerChat(normalized, text: caption);
    return opened ? 'chat' : null;
  }

  static Future<bool> openCustomerChat(String phone, {String? text}) async {
    final normalized = normalizeIraqPhone(phone);
    if (normalized.isEmpty) return false;

    if (Platform.isAndroid) {
      try {
        final ok = await _channel.invokeMethod<bool>('openWhatsAppChat', {
          'phone': normalized,
          if (text != null && text.isNotEmpty) 'text': text,
        });
        if (ok == true) return true;
      } catch (e) {
        debugPrint('openWhatsAppChat: $e');
      }
    }

    final uris = <Uri>[
      if (text != null && text.trim().isNotEmpty)
        Uri.parse(
          'whatsapp://send?phone=$normalized&text=${Uri.encodeComponent(text.trim())}',
        )
      else
        Uri.parse('whatsapp://send?phone=$normalized'),
      Uri.parse(
        text != null && text.trim().isNotEmpty
            ? 'https://wa.me/$normalized?text=${Uri.encodeComponent(text.trim())}'
            : 'https://wa.me/$normalized',
      ),
    ];
    for (final uri in uris) {
      try {
        if (await launchUrl(uri, mode: LaunchMode.externalApplication)) {
          return true;
        }
      } catch (_) {}
    }
    return false;
  }
}
