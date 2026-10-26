import 'dart:convert';

import 'package:follower_application/AsyncIdChecker.dart';
import 'package:follower_application/config/app_env.dart';
import 'package:follower_application/core/update/app_update_gate.dart';
import 'package:follower_application/list_detail_page.dart';
import 'package:follower_application/main.dart';
import 'package:follower_application/services/follower_assigned_tasks_service.dart';
import 'package:follower_application/services/follower_list_cards_cache.dart';
import 'package:follower_application/services/follower_tracking_repository.dart';
import 'package:follower_application/services/follower_warm_cache.dart';
import 'package:follower_application/tracking/follower_shift_debug.dart';
import 'package:follower_application/tracking/shift_gate_coordinator.dart';
import 'package:follower_application/tracking/shift_tracking_controller.dart';
import 'package:follower_application/tracking/work_shift.dart';
import 'package:follower_application/tutorial_questions_page.dart';
import 'package:follower_application/ui/announcements_banner.dart';
import 'package:follower_application/ui/day_rating_gate.dart';
import 'package:follower_application/ui/follower_complaint_sheet.dart';
import 'package:follower_application/ui/rating_trend_row.dart';
import 'package:follower_application/ui/stripe_container.dart';
import 'package:follower_application/utils/AppTheme.dart';
import 'package:follower_application/utils/display_name.dart';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';

/// Home: list cards with phantom score + month/week trends (cache-first).
class FollowerListsHome extends StatefulWidget {
  const FollowerListsHome({super.key});

  @override
  State<FollowerListsHome> createState() => _FollowerListsHomeState();
}

class _FollowerListsHomeState extends State<FollowerListsHome> {
  bool _loading = true;
  bool _refreshing = false;
  bool _shiftBusy = false;
  bool _committeeTasksUnread = false;
  String _error = '';
  String _shiftError = '';
  String _delegateName = '';
  WorkShift? _activeShift;
  late final ShiftTrackingController _tracking;
  List<Map<String, dynamic>> _cards = [];

  @override
  void initState() {
    super.initState();
    _tracking = TrackingRuntime.instance ??=
        ShiftTrackingController(repository: ApiFollowerTrackingRepository());
    _boot();
  }

  Future<void> _boot() async {
    // Paint cached cards immediately (no spinner if we have data).
    final cached = await FollowerListCardsCache.load();
    if (cached != null && cached.isNotEmpty && mounted) {
      setState(() {
        _cards = cached;
        _loading = false;
      });
    }

    if (!ShiftGateCoordinator.recentlyConfirmedHome) {
      final dest = await ShiftGateCoordinator(tracking: _tracking)
          .resolve(attachIfActive: true);
      if (!mounted) return;
      if (dest != ShiftGateDestination.home) {
        Navigator.pushNamedAndRemoveUntil(
          context,
          ShiftGateCoordinator.routeFor(dest),
          (r) => false,
        );
        return;
      }
      ShiftGateCoordinator.markHomeOk();
    }
    if (mounted) {
      setState(() => _activeShift = _tracking.activeShift);
    }
    await _loadCards(silent: _cards.isNotEmpty);
    // ignore: unawaited_futures
    _refreshCommitteeUnread();
    // Warm every major endpoint so navigation is cache-first.
    // ignore: unawaited_futures
    FollowerWarmCache.warmAll();
    if (mounted) await AppUpdateGate.checkAndPrompt(context);
  }

  Future<void> _refreshCommitteeUnread() async {
    final unread = await FollowerAssignedTasksService.hasUnreadBadge();
    if (!mounted) return;
    setState(() => _committeeTasksUnread = unread);
  }

  Future<Map<String, String>> _session() async {
    final prefs = await SharedPreferences.getInstance();
    return {
      'asyncId': prefs.getString('AsyncId') ?? '',
      'link': AppEnv.apiBase(fallback: prefs.getString('LinkDelegate') ?? ''),
      'name': prefs.getString('DelegateName') ?? '',
    };
  }

