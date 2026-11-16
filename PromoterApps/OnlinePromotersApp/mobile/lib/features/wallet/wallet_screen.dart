import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/app_state.dart';
import '../../core/network/api_exception.dart';
import '../../core/theme/app_theme.dart';
import '../../core/utils/formatters.dart';
import '../../core/widgets/common.dart';
import '../../core/widgets/paged_list.dart';
import '../../core/widgets/state_views.dart';
import '../../data/models.dart';
import '../../data/repository.dart';

final _payoutsProvider = FutureProvider.autoDispose<List<Payout>>((ref) async {
  ref.watch(dataVersionProvider);
  return (await ref.watch(repositoryProvider).payouts()).items;
});

final _recentEntriesProvider = FutureProvider.autoDispose<List<WalletEntry>>((ref) async {
  ref.watch(dataVersionProvider);
  return (await ref.watch(repositoryProvider).entries()).items.take(5).toList();
});

/// «المحفظة»: balance, withdrawal requests (with their chat) and the latest transactions.
class WalletScreen extends ConsumerWidget {
  const WalletScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final wallet = ref.watch(walletProvider);
    final payouts = ref.watch(_payoutsProvider);
    final entries = ref.watch(_recentEntriesProvider);
    final hasPending = payouts.value?.any((p) => p.isPending) ?? false;

    return Scaffold(
      appBar: AppBar(title: const Text('المحفظة')),
      body: RefreshIndicator(
        onRefresh: () async {
          ref.read(dataVersionProvider.notifier).bump();
          try {
            await ref.read(walletProvider.future);
          } catch (_) {}
        },
        child: ListView(
          padding: const EdgeInsets.fromLTRB(16, 4, 16, 32),
          children: [
            AsyncView<WalletSummary>(
              value: wallet,
              onRetry: () => ref.invalidate(walletProvider),
              loading: const SizedBox(height: 200, child: Center(child: CircularProgressIndicator())),
              data: (w) => Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  AppCard(
                    padding: const EdgeInsets.all(20),
                    child: Column(
                      children: [
                        const Text('الرصيد المتاح', style: TextStyle(color: AppColors.textMuted)),
                        const SizedBox(height: 4),
                        FittedBox(
                          child: Text(Fmt.money(w.balance),
                              style: TextStyle(fontSize: 32, fontWeight: FontWeight.w800, color: w.balance < 0 ? AppColors.danger : AppColors.primary)),
                        ),
                        if (w.pendingWithdrawal > 0)
                          Padding(
                            padding: const EdgeInsets.only(top: 4),
                            child: Text('قيد السحب: ${Fmt.money(w.pendingWithdrawal)}', style: const TextStyle(color: AppColors.warning, fontWeight: FontWeight.w600)),
                          ),
                        const SizedBox(height: 16),
                        LoadingButton(
                          label: hasPending ? 'لديك طلب سحب قيد المعالجة' : 'طلب سحب الأرباح',
                          icon: Icons.outbox_rounded,
                          onPressed: hasPending || w.balance < w.minPayout ? null : () => _requestPayout(context, ref, w),
                        ),
                        if (!hasPending && w.balance < w.minPayout)
                          Padding(
                            padding: const EdgeInsets.only(top: 8),
                            child: Text('أقل مبلغ للسحب ${Fmt.money(w.minPayout)}', style: const TextStyle(color: AppColors.textMuted, fontSize: 12.5)),
                          ),
                      ],
                    ),
                  ),
                  const SizedBox(height: 12),
                  Row(
                    children: [
                      Expanded(child: StatTile(label: 'مجموع أرباحي', value: Fmt.money(w.totalEarned), icon: Icons.trending_up_rounded, color: AppColors.success)),
                      const SizedBox(width: 10),
                      Expanded(child: StatTile(label: 'أرباح الشهر', value: Fmt.money(w.earnedThisMonth), icon: Icons.calendar_month_rounded, color: AppColors.info)),
                      const SizedBox(width: 10),
                      Expanded(child: StatTile(label: 'المستلم', value: Fmt.money(w.totalWithdrawn), icon: Icons.payments_rounded, color: AppColors.purple)),
                    ],
                  ),
                ],
              ),
            ),
            const SectionHeader(title: 'طلبات السحب', padding: EdgeInsets.fromLTRB(4, 22, 4, 8)),
            AsyncView<List<Payout>>(
              value: payouts,
              onRetry: () => ref.invalidate(_payoutsProvider),
              loading: const LinearProgressIndicator(),
              data: (list) => list.isEmpty
                  ? const AppCard(child: Text('لم تطلب سحباً بعد. عندما تطلب، تتابع طلبك وتراسل الإدارة من هنا.', style: TextStyle(color: AppColors.textMuted, height: 1.6)))
                  : Column(children: [for (final p in list) Padding(padding: const EdgeInsets.only(bottom: 10), child: PayoutTile(payout: p))]),
            ),
            SectionHeader(
              title: 'سجل الحركات',
              actionLabel: 'عرض الكل',
              onAction: () => context.push('/wallet/entries'),
              padding: const EdgeInsets.fromLTRB(4, 22, 0, 8),
            ),
            AsyncView<List<WalletEntry>>(
              value: entries,
              onRetry: () => ref.invalidate(_recentEntriesProvider),
              loading: const LinearProgressIndicator(),
              data: (list) => list.isEmpty
                  ? const AppCard(child: Text('لا توجد حركات بعد. تُضاف العمولة عندما يتحول طلبك إلى مبيعة.', style: TextStyle(color: AppColors.textMuted, height: 1.6)))
                  : Card(child: Column(children: [for (final (i, e) in list.indexed) ...[if (i > 0) const Divider(height: 1), EntryRow(entry: e)]])),
            ),
          ],
        ),
      ),
    );
  }

  Future<void> _requestPayout(BuildContext context, WidgetRef ref, WalletSummary w) async {
    final created = await showModalBottomSheet<Payout>(
      context: context,
      isScrollControlled: true,
      builder: (_) => PayoutSheet(wallet: w),
    );
    if (created != null && context.mounted) {
      ref.read(dataVersionProvider.notifier).bump();
      await context.push('/payouts/${created.id}');
    }
  }
}

