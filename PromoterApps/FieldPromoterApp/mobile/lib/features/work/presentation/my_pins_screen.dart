import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:latlong2/latlong.dart';
import 'package:url_launcher/url_launcher.dart';

import '../../../core/theme/app_theme.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/widgets/common.dart';
import '../../../core/widgets/paged_list.dart';
import '../../../core/widgets/skeleton.dart';
import '../../../core/widgets/state_views.dart';
import '../../notifications/data/notifications_repository.dart';
import '../../orders/data/orders_repository.dart';
import '../../orders/presentation/shop_capture_widgets.dart';
import '../data/prospects_repository.dart';
import 'prospect_sheets.dart';

/// The date ranges of the «المحلات» tab. Days are Iraq days.
enum _Range { today, yesterday, week, month, custom }

/// «المحلات» (tab): the shops the agent pinned, newest first, for a range of days (today by default),
/// and a button to pin a shop. Tapping a shop opens [PinDetailScreen].
class StoresScreen extends ConsumerStatefulWidget {
  const StoresScreen({super.key});

  @override
  ConsumerState<StoresScreen> createState() => _StoresScreenState();
}

class _StoresScreenState extends ConsumerState<StoresScreen> {
  _Range _range = _Range.today;
  DateTimeRange? _custom;
  bool _searching = false;
  String _q = '';
  int? _total;
  Timer? _debounce;

  @override
  void dispose() {
    _debounce?.cancel();
    super.dispose();
  }

  static DateTime get _today {
    final now = Fmt.iraq(DateTime.now());
    return DateTime(now.year, now.month, now.day);
  }

  static String _ymd(DateTime d) => '${d.year}-${d.month.toString().padLeft(2, '0')}-${d.day.toString().padLeft(2, '0')}';

  (DateTime, DateTime) get _days {
    final t = _today;
    return switch (_range) {
      _Range.today => (t, t),
      _Range.yesterday => (t.subtract(const Duration(days: 1)), t.subtract(const Duration(days: 1))),
      _Range.week => (t.subtract(const Duration(days: 6)), t),
      _Range.month => (DateTime(t.year, t.month), t),
      _Range.custom => (_custom?.start ?? t, _custom?.end ?? t),
    };
  }

  String _label(_Range r) => switch (r) {
        _Range.today => 'اليوم',
        _Range.yesterday => 'أمس',
        _Range.week => 'آخر 7 أيام',
        _Range.month => 'هذا الشهر',
        _Range.custom => _custom == null ? 'تاريخ محدد' : _rangeText(_custom!.start, _custom!.end),
      };

  static String _rangeText(DateTime a, DateTime b) => a == b ? '${a.day}/${a.month}/${a.year}' : '${a.day}/${a.month} – ${b.day}/${b.month}';

  Future<void> _pick(_Range r) async {
    if (r != _Range.custom) {
      setState(() => _range = r);
      return;
    }
    final t = _today;
    final picked = await showDateRangePicker(
      context: context,
      firstDate: DateTime(2026),
      lastDate: t,
      initialDateRange: _custom ?? DateTimeRange(start: t.subtract(const Duration(days: 6)), end: t),
      helpText: 'اختر الفترة',
      saveText: 'عرض',
    );
    if (picked == null || !mounted) return;
    setState(() {
      _custom = DateTimeRange(start: DateUtils.dateOnly(picked.start), end: DateUtils.dateOnly(picked.end));
      _range = _Range.custom;
    });
  }

  void _search(String v) {
    _debounce?.cancel();
    _debounce = Timer(const Duration(milliseconds: 450), () {
      if (mounted && v.trim() != _q) setState(() => _q = v.trim());
    });
  }