  Map<String, dynamic> _mapCard(Map m) {
    return {
      'listId': int.tryParse('${m['listId'] ?? m['ListId'] ?? 0}') ?? 0,
      'listName': '${m['listName'] ?? m['ListName'] ?? 'قائمة'}',
      'score': double.tryParse('${m['score'] ?? m['Score'] ?? 0}') ?? 0.0,
      'ratingLabel': '${m['ratingLabel'] ?? m['RatingLabel'] ?? ''}',
      'scoreWeekAgo': _toD(m['scoreWeekAgo'] ?? m['ScoreWeekAgo']),
      'trend': '${m['trend'] ?? m['Trend'] ?? 'flat'}'.toLowerCase(),
      'scoreMonthAgo': _toD(m['scoreMonthAgo'] ?? m['ScoreMonthAgo']),
      'monthTrend':
          '${m['monthTrend'] ?? m['MonthTrend'] ?? 'flat'}'.toLowerCase(),
    };
  }

  double? _toD(dynamic v) {
    if (v == null) return null;
    return double.tryParse('$v');
  }

  Future<void> _loadCards({bool silent = false}) async {
    if (!silent) {
      setState(() {
        _loading = _cards.isEmpty;
        _refreshing = _cards.isNotEmpty;
        _error = '';
      });
    } else if (mounted) {
      setState(() => _refreshing = true);
    }
    try {
      final s = await _session();
      _delegateName = s['name'] ?? '';
      final uri = Uri.parse('${s['link']}Followers/ListCards')
          .replace(queryParameters: {'asyncId': s['asyncId']});
      final res = await http.get(uri).timeout(const Duration(seconds: 45));
      if (res.statusCode == 401) {
        await AsyncIdChecker.logout();
        if (mounted) Navigator.pushReplacementNamed(context, '/Login');
        return;
      }
      if (res.statusCode != 200) throw Exception('cards');
      final data = json.decode(res.body) as Map;
      final raw = data['items'] ?? data['Items'] ?? [];
      final cards = (raw as List)
          .map((e) => _mapCard(Map<String, dynamic>.from(e as Map)))
          .where((c) => (c['listId'] as int) > 0)
          .toList();
      await FollowerListCardsCache.save(cards);
      if (mounted) {
        setState(() {
          _cards = cards;
          _loading = false;
          _refreshing = false;
          _error = '';
        });
      }
    } catch (_) {
      if (mounted) {
        setState(() {
          _loading = false;
          _refreshing = false;
          if (_cards.isEmpty) {
            _error = 'تعذر تحميل كروت القوائم';
          }
        });
      }
    }
  }

  Future<void> _startShift() async {
    setState(() {
      _shiftBusy = true;
      _shiftError = '';
    });
    try {
      final shift = await _tracking.startShiftFlow();
      if (!mounted) return;
      setState(() {
        _shiftBusy = false;
        _activeShift = shift ?? _tracking.activeShift;
        _shiftError =
            shift == null ? (_tracking.lastError ?? FollowerShiftDebug.generic) : '';
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _shiftBusy = false;
        _shiftError = FollowerShiftDebug.apiFailure(e);
      });
    }
  }