class PayoutTile extends StatelessWidget {
  const PayoutTile({super.key, required this.payout});

  final Payout payout;

  @override
  Widget build(BuildContext context) {
    return AppCard(
      onTap: () => context.push('/payouts/${payout.id}'),
      padding: const EdgeInsets.all(14),
      child: Row(
        children: [
          Icon(payout.method == 'ZainCash' ? Icons.phone_android_rounded : Icons.credit_card_rounded, color: AppColors.primary),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(Fmt.money(payout.amount), style: const TextStyle(fontWeight: FontWeight.w800)),
                Text('${payout.methodLabel} · ${Fmt.date(payout.createdAt)}', style: const TextStyle(color: AppColors.textMuted, fontSize: 12.5)),
              ],
            ),
          ),
          if (payout.unreadMessages > 0)
            Padding(
              padding: const EdgeInsetsDirectional.only(end: 8),
              child: Badge(label: Text('${payout.unreadMessages}'), child: const Icon(Icons.chat_bubble_outline_rounded)),
            ),
          StatusChip(label: payout.statusLabel, color: payout.color),
        ],
      ),
    );
  }
}

class EntryRow extends StatelessWidget {
  const EntryRow({super.key, required this.entry});

  final WalletEntry entry;

  @override
  Widget build(BuildContext context) {
    final positive = entry.amount >= 0;
    final color = positive ? AppColors.success : AppColors.danger;
    return InkWell(
      onTap: entry.orderId != null
          ? () => entry.type == 'Sale' || entry.type == 'SaleReversal' ? context.push('/orders/${entry.orderId}') : null
          : entry.payoutId != null
              ? () => context.push('/payouts/${entry.payoutId}')
              : null,
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
        child: Row(
          children: [
            CircleAvatar(radius: 18, backgroundColor: color.withValues(alpha: 0.1), child: Icon(entry.icon, size: 18, color: color)),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(entry.typeLabel, style: const TextStyle(fontWeight: FontWeight.w700)),
                  Text(
                    [if (entry.details.isNotEmpty) entry.details, Fmt.dateTime(entry.createdAt)].join('\n'),
                    style: const TextStyle(color: AppColors.textMuted, fontSize: 12, height: 1.5),
                  ),
                ],
              ),
            ),
            Text('${positive ? '+' : '−'}${Fmt.number(entry.amount.abs())}',
                textDirection: TextDirection.ltr, style: TextStyle(color: color, fontWeight: FontWeight.w800)),
          ],
        ),
      ),
    );
  }
}

/// Request form: amount, Qi Card or Zain Cash, and the account / phone number.
class PayoutSheet extends ConsumerStatefulWidget {
  const PayoutSheet({super.key, required this.wallet});

  final WalletSummary wallet;

  @override
  ConsumerState<PayoutSheet> createState() => _PayoutSheetState();
}

class _PayoutSheetState extends ConsumerState<PayoutSheet> {
  late final _amount = TextEditingController(text: '${widget.wallet.balance}');
  final _account = TextEditingController();
  String _method = 'ZainCash';
  bool _busy = false;
  Map<String, String> _errors = {};
  String? _error;

