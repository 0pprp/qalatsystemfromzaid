import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:latlong2/latlong.dart';

import '../../../core/theme/app_theme.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/widgets/common.dart';
import '../../../core/widgets/state_views.dart';
import '../../orders/data/orders_repository.dart';
import '../../orders/presentation/shop_capture_widgets.dart';
import '../data/prospects_repository.dart';
import '../data/work_controller.dart';
import '../data/work_repository.dart';
import 'auto_start.dart';
import 'prospect_sheets.dart';

/// Baghdad, used before the first GPS fix.
const _fallbackCenter = LatLng(33.3152, 44.3661);

/// Pins load only when this zoomed in (a neighbourhood), to keep requests small.
const _pinsMinZoom = 12.0;

/// «الدوام»: today's route on a live map, with start / finish of the working day, and the shops agents pinned
/// as potential customers («المحلات المحتملة»).
class WorkScreen extends ConsumerStatefulWidget {
  const WorkScreen({super.key});

  @override
  ConsumerState<WorkScreen> createState() => _WorkScreenState();
}

class _WorkScreenState extends ConsumerState<WorkScreen> {
  final _map = MapController();
  Timer? _tick;
  Timer? _pinsDebounce;
  bool _fitted = false;
  bool _follow = true;

  /// flutter_map 8.3 sends a late "size changed" event with the first camera right after the map is laid out. A
  /// camera move made before that event lands loses its tiles (the map stays grey until dragged), so programmatic
  /// moves wait until the map has settled.
  bool _mapBuilt = false;
  bool _mapSettled = false;
  List<LatLng>? _pendingFit;
  List<Prospect> _pins = const [];
  bool _pinsZoomedOut = false;
  int _pinsGen = 0;

  @override
  void initState() {
    super.initState();
    // Keeps the worked-time counter moving.
    _tick = Timer.periodic(const Duration(seconds: 30), (_) {
      if (mounted) setState(() {});
    });
  }

  @override
  void dispose() {
    _tick?.cancel();
    _pinsDebounce?.cancel();
    super.dispose();
  }

  // ---------------------------------------------------------------- pins

  void _schedulePins() {
    _pinsDebounce?.cancel();
    _pinsDebounce = Timer(const Duration(milliseconds: 700), _loadPins);
  }

  Future<void> _loadPins() async {
    if (!mounted) return;
    final camera = _map.camera;
    if (camera.zoom < _pinsMinZoom) {
      if (!_pinsZoomedOut) setState(() => _pinsZoomedOut = true);
      return;
    }
    final b = camera.visibleBounds;
    final gen = ++_pinsGen;
    try {
      final pins = await ref.read(prospectsRepositoryProvider).inArea(
            minLat: b.south,
            minLng: b.west,
            maxLat: b.north,
            maxLng: b.east,
          );
      if (!mounted || gen != _pinsGen) return;
      setState(() {
        _pins = pins;
        _pinsZoomedOut = false;
      });
    } catch (_) {
      // Offline: keep the pins already shown.
    }
  }

  Future<void> _addPin([LatLng? at]) async {
    final s = ref.read(workControllerProvider);
    // Where the agent stands (sent with the pin), and where the pin goes (a long-pressed point, or the same place).
    LatLng? me = s.position == null ? null : LatLng(s.position!.lat, s.position!.lng);
    if (me == null) {
      final fix = await currentPosition(context);
      if (!mounted) return;
      if (fix != null) me = LatLng(fix.latitude, fix.longitude);
    }
    final point = at ?? me ?? _map.camera.center;
    final saved = await showProspectForm(context,
        lat: point.latitude, lng: point.longitude, agentLat: me?.latitude, agentLng: me?.longitude);
    if (saved == null || !mounted) return;
    setState(() => _pins = [saved, ..._pins.where((p) => p.id != saved.id)]);
    ref.invalidate(overviewProvider);
    context.showSnack('تم تثبيت «${saved.shopName}» على الخريطة');
    // Pinning a shop means the agent is working: «الدوام» starts by itself.
    final wasWorking = ref.read(workControllerProvider).isWorking;
    await autoStartWorkDay(context, ref);
    if (mounted && !wasWorking && ref.read(workControllerProvider).isWorking) setState(() => _follow = true);
  }

