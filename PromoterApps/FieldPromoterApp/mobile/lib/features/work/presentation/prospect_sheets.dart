import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:url_launcher/url_launcher.dart';

import '../../../core/network/api_exception.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/widgets/common.dart';
import '../../../core/widgets/state_views.dart';
import '../../orders/data/orders_repository.dart';
import '../../orders/presentation/shop_capture_widgets.dart';
import '../data/prospects_repository.dart';
import '../data/work_controller.dart';
import 'auto_start.dart';

/// «إضافة محل»: pin a shop that may buy later. Returns the saved pin, or null when cancelled.
///
/// [agentLat]/[agentLng]: where the agent is standing now (sent with a new pin; the dashboard shows it when the pin was
/// moved away from it). When unknown, the live work-day position is used.
Future<Prospect?> showProspectForm(BuildContext context,
        {required double lat, required double lng, Prospect? editing, double? agentLat, double? agentLng}) =>
    showModalBottomSheet<Prospect>(
      context: context,
      isScrollControlled: true,
      useSafeArea: true,
      useRootNavigator: true, // above the bottom tab bar
      builder: (_) => _ProspectForm(lat: lat, lng: lng, editing: editing, agentLat: agentLat, agentLng: agentLng),
    );

/// «تثبيت محل» from anywhere in the app: opens the pin form at the agent's current location (the live work-day
/// position when tracking, otherwise a fresh GPS fix). Lists and the «الدوام» map reload after saving.
Future<Prospect?> pinShopHere(BuildContext context, WidgetRef ref) async {
  final pos = ref.read(workControllerProvider).position;
  double lat, lng;
  if (pos != null) {
    (lat, lng) = (pos.lat, pos.lng);
  } else {
    final fix = await currentPosition(context);
    if (fix == null || !context.mounted) return null;
    (lat, lng) = (fix.latitude, fix.longitude);
  }
  final saved = await showProspectForm(context, lat: lat, lng: lng, agentLat: lat, agentLng: lng);
  if (saved == null || !context.mounted) return null;
  ref.read(prospectsVersionProvider.notifier).bump();
  ref.invalidate(overviewProvider);
  context.showSnack('تم تثبيت «${saved.shopName}» على الخريطة');
  await autoStartWorkDay(context, ref);
  return saved;
}

class _ProspectForm extends ConsumerStatefulWidget {
  const _ProspectForm({required this.lat, required this.lng, this.editing, this.agentLat, this.agentLng});

  final double lat;
  final double lng;
  final Prospect? editing;
  final double? agentLat;
  final double? agentLng;

  @override
  ConsumerState<_ProspectForm> createState() => _ProspectFormState();
}

class _ProspectFormState extends ConsumerState<_ProspectForm> {
  late double _lat = widget.editing?.lat ?? widget.lat;
  late double _lng = widget.editing?.lng ?? widget.lng;
  late final _name = TextEditingController(text: widget.editing?.shopName);
  late int? _interest = widget.editing?.interest;
  Map<String, String> _errors = {};
  bool _saving = false;

  @override
  void dispose() {
    _name.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    final errors = <String, String>{};
    final name = _name.text.trim();
    if (name.length < 2) errors['shopName'] = 'أدخل اسم المحل';
    if (_interest == null) errors['interest'] = 'اختر مدى اهتمام صاحب المحل';
    setState(() => _errors = errors);
    if (errors.isNotEmpty) return;

    // New pins carry where the agent stood (the pin may have been dragged elsewhere).
    final live = ref.read(workControllerProvider).position;
    final agentLat = widget.editing != null ? null : widget.agentLat ?? live?.lat;
    final agentLng = widget.editing != null ? null : widget.agentLng ?? live?.lng;
    final input = ProspectInput(
      lat: _lat,
      lng: _lng,
      shopName: name,
      interest: _interest!,
      // The form only asks for the name, interest and location; an edit keeps what older versions recorded.
      description: widget.editing?.description,
      phone: widget.editing?.phone,
      goodsValue: widget.editing?.goodsValue,
      lengthM: widget.editing?.lengthM,
      widthM: widget.editing?.widthM,
      agentLat: agentLat,
      agentLng: agentLng,
    );
    setState(() => _saving = true);
    try {
      final repo = ref.read(prospectsRepositoryProvider);
      final saved = widget.editing == null ? await repo.create(input) : await repo.update(widget.editing!.id, input);
      if (mounted) Navigator.pop(context, saved);
    } catch (e) {
      if (!mounted) return;
      final ex = ApiException.from(e);
      setState(() {
        _saving = false;
        _errors = ex.fields;
      });
      if (ex.fields.isEmpty) context.showSnack(ex.message, error: true);
    }
  }

