import 'package:delegate_application/services/DatabaseHelper.dart';
import 'package:delegate_application/services/today_payments_logic.dart';
import 'package:delegate_application/ui/app_safe_scaffold.dart';
import 'package:delegate_application/utils/AppTheme.dart';
import 'package:delegate_application/utils/Formatters.dart';
import 'package:delegate_application/utils/iraq_date.dart';
import 'package:flutter/material.dart';

/// Notifier so local payment insert can refresh «تسديدات اليوم» immediately.
class TodayPaymentsRefresh {
  static final ValueNotifier<int> tick = ValueNotifier<int>(0);

  static void notify() => tick.value++;
}

class TodayPaymentsPage extends StatefulWidget {
  const TodayPaymentsPage({super.key, this.embedded = false});

  /// When true (shell tab), skip outer bottom SafeArea (shell bar handles it).
  final bool embedded;

  @override
  State<TodayPaymentsPage> createState() => TodayPaymentsPageState();
}

class TodayPaymentsPageState extends State<TodayPaymentsPage>
    with AutomaticKeepAliveClientMixin {
  final TextEditingController _search = TextEditingController();
  TodayPaymentFilter _filter = TodayPaymentFilter.all;
  bool _loading = true;
  List<TodayPaymentRow> _rows = [];
  TodayPaymentsSummary _summary = const TodayPaymentsSummary(
    dueCount: 0,
    paidCount: 0,
    unpaidCount: 0,
    totalReceivedToday: 0,
  );

  @override
  bool get wantKeepAlive => true;

  @override
  void initState() {
    super.initState();
    TodayPaymentsRefresh.tick.addListener(_onExternalRefresh);
    reload();
  }

  @override
  void dispose() {
    TodayPaymentsRefresh.tick.removeListener(_onExternalRefresh);
    _search.dispose();
    super.dispose();
  }

  void _onExternalRefresh() => reload();

  Future<void> reload() async {
    final db = await DatabaseHelper().database;
    final customers = await db.query('Customer');
    final payments = await db.query('CustomerPayment');
    final delegates = await db.query('SelectDelegate');
    final names = <int, String>{};
    for (final d in delegates) {
      final id = int.tryParse(d['DelegateId']?.toString() ?? '') ?? 0;
      if (id > 0) {
        names[id] = d['DelegateName']?.toString() ?? '';
      }
    }

    final allEligible = TodayPaymentsLogic.buildRows(
      customers: customers,
      localPayments: payments,
      delegateNames: names,
      filter: TodayPaymentFilter.all,
    );
    final summary = TodayPaymentsLogic.summarize(allEligible);
    final filtered = TodayPaymentsLogic.buildRows(
      customers: customers,
      localPayments: payments,
      delegateNames: names,
      filter: _filter,
      searchQuery: _search.text,
    );

    if (!mounted) return;
    setState(() {
      _summary = summary;
      _rows = filtered;
      _loading = false;
    });
  }

  @override
  Widget build(BuildContext context) {
    super.build(context);
    final body = Directionality(
      textDirection: TextDirection.rtl,
      child: Column(
        children: [
          _buildHeaderBar(),
          _buildSummary(),
          _buildFilters(),
          Expanded(
            child: _loading
                ? const Center(child: CircularProgressIndicator())
                : _rows.isEmpty
                    ? const Center(
                        child: Text(
                          'لا يوجد عملاء مستحقون للتحصيل اليوم حسب الفلتر',
                          style: TextStyle(fontFamily: 'Cairo', color: Colors.grey),
                          textAlign: TextAlign.center,
                        ),
                      )
                    : RefreshIndicator(
                        onRefresh: reload,
                        color: AppTheme.primaryColor,
                        child: ListView.builder(
                          padding: const EdgeInsets.fromLTRB(16, 0, 16, 24),
                          itemCount: _rows.length,
                          itemBuilder: (context, i) => _buildCard(_rows[i]),
                        ),
                      ),
          ),
        ],
      ),
    );

    if (widget.embedded) {
      return Material(color: AppTheme.backgroundColor, child: body);
    }

    return AppSafeScaffold(
      backgroundColor: AppTheme.backgroundColor,
      body: body,
    );
  }

  Widget _buildHeaderBar() {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.fromLTRB(16, 12, 16, 8),
      color: AppTheme.primaryColor,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const Text(
            'تسديدات اليوم',
            textAlign: TextAlign.center,
            style: TextStyle(
              fontFamily: 'Cairo',
              color: Colors.white,
              fontSize: 18,
              fontWeight: FontWeight.bold,
            ),
          ),
          const SizedBox(height: 10),
          Container(
            height: 44,
            decoration: BoxDecoration(
              color: Colors.white,
              borderRadius: BorderRadius.circular(22),
            ),
            padding: const EdgeInsets.symmetric(horizontal: 12),
            child: TextField(
              controller: _search,
              style: const TextStyle(fontFamily: 'Cairo'),
              decoration: const InputDecoration(
                hintText: 'بحث باسم الزبون...',
                border: InputBorder.none,
                prefixIcon: Icon(Icons.search, color: AppTheme.primaryColor),
              ),
              onChanged: (_) => reload(),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildSummary() {
    Widget cell(String label, String value, {Color? color}) {
      return Expanded(
        child: Column(
          children: [
            Text(
              value,
              style: TextStyle(
                fontFamily: 'Cairo',
                fontWeight: FontWeight.bold,
                fontSize: 16,
                color: color ?? AppTheme.textColor,
              ),
            ),
            Text(
              label,
              textAlign: TextAlign.center,
              style: const TextStyle(
                fontFamily: 'Cairo',
                fontSize: 11,
                color: Colors.grey,
              ),
            ),
          ],
        ),
      );
    }

    return Container(
      margin: const EdgeInsets.fromLTRB(16, 12, 16, 8),
      padding: const EdgeInsets.symmetric(vertical: 14, horizontal: 8),
      decoration: BoxDecoration(
        color: Theme.of(context).cardColor,
        borderRadius: BorderRadius.circular(16),
        boxShadow: [
          BoxShadow(
            color: Colors.black.withValues(alpha: 0.04),
            blurRadius: 10,
            offset: const Offset(0, 4),
          ),
        ],
      ),
      child: Row(
        children: [
          cell('مطلوب اليوم', '${_summary.dueCount}'),
          cell('سددوا', '${_summary.paidCount}', color: Colors.green[700]),
          cell('لم يسددوا', '${_summary.unpaidCount}', color: Colors.orange[800]),
          cell(
            'المبلغ اليوم',
            Formatters.formatNumber(_summary.totalReceivedToday),
            color: AppTheme.primaryColor,
          ),
        ],
      ),
    );
  }

  Widget _buildFilters() {
    Widget chip(String label, TodayPaymentFilter f) {
      final selected = _filter == f;
      return Padding(
        padding: const EdgeInsets.only(left: 8),
        child: ChoiceChip(
          label: Text(label, style: const TextStyle(fontFamily: 'Cairo')),
          selected: selected,
          selectedColor: AppTheme.primaryColor.withValues(alpha: 0.2),
          onSelected: (_) {
            setState(() => _filter = f);
            reload();
          },
        ),
      );
    }

    return SingleChildScrollView(
      scrollDirection: Axis.horizontal,
      padding: const EdgeInsets.fromLTRB(16, 0, 16, 8),
      child: Row(
        children: [
          chip('الكل', TodayPaymentFilter.all),
          chip('سدد', TodayPaymentFilter.paid),
          chip('لم يسدد', TodayPaymentFilter.unpaid),
        ],
      ),
    );
  }

  Widget _buildCard(TodayPaymentRow row) {
    final paid = row.status == TodayPaymentStatus.paid;
    final statusColor = paid ? Colors.green : Colors.orange.shade800;
    final time = IraqDate.formatIraqTime(row.paidAtUtc);

    return Container(
      margin: const EdgeInsets.only(bottom: 10),
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: Theme.of(context).cardColor,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(
          color: paid
              ? Colors.green.withValues(alpha: 0.35)
              : Colors.orange.withValues(alpha: 0.25),
        ),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            width: 40,
            height: 40,
            decoration: BoxDecoration(
              color: statusColor.withValues(alpha: 0.12),
              shape: BoxShape.circle,
            ),
            child: Icon(
              paid ? Icons.check_circle : Icons.schedule,
              color: statusColor,
            ),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  row.customerName,
                  style: const TextStyle(
                    fontFamily: 'Cairo',
                    fontWeight: FontWeight.bold,
                    fontSize: 15,
                  ),
                ),
                if (row.listName.isNotEmpty)
                  Text(
                    row.listName,
                    style: const TextStyle(
                      fontFamily: 'Cairo',
                      color: Colors.grey,
                      fontSize: 12,
                    ),
                  ),
                const SizedBox(height: 4),
                Text(
                  'القسط: ${Formatters.formatNumber(row.installment)} دع',
                  style: const TextStyle(fontFamily: 'Cairo', fontSize: 12),
                ),
                const SizedBox(height: 6),
                Text(
                  paid
                      ? '✓ سدد اليوم${row.paidAmount != null ? ' · ${Formatters.formatNumber(row.paidAmount!)} دع' : ''}${time.isNotEmpty ? ' · $time' : ''}'
                      : 'لم يسدد اليوم',
                  style: TextStyle(
                    fontFamily: 'Cairo',
                    color: statusColor,
                    fontWeight: FontWeight.w600,
                    fontSize: 13,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