  Future<void> _openPin(Prospect p) async {
    final action = await showProspectDetails(context, ref, p);
    if (!mounted) return;
    if (action != null) ref.invalidate(overviewProvider); // «ملخصي» counts pins per interest level
    if (action == 'deleted') {
      setState(() => _pins = _pins.where((x) => x.id != p.id).toList());
      context.showSnack('تم حذف المحل');
    } else if (action == 'edit') {
      final saved = await showProspectForm(context, lat: p.lat, lng: p.lng, editing: p);
      if (saved == null || !mounted) return;
      setState(() => _pins = [for (final x in _pins) x.id == saved.id ? saved : x]);
    }
  }

  Future<void> _end() async {
    final ok = await confirmDialog(
      context,
      title: 'إنهاء الدوام',
      message: 'سيتوقف تسجيل موقعك ويُحتسب وقت الدوام حتى الآن. هل تريد إنهاء الدوام؟',
      confirm: 'إنهاء الدوام',
      destructive: true,
    );
    if (!ok) return;
    final error = await ref.read(workControllerProvider.notifier).endDay();
    if (!mounted) return;
    context.showSnack(error ?? 'تم إنهاء الدوام. شكراً لجهودك!', error: error != null);
  }

  static CameraFit _routeFit(List<LatLng> points) =>
      CameraFit.coordinates(coordinates: points, padding: const EdgeInsets.fromLTRB(40, 200, 40, 260), maxZoom: 17);

  void _recenter(WorkState s) {
    final p = s.position ?? (s.fullTrack.isEmpty ? null : s.fullTrack.last);
    if (p == null || !_mapSettled) return;
    setState(() => _follow = true);
    _map.move(LatLng(p.lat, p.lng), 16);
  }

