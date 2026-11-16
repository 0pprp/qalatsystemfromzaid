import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/app_state.dart';
import '../../core/network/api_exception.dart';
import '../../core/theme/app_theme.dart';
import '../../core/utils/formatters.dart';
import '../../core/utils/launcher.dart';
import '../../core/widgets/common.dart';
import '../../core/widgets/state_views.dart';
import '../../data/models.dart';
import '../../data/repository.dart';

String _pct(double v) => '${v == v.roundToDouble() ? v.toInt() : v}%';

/// «فريقي»: accounts this promoter created, what they earned from each, and creating a new one.
class TeamScreen extends ConsumerWidget {
  const TeamScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final team = ref.watch(teamProvider);
    final canCreate = team.value?.canCreateAccounts ?? false;
    return Scaffold(
      appBar: AppBar(title: const Text('فريقي')),
      floatingActionButton: canCreate
          ? FloatingActionButton.extended(
              onPressed: () => _create(context, ref),
              icon: const Icon(Icons.person_add_alt_1_rounded),
              label: const Text('حساب جديد'),
            )
          : null,
      body: RefreshIndicator(
        onRefresh: () => ref.refresh(teamProvider.future),
        child: AsyncView<Team>(
          value: team,
          onRetry: () => ref.invalidate(teamProvider),
          data: (t) => ListView(
            padding: const EdgeInsets.fromLTRB(16, 4, 16, 96),
            children: [
              AppCard(
                child: Row(
                  children: [
                    const Icon(Icons.savings_rounded, color: AppColors.success, size: 32),
                    const SizedBox(width: 12),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          const Text('أرباحي من فريقي', style: TextStyle(color: AppColors.textMuted)),
                          Text(Fmt.money(t.totalEarned), style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 22, color: AppColors.success)),
                        ],
                      ),
                    ),
                    Text('${t.items.length} حساب', style: const TextStyle(fontWeight: FontWeight.w700)),
                  ],
                ),
              ),
              if (!t.canCreateAccounts) ...[
                const SizedBox(height: 12),
                const NoticeBanner(message: 'لا يمكن لهذا الحساب إنشاء حسابات جديدة. تواصل مع الإدارة.'),
              ],
              const SizedBox(height: 12),
              if (t.items.isEmpty)
                const EmptyState(
                  icon: Icons.group_add_rounded,
                  title: 'لم تنشئ حسابات بعد',
                  message: 'أنشئ حساباً لمروّج يعمل معك وحدد نسبتك من عمولته. تصلك نسبتك عند كل بيع يتمه.',
                )
              else
                for (final m in t.items) ...[
                  _MemberCard(member: m),
                  const SizedBox(height: 10),
                ],
            ],
          ),
        ),
      ),
    );
  }

  Future<void> _create(BuildContext context, WidgetRef ref) async {
    final created = await showModalBottomSheet<TeamMember>(context: context, isScrollControlled: true, builder: (_) => const _CreateMemberSheet());
    if (created != null && context.mounted) {
      ref.read(dataVersionProvider.notifier).bump();
      await showDialog<void>(
        context: context,
        builder: (ctx) => AlertDialog(
          icon: const Icon(Icons.check_circle_rounded, color: AppColors.success, size: 48),
          title: const Text('تم إنشاء الحساب'),
          content: Text('أعطِ المروّج اسم المستخدم «${created.username}» وكلمة المرور التي اخترتها ليدخل إلى التطبيق.', textAlign: TextAlign.center, style: const TextStyle(height: 1.6)),
          actions: [FilledButton(onPressed: () => Navigator.pop(ctx), child: const Text('تم'))],
        ),
      );
    }
  }
}

class _MemberCard extends ConsumerWidget {
  const _MemberCard({required this.member});

  final TeamMember member;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final m = member;
    return AppCard(
      padding: const EdgeInsets.all(14),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              CircleAvatar(
                backgroundColor: AppColors.primaryLight,
                child: Text(m.username.characters.first.toUpperCase(), style: const TextStyle(color: AppColors.primary, fontWeight: FontWeight.w800)),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(m.username, style: const TextStyle(fontWeight: FontWeight.w800)),
                    Text(m.phone, textDirection: TextDirection.ltr, style: const TextStyle(color: AppColors.textMuted, fontSize: 12.5)),
                  ],
                ),
              ),
              if (m.isSuspended) const StatusChip(label: 'موقوف', color: AppColors.danger),
              IconButton(icon: const Icon(Icons.call_rounded, color: AppColors.primary), onPressed: () => Launch.call(context, m.phone)),
            ],
          ),
          const Divider(height: 20),
          Row(
            children: [
              _Num(label: 'نسبتي', value: m.isFull ? 'حساب كامل' : _pct(m.cutPercent)),
              _Num(label: 'الطلبات', value: '${m.ordersCount}'),
              _Num(label: 'المبيعات', value: '${m.soldCount}'),
              _Num(label: 'ربحي منه', value: Fmt.number(m.earnedFromMember), color: AppColors.success),
            ],
          ),
          if (!m.isFull)
            Align(
              alignment: AlignmentDirectional.centerEnd,
              child: TextButton.icon(
                onPressed: () => _editCut(context, ref),
                icon: const Icon(Icons.percent_rounded, size: 18),
                label: const Text('تعديل النسبة'),
              ),
            ),
        ],
      ),
    );
  }

  Future<void> _editCut(BuildContext context, WidgetRef ref) async {
    final controller = TextEditingController(text: _pct(member.cutPercent).replaceAll('%', ''));
    final value = await showDialog<double>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text('نسبتي من ${member.username}'),
        content: TextField(
          controller: controller,
          autofocus: true,
          keyboardType: const TextInputType.numberWithOptions(decimal: true),
          decoration: const InputDecoration(suffixText: '%', helperText: 'تطبق على الطلبات الجديدة فقط'),
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx), child: const Text('تراجع')),
          FilledButton(onPressed: () => Navigator.pop(ctx, double.tryParse(controller.text.trim())), child: const Text('حفظ')),
        ],
      ),
    );
    if (value == null) return;
    try {
      await ref.read(repositoryProvider).updateMemberCut(member.id, value);
      ref.read(dataVersionProvider.notifier).bump();
      if (context.mounted) context.showSnack('تم حفظ النسبة');
    } catch (e) {
      if (context.mounted) context.showError(e);
    }
  }
}

