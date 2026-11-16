import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';
import 'package:sales_employee_application/data/archive_api.dart';
import 'package:sales_employee_application/data/archive_models.dart';
import 'package:sales_employee_application/services/api_client.dart';
import 'package:sales_employee_application/utils/app_theme.dart';
import 'package:sales_employee_application/utils/sales_format.dart';
import 'package:sales_employee_application/widgets/tappable_phone.dart';

class ArchiveBundleScreen extends StatefulWidget {
  const ArchiveBundleScreen({super.key, required this.bundleId});

  final int bundleId;

  @override
  State<ArchiveBundleScreen> createState() => _ArchiveBundleScreenState();
}

class _ArchiveBundleScreenState extends State<ArchiveBundleScreen> {
  final _note = TextEditingController();
  ArchiveBundle? _bundle;
  List<ArchiveRecipient> _recipients = const [];
  String? _recipient;
  bool _loading = true;
  bool _sending = false;
  String? _error;
  final List<({List<int> bytes, String name})> _photos = [];
  static const _maxPhotos = 8;

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _note.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final bundle = await ArchiveApi.getBundle(widget.bundleId);
      var recipients = _recipients;
      if (bundle.canSend) {
        try {
          recipients = await ArchiveApi.recipients();
        } catch (_) {
          recipients = const [];
        }
      }
      if (!mounted) return;
      setState(() {
        _bundle = bundle;
        _recipients = recipients;
        if (_recipient == null || !recipients.any((r) => r.name == _recipient)) {
          _recipient = recipients.length == 1 ? recipients.first.name : null;
        }
        _loading = false;
      });
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() {
        _error = e.message;
        _loading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _error = 'تعذر تحميل الحزمة';
        _loading = false;
      });
    }
  }

  Future<void> _addFiles(List<XFile> picked) async {
    if (picked.isEmpty) return;
    final next = [..._photos];
    var capped = false;
    for (final file in picked) {
      if (next.length >= _maxPhotos) {
        capped = true;
        break;
      }
      final bytes = await file.readAsBytes();
      if (bytes.isEmpty) continue;
      final name = file.name.trim().isEmpty ? 'delivery.jpg' : file.name.trim();
      next.add((bytes: bytes, name: name));
    }
    if (!mounted) return;
    setState(() {
      _photos
        ..clear()
        ..addAll(next);
    });
    if (capped) {
      _toast('يمكن إرفاق 8 صور كحد أقصى.');
    }
  }

  Future<void> _choosePhoto() async {
    if (_photos.length >= _maxPhotos) {
      _toast('يمكن إرفاق 8 صور كحد أقصى.');
      return;
    }
    final source = await showModalBottomSheet<ImageSource>(
      context: context,
      builder: (ctx) => SafeArea(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            ListTile(
              leading: const Icon(Icons.photo_camera_outlined),
              title: const Text('التقاط صورة'),
              onTap: () => Navigator.pop(ctx, ImageSource.camera),
            ),
            ListTile(
              leading: const Icon(Icons.photo_library_outlined),
              title: const Text('اختيار من المعرض'),
              onTap: () => Navigator.pop(ctx, ImageSource.gallery),
            ),
          ],
        ),
      ),
    );
    if (source == null) return;
    if (source == ImageSource.gallery) {
      final picked = await ImagePicker().pickMultiImage(imageQuality: 85, maxWidth: 1600);
      await _addFiles(picked);
      return;
    }
    final picked = await ImagePicker().pickImage(
      source: ImageSource.camera,
      imageQuality: 85,
      maxWidth: 1600,
    );
    if (picked != null) {
      await _addFiles([picked]);
    }
  }

  Future<void> _send() async {
    final bundle = _bundle;
    if (bundle == null || !bundle.canSend || _sending) return;
    final recipient = _recipient;
    if (recipient == null || recipient.isEmpty) {
      _toast('اختر مسؤول اللجنة أو مساعده الذي سيستلم الأرشيف.');
      return;
    }
    setState(() => _sending = true);
    try {
      await ArchiveApi.send(
        bundleId: bundle.bundleId,
        recipientName: recipient,
        note: _note.text.trim(),
        photos: _photos,
      );
      if (!mounted) return;
      _toast(bundle.phase == 'NeedsFix' ? 'أُعيد إرسال الحزمة.' : 'تم إرسال الحزمة.');
      Navigator.of(context).pop(true);
      return;
    } on ApiException catch (e) {
      _toast(e.message);
    } catch (_) {
      _toast('تعذر إرسال الحزمة.');
    } finally {
      if (mounted) setState(() => _sending = false);
    }
  }

  void _toast(String message) {
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));
  }

  @override
  Widget build(BuildContext context) {
    final bundle = _bundle;
    return Scaffold(
      appBar: AppBar(title: Text(bundle == null ? 'الارشيف' : 'حزمة ${bundle.bundleNumber}')),
      body: _loading
          ? const Center(child: CircularProgressIndicator(color: AppColors.darkGreen))
          : _error != null
              ? Center(child: Padding(padding: const EdgeInsets.all(24), child: Text(_error!, textAlign: TextAlign.center)))
              : ListView(
                  padding: const EdgeInsets.all(16),
                  children: [
                    Text(archivePhaseLabel(bundle!.phase), style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w700)),
                    const SizedBox(height: 4),
                    Text(bundle.periodLabel, style: const TextStyle(color: AppColors.muted)),
                    if (bundle.phase == 'Open') ...[
                      const SizedBox(height: 12),
                      Text('لا يمكن إرسال هذه الحزمة قبل انتهاء ${formatArchiveDate(bundle.periodEnd)}.'),
                    ],
                    if (bundle.phase == 'NeedsFix') ...[
                      const SizedBox(height: 12),
                      const Text(
                        'الحزمة غير مكتملة. الأوراق المعتمدة تبقى كما هي. صحّح الأوراق التي فيها مشكلة ثم أعد إرسال الحزمة.',
                        style: TextStyle(color: AppColors.danger, fontWeight: FontWeight.w600),
                      ),
                    ],
                    if ((bundle.recipientName ?? '').trim().isNotEmpty) ...[
                      const SizedBox(height: 16),
                      Text('المستلم: ${bundle.recipientName}'),
                    ] else if ((bundle.intermediateName ?? '').trim().isNotEmpty) ...[
                      const SizedBox(height: 16),
                      Text('الوسيط: ${bundle.intermediateName}'),
                      if ((bundle.intermediatePhone ?? '').trim().isNotEmpty)
                        TappablePhone(bundle.intermediatePhone),
                    ],
                    if ((bundle.note ?? '').trim().isNotEmpty) ...[
                      const SizedBox(height: 8),
                      Text('ملاحظة الإرسال: ${bundle.note}'),
                    ],
                    const SizedBox(height: 16),
                    ...bundle.items.map(_itemCard),
                    if (bundle.canSend) ...[
                      const SizedBox(height: 8),
                      const Text('تسليم إلى', style: TextStyle(fontSize: 16, fontWeight: FontWeight.w700)),
                      const SizedBox(height: 8),
                      if (_recipients.isEmpty)
                        const Text(
                          'لا يوجد مسؤول لجنة مراقبة أو مساعد لفرعك. راجع الإدارة.',
                          style: TextStyle(color: AppColors.danger),
                        )
                      else
                        DropdownButtonFormField<String>(
                          initialValue: _recipient,
                          isExpanded: true,
                          decoration: const InputDecoration(labelText: 'المستلم'),
                          items: [
                            for (final r in _recipients)
                              DropdownMenuItem(value: r.name, child: Text(r.label, overflow: TextOverflow.ellipsis)),
                          ],
                          onChanged: _sending ? null : (v) => setState(() => _recipient = v),
                        ),
                      const SizedBox(height: 8),
                      TextField(
                        controller: _note,
                        decoration: const InputDecoration(labelText: 'ملاحظة (اختياري)'),
                        minLines: 2,
                        maxLines: 4,
                      ),
                      const SizedBox(height: 12),
                      OutlinedButton.icon(
                        onPressed: _sending ? null : _choosePhoto,
                        icon: const Icon(Icons.photo_camera_outlined),
                        label: Text(_photos.isEmpty ? 'إضافة صور التسليم (اختياري)' : 'إضافة صورة أخرى'),
                      ),
                      if (bundle.hasPhoto && _photos.isEmpty)
                        const Padding(
                          padding: EdgeInsets.only(top: 8),
                          child: Text('الصور المرسلة سابقاً تبقى إن لم تضف صوراً جديدة.'),
                        ),
                      if (bundle.hasPhoto && _photos.isNotEmpty)
                        const Padding(
                          padding: EdgeInsets.only(top: 8),
                          child: Text('الصور الجديدة تستبدل الصور السابقة.'),
                        ),
                      if (_photos.isNotEmpty) ...[
                        const SizedBox(height: 8),
                        Text(
                          _photos.length == 1 ? 'صورة واحدة' : '${_photos.length} صور',
                          style: const TextStyle(color: AppColors.muted),
                        ),
                        const SizedBox(height: 8),
                        Wrap(
                          spacing: 8,
                          runSpacing: 8,
                          children: [
                            for (var i = 0; i < _photos.length; i++)
                              Stack(
                                children: [
                                  ClipRRect(
                                    borderRadius: BorderRadius.circular(12),
                                    child: Image.memory(
                                      Uint8List.fromList(_photos[i].bytes),
                                      width: 108,
                                      height: 108,
                                      fit: BoxFit.cover,
                                    ),
                                  ),
                                  Positioned(
                                    top: 4,
                                    left: 4,
                                    child: Material(
                                      color: Colors.black54,
                                      shape: const CircleBorder(),
                                      child: InkWell(
                                        customBorder: const CircleBorder(),
                                        onTap: _sending ? null : () => setState(() => _photos.removeAt(i)),
                                        child: const Padding(
                                          padding: EdgeInsets.all(4),
                                          child: Icon(Icons.close, size: 16, color: Colors.white),
                                        ),
                                      ),
                                    ),
                                  ),
                                ],
                              ),
                          ],
                        ),
                      ],
                      const SizedBox(height: 16),
                      FilledButton(
                        onPressed: _sending || _recipients.isEmpty ? null : _send,
                        child: Text(_sending
                            ? 'جارٍ الإرسال...'
                            : bundle.phase == 'NeedsFix'
                                ? 'إعادة إرسال الحزمة'
                                : 'إرسال الحزمة'),
                      ),
                    ],
                  ],
                ),
    );
  }

  Widget _itemCard(ArchiveItem item) {
    final refused = item.status == 'Refused';
    // Notes on papers still in the committee/archive chain are internal to them.
    final showNote = refused || item.status == 'Pending';
    final approved = item.status == 'Approved';
    return Container(
      margin: const EdgeInsets.only(bottom: 10),
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: refused ? AppColors.danger : AppColors.line),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(child: Text(item.customerName, style: const TextStyle(fontWeight: FontWeight.w700))),
              Text(
                archiveItemStatusLabel(item.status),
                style: TextStyle(
                  color: refused ? AppColors.danger : (approved ? AppColors.darkGreen : AppColors.muted),
                  fontWeight: FontWeight.w700,
                  fontSize: 12,
                ),
              ),
            ],
          ),
          const SizedBox(height: 4),
          Text('${formatArchiveDate(item.saleDate)} · ${MoneyFormat.iqd(item.salePrice)}'),
          Text(item.itemName),
          Text('القائمة: ${item.listName}'),
          if (showNote && (item.refusalNote ?? '').trim().isNotEmpty)
            Padding(
              padding: const EdgeInsets.only(top: 6),
              child: Text(
                refused ? 'الملاحظة: ${item.refusalNote}' : 'آخر ملاحظة: ${item.refusalNote}',
                style: const TextStyle(color: AppColors.danger),
              ),
            ),
        ],
      ),
    );
  }
}
