import 'dart:async';
import 'dart:io';

import 'package:cached_network_image/cached_network_image.dart';
import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_file_dialog/flutter_file_dialog.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:gal/gal.dart';
import 'package:open_filex/open_filex.dart';
import 'package:path_provider/path_provider.dart';
import 'package:share_plus/share_plus.dart';
import 'package:video_player/video_player.dart';

import '../../core/app_state.dart';
import '../../core/theme/app_theme.dart';
import '../../core/widgets/state_views.dart';
import '../../data/models.dart';

/// «المواد»: photos, videos and PDFs from the administration, to view, download and share.
class MaterialsScreen extends ConsumerStatefulWidget {
  const MaterialsScreen({super.key});

  @override
  ConsumerState<MaterialsScreen> createState() => _MaterialsScreenState();
}

class _MaterialsScreenState extends ConsumerState<MaterialsScreen> {
  String _kind = 'all';

  @override
  Widget build(BuildContext context) {
    final value = ref.watch(materialsProvider);
    return Scaffold(
      appBar: AppBar(title: const Text('المواد التسويقية')),
      body: Column(
        children: [
          SizedBox(
            height: 50,
            child: ListView(
              scrollDirection: Axis.horizontal,
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 6),
              children: [
                for (final (k, label) in const [('all', 'الكل'), ('image', 'صور'), ('video', 'فيديو'), ('pdf', 'PDF')])
                  Padding(
                    padding: const EdgeInsetsDirectional.only(end: 8),
                    child: ChoiceChip(label: Text(label), selected: _kind == k, onSelected: (_) => setState(() => _kind = k)),
                  ),
              ],
            ),
          ),
          Expanded(
            child: RefreshIndicator(
              onRefresh: () => ref.refresh(materialsProvider.future),
              child: AsyncView<List<MarketingMaterial>>(
                value: value,
                onRetry: () => ref.invalidate(materialsProvider),
                data: (all) {
                  final list = _kind == 'all' ? all : all.where((m) => m.kind == _kind).toList();
                  if (list.isEmpty) {
                    return ListView(children: const [
                      SizedBox(height: 60),
                      EmptyState(icon: Icons.perm_media_outlined, title: 'لا توجد مواد بعد', message: 'ستظهر هنا الصور والفيديوهات والملفات التي تنشرها الإدارة'),
                    ]);
                  }
                  return GridView.builder(
                    padding: const EdgeInsets.fromLTRB(16, 4, 16, 24),
                    gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(crossAxisCount: 2, mainAxisSpacing: 12, crossAxisSpacing: 12, childAspectRatio: 0.82),
                    itemCount: list.length,
                    itemBuilder: (_, i) => _MaterialCard(material: list[i]),
                  );
                },
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _MaterialCard extends StatelessWidget {
  const _MaterialCard({required this.material});

  final MarketingMaterial material;

  @override
  Widget build(BuildContext context) {
    final m = material;
    return Card(
      child: InkWell(
        onTap: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => MaterialViewer(material: m))),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Expanded(child: MaterialThumb(material: m)),
            Padding(
              padding: const EdgeInsets.fromLTRB(10, 8, 4, 2),
              child: Text(m.title, maxLines: 1, overflow: TextOverflow.ellipsis, style: const TextStyle(fontWeight: FontWeight.w700)),
            ),
            Row(
              children: [
                Padding(
                  padding: const EdgeInsetsDirectional.only(start: 10),
                  child: Text(m.sizeLabel, style: const TextStyle(color: AppColors.textMuted, fontSize: 12)),
                ),
                const Spacer(),
                IconButton(
                  visualDensity: VisualDensity.compact,
                  tooltip: 'مشاركة',
                  icon: const Icon(Icons.share_rounded, size: 20),
                  onPressed: () => MaterialActions.share(context, m),
                ),
                IconButton(
                  visualDensity: VisualDensity.compact,
                  tooltip: 'تحميل',
                  icon: const Icon(Icons.download_rounded, size: 20),
                  onPressed: () => MaterialActions.download(context, m),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

class MaterialThumb extends StatelessWidget {
  const MaterialThumb({super.key, required this.material});

  final MarketingMaterial material;

  @override
  Widget build(BuildContext context) {
    if (material.isImage) {
      return CachedNetworkImage(
        imageUrl: material.url,
        fit: BoxFit.cover,
        memCacheWidth: 500,
        placeholder: (_, _) => const ColoredBox(color: AppColors.primaryLight),
        errorWidget: (_, _, _) => const ColoredBox(color: AppColors.primaryLight, child: Icon(Icons.broken_image_outlined)),
      );
    }
    final (icon, color) = material.isVideo ? (Icons.play_circle_fill_rounded, AppColors.danger) : (Icons.picture_as_pdf_rounded, AppColors.warning);
    return ColoredBox(
      color: color.withValues(alpha: 0.08),
      child: Center(child: Icon(icon, size: 56, color: color)),
    );
  }
}

/// Opens one item: zoomable photo, video player, or the phone's PDF viewer.
class MaterialViewer extends StatefulWidget {
  const MaterialViewer({super.key, required this.material});

  final MarketingMaterial material;

  @override
  State<MaterialViewer> createState() => _MaterialViewerState();
}

class _MaterialViewerState extends State<MaterialViewer> {
  VideoPlayerController? _video;
  Object? _videoError;
  bool _openingPdf = false;

  MarketingMaterial get m => widget.material;

  @override
  void initState() {
    super.initState();
    if (m.isVideo) {
      _video = VideoPlayerController.networkUrl(Uri.parse(m.url))
        ..initialize().then((_) {
          if (mounted) {
            setState(() {});
            _video!.play();
          }
        }).catchError((Object e) {
          if (mounted) setState(() => _videoError = e);
        })
        ..addListener(() {
          if (mounted) setState(() {});
        });
    }
  }

  @override
  void dispose() {
    _video?.dispose();
    super.dispose();
  }

  Future<void> _openPdf() async {
    setState(() => _openingPdf = true);
    try {
      final file = await MaterialActions.cached(m);
      final result = await OpenFilex.open(file.path, type: 'application/pdf');
      if (result.type != ResultType.done && mounted) context.showSnack('لا يوجد تطبيق لفتح ملفات PDF على هذا الهاتف', error: true);
    } catch (e) {
      if (mounted) context.showSnack('تعذّر فتح الملف', error: true);
    } finally {
      if (mounted) setState(() => _openingPdf = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: m.isPdf ? null : Colors.black,
      appBar: AppBar(
        backgroundColor: m.isPdf ? null : Colors.black,
        foregroundColor: m.isPdf ? null : Colors.white,
        title: Text(m.title, maxLines: 1, overflow: TextOverflow.ellipsis),
        actions: [
          IconButton(tooltip: 'مشاركة', icon: const Icon(Icons.share_rounded), onPressed: () => MaterialActions.share(context, m)),
          IconButton(tooltip: 'تحميل', icon: const Icon(Icons.download_rounded), onPressed: () => MaterialActions.download(context, m)),
        ],
      ),
      body: SafeArea(child: _body()),
    );
  }

  Widget _body() {
    if (m.isImage) {
      return InteractiveViewer(
        maxScale: 5,
        child: Center(
          child: CachedNetworkImage(
            imageUrl: m.url,
            fit: BoxFit.contain,
            placeholder: (_, _) => const Center(child: CircularProgressIndicator(color: Colors.white)),
          ),
        ),
      );
    }
    if (m.isVideo) {
      final v = _video!;
      if (_videoError != null) {
        return const Center(child: Text('تعذّر تشغيل الفيديو. يمكنك تحميله أو مشاركته', style: TextStyle(color: Colors.white)));
      }
      if (!v.value.isInitialized) return const Center(child: CircularProgressIndicator(color: Colors.white));
      return Column(
        children: [
          Expanded(
            child: GestureDetector(
              onTap: () => v.value.isPlaying ? v.pause() : v.play(),
              child: Center(child: AspectRatio(aspectRatio: v.value.aspectRatio, child: VideoPlayer(v))),
            ),
          ),
          VideoProgressIndicator(v, allowScrubbing: true, padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8)),
          IconButton(
            iconSize: 48,
            color: Colors.white,
            icon: Icon(v.value.isPlaying ? Icons.pause_circle_filled_rounded : Icons.play_circle_fill_rounded),
            onPressed: () => v.value.isPlaying ? v.pause() : v.play(),
          ),
        ],
      );
    }
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(32),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.picture_as_pdf_rounded, size: 96, color: AppColors.warning),
            const SizedBox(height: 12),
            Text(m.title, textAlign: TextAlign.center, style: Theme.of(context).textTheme.titleMedium),
            Text(m.sizeLabel, style: const TextStyle(color: AppColors.textMuted)),
            const SizedBox(height: 24),
            FilledButton.icon(
              onPressed: _openingPdf ? null : _openPdf,
              icon: _openingPdf
                  ? const SizedBox(width: 18, height: 18, child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white))
                  : const Icon(Icons.open_in_new_rounded),
              label: const Text('فتح الملف'),
            ),
          ],
        ),
      ),
    );
  }
}

/// Download / share helpers. Files are fetched once into the app's cache and reused.
abstract final class MaterialActions {
  static final _dio = Dio(BaseOptions(connectTimeout: const Duration(seconds: 20), receiveTimeout: const Duration(minutes: 10)));

  static Future<File> cached(MarketingMaterial m, {void Function(double)? onProgress}) async {
    final dir = await getTemporaryDirectory();
    final file = File('${dir.path}/materials/${m.id}${m.extension}');
    if (await file.exists() && await file.length() == m.sizeBytes) return file;
    await file.parent.create(recursive: true);
    final part = File('${file.path}.part');
    await _dio.download(m.url, part.path, onReceiveProgress: (r, t) => onProgress?.call(t > 0 ? r / t : 0));
    return part.rename(file.path);
  }

  static Future<File?> _withProgress(BuildContext context, MarketingMaterial m) async {
    final progress = ValueNotifier<double>(0);
    final nav = Navigator.of(context, rootNavigator: true);
    unawaited(showDialog<void>(
      context: context,
      barrierDismissible: false,
      builder: (_) => AlertDialog(
        content: ValueListenableBuilder<double>(
          valueListenable: progress,
          builder: (_, v, _) => Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Text('جاري التحميل…'),
              const SizedBox(height: 14),
              LinearProgressIndicator(value: v > 0 ? v : null),
              const SizedBox(height: 6),
              Text('${(v * 100).round()}%'),
            ],
          ),
        ),
      ),
    ));
    try {
      return await cached(m, onProgress: (v) => progress.value = v);
    } catch (_) {
      if (context.mounted) context.showSnack('تعذّر تحميل الملف. تأكد من الاتصال', error: true);
      return null;
    } finally {
      nav.pop();
      progress.dispose();
    }
  }

  /// Photos and videos go to the gallery; a PDF opens the system "save as" screen.
  static Future<void> download(BuildContext context, MarketingMaterial m) async {
    final file = await _withProgress(context, m);
    if (file == null || !context.mounted) return;
    try {
      if (m.isPdf) {
        final saved = await FlutterFileDialog.saveFile(params: SaveFileDialogParams(sourceFilePath: file.path, fileName: _safeName(m)));
        if (saved != null && context.mounted) context.showSnack('تم حفظ الملف');
        return;
      }
      if (!await Gal.hasAccess(toAlbum: true)) await Gal.requestAccess(toAlbum: true);
      if (m.isImage) {
        await Gal.putImage(file.path, album: 'قلعة الضمان');
      } else {
        await Gal.putVideo(file.path, album: 'قلعة الضمان');
      }
      if (context.mounted) context.showSnack(m.isImage ? 'تم حفظ الصورة في المعرض' : 'تم حفظ الفيديو في المعرض');
    } on GalException catch (e) {
      if (context.mounted) {
        context.showSnack(e.type == GalExceptionType.accessDenied ? 'اسمح للتطبيق بالوصول إلى الصور لحفظ الملف' : 'تعذّر حفظ الملف', error: true);
      }
    } catch (_) {
      if (context.mounted) context.showSnack('تعذّر حفظ الملف', error: true);
    }
  }

  static Future<void> share(BuildContext context, MarketingMaterial m) async {
    final file = await _withProgress(context, m);
    if (file == null) return;
    await SharePlus.instance.share(ShareParams(files: [XFile(file.path, mimeType: m.contentType)], text: m.title));
  }

  static String _safeName(MarketingMaterial m) {
    final base = m.title.replaceAll(RegExp(r'[\\/:*?"<>|]'), ' ').trim();
    return '${base.isEmpty ? 'material-${m.id}' : base}${m.extension}';
  }
}
