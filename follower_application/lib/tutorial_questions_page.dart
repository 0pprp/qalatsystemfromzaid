import 'package:follower_application/services/tutorial_video_service.dart';
import 'package:follower_application/ui/app_safe_scaffold.dart';
import 'package:follower_application/utils/AppTheme.dart';
import 'package:flutter/material.dart';
import 'package:url_launcher/url_launcher.dart';

class TutorialQuestionsPage extends StatefulWidget {
  const TutorialQuestionsPage({super.key});

  @override
  State<TutorialQuestionsPage> createState() => _TutorialQuestionsPageState();
}

class _TutorialQuestionsPageState extends State<TutorialQuestionsPage> {
  bool _loading = true;
  String? _error;
  List<TutorialQuestion> _items = const [];

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    final rows = await TutorialVideoService.fetchQuestions();
    if (!mounted) return;
    setState(() {
      _items = rows;
      _loading = false;
      if (rows.isEmpty) {
        _error = 'لا توجد تساؤلات تعليمية بعد. أضفها من لوحة المدير.';
      }
    });
  }

  Future<void> _openVideo(TutorialQuestion q) async {
    final uri = Uri.tryParse(q.videoUrl);
    if (uri == null) return;
    try {
      final ok = await launchUrl(uri, mode: LaunchMode.externalApplication);
      if (!ok && mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('تعذر فتح الفيديو',
                style: TextStyle(fontFamily: 'Cairo')),
          ),
        );
      }
    } catch (_) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('تعذر فتح الفيديو',
              style: TextStyle(fontFamily: 'Cairo')),
        ),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    return AppSafeScaffold(
      appBar: AppBar(
        title: const Text('الفيديوهات التعليمية',
            style: TextStyle(fontFamily: 'Cairo')),
        backgroundColor: AppTheme.primaryColor,
        foregroundColor: Colors.white,
      ),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : RefreshIndicator(
              onRefresh: _load,
              child: _items.isEmpty
                  ? ListView(
                      physics: const AlwaysScrollableScrollPhysics(),
                      children: [
                        const SizedBox(height: 80),
                        Padding(
                          padding: const EdgeInsets.all(24),
                          child: Text(
                            _error ?? 'لا توجد تساؤلات',
                            textAlign: TextAlign.center,
                            style: const TextStyle(
                                fontFamily: 'Cairo', fontSize: 15),
                          ),
                        ),
                      ],
                    )
                  : ListView.separated(
                      padding: const EdgeInsets.all(16),
                      itemCount: _items.length,
                      separatorBuilder: (_, __) => const SizedBox(height: 10),
                      itemBuilder: (context, i) {
                        final q = _items[i];
                        return Material(
                          color: Colors.white,
                          borderRadius: BorderRadius.circular(12),
                          elevation: 1,
                          child: InkWell(
                            borderRadius: BorderRadius.circular(12),
                            onTap: () => _openVideo(q),
                            child: Padding(
                              padding: const EdgeInsets.symmetric(
                                  horizontal: 16, vertical: 14),
                              child: Row(
                                children: [
                                  const Icon(Icons.play_circle_fill,
                                      color: Colors.green, size: 32),
                                  const SizedBox(width: 12),
                                  Expanded(
                                    child: Column(
                                      crossAxisAlignment:
                                          CrossAxisAlignment.start,
                                      children: [
                                        Text(
                                          q.questionAr,
                                          style: const TextStyle(
                                            fontFamily: 'Cairo',
                                            fontWeight: FontWeight.w600,
                                            fontSize: 15,
                                          ),
                                        ),
                                        const SizedBox(height: 4),
                                        const Text(
                                          'شاهد الفيديو',
                                          style: TextStyle(
                                            fontFamily: 'Cairo',
                                            fontSize: 12,
                                            color: Colors.black54,
                                          ),
                                        ),
                                      ],
                                    ),
                                  ),
                                  const Icon(Icons.chevron_left),
                                ],
                              ),
                            ),
                          ),
                        );
                      },
                    ),
            ),
    );
  }
}
