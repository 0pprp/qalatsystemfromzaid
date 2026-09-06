import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:sales_employee_application/utils/app_theme.dart';

class CustomerDocumentSlot extends StatelessWidget {
  const CustomerDocumentSlot({
    super.key,
    required this.label,
    required this.busy,
    this.bytes,
    this.onCamera,
    this.onGallery,
    this.onDelete,
  });

  final String label;
  final bool busy;
  final List<int>? bytes;
  final VoidCallback? onCamera;
  final VoidCallback? onGallery;
  final VoidCallback? onDelete;

  bool get _hasImage => bytes != null && bytes!.isNotEmpty;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: AppSpacing.md),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(label, style: const TextStyle(fontWeight: FontWeight.w700)),
          const SizedBox(height: 8),
          if (_hasImage) ...[
            ClipRRect(
              borderRadius: BorderRadius.circular(AppRadius.sm),
              child: Image.memory(
                Uint8List.fromList(bytes!),
                height: 140,
                fit: BoxFit.cover,
                errorBuilder: (_, _, _) => const SizedBox(
                  height: 80,
                  child: Center(child: Text('تم اختيار الصورة')),
                ),
              ),
            ),
            const SizedBox(height: 8),
          ],
          if (busy)
            const Padding(
              padding: EdgeInsets.symmetric(vertical: 8),
              child: Center(child: SizedBox(height: 22, width: 22, child: CircularProgressIndicator(strokeWidth: 2))),
            )
          else
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: [
                OutlinedButton.icon(
                  onPressed: onCamera,
                  icon: const Icon(Icons.photo_camera_outlined, size: 18),
                  label: Text(_hasImage ? 'إعادة تصوير' : 'كاميرا'),
                ),
                OutlinedButton.icon(
                  onPressed: onGallery,
                  icon: const Icon(Icons.photo_library_outlined, size: 18),
                  label: Text(_hasImage ? 'استبدال من المعرض' : 'معرض الصور'),
                ),
                if (_hasImage)
                  TextButton.icon(
                    onPressed: onDelete,
                    icon: const Icon(Icons.delete_outline, size: 18),
                    label: const Text('حذف'),
                  ),
              ],
            ),
        ],
      ),
    );
  }
}