  @override
  Widget build(BuildContext context) {
    final version = ref.watch(prospectsVersionProvider);
    final unread = ref.watch(unreadCountProvider);
    final (from, to) = _days;
    final singleDay = from == to;

    return Scaffold(
      appBar: AppBar(
        title: _searching
            ? TextField(
                autofocus: true,
                onChanged: _search,
                textInputAction: TextInputAction.search,
                decoration: const InputDecoration(
                  hintText: 'ابحث باسم المحل أو نوعه أو الهاتف',
                  border: InputBorder.none,
                  enabledBorder: InputBorder.none,
                  focusedBorder: InputBorder.none,
                  filled: false,
                ),
              )
            : const Text('المحلات'),
        actions: [
          IconButton(
            tooltip: _searching ? 'إغلاق البحث' : 'بحث',
            icon: Icon(_searching ? Icons.close_rounded : Icons.search_rounded),
            onPressed: () => setState(() {
              _searching = !_searching;
              if (!_searching) _q = '';
            }),
          ),
          if (!_searching)
            IconButton(
              tooltip: 'الإشعارات',
              onPressed: () => context.push('/notifications'),
              icon: Badge(isLabelVisible: unread > 0, label: Text('$unread'), child: const Icon(Icons.notifications_none_rounded)),
            ),
          const SizedBox(width: 4),
        ],
      ),
      floatingActionButton: FloatingActionButton.extended(
        heroTag: 'stores-add',
        onPressed: () => pinShopHere(context, ref),
        icon: const Icon(Icons.add_location_alt_rounded),
        label: const Text('تثبيت محل', style: TextStyle(fontWeight: FontWeight.w800)),
      ),
      body: Column(
        children: [
          SizedBox(
            height: 48,
            child: ListView(
              scrollDirection: Axis.horizontal,
              padding: const EdgeInsets.fromLTRB(16, 6, 16, 6),
              children: [
                for (final r in _Range.values)
                  Padding(
                    padding: const EdgeInsetsDirectional.only(end: 8),
                    child: ChoiceChip(
                      selected: _range == r,
                      showCheckmark: false,
                      avatar: r == _Range.custom ? const Icon(Icons.date_range_rounded, size: 18) : null,
                      label: Text(_label(r)),
                      onSelected: (_) => _pick(r),
                    ),
                  ),
              ],
            ),
          ),
          Padding(
            padding: const EdgeInsets.fromLTRB(20, 2, 20, 4),
            child: Row(
              children: [
                Text(
                  _total == null ? ' ' : '${Fmt.number(_total!)} ${_total == 1 ? 'محل' : 'محلات'}',
                  style: const TextStyle(fontWeight: FontWeight.w800),
                ),
                const Spacer(),
                Text(_rangeText(from, to), style: const TextStyle(color: AppColors.textMuted, fontSize: 12.5)),
              ],
            ),
          ),
          Expanded(
            child: PagedListView<Prospect>(
              key: ValueKey('stores-${_ymd(from)}-${_ymd(to)}-$_q-$version'),
              fetch: (page) => ref.read(prospectsRepositoryProvider).mine(page: page, q: _q, from: _ymd(from), to: _ymd(to)),
              onLoaded: (p) {
                if (mounted && p.page <= 1 && _total != p.total) setState(() => _total = p.total);
              },
              onRefresh: () => ref.invalidate(overviewProvider),
              padding: const EdgeInsets.fromLTRB(16, 4, 16, 96),
              separator: 10,
              skeleton: const SkeletonList(count: 6, itemHeight: 96),
              empty: EmptyState(
                icon: Icons.storefront_outlined,
                title: _q.isNotEmpty
                    ? 'لا توجد محلات مطابقة'
                    : _range == _Range.today
                        ? 'لم تثبّت أي محل اليوم'
                        : 'لا توجد محلات في هذه الفترة',
                message: _range == _Range.today && _q.isEmpty ? 'اضغط «تثبيت محل» عند زيارة محل قد يشتري لاحقاً.' : null,
              ),
              itemBuilder: (context, p, _) => _PinTile(
                p: p,
                when: singleDay ? Fmt.time(p.createdAt) : Fmt.date(p.createdAt),
                onTap: () => context.push('/pins/${p.id}', extra: p),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _PinTile extends StatelessWidget {
  const _PinTile({required this.p, required this.when, required this.onTap});

  final Prospect p;
  final String when;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final l = p.level;
    return AppCard(
      onTap: onTap,
      padding: const EdgeInsets.fromLTRB(14, 12, 10, 12),
      child: Row(
        children: [
          CircleAvatar(
            radius: 22,
            backgroundColor: l.color.withValues(alpha: 0.15),
            child: Icon(Icons.storefront_rounded, color: l.color),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  p.shopName,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 15.5),
                ),
                const SizedBox(height: 3),
                Row(
                  children: [
                    InterestDots(level: p.interest, size: 7),
                    const SizedBox(width: 6),
                    Flexible(
                      child: Text(
                        l.label,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: TextStyle(color: l.color, fontWeight: FontWeight.w700, fontSize: 12.5),
                      ),
                    ),
                  ],
                ),
                if (p.description != null) ...[
                  const SizedBox(height: 3),
                  Text(
                    p.description!,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(color: AppColors.textMuted, fontSize: 12.5),
                  ),
                ],
              ],
            ),
          ),
          const SizedBox(width: 8),
          Column(
            crossAxisAlignment: CrossAxisAlignment.end,
            children: [
              Text(when, style: const TextStyle(color: AppColors.textMuted, fontSize: 12)),
              const SizedBox(height: 6),
              const Icon(Icons.chevron_left_rounded, color: AppColors.textMuted),
            ],
          ),
        ],
      ),
    );
  }
}

/// One pinned shop: its location on a map and everything the agent entered, with call / directions, and edit /
/// delete for the agent's own pins. [initial] (from the list) shows at once; the pin is reloaded by id otherwise.
class PinDetailScreen extends ConsumerStatefulWidget {
  const PinDetailScreen({super.key, required this.id, this.initial});

  final int id;
  final Prospect? initial;

  @override
  ConsumerState<PinDetailScreen> createState() => _PinDetailScreenState();
}

class _PinDetailScreenState extends ConsumerState<PinDetailScreen> {
  Prospect? _p;
  Object? _error;

  @override
  void initState() {
    super.initState();
    _p = widget.initial;
    if (_p == null) _load();
  }

  Future<void> _load() async {
    setState(() => _error = null);
    try {
      final p = await ref.read(prospectsRepositoryProvider).get(widget.id);
      if (mounted) setState(() => _p = p);
    } catch (e) {
      if (mounted) setState(() => _error = e);
    }
  }

  void _changed() {
    ref.read(prospectsVersionProvider.notifier).bump();
    ref.invalidate(overviewProvider);
  }

  Future<void> _edit(Prospect p) async {
    final saved = await showProspectForm(context, lat: p.lat, lng: p.lng, editing: p);
    if (saved == null || !mounted) return;
    setState(() => _p = saved);
    _changed();
    context.showSnack('تم حفظ التعديلات');
  }

  Future<void> _delete(Prospect p) async {
    final ok = await confirmDialog(
      context,
      title: 'حذف المحل',
      message: 'سيختفي «${p.shopName}» من الخريطة لك ولزملائك.',
      confirm: 'حذف',
      destructive: true,
    );
    if (!ok || !mounted) return;
    try {
      await ref.read(prospectsRepositoryProvider).delete(p.id);
      if (!mounted) return;
      _changed();
      context.showSnack('تم حذف المحل');
      context.pop();
    } catch (e) {
      if (mounted) context.showError(e);
    }
  }

  @override
  Widget build(BuildContext context) {
    final p = _p;
    return Scaffold(
      appBar: AppBar(title: Text(p?.shopName ?? 'المحل')),
      body: p == null
          ? (_error != null ? ErrorView(error: _error!, onRetry: _load) : const Center(child: CircularProgressIndicator()))
          : ListView(
              padding: const EdgeInsets.fromLTRB(16, 4, 16, 24),
              children: [
                ClipRRect(
                  borderRadius: BorderRadius.circular(16),
                  child: SizedBox(
                    height: 260,
                    child: FlutterMap(
                      key: ValueKey('${p.lat},${p.lng}'), // a moved pin gets a fresh map (no programmatic camera move)
                      options: MapOptions(initialCenter: LatLng(p.lat, p.lng), initialZoom: 17),
                      children: [
                        osmTiles(),
                        MarkerLayer(
                          markers: [
                            Marker(
                              point: LatLng(p.lat, p.lng),
                              width: 48,
                              height: 48,
                              alignment: Alignment.topCenter,
                              child: Icon(
                                Icons.location_on_rounded,
                                size: 48,
                                color: p.level.color,
                                shadows: const [Shadow(color: Colors.black38, blurRadius: 6)],
                              ),
                            ),
                          ],
                        ),
                      ],
                    ),
                  ),
                ),
                const SizedBox(height: 14),
                Row(
                  children: [
                    InterestDots(level: p.interest),
                    const SizedBox(width: 8),
                    Text(
                      p.level.label,
                      style: TextStyle(color: p.level.color, fontWeight: FontWeight.w800, fontSize: 15),
                    ),
                  ],
                ),
                const SizedBox(height: 8),
                AppCard(child: ProspectFacts(p: p)),
                const SizedBox(height: 12),
                Row(
                  children: [
                    if (p.phone != null) ...[
                      Expanded(
                        child: FilledButton.icon(
                          onPressed: () => launchUrl(Uri(scheme: 'tel', path: p.phone)),
                          icon: const Icon(Icons.call_rounded),
                          label: const Text('اتصال'),
                        ),
                      ),
                      const SizedBox(width: 8),
                    ],
                    Expanded(
                      child: OutlinedButton.icon(
                        onPressed: () => launchUrl(
                          Uri.parse('https://www.google.com/maps/dir/?api=1&destination=${p.lat},${p.lng}'),
                          mode: LaunchMode.externalApplication,
                        ),
                        icon: const Icon(Icons.directions_rounded),
                        label: const Text('الاتجاهات'),
                      ),
                    ),
                  ],
                ),
                if (p.isMine) ...[
                  const SizedBox(height: 8),
                  Row(
                    children: [
                      Expanded(
                        child: TextButton.icon(onPressed: () => _edit(p), icon: const Icon(Icons.edit_rounded), label: const Text('تعديل')),
                      ),
                      Expanded(
                        child: TextButton.icon(
                          style: TextButton.styleFrom(foregroundColor: AppColors.danger),
                          onPressed: () => _delete(p),
                          icon: const Icon(Icons.delete_outline_rounded),
                          label: const Text('حذف'),
                        ),
                      ),
                    ],
                  ),
                ],
              ],
            ),
    );
  }
}
