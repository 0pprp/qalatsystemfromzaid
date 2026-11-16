import 'dart:io';

import 'package:flutter/material.dart';
import 'package:follower_application/shop_capture/shop_capture_api.dart';
import 'package:follower_application/shop_capture/shop_capture_widgets.dart';
import 'package:follower_application/utils/AppTheme.dart';
import 'package:image_picker/image_picker.dart';

/// Shop GPS + photo collected inside the «مصادقة المبيع» dialog (both required).
class ShopCaptureDraft {
  CapturedPoint? point;
  XFile? photo;

  String? missing() {
    if (point == null) return 'موقع المحل مطلوب — اضغط «تحديد الموقع» وأنت عند المحل';
    if (photo == null) return 'صورة المحل مطلوبة';
    return null;
  }

  /// Uploads the photo, then returns the body fields for PendingSales/Authenticate.
  Future<Map<String, dynamic>> submit(int listId, int customerId) async {
    await ShopCaptureApi.uploadPhoto(listId, customerId, photo!);
    return {
      'latitude': point!.latitude,
      'longitude': point!.longitude,
      'accuracy': point!.accuracy,
    };
  }
}

class ShopCaptureFields extends StatelessWidget {
  const ShopCaptureFields({super.key, required this.draft, required this.onChanged});

  final ShopCaptureDraft draft;
  final VoidCallback onChanged;

  static const _cairo = TextStyle(fontFamily: 'Cairo');

  @override
  Widget build(BuildContext context) {
    final p = draft.point;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        const SizedBox(height: 10),
        OutlinedButton.icon(
          onPressed: () async {
            final got = await captureLocationWithDialog(context);
            if (got != null) {
              draft.point = got;
              onChanged();
            }
          },
          icon: Icon(p == null ? Icons.add_location_alt : Icons.check_circle,
              color: p == null ? AppTheme.primaryColor : Colors.green.shade700),
          label: Text(
            p == null ? 'تحديد موقع المحل *' : 'تم تحديد الموقع (دقة ${p.accuracy.round()} م) — إعادة',
            style: _cairo.copyWith(fontSize: 13),
          ),
        ),
        const SizedBox(height: 8),
        Row(
          children: [
            Expanded(
              child: OutlinedButton.icon(
                onPressed: () async {
                  final file = await ShopCaptureApi.takePhoto();
                  if (file != null) {
                    draft.photo = file;
                    onChanged();
                  }
                },
                icon: Icon(draft.photo == null ? Icons.photo_camera : Icons.check_circle,
                    color: draft.photo == null ? AppTheme.primaryColor : Colors.green.shade700),
                label: Text(
                  draft.photo == null ? 'صورة المحل *' : 'إعادة التقاط',
                  style: _cairo.copyWith(fontSize: 13),
                ),
              ),
            ),
            if (draft.photo != null) ...[
              const SizedBox(width: 8),
              ClipRRect(
                borderRadius: BorderRadius.circular(8),
                child: Image.file(File(draft.photo!.path), width: 56, height: 56, fit: BoxFit.cover),
              ),
            ],
          ],
        ),
      ],
    );
  }
}
