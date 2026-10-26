import 'dart:io';

import 'package:branch_manager_application/core/api.dart';
import 'package:branch_manager_application/theme/bm_theme.dart';
import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';

/// Leave branch: rate monitor lists (0–10), long note, optional attachments.
Future<bool> showBranchVisitDepartSheet(
  BuildContext context, {
  required String visitId,
}) async {
  final notesCtrl = TextEditingController();
  final files = <File>[];
  final scores = <int, int>{};
  var lists = <Map<String, dynamic>>[];
  var loadingLists = true;
  var busy = false;
  String? error;

  try {
    lists = await BmApi.instance.visitRatingLists();
  } catch (e) {
    error = '$e';
  }
  loadingLists = false;

  final ok = await showModalBottomSheet<bool>(
    context: context,
    isScrollControlled: true,
    isDismissible: false,
    enableDrag: false,
    builder: (ctx) {
      return StatefulBuilder(
        builder: (ctx, setLocal) {
          final listIds = lists
              .map((m) =>
                  int.tryParse(
                      '${m['id'] ?? m['Id'] ?? m['delegateId'] ?? 0}') ??
                  0)
              .where((id) => id > 0)
              .toList();
          final allRated =
              listIds.isEmpty || listIds.every(scores.containsKey);

          return Padding(
            padding: EdgeInsets.only(
              left: 20,
              right: 20,
              top: 20,
              bottom: MediaQuery.of(ctx).viewInsets.bottom + 20,
            ),
            child: SingleChildScrollView(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                mainAxisSize: MainAxisSize.min,
                children: [
                  const Text(
                    'مغادرة الفرع',
                    textAlign: TextAlign.center,
                    style: TextStyle(
                      fontFamily: 'Cairo',
                      fontSize: 18,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                  const SizedBox(height: 8),
                  const Text(
                    'قيّم قوائم المراقب (0–10) ثم اكتب ملاحظة المغادرة.',
                    textAlign: TextAlign.center,
                    style: TextStyle(
                      fontFamily: 'Cairo',
                      fontSize: 12,
                      color: Colors.black54,
                    ),
                  ),
                  const SizedBox(height: 16),
                  const Text(
                    'تقييم قوائم المراقب (0–10) — إلزامي',
                    style: TextStyle(
                      fontFamily: 'Cairo',
                      fontWeight: FontWeight.w700,
                      fontSize: 15,
                    ),
                  ),
                  const SizedBox(height: 4),
                  const Text(
                    'تُعرض فقط القوائم الظاهرة عند المراقب (تسديد خلال 40 يوماً).',
                    style: TextStyle(
                      fontFamily: 'Cairo',
                      fontSize: 12,
                      color: Colors.black54,
                    ),
                  ),
                  const SizedBox(height: 8),
                  if (loadingLists)
                    const Padding(
                      padding: EdgeInsets.all(24),
                      child: Center(child: CircularProgressIndicator()),
                    )
                  else if (lists.isEmpty)
                    const Text(
                      'لا توجد قوائم نشطة لدى المراقب في هذا الفرع',
                      style: TextStyle(fontFamily: 'Cairo', color: Colors.grey),
                    )
                  else
                    ...lists.map((list) {
                      final id = int.tryParse(
                              '${list['id'] ?? list['Id'] ?? list['delegateId'] ?? 0}') ??
                          0;
                      if (id <= 0) return const SizedBox.shrink();
                      final name =
                          '${list['name'] ?? list['Name'] ?? list['delegateName'] ?? 'مندوب $id'}';
                      final score = scores[id];
                      final rated = score != null;
                      return Padding(
                        padding: const EdgeInsets.only(bottom: 8),
                        child: Card(
                          color: rated ? null : const Color(0xFFFFF8E1),
                          child: Padding(
                            padding: const EdgeInsets.fromLTRB(12, 10, 12, 12),
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.stretch,
                              children: [
                                Row(
                                  children: [
                                    Expanded(
                                      child: Text(
                                        name,
                                        style: const TextStyle(
                                          fontFamily: 'Cairo',
                                          fontWeight: FontWeight.w600,
                                        ),
                                      ),
                                    ),
                                    Text(
                                      rated ? '$score / 10' : 'مطلوب',
                                      style: TextStyle(
                                        fontFamily: 'Cairo',
                                        fontWeight: FontWeight.bold,
                                        color: rated
                                            ? BmTheme.primary
                                            : const Color(0xFFE65100),
                                      ),
                                    ),
                                  ],
                                ),
                                const SizedBox(height: 8),
                                Wrap(
                                  spacing: 6,
                                  runSpacing: 6,
                                  children: [
                                    for (var s = 0; s <= 10; s++)
                                      ChoiceChip(
                                        label: Text(
                                          '$s',
                                          style: const TextStyle(
                                            fontFamily: 'Cairo',
                                            fontSize: 13,
                                          ),
                                        ),
                                        selected: score == s,
                                        onSelected: busy
                                            ? null
                                            : (_) => setLocal(
                                                () => scores[id] = s),
                                      ),
                                  ],
                                ),
                              ],
                            ),
                          ),
                        ),
                      );
                    }),
                  const Divider(height: 28),
                  const Text(
                    'ما هي الملاحظات وماذا عملتم بالفرع؟',
                    style: TextStyle(
                      fontFamily: 'Cairo',
                      fontSize: 15,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                  const SizedBox(height: 8),
                  TextField(
                    controller: notesCtrl,
                    maxLines: 8,
                    minLines: 5,
                    enabled: !busy,
                    style: const TextStyle(fontFamily: 'Cairo'),
                    decoration: const InputDecoration(
                      hintText:
                          'اكتب وصفاً مفصّلاً للعمل والملاحظات (50 حرفاً على الأقل)…',
                      border: OutlineInputBorder(),
                    ),
                  ),
                  const SizedBox(height: 12),
                  OutlinedButton.icon(
                    onPressed: busy
                        ? null
                        : () async {
                            final result = await FilePicker.platform.pickFiles(
                              allowMultiple: true,
                              type: FileType.custom,
                              allowedExtensions: [
                                'jpg',
                                'jpeg',
                                'png',
                                'webp',
                                'gif',
                                'pdf'
                              ],
                            );
                            if (result == null) return;
                            setLocal(() {
                              for (final f in result.files) {
                                if (f.path != null) files.add(File(f.path!));
                              }
                            });
                          },
                    icon: const Icon(Icons.attach_file),
                    label: Text(
                      files.isEmpty
                          ? 'تحميل مرفقات (صور / PDF)'
                          : 'مرفقات: ${files.length}',
                      style: const TextStyle(fontFamily: 'Cairo'),
                    ),
                  ),
                  if (error != null) ...[
                    const SizedBox(height: 8),
                    Text(
                      error!,
                      style: const TextStyle(
                          fontFamily: 'Cairo', color: Colors.red),
                    ),
                  ],
                  const SizedBox(height: 16),
                  Row(
                    children: [
                      Expanded(
                        child: OutlinedButton(
                          onPressed:
                              busy ? null : () => Navigator.pop(ctx, false),
                          child: const Text('إلغاء',
                              style: TextStyle(fontFamily: 'Cairo')),
                        ),
                      ),
                      const SizedBox(width: 10),
                      Expanded(
                        child: FilledButton(
                          style: FilledButton.styleFrom(
                              backgroundColor: BmTheme.primary),
                          onPressed: busy || loadingLists
                              ? null
                              : () async {
                                  if (listIds.isNotEmpty && !allRated) {
                                    setLocal(() => error =
                                        'قيّم كل قوائم المراقب من 0 إلى 10 قبل المغادرة');
                                    return;
                                  }
                                  final note = notesCtrl.text.trim();
                                  if (note.length < 50) {
                                    setLocal(() => error =
                                        'الملاحظة الطويلة مطلوبة (50 حرفاً على الأقل)');
                                    return;
                                  }
                                  setLocal(() {
                                    busy = true;
                                    error = null;
                                  });
                                  try {
                                    if (listIds.isNotEmpty) {
                                      final payload = <Map<String, dynamic>>[];
                                      for (final id in listIds) {
                                        final list = lists.firstWhere((m) {
                                          final mid = int.tryParse(
                                                  '${m['id'] ?? m['Id'] ?? m['delegateId'] ?? 0}') ??
                                              0;
                                          return mid == id;
                                        });
                                        final name =
                                            '${list['name'] ?? list['Name'] ?? list['delegateName'] ?? '#$id'}';
                                        payload.add({
                                          'delegateId': id,
                                          'delegateName': name,
                                          'score': scores[id]!,
                                        });
                                      }
                                      await BmApi.instance
                                          .saveBranchVisitRatings(
                                        visitId: visitId,
                                        ratings: payload,
                                      );
                                    }
                                    await BmApi.instance.departBranchVisit(
                                      visitId: visitId,
                                      notes: note,
                                      files: files,
                                      requiredDelegateIds: listIds,
                                    );
                                    if (ctx.mounted) {
                                      Navigator.pop(ctx, true);
                                    }
                                  } catch (e) {
                                    setLocal(() {
                                      busy = false;
                                      error = '$e';
                                    });
                                  }
                                },
                          child: busy
                              ? const SizedBox(
                                  width: 20,
                                  height: 20,
                                  child: CircularProgressIndicator(
                                      strokeWidth: 2, color: Colors.white),
                                )
                              : const Text('مغادرة الفرع',
                                  style: TextStyle(fontFamily: 'Cairo')),
                        ),
                      ),
                    ],
                  ),
                ],
              ),
            ),
          );
        },
      );
    },
  );
  notesCtrl.dispose();
  return ok == true;
}
