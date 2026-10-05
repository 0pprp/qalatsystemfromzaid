import 'package:flutter/material.dart';
import 'package:main_follower_application/core/auth/session.dart';
import 'package:main_follower_application/core/config/province_labels.dart';
import 'package:main_follower_application/core/update/app_update_gate.dart';
import 'package:main_follower_application/data/auth_repository.dart';
import 'package:main_follower_application/data/mission_repository.dart';
import 'package:main_follower_application/domain/mission.dart';
import 'package:main_follower_application/presentation/login_screen.dart';
import 'package:main_follower_application/presentation/mission_detail_screen.dart';
import 'package:main_follower_application/presentation/start_shift_screen.dart';
import 'package:main_follower_application/services/tutorial_video_service.dart';
import 'package:main_follower_application/tracking/shift_tracking_controller.dart';
import 'package:url_launcher/url_launcher.dart';

class MissionsScreen extends StatefulWidget {
  const MissionsScreen({super.key});

  @override
  State<MissionsScreen> createState() => _MissionsScreenState();
}

class _MissionsScreenState extends State<MissionsScreen> {
  late Future<List<Mission>> _future;

  @override
  void initState() {
    super.initState();
    _future = MissionRepository.listMine();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) AppUpdateGate.checkAndPrompt(context);
    });
  }

  Future<void> _reload() async {
    setState(() => _future = MissionRepository.listMine());
    await _future;
  }

  Future<void> _openTutorialVideo() async {
    showDialog(
      context: context,
      barrierDismissible: false,
      builder: (_) => const Center(child: CircularProgressIndicator()),
    );
    Uri? uri;
    try {
      uri = await TutorialVideoService.resolveLaunchUri();
    } finally {
      if (mounted) Navigator.of(context, rootNavigator: true).pop();
    }
    if (!mounted) return;
    if (uri == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text(
            'لم يُحدَّد رابط الفيديو بعد. عدّله من لوحة مدير المبيعات.',
          ),
        ),
      );
      return;
    }
    try {
      final ok = await launchUrl(uri, mode: LaunchMode.externalApplication);
      if (!ok && mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('تعذر فتح الفيديو التعليمي')),
        );
      }
    } catch (_) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('تعذر فتح الفيديو التعليمي')),
      );
    }
  }

  Future<void> _logout() async {
    ShiftTrackingController.instance.stopTimer();
    await AuthRepository.logout();
    if (!mounted) return;
    Navigator.of(context).pushAndRemoveUntil(
      MaterialPageRoute(builder: (_) => const LoginScreen()),
      (_) => false,
    );
  }

  Future<void> _endShift() async {
    final ok = await ShiftTrackingController.instance.endShift();
    if (!mounted) return;
    if (!ok) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            ShiftTrackingController.instance.lastError ?? 'تعذر إنهاء الدوام',
          ),
        ),
      );
      return;
    }
    Navigator.of(context).pushAndRemoveUntil(
      MaterialPageRoute(builder: (_) => const StartShiftScreen()),
      (_) => false,
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: Text(Session.displayName ?? 'مهامي'),
        backgroundColor: const Color(0xFF0F3D3E),
        foregroundColor: Colors.white,
        actions: [
          IconButton(onPressed: _reload, icon: const Icon(Icons.refresh)),
        ],
      ),
      drawer: Drawer(
        child: SafeArea(
          child: ListView(
            children: [
              DrawerHeader(
                decoration: const BoxDecoration(color: Color(0xFF0F3D3E)),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Text(
                      'المتابع الرئيسي',
                      style: TextStyle(color: Colors.white70),
                    ),
                    const SizedBox(height: 8),
                    Text(
                      Session.displayName ?? Session.userName ?? '—',
                      style: const TextStyle(
                        color: Colors.white,
                        fontSize: 20,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                  ],
                ),
              ),
              ListTile(
                leading: const Icon(Icons.assignment_outlined),
                title: const Text('مهامي'),
                selected: true,
                selectedTileColor: const Color(0xFFE8F1F1),
                onTap: () => Navigator.pop(context),
              ),
              ListTile(
                leading: const Icon(Icons.stop_circle_outlined),
                title: const Text('إنهاء الدوام'),
                onTap: () async {
                  Navigator.pop(context);
                  await _endShift();
                },
              ),
              ListTile(
                leading: const Icon(Icons.play_circle_filled_rounded,
                    color: Color(0xFFE6A800)),
                title: const Text('الفيديو التعليمي'),
                onTap: () async {
                  Navigator.pop(context);
                  await _openTutorialVideo();
                },
              ),
              const Divider(),
              ListTile(
                leading: const Icon(Icons.logout, color: Colors.red),
                title: const Text('تسجيل الخروج'),
                onTap: () async {
                  Navigator.pop(context);
                  await _logout();
                },
              ),
            ],
          ),
        ),
      ),
      body: FutureBuilder<List<Mission>>(
        future: _future,
        builder: (context, snap) {
          if (snap.connectionState != ConnectionState.done) {
            return const Center(child: CircularProgressIndicator());
          }
          if (snap.hasError) {
            return Center(child: Text(snap.error.toString()));
          }
          final rows = snap.data ?? [];
          if (rows.isEmpty) {
            return const Center(child: Text('لا توجد مهام مكلَّف بها حالياً'));
          }
          return RefreshIndicator(
            onRefresh: _reload,
            child: ListView.separated(
              padding: const EdgeInsets.fromLTRB(16, 16, 16, 32),
              itemCount: rows.length,
              separatorBuilder: (_, __) => const SizedBox(height: 12),
              itemBuilder: (context, i) {
                final m = rows[i];
                return Card(
                  elevation: 0,
                  color: const Color(0xFFF4F7F7),
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(12),
                    side: const BorderSide(color: Color(0xFFD7E2E2)),
                  ),
                  child: InkWell(
                    borderRadius: BorderRadius.circular(12),
                    onTap: () {
                      Navigator.of(context).push(
                        MaterialPageRoute(
                          builder: (_) => MissionDetailScreen(mission: m),
                        ),
                      );
                    },
                    child: Padding(
                      padding: const EdgeInsets.all(16),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            m.committeeName.isEmpty
                                ? 'لجنة #${m.committeeId}'
                                : m.committeeName,
                            style: const TextStyle(
                              fontWeight: FontWeight.bold,
                              fontSize: 18,
                            ),
                          ),
                          const SizedBox(height: 6),
                          Text('الحالة: ${m.status}'),
                          if (m.note != null && m.note!.isNotEmpty)
                            Text('ملاحظة: ${m.note}'),
                          const SizedBox(height: 8),
                          Wrap(
                            spacing: 8,
                            runSpacing: 8,
                            children: m.cityValues
                                .map(
                                  (c) => Chip(
                                    label: Text(ProvinceLabels.display(c)),
                                    backgroundColor: const Color(0xFF0F3D3E),
                                    labelStyle:
                                        const TextStyle(color: Colors.white),
                                  ),
                                )
                                .toList(),
                          ),
                        ],
                      ),
                    ),
                  ),
                );
              },
            ),
          );
        },
      ),
    );
  }
}