  Future<void> _endShift() async {
    setState(() {
      _shiftBusy = true;
      _shiftError = '';
    });
    try {
      final rated = await ensureDayRatingsBeforeEndShift(context);
      if (!rated) {
        if (mounted) {
          setState(() {
            _shiftBusy = false;
            _shiftError = 'يجب تقييم مناديب الجباية قبل إنهاء الدوام';
          });
        }
        return;
      }
      await _tracking.endShiftFlow();
      if (!mounted) return;
      setState(() {
        _shiftBusy = false;
        _activeShift = null;
      });
      Navigator.pushNamedAndRemoveUntil(context, '/StartShift', (r) => false);
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _shiftBusy = false;
        _shiftError = FollowerShiftDebug.apiFailure(e);
      });
    }
  }

  Future<void> _openTutorialVideo() async {
    if (!mounted) return;
    await Navigator.of(context).push(
      MaterialPageRoute(builder: (_) => const TutorialQuestionsPage()),
    );
  }

  Future<void> _openNewSalesRequest() async {
    if (_cards.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text(
            'لا توجد قوائم مسندة — لا يمكن إرسال طلب مبيع',
            style: TextStyle(fontFamily: 'Cairo'),
          ),
        ),
      );
      return;
    }
    int? listId = _cards.length == 1 ? _cards.first['listId'] as int : null;
    if (listId == null) {
      listId = await showDialog<int>(
        context: context,
        builder: (ctx) => Directionality(
          textDirection: TextDirection.rtl,
          child: SimpleDialog(
            title: const Text('اختر القائمة',
                style: TextStyle(fontFamily: 'Cairo', fontWeight: FontWeight.bold)),
            children: _cards
                .map(
                  (c) => SimpleDialogOption(
                    onPressed: () =>
                        Navigator.pop(ctx, c['listId'] as int),
                    child: Text(
                      '${c['listName']}',
                      style: const TextStyle(fontFamily: 'Cairo', fontSize: 15),
                    ),
                  ),
                )
                .toList(),
          ),
        ),
      );
    }
    if (listId == null || listId <= 0 || !mounted) return;
    await Navigator.pushNamed(
      context,
      '/FollowerSalesRequest',
      arguments: {'listId': listId},
    );
  }

  double? _avgScore(String key) {
    if (_cards.isEmpty) return null;
    var sum = 0.0;
    var n = 0;
    for (final c in _cards) {
      final v = c[key];
      if (v is double) {
        sum += v;
        n++;
      } else if (v != null) {
        final d = double.tryParse('$v');
        if (d != null) {
          sum += d;
          n++;
        }
      }
    }
    if (n == 0) return null;
    return sum / n;
  }

  String _majorityTrend(String key) {
    if (_cards.isEmpty) return 'flat';
    var up = 0, down = 0, flat = 0;
    for (final c in _cards) {
      switch ('${c[key] ?? 'flat'}'.toLowerCase()) {
        case 'up':
          up++;
          break;
        case 'down':
          down++;
          break;
        default:
          flat++;
      }
    }
    if (up >= down && up >= flat) return 'up';
    if (down >= up && down >= flat) return 'down';
    return 'flat';
  }

  /// متوسط التقييم الوهمي لكل القوائم المعروضة (رقم فقط، بدون تسمية).
  String? _phantomScoreLabel() {
    final avg = _avgScore('score');
    if (avg == null) return null;
    return avg == avg.roundToDouble()
        ? avg.toStringAsFixed(0)
        : avg.toStringAsFixed(1);
  }

  Widget _phantomScoreText(String value, {double fontSize = 20}) {
    return Container(
      margin: const EdgeInsetsDirectional.only(start: 2),
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(10),
        border: Border.all(color: Colors.black.withValues(alpha: 0.08)),
      ),
      child: Text(
        value,
        style: TextStyle(
          fontFamily: 'Cairo',
          fontWeight: FontWeight.w900,
          fontSize: fontSize,
          color: Colors.black,
          height: 1.1,
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final headerScore = _phantomScoreLabel();
    final monthCurrent = _avgScore('score') ?? 0;
    final monthPrev = _avgScore('scoreMonthAgo');
    final monthTrend = _majorityTrend('monthTrend');

    return RefreshIndicator(
      onRefresh: () => _loadCards(silent: false),
      color: AppTheme.primaryColor,
      child: ListView(
        padding: const EdgeInsets.fromLTRB(16, 12, 16, 24),
        children: [
          Row(
            children: [
              IconButton(
                onPressed: () {
                  MyApp.themeNotifier.value =
                      MyApp.themeNotifier.value == ThemeMode.light
                          ? ThemeMode.dark
                          : ThemeMode.light;
                  setState(() {});
                },
                icon: Icon(
                  MyApp.themeNotifier.value == ThemeMode.light
                      ? Icons.dark_mode
                      : Icons.light_mode,
                ),
              ),
              if (headerScore != null) _phantomScoreText(headerScore),
              if (_refreshing) ...[
                const SizedBox(width: 8),
                const SizedBox(
                  width: 16,
                  height: 16,
                  child: CircularProgressIndicator(strokeWidth: 2),
                ),
              ],
              const Spacer(),
              Column(
                crossAxisAlignment: CrossAxisAlignment.end,
                children: [
                  const Text(
                    'تطبيق مسؤول اللجنة',
                    style: TextStyle(
                      fontFamily: 'Cairo',
                      fontWeight: FontWeight.bold,
                      fontSize: 16,
                    ),
                  ),
                  Text(
                    _delegateName.isEmpty ? '—' : _delegateName,
                    style: const TextStyle(
                      fontFamily: 'Cairo',
                      fontSize: 13,
                      fontWeight: FontWeight.w600,
                      color: Colors.grey,
                    ),
                  ),
                ],
              ),
            ],
          ),
          const SizedBox(height: 12),
          const AnnouncementsBanner(),
          const SizedBox(height: 12),
          SizedBox(
            width: double.infinity,
            child: ElevatedButton.icon(
              onPressed: () async {
                await Navigator.pushNamed(context, '/CommitteeTasks');
                if (mounted) await _refreshCommitteeUnread();
              },
              icon: Icon(
                _committeeTasksUnread
                    ? Icons.assignment_late
                    : Icons.assignment_outlined,
                color: Colors.white,
              ),
              label: Text(
                _committeeTasksUnread
                    ? 'مهام مسؤول اللجنة — جديدة'
                    : 'مهام مسؤول اللجنة',
                style: const TextStyle(
                  fontFamily: 'Cairo',
                  color: Colors.white,
                  fontWeight: FontWeight.bold,
                  fontSize: 15,
                ),
              ),
              style: ElevatedButton.styleFrom(
                backgroundColor: _committeeTasksUnread
                    ? const Color(0xFFE65100)
                    : AppTheme.primaryColor,
                padding: const EdgeInsets.symmetric(vertical: 13),
                shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(12),
                ),
              ),
            ),
          ),
          const SizedBox(height: 12),
          Container(
            padding: const EdgeInsets.all(14),
            decoration: BoxDecoration(
              color: Theme.of(context).cardColor,
              borderRadius: BorderRadius.circular(16),
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Text(
                  _activeShift != null && _activeShift!.isActive
                      ? 'الدوام فعال — #${_activeShift!.shiftId}'
                      : 'الدوام غير فعال',
                  style: const TextStyle(
                    fontFamily: 'Cairo',
                    fontWeight: FontWeight.bold,
                  ),
                ),
                if (_shiftError.isNotEmpty) ...[
                  const SizedBox(height: 6),
                  Text(_shiftError,
                      style: const TextStyle(
                          fontFamily: 'Cairo', color: Colors.red, fontSize: 12)),
                ],
                const SizedBox(height: 10),
                Row(
                  children: [
                    Expanded(
                      child: ElevatedButton.icon(
                        onPressed: _shiftBusy || (_activeShift?.isActive ?? false)
                            ? null
                            : _startShift,
                        icon: const Icon(Icons.play_arrow, size: 18),
                        label: const Text('بدء الدوام',
                            style: TextStyle(fontFamily: 'Cairo', fontSize: 13)),
                        style: ElevatedButton.styleFrom(
                          backgroundColor: Colors.green.shade700,
                          foregroundColor: Colors.white,
                        ),
                      ),
                    ),
                    const SizedBox(width: 8),
                    Expanded(
                      child: OutlinedButton.icon(
                        onPressed:
                            _shiftBusy || !(_activeShift?.isActive ?? false)
                                ? null
                                : _endShift,
                        icon: const Icon(Icons.stop, size: 18),
                        label: const Text('إنهاء الدوام',
                            style: TextStyle(fontFamily: 'Cairo', fontSize: 13)),
                      ),
                    ),
                  ],
                ),
              ],
            ),
          ),
          const SizedBox(height: 12),
          SizedBox(
            width: double.infinity,
            child: ElevatedButton.icon(
              onPressed: _openTutorialVideo,
              icon: const Icon(Icons.play_circle_filled_rounded,
                  color: Colors.white, size: 28),
              label: const Text(
                'مشاهدة الفيديو التعليمي',
                style: TextStyle(
                  fontFamily: 'Cairo',
                  color: Colors.white,
                  fontWeight: FontWeight.bold,
                  fontSize: 16,
                ),
              ),
              style: ElevatedButton.styleFrom(
                backgroundColor: const Color(0xFFFFC107),
                padding: const EdgeInsets.symmetric(vertical: 14),
                shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(12)),
              ),
            ),
          ),
          const SizedBox(height: 10),
          SizedBox(
            width: double.infinity,
            child: OutlinedButton.icon(
              onPressed: () =>
                  FollowerComplaintSheet.show(context, asNote: true),
              icon: const Icon(Icons.note_alt_outlined, size: 22),
              label: const Text(
                'إرسال ملاحظة إلى المدير',
                style: TextStyle(
                  fontFamily: 'Cairo',
                  fontWeight: FontWeight.bold,
                  fontSize: 15,
                ),
              ),
              style: OutlinedButton.styleFrom(
                foregroundColor: AppTheme.primaryColor,
                side: BorderSide(color: AppTheme.primaryColor),
                padding: const EdgeInsets.symmetric(vertical: 12),
                shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(12)),
              ),
            ),
          ),
          const SizedBox(height: 10),
          SizedBox(
            width: double.infinity,
            child: ElevatedButton.icon(
              onPressed: _openNewSalesRequest,
              icon: const Icon(Icons.add_shopping_cart_outlined, size: 22),
              label: const Text(
                'طلب مبيع جديد',
                style: TextStyle(
                  fontFamily: 'Cairo',
                  fontWeight: FontWeight.bold,
                  fontSize: 15,
                  color: Colors.white,
                ),
              ),
              style: ElevatedButton.styleFrom(
                backgroundColor: AppTheme.primaryColor,
                padding: const EdgeInsets.symmetric(vertical: 12),
                shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(12)),
              ),
            ),
          ),
          if (_cards.isNotEmpty) ...[
            const SizedBox(height: 14),
            const Text(
              'تقييم مسؤول اللجنة',
              style: TextStyle(
                fontFamily: 'Cairo',
                fontWeight: FontWeight.bold,
                fontSize: 14,
                color: AppTheme.primaryColor,
              ),
            ),
            const SizedBox(height: 8),
            RatingTrendRow(
              previousLabel: 'الشهر السابق',
              currentLabel: 'الشهر الحالي',
              previousScore: monthPrev,
              currentScore: monthCurrent,
              trend: monthTrend,
              stripeIndex: 0,
            ),
          ],
          const SizedBox(height: 16),
          if (_loading)
            const Padding(
              padding: EdgeInsets.all(40),
              child: Center(
                  child: CircularProgressIndicator(color: AppTheme.primaryColor)),
            )
          else if (_error.isNotEmpty)
            Text(_error,
                style: const TextStyle(fontFamily: 'Cairo', color: Colors.red))
          else if (_cards.isEmpty)
            const Padding(
              padding: EdgeInsets.all(30),
              child: Center(
                child: Text(
                  'لا توجد قوائم مخصصة لك حاليًا',
                  style: TextStyle(
                      fontFamily: 'Cairo',
                      fontWeight: FontWeight.bold,
                      color: Colors.grey),
                ),
              ),
            )
          else
            ...withNameSeparators(_cards.asMap().entries.map((entry) {
              final i = entry.key;
              final c = entry.value;
              final monthTrend = '${c['monthTrend'] ?? 'flat'}';
              final score = (c['score'] as double);
              final scoreText = score == score.roundToDouble()
                  ? score.toStringAsFixed(0)
                  : score.toStringAsFixed(1);
              final prevMonth = c['scoreMonthAgo'] as double?;
              final stripe = i + 1; // after تقييم مسؤول اللجنة (0)
              return Material(
                color: Colors.transparent,
                borderRadius: BorderRadius.circular(16),
                child: InkWell(
                  borderRadius: BorderRadius.circular(16),
                  onTap: () {
                    Navigator.push(
                      context,
                      MaterialPageRoute(
                        builder: (_) => ListDetailPage(
                          listId: c['listId'] as int,
                          listName: c['listName'] as String,
                        ),
                      ),
                    );
                  },
                  child: Container(
                    padding: const EdgeInsets.all(14),
                    decoration: StripeContainer.decoration(stripe),
                    child: Column(
                      children: [
                        Row(
                          children: [
                            Icon(
                              RatingTrendRow.trendIcon(monthTrend),
                              color: RatingTrendRow.trendColor(monthTrend),
                            ),
                            const SizedBox(width: 10),
                            Expanded(
                              child: Text(
                                c['listName'] as String,
                                style: const TextStyle(
                                  fontFamily: 'Cairo',
                                  fontWeight: FontWeight.bold,
                                  fontSize: 16,
                                ),
                              ),
                            ),
                            _phantomScoreText(scoreText, fontSize: 18),
                            const SizedBox(width: 4),
                            const Icon(Icons.chevron_left),
                          ],
                        ),
                        const SizedBox(height: 10),
                        RatingTrendRow(
                          previousLabel: 'الشهر السابق',
                          currentLabel: 'الحالي',
                          previousScore: prevMonth,
                          currentScore: score,
                          trend: monthTrend,
                          compact: true,
                          stripeIndex: stripe,
                        ),
                      ],
                    ),
                  ),
                ),
              );
            })),
        ],
      ),
    );
  }
}
