import 'dart:async';

import 'package:follower_application/services/announcement_service.dart';
import 'package:follower_application/utils/AppTheme.dart';
import 'package:flutter/material.dart';
import 'package:url_launcher/url_launcher.dart';

/// Modern top-of-home announcements carousel (3s auto-rotate when 2+).
class AnnouncementsBanner extends StatefulWidget {
  const AnnouncementsBanner({super.key, this.appKey});

  final String? appKey;

  @override
  State<AnnouncementsBanner> createState() => _AnnouncementsBannerState();
}

class _AnnouncementsBannerState extends State<AnnouncementsBanner> {
  List<AppAnnouncement> _items = const [];
  bool _loading = true;
  int _page = 0;
  PageController? _controller;
  Timer? _timer;
  bool _previewOpen = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _timer?.cancel();
    _controller?.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    final rows = await AnnouncementService.fetch(appKey: widget.appKey);
    if (!mounted) return;
    _timer?.cancel();
    _controller?.dispose();
    _controller = null;
    setState(() {
      _items = rows;
      _loading = false;
      _page = 0;
      if (rows.length > 1) {
        _controller = PageController();
        _startAutoRotate();
      }
    });
    // Preload zoom images so tap-to-expand is instant.
    for (final a in rows) {
      final url = a.previewImageUrl;
      if (url.isEmpty) continue;
      try {
        await precacheImage(NetworkImage(url), context);
      } catch (_) {}
    }
  }

  void _startAutoRotate() {
    _timer?.cancel();
    if (_items.length < 2) return;
    _timer = Timer.periodic(const Duration(seconds: 3), (_) {
      if (!mounted ||
          _previewOpen ||
          _controller == null ||
          !_controller!.hasClients) {
        return;
      }
      final next = (_page + 1) % _items.length;
      _controller!.animateToPage(
        next,
        duration: const Duration(milliseconds: 700),
        curve: Curves.easeInOutCubicEmphasized,
      );
    });
  }

  Future<void> _openLink(String url) async {
    final uri = Uri.tryParse(url);
    if (uri == null) return;
    await launchUrl(uri, mode: LaunchMode.externalApplication);
  }

  Future<void> _openPreview(AppAnnouncement a) async {
    if (_previewOpen) return;
    setState(() => _previewOpen = true);
    await showGeneralDialog<void>(
      context: context,
      barrierDismissible: true,
      barrierLabel: 'إغلاق',
      barrierColor: Colors.black.withValues(alpha: 0.72),
      transitionDuration: const Duration(milliseconds: 380),
      pageBuilder: (ctx, anim, secondary) {
        return SafeArea(
          child: Center(
            child: _AnnouncementPreview(
              announcement: a,
              onClose: () => Navigator.of(ctx).pop(),
              onOpenLink: _openLink,
            ),
          ),
        );
      },
      transitionBuilder: (ctx, anim, secondary, child) {
        final curved = CurvedAnimation(
          parent: anim,
          curve: Curves.easeOutCubic,
          reverseCurve: Curves.easeInCubic,
        );
        return FadeTransition(
          opacity: curved,
          child: ScaleTransition(
            scale: Tween<double>(begin: 0.88, end: 1).animate(curved),
            child: child,
          ),
        );
      },
    );
    if (mounted) setState(() => _previewOpen = false);
  }

  @override
  Widget build(BuildContext context) {
    if (_loading || _items.isEmpty) return const SizedBox.shrink();

    final height = _items.any((e) => e.isImage) ? 168.0 : 132.0;

    return Padding(
      padding: const EdgeInsets.only(bottom: 16),
      child: Column(
        children: [
          SizedBox(
            height: height,
            child: _items.length == 1
                ? _card(_items.first)
                : PageView.builder(
                    controller: _controller,
                    itemCount: _items.length,
                    onPageChanged: (i) => setState(() => _page = i),
                    itemBuilder: (_, i) => _card(_items[i]),
                  ),
          ),
          if (_items.length > 1) ...[
            const SizedBox(height: 8),
            Row(
              mainAxisAlignment: MainAxisAlignment.center,
              children: List.generate(_items.length, (i) {
                final on = i == _page;
                return AnimatedContainer(
                  duration: const Duration(milliseconds: 320),
                  curve: Curves.easeOutCubic,
                  margin: const EdgeInsets.symmetric(horizontal: 3),
                  width: on ? 16 : 7,
                  height: 7,
                  decoration: BoxDecoration(
                    color: on
                        ? AppTheme.primaryColor
                        : AppTheme.primaryColor.withValues(alpha: 0.28),
                    borderRadius: BorderRadius.circular(8),
                  ),
                );
              }),
            ),
          ],
        ],
      ),
    );
  }

  Widget _card(AppAnnouncement a) {
    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: () => _openPreview(a),
        borderRadius: BorderRadius.circular(18),
        child: Container(
          margin: const EdgeInsets.symmetric(horizontal: 2),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(18),
            gradient: LinearGradient(
              begin: Alignment.topRight,
              end: Alignment.bottomLeft,
              colors: [
                AppTheme.primaryColor.withValues(alpha: 0.95),
                const Color(0xFF1B5E4A),
              ],
            ),
            boxShadow: [
              BoxShadow(
                color: AppTheme.primaryColor.withValues(alpha: 0.22),
                blurRadius: 16,
                offset: const Offset(0, 8),
              ),
            ],
          ),
          clipBehavior: Clip.antiAlias,
          child: Stack(
            fit: StackFit.expand,
            children: [
              if (a.isImage && a.imageUrl.isNotEmpty)
                Image.network(
                  a.imageUrl,
                  fit: BoxFit.cover,
                  errorBuilder: (_, __, ___) => const SizedBox.shrink(),
                ),
              if (!a.isImage)
                Padding(
                  padding: const EdgeInsets.fromLTRB(16, 14, 16, 12),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      Align(
                        alignment: Alignment.centerRight,
                        child: Container(
                          padding: const EdgeInsets.symmetric(
                              horizontal: 10, vertical: 4),
                          decoration: BoxDecoration(
                            color: Colors.white.withValues(alpha: 0.18),
                            borderRadius: BorderRadius.circular(20),
                          ),
                          child: const Text(
                            'إعلان',
                            style: TextStyle(
                              fontFamily: 'Cairo',
                              color: Colors.white,
                              fontSize: 11,
                              fontWeight: FontWeight.bold,
                            ),
                          ),
                        ),
                      ),
                      const Spacer(),
                      Text(
                        a.textBody,
                        maxLines: 3,
                        overflow: TextOverflow.ellipsis,
                        textAlign: TextAlign.right,
                        style: const TextStyle(
                          fontFamily: 'Cairo',
                          color: Colors.white,
                          fontSize: 15,
                          fontWeight: FontWeight.w700,
                          height: 1.35,
                        ),
                      ),
                      if (a.hasLink) ...[
                        const SizedBox(height: 10),
                        Align(
                          alignment: Alignment.centerLeft,
                          child: TextButton(
                            onPressed: () => _openLink(a.linkUrl),
                            style: TextButton.styleFrom(
                              backgroundColor: Colors.white,
                              foregroundColor: AppTheme.primaryColor,
                              padding: const EdgeInsets.symmetric(
                                  horizontal: 14, vertical: 8),
                              shape: RoundedRectangleBorder(
                                borderRadius: BorderRadius.circular(22),
                              ),
                            ),
                            child: const Text(
                              'اضغط هنا',
                              style: TextStyle(
                                fontFamily: 'Cairo',
                                fontWeight: FontWeight.bold,
                                fontSize: 13,
                              ),
                            ),
                          ),
                        ),
                      ],
                    ],
                  ),
                )
              else if (a.hasLink)
                Positioned(
                  left: 12,
                  bottom: 10,
                  child: TextButton(
                    onPressed: () => _openLink(a.linkUrl),
                    style: TextButton.styleFrom(
                      backgroundColor: Colors.white,
                      foregroundColor: AppTheme.primaryColor,
                      padding: const EdgeInsets.symmetric(
                          horizontal: 14, vertical: 8),
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(22),
                      ),
                    ),
                    child: const Text(
                      'اضغط هنا',
                      style: TextStyle(
                        fontFamily: 'Cairo',
                        fontWeight: FontWeight.bold,
                        fontSize: 13,
                      ),
                    ),
                  ),
                ),
            ],
          ),
        ),
      ),
    );
  }
}