  @override
  Widget build(BuildContext context) {
    final s = ref.watch(workControllerProvider);
    final track = s.fullTrack;
    final points = [for (final p in track) LatLng(p.lat, p.lng)];
    final here = s.position == null ? (points.isEmpty ? null : points.last) : LatLng(s.position!.lat, s.position!.lng);

    // A shop pinned from «ملخصي» shows up here too.
    ref.listen(prospectsVersionProvider, (_, _) {
      if (_mapSettled) _loadPins();
    });

    // First load: show the whole route. Afterwards follow the agent unless they moved the map.
    ref.listen(workControllerProvider, (prev, next) {
      final pos = next.position;
      if (_mapSettled && _follow && pos != null && prev?.position != pos) {
        _map.move(LatLng(pos.lat, pos.lng), _map.camera.zoom < 13 ? 16 : _map.camera.zoom);
      }
    });
    // The route known at the first build is the map's starting camera; a route that arrives later is fitted once
    // the map has settled (see [_mapSettled]).
    CameraFit? initialFit;
    if (!_fitted && points.length >= 2) {
      _fitted = true;
      if (!_mapBuilt) {
        initialFit = _routeFit(points);
      } else if (_mapSettled) {
        WidgetsBinding.instance.addPostFrameCallback((_) {
          if (mounted) _map.fitCamera(_routeFit(points));
        });
      } else {
        _pendingFit = points;
      }
    }
    _mapBuilt = true;

    return Scaffold(
      body: Stack(
        children: [
          FlutterMap(
            mapController: _map,
            options: MapOptions(
              initialCenter: here ?? _fallbackCenter,
              initialZoom: here == null ? 11 : 16,
              initialCameraFit: initialFit,
              onMapReady: () {
                Future.delayed(const Duration(milliseconds: 300), () {
                  if (!mounted) return;
                  _mapSettled = true;
                  final pending = _pendingFit;
                  _pendingFit = null;
                  if (pending != null) _map.fitCamera(_routeFit(pending));
                });
                _schedulePins();
              },
              onPositionChanged: (_, hasGesture) {
                if (hasGesture && _follow) setState(() => _follow = false);
                _schedulePins();
              },
              onLongPress: (_, p) => _addPin(p),
            ),
            children: [
              osmTiles(),
              if (points.length >= 2)
                PolylineLayer(polylines: [
                  Polyline(points: points, strokeWidth: 5, color: AppColors.primary, borderStrokeWidth: 2, borderColor: Colors.white),
                ]),
              MarkerLayer(markers: [
                for (final pin in _pins)
                  Marker(
                    point: LatLng(pin.lat, pin.lng),
                    width: 40,
                    height: 40,
                    alignment: Alignment.topCenter,
                    child: GestureDetector(
                      onTap: () => _openPin(pin),
                      child: _ShopPin(color: pin.level.color, mine: pin.isMine),
                    ),
                  ),
              ]),
              MarkerLayer(markers: [
                if (points.isNotEmpty)
                  Marker(
                    point: points.first,
                    width: 34,
                    height: 34,
                    child: const _Pin(color: AppColors.success, icon: Icons.flag_rounded),
                  ),
                if (here != null)
                  Marker(
                    point: here,
                    width: 26,
                    height: 26,
                    child: Container(
                      decoration: BoxDecoration(
                        color: s.isWorking ? AppColors.info : AppColors.textMuted,
                        shape: BoxShape.circle,
                        border: Border.all(color: Colors.white, width: 4),
                        boxShadow: const [BoxShadow(color: Colors.black26, blurRadius: 6)],
                      ),
                    ),
                  ),
              ]),
            ],
          ),
          Positioned(
            top: 0,
            left: 0,
            right: 0,
            child: SafeArea(
              child: Padding(
                padding: const EdgeInsets.all(12),
                child: _StatusCard(state: s, onRefresh: () => ref.read(workControllerProvider.notifier).load()),
              ),
            ),
          ),
          if (_pinsZoomedOut)
            PositionedDirectional(
              start: 16,
              bottom: 206,
              child: Material(
                color: Colors.white,
                borderRadius: BorderRadius.circular(12),
                elevation: 2,
                child: const Padding(
                  padding: EdgeInsets.symmetric(horizontal: 10, vertical: 6),
                  child: Text('قرّب الخريطة لعرض المحلات المثبتة', style: TextStyle(fontSize: 12, color: AppColors.textMuted)),
                ),
              ),
            ),
          PositionedDirectional(
            start: 16,
            bottom: 150,
            child: FloatingActionButton.extended(
              heroTag: 'add-pin',
              tooltip: 'تثبيت محل محتمل (أو اضغط مطولاً على الخريطة)',
              backgroundColor: Colors.white,
              foregroundColor: AppColors.primary,
              onPressed: _addPin,
              icon: const Icon(Icons.add_location_alt_rounded),
              label: const Text('تثبيت محل', style: TextStyle(fontWeight: FontWeight.w800)),
            ),
          ),
          PositionedDirectional(
            end: 16,
            bottom: 150,
            child: FloatingActionButton.small(
              heroTag: 'recenter',
              tooltip: 'موقعي',
              backgroundColor: Colors.white,
              onPressed: () => _recenter(s),
              child: Icon(_follow ? Icons.my_location_rounded : Icons.location_searching_rounded, color: AppColors.primary),
            ),
          ),
          Positioned(
            left: 16,
            right: 16,
            bottom: 16,
            child: SafeArea(
              top: false,
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Row(
                    children: [
                      TextButton.icon(
                        style: TextButton.styleFrom(backgroundColor: Colors.white),
                        onPressed: () => context.go('/stores'),
                        icon: const Icon(Icons.storefront_rounded, size: 18),
                        label: const Text('محلاتي'),
                      ),
                      const Spacer(),
                      if ((s.day?.sessions.length ?? 0) > 0)
                        TextButton.icon(
                          style: TextButton.styleFrom(backgroundColor: Colors.white),
                          onPressed: () => _showSessions(context, s.day!),
                          icon: const Icon(Icons.history_rounded, size: 18),
                          label: Text('فترات اليوم (${s.day!.sessions.length})'),
                        ),
                    ],
                  ),
                  const SizedBox(height: 8),
                  // The day starts (or resumes) only by pinning a shop or sending a sale request; the agent can end it here.
                  SizedBox(
                    height: 58,
                    child: s.isWorking
                        ? FilledButton.icon(
                            style: FilledButton.styleFrom(
                              backgroundColor: AppColors.danger,
                              textStyle: const TextStyle(fontSize: 17, fontWeight: FontWeight.w800, fontFamily: AppTheme.bodyFont),
                              shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(18)),
                            ),
                            onPressed: s.busy || s.day == null ? null : _end,
                            icon: s.busy
                                ? const SizedBox(width: 22, height: 22, child: CircularProgressIndicator(strokeWidth: 2.5, color: Colors.white))
                                : const Icon(Icons.stop_circle_rounded, size: 26),
                            label: const Text('إنهاء الدوام'),
                          )
                        : FilledButton.icon(
                            style: FilledButton.styleFrom(
                              backgroundColor: AppColors.success,
                              textStyle: const TextStyle(fontSize: 16, fontWeight: FontWeight.w800, fontFamily: AppTheme.bodyFont),
                              shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(18)),
                            ),
                            onPressed: s.busy || s.day == null ? null : () => _addPin(),
                            icon: s.busy
                                ? const SizedBox(width: 22, height: 22, child: CircularProgressIndicator(strokeWidth: 2.5, color: Colors.white))
                                : const Icon(Icons.add_location_alt_rounded, size: 24),
                            label: const Text('ثبّت محلاً لبدء الدوام'),
                          ),
                  ),
                  if (!s.isWorking)
                    const Padding(
                      padding: EdgeInsets.only(top: 6),
                      child: Text(
                        'يبدأ الدوام تلقائياً عند تثبيت محل أو إرسال طلب بيع.',
                        textAlign: TextAlign.center,
                        style: TextStyle(fontSize: 12, color: AppColors.textMuted),
                      ),
                    ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }

  void _showSessions(BuildContext context, WorkDay day) {
    showModalBottomSheet<void>(
      context: context,
      builder: (_) => SafeArea(
        child: ListView(
          shrinkWrap: true,
          padding: const EdgeInsets.fromLTRB(20, 0, 20, 20),
          children: [
            Text('فترات الدوام اليوم', style: Theme.of(context).textTheme.titleLarge),
            const SizedBox(height: 4),
            Text('المجموع: ${Fmt.duration(day.total)}', style: const TextStyle(color: AppColors.textMuted)),
            const SizedBox(height: 12),
            for (final session in day.sessions)
              ListTile(
                contentPadding: EdgeInsets.zero,
                leading: CircleAvatar(
                  backgroundColor: (session.isOpen ? AppColors.success : AppColors.primary).withValues(alpha: 0.12),
                  child: Icon(session.isOpen ? Icons.play_arrow_rounded : Icons.check_rounded,
                      color: session.isOpen ? AppColors.success : AppColors.primary),
                ),
                title: Text('${Fmt.time(session.startedAt)} ← ${session.isOpen ? 'مستمر' : Fmt.time(session.endedAt)}'),
                subtitle: session.endedBy == 'auto' ? const Text('أُغلق تلقائياً بنهاية اليوم') : null,
                trailing: Text(Fmt.duration(session.duration), style: const TextStyle(fontWeight: FontWeight.w800)),
              ),
          ],
        ),
      ),
    );
  }
}

class _StatusCard extends StatelessWidget {
  const _StatusCard({required this.state, required this.onRefresh});

  final WorkState state;
  final VoidCallback onRefresh;

  @override
  Widget build(BuildContext context) {
    final s = state;
    final day = s.day;
    final working = s.isWorking;
    final color = working ? AppColors.success : AppColors.textMuted;
    return Material(
      color: Colors.white,
      elevation: 3,
      shadowColor: Colors.black26,
      borderRadius: BorderRadius.circular(20),
      child: Padding(
        padding: const EdgeInsets.fromLTRB(16, 12, 8, 12),
        child: day == null
            ? Row(
                children: [
                  Expanded(child: Text(s.loading ? 'جارٍ تحميل الدوام…' : (s.error ?? 'تعذّر تحميل الدوام'))),
                  if (s.loading)
                    const Padding(padding: EdgeInsets.all(12), child: SizedBox(width: 20, height: 20, child: CircularProgressIndicator(strokeWidth: 2)))
                  else
                    IconButton(onPressed: onRefresh, icon: const Icon(Icons.refresh_rounded)),
                ],
              )
            : Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Container(
                        width: 12,
                        height: 12,
                        decoration: BoxDecoration(color: color, shape: BoxShape.circle),
                      ),
                      const SizedBox(width: 8),
                      Expanded(
                        child: Text(
                          working
                              ? 'في الدوام منذ ${Fmt.time(day.current?.startedAt)}'
                              : day.startedToday
                                  ? 'خارج الدوام — انتهى ${Fmt.time(day.lastEndedAt)}'
                                  : 'لم يبدأ الدوام اليوم',
                          style: TextStyle(fontWeight: FontWeight.w800, fontSize: 15.5, color: working ? AppColors.text : AppColors.textMuted),
                        ),
                      ),
                      IconButton(visualDensity: VisualDensity.compact, onPressed: onRefresh, icon: const Icon(Icons.refresh_rounded)),
                    ],
                  ),
                  const SizedBox(height: 6),
                  Row(
                    children: [
                      _Figure(label: 'ساعات اليوم', value: Fmt.duration(day.total), color: AppColors.primary),
                      _Figure(label: 'بداية اليوم', value: Fmt.time(day.firstStartedAt)),
                      _Figure(label: 'نقاط الموقع', value: Fmt.number(s.fullTrack.length)),
                    ],
                  ),
                  if (working) ...[
                    const SizedBox(height: 6),
                    Row(
                      children: [
                        Icon(s.tracking ? Icons.gps_fixed_rounded : Icons.gps_off_rounded,
                            size: 15, color: s.tracking ? AppColors.success : AppColors.danger),
                        const SizedBox(width: 6),
                        Expanded(
                          child: Text(
                            s.error ??
                                (s.pending > 0
                                    ? '${s.pending} نقطة بانتظار الإرسال${s.lastSentAt == null ? '' : ' • آخر إرسال ${Fmt.time(s.lastSentAt)}'}'
                                    : s.lastSentAt == null
                                        ? 'يتم تسجيل موقعك'
                                        : 'تم إرسال موقعك ${Fmt.relative(s.lastSentAt)}'),
                            style: TextStyle(fontSize: 12, color: s.error != null ? AppColors.danger : AppColors.textMuted),
                          ),
                        ),
                      ],
                    ),
                  ],
                ],
              ),
      ),
    );
  }
}