  @override
  Widget build(BuildContext context) {
    final bottom = MediaQuery.viewInsetsOf(context).bottom;
    return Padding(
      padding: EdgeInsets.only(bottom: bottom),
      child: DraggableScrollableSheet(
        expand: false,
        initialChildSize: 0.92,
        maxChildSize: 0.96,
        minChildSize: 0.5,
        builder: (context, scroll) => ListView(
          controller: scroll,
          padding: const EdgeInsets.fromLTRB(20, 4, 20, 24),
          children: [
            Text(widget.editing == null ? 'تثبيت محل محتمل' : 'تعديل المحل', style: Theme.of(context).textTheme.titleLarge),
            const SizedBox(height: 4),
            const Text(
              'يظهر هذا الدبوس لك ولزملائك عند المرور بالمنطقة، وللإدارة في لوحة التحكم.',
              style: TextStyle(color: AppColors.textMuted, fontSize: 12.5, height: 1.5),
            ),
            const SizedBox(height: 14),
            ShopLocationCard(
              latitude: _lat,
              longitude: _lng,
              errorText: _errors['latitude'],
              onChanged: (lat, lng) => setState(() {
                _lat = lat;
                _lng = lng;
              }),
            ),
            const SizedBox(height: 14),
            TextField(
              controller: _name,
              textInputAction: TextInputAction.next,
              decoration: InputDecoration(labelText: 'اسم المحل *', prefixIcon: const Icon(Icons.storefront_rounded), errorText: _errors['shopName']),
            ),
            const SizedBox(height: 14),
            Text('مدى اهتمام صاحب المحل *', style: Theme.of(context).textTheme.titleSmall),
            const SizedBox(height: 8),
            for (final l in InterestLevel.all.reversed)
              Padding(
                padding: const EdgeInsets.only(bottom: 6),
                child: _InterestOption(level: l, selected: _interest == l.value, onTap: () => setState(() => _interest = l.value)),
              ),
            if (_errors['interest'] != null)
              Text(_errors['interest']!, style: const TextStyle(color: AppColors.danger, fontSize: 12.5)),
            const SizedBox(height: 20),
            FilledButton.icon(
              style: FilledButton.styleFrom(minimumSize: const Size.fromHeight(52)),
              onPressed: _saving ? null : _save,
              icon: _saving
                  ? const SizedBox(width: 20, height: 20, child: CircularProgressIndicator(strokeWidth: 2.5, color: Colors.white))
                  : const Icon(Icons.push_pin_rounded),
              label: Text(widget.editing == null ? 'تثبيت المحل على الخريطة' : 'حفظ التعديلات'),
            ),
          ],
        ),
      ),
    );
  }
}

class _InterestOption extends StatelessWidget {
  const _InterestOption({required this.level, required this.selected, required this.onTap});

  final InterestLevel level;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: selected ? level.color.withValues(alpha: 0.12) : Colors.white,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(14),
        side: BorderSide(color: selected ? level.color : AppColors.border, width: selected ? 2 : 1),
      ),
      child: InkWell(
        borderRadius: BorderRadius.circular(14),
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 11),
          child: Row(
            children: [
              InterestDots(level: level.value),
              const SizedBox(width: 12),
              Expanded(child: Text(level.label, style: TextStyle(fontWeight: selected ? FontWeight.w800 : FontWeight.w600))),
              Icon(selected ? Icons.radio_button_checked_rounded : Icons.radio_button_off_rounded, color: selected ? level.color : AppColors.textMuted),
            ],
          ),
        ),
      ),
    );
  }
}

/// Five dots, the first [level] filled in the level's colour.
class InterestDots extends StatelessWidget {
  const InterestDots({super.key, required this.level, this.size = 10});

  final int level;
  final double size;

  @override
  Widget build(BuildContext context) {
    final color = InterestLevel.of(level).color;
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        for (var i = 1; i <= 5; i++)
          Container(
            width: size,
            height: size,
            margin: const EdgeInsetsDirectional.only(end: 3),
            decoration: BoxDecoration(color: i <= level ? color : AppColors.border, shape: BoxShape.circle),
          ),
      ],
    );
  }
}