class _AnnouncementPreview extends StatelessWidget {
  const _AnnouncementPreview({
    required this.announcement,
    required this.onClose,
    required this.onOpenLink,
  });

  final AppAnnouncement announcement;
  final VoidCallback onClose;
  final Future<void> Function(String url) onOpenLink;

  @override
  Widget build(BuildContext context) {
    final size = MediaQuery.sizeOf(context);
    // ~80% of the screen so the image/text feels large and centered.
    final frameW = size.width * 0.80;
    final frameH = size.height * 0.80;

    return Material(
      color: Colors.transparent,
      child: SizedBox(
        width: frameW,
        height: frameH,
        child: Stack(
          fit: StackFit.expand,
          children: [
            if (announcement.isImage &&
                announcement.previewImageUrl.isNotEmpty)
              ClipRRect(
                borderRadius: BorderRadius.circular(20),
                child: InteractiveViewer(
                  minScale: 1,
                  maxScale: 4,
                  child: SizedBox(
                    width: frameW,
                    height: frameH,
                    child: Image.network(
                      announcement.previewImageUrl,
                      fit: BoxFit.fill,
                      width: frameW,
                      height: frameH,
                      errorBuilder: (_, __, ___) => const ColoredBox(
                        color: Colors.black26,
                        child: Center(
                          child: Text(
                            'تعذر تحميل الصورة',
                            style: TextStyle(
                              fontFamily: 'Cairo',
                              color: Colors.white,
                            ),
                          ),
                        ),
                      ),
                    ),
                  ),
                ),
              )
            else
              Container(
                width: frameW,
                height: frameH,
                padding: const EdgeInsets.fromLTRB(22, 28, 22, 22),
                decoration: BoxDecoration(
                  borderRadius: BorderRadius.circular(22),
                  gradient: LinearGradient(
                    begin: Alignment.topRight,
                    end: Alignment.bottomLeft,
                    colors: [
                      AppTheme.primaryColor.withValues(alpha: 0.98),
                      const Color(0xFF1B5E4A),
                    ],
                  ),
                  boxShadow: [
                    BoxShadow(
                      color: Colors.black.withValues(alpha: 0.35),
                      blurRadius: 28,
                      offset: const Offset(0, 12),
                    ),
                  ],
                ),
                child: SingleChildScrollView(
                  child: Text(
                    announcement.textBody,
                    textAlign: TextAlign.right,
                    style: const TextStyle(
                      fontFamily: 'Cairo',
                      color: Colors.white,
                      fontSize: 20,
                      fontWeight: FontWeight.w700,
                      height: 1.55,
                    ),
                  ),
                ),
              ),
            Positioned(
              top: 8,
              left: 8,
              child: IconButton.filled(
                onPressed: onClose,
                style: IconButton.styleFrom(
                  backgroundColor: Colors.black.withValues(alpha: 0.45),
                  foregroundColor: Colors.white,
                ),
                icon: const Icon(Icons.close_rounded),
              ),
            ),
            if (announcement.hasLink)
              Positioned(
                bottom: 14,
                left: 0,
                right: 0,
                child: Center(
                  child: FilledButton(
                    onPressed: () => onOpenLink(announcement.linkUrl),
                    style: FilledButton.styleFrom(
                      backgroundColor: Colors.white,
                      foregroundColor: AppTheme.primaryColor,
                      padding: const EdgeInsets.symmetric(
                          horizontal: 22, vertical: 12),
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(24),
                      ),
                    ),
                    child: const Text(
                      'اضغط هنا',
                      style: TextStyle(
                        fontFamily: 'Cairo',
                        fontWeight: FontWeight.bold,
                        fontSize: 14,
                      ),
                    ),
                  ),
                ),
              ),
          ],
        ),
      ),
    );
  }
}