class _Figure extends StatelessWidget {
  const _Figure({required this.label, required this.value, this.color = AppColors.text});

  final String label;
  final String value;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Expanded(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label, style: const TextStyle(color: AppColors.textMuted, fontSize: 11.5)),
          Text(value, style: TextStyle(fontWeight: FontWeight.w800, fontSize: 15, color: color)),
        ],
      ),
    );
  }
}

/// Map pin of a potential customer's shop, coloured by interest; the agent's own pins have a dark ring.
class _ShopPin extends StatelessWidget {
  const _ShopPin({required this.color, required this.mine});

  final Color color;
  final bool mine;

  @override
  Widget build(BuildContext context) {
    return Stack(
      alignment: Alignment.topCenter,
      children: [
        Icon(Icons.location_on_rounded, size: 40, color: mine ? AppColors.text : Colors.white),
        Padding(padding: const EdgeInsets.only(top: 3), child: Icon(Icons.location_on_rounded, size: 33, color: color)),
        const Padding(padding: EdgeInsets.only(top: 9), child: Icon(Icons.storefront_rounded, size: 14, color: Colors.white)),
      ],
    );
  }
}

class _Pin extends StatelessWidget {
  const _Pin({required this.color, required this.icon});

  final Color color;
  final IconData icon;

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        color: color,
        shape: BoxShape.circle,
        border: Border.all(color: Colors.white, width: 3),
        boxShadow: const [BoxShadow(color: Colors.black26, blurRadius: 4)],
      ),
      child: Icon(icon, color: Colors.white, size: 16),
    );
  }
}