  @override
  void dispose() {
    _amount.dispose();
    _account.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    final amount = int.tryParse(_amount.text.replaceAll(RegExp(r'[^\d]'), ''));
    final e = <String, String>{};
    if (amount == null || amount <= 0) e['amount'] = 'أدخل المبلغ';
    final account = _account.text.trim();
    if (_method == 'QiCard') {
      if (!RegExp(r'^\d{10,13}$').hasMatch(account)) e['accountNumber'] = qiCardRule;
    } else if (account.isEmpty) {
      e['accountNumber'] = 'أدخل رقم زين كاش';
    }
    setState(() {
      _errors = e;
      _error = null;
    });
    if (e.isNotEmpty) return;
    setState(() => _busy = true);
    try {
      final (payout, _) = await ref.read(repositoryProvider).requestPayout(amount: amount!, method: _method, accountNumber: _account.text.trim());
      if (mounted) Navigator.pop(context, payout);
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
    return Padding(
      padding: EdgeInsets.fromLTRB(20, 0, 20, MediaQuery.viewInsetsOf(context).bottom + 24),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text('طلب سحب', style: Theme.of(context).textTheme.titleLarge),
          const SizedBox(height: 4),
          Text('رصيدك ${Fmt.money(widget.wallet.balance)}', style: const TextStyle(color: AppColors.textMuted)),
          const SizedBox(height: 16),
          SegmentedButton<String>(
            segments: const [
              ButtonSegment(value: 'ZainCash', label: Text('زين كاش'), icon: Icon(Icons.phone_android_rounded)),
              ButtonSegment(value: 'QiCard', label: Text('كي كارد'), icon: Icon(Icons.credit_card_rounded)),
            ],
            selected: {_method},
            onSelectionChanged: (s) => setState(() {
              _method = s.first;
              _account.clear(); // the two numbers have different rules
              _errors.remove('accountNumber');
            }),
          ),
          const SizedBox(height: 14),
          TextField(
            key: ValueKey(_method),
            controller: _account,
            keyboardType: TextInputType.number,
            textDirection: TextDirection.ltr,
            // Qi Card: digits only, 13 at most (Arabic-Indic digits are turned into 0-9).
            inputFormatters: _method == 'QiCard'
                ? [const LatinDigitsFormatter(), FilteringTextInputFormatter.digitsOnly, LengthLimitingTextInputFormatter(13)]
                : [FilteringTextInputFormatter.allow(RegExp(r'[0-9٠-٩ \-]'))],
            onChanged: (_) {
              if (_errors.containsKey('accountNumber')) setState(() => _errors = {..._errors}..remove('accountNumber'));
            },
            decoration: InputDecoration(
              labelText: _method == 'ZainCash' ? 'رقم محفظة زين كاش' : 'رقم كي كارد',
              hintText: _method == 'ZainCash' ? '07XXXXXXXXX' : 'من 10 إلى 13 رقماً',
              helperText: _method == 'QiCard' ? 'أرقام فقط، من 10 إلى 13 رقماً' : null,
              counterText: _method == 'QiCard' ? '${_account.text.length}/13' : null,
              errorText: _errors['accountNumber'],
            ),
          ),
          const SizedBox(height: 12),
          TextField(
            controller: _amount,
            keyboardType: TextInputType.number,
            textDirection: TextDirection.ltr,
            inputFormatters: [FilteringTextInputFormatter.digitsOnly],
            decoration: InputDecoration(labelText: 'المبلغ (د.ع)', errorText: _errors['amount'], helperText: 'أقل مبلغ ${Fmt.money(widget.wallet.minPayout)}'),
          ),
          if (_error != null) ...[
            const SizedBox(height: 10),
            NoticeBanner(message: _error!, color: AppColors.danger, icon: Icons.error_outline_rounded),
          ],
          const SizedBox(height: 18),
          LoadingButton(label: 'إرسال الطلب', loading: _busy, onPressed: _submit),
        ],
      ),
    );
  }
}

/// Every change to the balance.
class EntriesScreen extends ConsumerWidget {
  const EntriesScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return Scaffold(
      appBar: AppBar(title: const Text('سجل الحركات')),
      body: PagedListView<WalletEntry>(
        fetch: (page) => ref.read(repositoryProvider).entries(page: page),
        separator: 8,
        itemBuilder: (_, e, _) => Card(child: EntryRow(entry: e)),
        empty: const EmptyState(icon: Icons.receipt_rounded, title: 'لا توجد حركات بعد'),
      ),
    );
  }
}

const qiCardRule = 'رقم كي كارد من 10 إلى 13 رقماً (أرقام فقط)';

/// Turns Arabic-Indic / Persian digits typed on an Arabic keyboard into 0-9.
class LatinDigitsFormatter extends TextInputFormatter {
  const LatinDigitsFormatter();

  @override
  TextEditingValue formatEditUpdate(TextEditingValue oldValue, TextEditingValue newValue) {
    final text = String.fromCharCodes(newValue.text.runes.map((c) {
      if (c >= 0x0660 && c <= 0x0669) return 0x30 + c - 0x0660;
      if (c >= 0x06F0 && c <= 0x06F9) return 0x30 + c - 0x06F0;
      return c;
    }));
    return text == newValue.text ? newValue : newValue.copyWith(text: text);
  }
}