class _Num extends StatelessWidget {
  const _Num({required this.label, required this.value, this.color});

  final String label;
  final String value;
  final Color? color;

  @override
  Widget build(BuildContext context) => Expanded(
        child: Column(
          children: [
            Text(value, style: TextStyle(fontWeight: FontWeight.w800, color: color), maxLines: 1, overflow: TextOverflow.ellipsis),
            Text(label, style: const TextStyle(color: AppColors.textMuted, fontSize: 11.5)),
          ],
        ),
      );
}

class _CreateMemberSheet extends ConsumerStatefulWidget {
  const _CreateMemberSheet();

  @override
  ConsumerState<_CreateMemberSheet> createState() => _CreateMemberSheetState();
}

class _CreateMemberSheetState extends ConsumerState<_CreateMemberSheet> {
  final _username = TextEditingController();
  final _password = TextEditingController();
  final _phone = TextEditingController();
  final _cut = TextEditingController(text: '50');
  bool _busy = false;
  Map<String, String> _errors = {};
  String? _error;

  @override
  void dispose() {
    for (final c in [_username, _password, _phone, _cut]) {
      c.dispose();
    }
    super.dispose();
  }

  double? get _cutValue => double.tryParse(_cut.text.trim());

  Future<void> _submit() async {
    final e = <String, String>{};
    if (_username.text.trim().length < 2) e['username'] = 'اسم المستخدم حرفان على الأقل';
    if (_password.text.isEmpty) e['password'] = 'أدخل كلمة المرور';
    if (_phone.text.replaceAll(RegExp(r'\D'), '').length < 10) e['phone'] = 'رقم الهاتف غير صحيح';
    final cut = _cutValue;
    if (cut == null || cut < 0 || cut > 100) e['cutPercent'] = 'النسبة بين 0 و 100';
    setState(() {
      _errors = e;
      _error = null;
    });
    if (e.isNotEmpty) return;
    setState(() => _busy = true);
    try {
      final m = await ref.read(repositoryProvider).createMember(
            username: _username.text.trim(),
            password: _password.text,
            phone: _phone.text.trim(),
            cutPercent: cut!,
          );
      if (mounted) Navigator.pop(context, m);
    } catch (err) {
      final ex = ApiException.from(err);
      if (mounted) {
        setState(() {
          _errors = ex.fields;
          _error = ex.fields.isEmpty ? ex.message : null;
        });
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final products = ref.watch(productsProvider).value ?? const <Product>[];
    final example = products.isEmpty ? null : products.first;
    final cut = _cutValue;
    return Padding(
      padding: EdgeInsets.fromLTRB(20, 0, 20, MediaQuery.viewInsetsOf(context).bottom + 24),
      child: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text('حساب جديد في فريقي', style: Theme.of(context).textTheme.titleLarge),
            const SizedBox(height: 16),
            TextField(controller: _username, decoration: InputDecoration(labelText: 'اسم المستخدم', errorText: _errors['username'])),
            const SizedBox(height: 12),
            TextField(controller: _password, decoration: InputDecoration(labelText: 'كلمة المرور', errorText: _errors['password'])),
            const SizedBox(height: 12),
            TextField(
              controller: _phone,
              keyboardType: TextInputType.phone,
              textDirection: TextDirection.ltr,
              inputFormatters: [FilteringTextInputFormatter.allow(RegExp(r'[0-9٠-٩+ ]'))],
              decoration: InputDecoration(labelText: 'رقم الهاتف', hintText: '07XXXXXXXXX', errorText: _errors['phone']),
            ),
            const SizedBox(height: 12),
            TextField(
              controller: _cut,
              keyboardType: const TextInputType.numberWithOptions(decimal: true),
              onChanged: (_) => setState(() {}),
              decoration: InputDecoration(labelText: 'نسبتي من عمولته', suffixText: '%', errorText: _errors['cutPercent']),
            ),
            if (example != null && cut != null && cut >= 0 && cut <= 100) ...[
              const SizedBox(height: 10),
              NoticeBanner(
                color: AppColors.info,
                icon: Icons.calculate_outlined,
                message: () {
                  final mine = (example.myCommission * cut / 100).round();
                  return 'مثال: ${example.name} عمولته ${Fmt.money(example.myCommission)} — يظهر له ${Fmt.money(example.myCommission - mine)} وتحصل أنت على ${Fmt.money(mine)} عند كل بيع.';
                }(),
              ),
            ],
            if (_error != null) ...[
              const SizedBox(height: 10),
              NoticeBanner(message: _error!, color: AppColors.danger, icon: Icons.error_outline_rounded),
            ],
            const SizedBox(height: 18),
            LoadingButton(label: 'إنشاء الحساب', loading: _busy, onPressed: _submit),
          ],
        ),
      ),
    );
  }
}