/// Details of a pin. Returns 'edit' or 'deleted' when the agent acted on their own pin.
Future<String?> showProspectDetails(BuildContext context, WidgetRef ref, Prospect p) => showModalBottomSheet<String>(
      context: context,
      isScrollControlled: true,
      useSafeArea: true,
      useRootNavigator: true, // above the bottom tab bar
      builder: (ctx) => SafeArea(
        child: Padding(
          padding: const EdgeInsets.fromLTRB(20, 4, 20, 16),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Row(
                children: [
                  CircleAvatar(
                    backgroundColor: p.level.color.withValues(alpha: 0.15),
                    child: Icon(Icons.storefront_rounded, color: p.level.color),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(p.shopName, style: Theme.of(ctx).textTheme.titleLarge),
                        Row(children: [
                          InterestDots(level: p.interest, size: 8),
                          const SizedBox(width: 6),
                          Text(p.level.label, style: TextStyle(color: p.level.color, fontWeight: FontWeight.w700, fontSize: 13)),
                        ]),
                      ],
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 12),
              ProspectFacts(p: p),
              const SizedBox(height: 12),
              Row(
                children: [
                  if (p.phone != null)
                    Expanded(
                      child: FilledButton.icon(
                        onPressed: () => launchUrl(Uri(scheme: 'tel', path: p.phone)),
                        icon: const Icon(Icons.call_rounded),
                        label: const Text('اتصال'),
                      ),
                    ),
                  if (p.phone != null) const SizedBox(width: 8),
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
                      child: TextButton.icon(
                        onPressed: () => Navigator.pop(ctx, 'edit'),
                        icon: const Icon(Icons.edit_rounded),
                        label: const Text('تعديل'),
                      ),
                    ),
                    Expanded(
                      child: TextButton.icon(
                        style: TextButton.styleFrom(foregroundColor: AppColors.danger),
                        onPressed: () async {
                          final ok = await confirmDialog(
                            ctx,
                            title: 'حذف المحل',
                            message: 'سيختفي «${p.shopName}» من الخريطة لك ولزملائك.',
                            confirm: 'حذف',
                            destructive: true,
                          );
                          if (!ok || !ctx.mounted) return;
                          try {
                            await ref.read(prospectsRepositoryProvider).delete(p.id);
                            if (ctx.mounted) Navigator.pop(ctx, 'deleted');
                          } catch (e) {
                            if (ctx.mounted) ctx.showError(e);
                          }
                        },
                        icon: const Icon(Icons.delete_outline_rounded),
                        label: const Text('حذف'),
                      ),
                    ),
                  ],
                ),
              ],
            ],
          ),
        ),
      ),
    );

String _d(double? v) => v == null ? '؟' : (v == v.roundToDouble() ? v.toInt().toString() : v.toString());

/// What the agent entered for a pin: type, goods value, size, phone, who added it and when.
class ProspectFacts extends StatelessWidget {
  const ProspectFacts({super.key, required this.p});

  final Prospect p;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        if (p.description != null) _Fact(icon: Icons.notes_rounded, label: 'نوع المحل', value: p.description!),
        if (p.goodsValue != null) _Fact(icon: Icons.inventory_2_outlined, label: 'قيمة البضاعة التقديرية', value: Fmt.money(p.goodsValue!)),
        if (p.lengthM != null || p.widthM != null)
          _Fact(
            icon: Icons.straighten_rounded,
            label: 'الأبعاد',
            value: '${_d(p.lengthM)} × ${_d(p.widthM)} م${p.areaM2 == null ? '' : ' (مساحة ${_d(p.areaM2)} متر مربع)'}',
          ),
        if (p.phone != null) _Fact(icon: Icons.phone_rounded, label: 'الهاتف', value: p.phone!, ltr: true),
        _Fact(icon: Icons.person_pin_circle_outlined, label: 'أضافه', value: '${p.isMine ? 'أنت' : p.createdByName} • ${Fmt.dateTime(p.createdAt)}'),
        if (p.updatedAt != null && p.updatedAt!.difference(p.createdAt).inMinutes >= 1)
          _Fact(icon: Icons.edit_calendar_outlined, label: 'آخر تعديل', value: Fmt.dateTime(p.updatedAt)),
      ],
    );
  }
}

class _Fact extends StatelessWidget {
  const _Fact({required this.icon, required this.label, required this.value, this.ltr = false});

  final IconData icon;
  final String label;
  final String value;
  final bool ltr;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 5),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(icon, size: 20, color: AppColors.textMuted),
          const SizedBox(width: 10),
          SizedBox(width: 120, child: Text(label, style: const TextStyle(color: AppColors.textMuted))),
          Expanded(
            child: Text(value, textDirection: ltr ? TextDirection.ltr : null, style: const TextStyle(fontWeight: FontWeight.w700)),
          ),
        ],
      ),
    );
  }
}
