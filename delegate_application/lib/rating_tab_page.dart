import 'dart:convert';
import 'dart:io';

import 'package:delegate_application/client.dart';
import 'package:delegate_application/services/DatabaseHelper.dart';
import 'package:delegate_application/services/account_statement_pdf.dart';
import 'package:delegate_application/services/customer_payments_cache.dart';
import 'package:delegate_application/services/delegate_data_refresh_service.dart';
import 'package:delegate_application/services/phantom_customer_rating.dart';
import 'package:delegate_application/services/whatsapp_pdf_share.dart';
import 'package:delegate_application/ui/app_safe_scaffold.dart';
import 'package:delegate_application/utils/AppTheme.dart';
import 'package:delegate_application/utils/Formatters.dart';
import 'package:delegate_application/utils/PdfAssetCache.dart';
import 'package:delegate_application/utils/iraq_date.dart';
import 'package:delegate_application/utils/safe_num.dart';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:path_provider/path_provider.dart';
import 'package:shared_preferences/shared_preferences.dart';

class RatingTabPage extends StatefulWidget {
  const RatingTabPage({super.key});

  @override
  State<RatingTabPage> createState() => RatingTabPageState();
}

class RatingTabPageState extends State<RatingTabPage>
    with AutomaticKeepAliveClientMixin {
  bool _loading = true;
  String _error = '';
  List<RatedCustomer> _rose = [];
  List<RatedCustomer> _flat = [];
  List<RatedCustomer> _down = [];

  @override
  bool get wantKeepAlive => true;

  @override
  void initState() {
    super.initState();
    // Defer past IndexedStack first build + any in-flight SQLite replace.
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) reload();
    });
  }

  Future<void> reload() async {
    if (!mounted) return;
    setState(() {
      _loading = true;
      _error = '';
    });
    try {
      if (DelegateDataRefreshService.instance.isRefreshing) {
        await DelegateDataRefreshService.instance.refreshIfPossible();
      }

      List<Map<String, dynamic>> customers = const [];
      Object? lastError;
      for (var attempt = 0; attempt < 3; attempt++) {
        try {
          final db = await DatabaseHelper().database;
          customers = await db.query('Customer');
          lastError = null;
          break;
        } catch (e) {
          lastError = e;
          await Future<void>.delayed(
              Duration(milliseconds: 120 * (attempt + 1)));
        }
      }
      if (lastError != null) throw lastError;

      final asOf = IraqDate.calendarDate();
      final fiveAgo = asOf.subtract(const Duration(days: 5));
      final rose = <RatedCustomer>[];
      final flat = <RatedCustomer>[];
      final down = <RatedCustomer>[];
      for (final row in customers) {
        try {
          if (!PhantomCustomerRating.isInOverallRatingCohort(row, asOf)) {
            continue;
          }
          final score = PhantomCustomerRating.scoreForRow(row, asOf);
          if (score == null) continue;
          final prior = PhantomCustomerRating.scoreForRow(row, fiveAgo);
          final item = RatedCustomer(
            row: Map<String, dynamic>.from(row),
            score: score,
            scoreFiveDaysAgo: prior,
          );
          final bucket =
              PhantomCustomerRating.fiveDayTrendBucket(score, prior);
          if (bucket == 'up') {
            rose.add(item);
          } else if (bucket == 'down') {
            down.add(item);
          } else {
            flat.add(item);
          }
        } catch (e, st) {
          debugPrint('rating row skipped: $e\n$st');
        }
      }
      int byName(RatedCustomer a, RatedCustomer b) =>
          a.name.compareTo(b.name);
      rose.sort(byName);
      flat.sort(byName);
      down.sort(byName);
      if (!mounted) return;
      setState(() {
        _rose = rose;
        _flat = flat;
        _down = down;
        _loading = false;
        _error = '';
      });
    } catch (e, st) {
      debugPrint('rating reload failed: $e\n$st');
      if (!mounted) return;
      // Keep last good buckets if we already showed something.
      final hadData =
          _rose.isNotEmpty || _flat.isNotEmpty || _down.isNotEmpty;
      setState(() {
        _loading = false;
        _error = hadData ? '' : 'تعذر تحميل التقييمات';
      });
    }
  }

  Future<void> _openDetail(RatedCustomer item) async {
    // Warm payment history before user taps كشف حساب.
    // ignore: unawaited_futures
    CustomerPaymentsCache.instance.get(item.customerId);
    await Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => RatingCustomerDetailPage(item: item),
      ),
    );
    if (mounted) reload();
  }

  @override
  Widget build(BuildContext context) {
    super.build(context);
    return Directionality(
      textDirection: TextDirection.rtl,
      child: AppSafeScaffold(
        backgroundColor: AppTheme.backgroundColor,
        safeBottom: false,
        appBar: AppBar(
          backgroundColor: AppTheme.primaryColor,
          title: const Text('التقييم',
              style: TextStyle(fontFamily: 'Cairo', color: Colors.white)),
          automaticallyImplyLeading: false,
        ),
        body: _loading
            ? const Center(child: CircularProgressIndicator())
            : _error.isNotEmpty
                ? Center(
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Text(_error,
                            style: const TextStyle(fontFamily: 'Cairo')),
                        const SizedBox(height: 12),
                        TextButton(
                          onPressed: reload,
                          child: const Text('إعادة المحاولة',
                              style: TextStyle(fontFamily: 'Cairo')),
                        ),
                      ],
                    ),
                  )
                : RefreshIndicator(
                    onRefresh: reload,
                    child: ListView(
                      padding: const EdgeInsets.all(12),
                      children: [
                        _BucketCard(
                          title: 'ارتفع تقييمهم خلال 5 أيام',
                          emptyHint: 'لا أسماء مرتفعة خلال 5 أيام',
                          stripeIndex: 0,
                          accent: Colors.green.shade700,
                          items: _rose,
                          onOpen: _openDetail,
                        ),
                        const SizedBox(height: 12),
                        _BucketCard(
                          title: 'مساوٍ تقييمهم خلال 5 أيام',
                          emptyHint: 'لا أسماء مساوية خلال 5 أيام',
                          stripeIndex: 1,
                          accent: Colors.blueGrey.shade700,
                          items: _flat,
                          onOpen: _openDetail,
                        ),
                        const SizedBox(height: 12),
                        _BucketCard(
                          title: 'هبط تقييمهم خلال 5 أيام',
                          emptyHint: 'لا أسماء هابطة خلال 5 أيام',
                          stripeIndex: 0,
                          accent: Colors.red.shade700,
                          items: _down,
                          onOpen: _openDetail,
                        ),
                      ],
                    ),
                  ),
      ),
    );
  }
}

class _BucketCard extends StatelessWidget {
  const _BucketCard({
    required this.title,
    required this.emptyHint,
    required this.stripeIndex,
    required this.accent,
    required this.items,
    required this.onOpen,
  });

  final String title;
  final String emptyHint;
  final int stripeIndex;
  final Color accent;
  final List<RatedCustomer> items;
  final Future<void> Function(RatedCustomer) onOpen;

  Color get _fill => stripeIndex.isEven
      ? AppTheme.primaryColor.withValues(alpha: 0.10)
      : const Color(0xFFE8EEF5);

  Color get _border => stripeIndex.isEven
      ? AppTheme.primaryColor.withValues(alpha: 0.38)
      : const Color(0xFF5B7C99).withValues(alpha: 0.40);

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        color: _fill,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: _border),
      ),
      child: ExpansionTile(
        initiallyExpanded: true,
        tilePadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 4),
        childrenPadding: const EdgeInsets.fromLTRB(8, 0, 8, 10),
        title: Text(
          '$title (${items.length})',
          style: TextStyle(
            fontFamily: 'Cairo',
            fontWeight: FontWeight.bold,
            color: accent,
            fontSize: 15,
          ),
        ),
        children: [
          if (items.isEmpty)
            Padding(
              padding: const EdgeInsets.all(12),
              child: Text(emptyHint,
                  style:
                      const TextStyle(fontFamily: 'Cairo', color: Colors.grey)),
            )
          else
            ...items.map((r) {
              final prior = r.scoreFiveDaysAgo;
              final delta = prior == null ? null : r.score - prior;
              return ListTile(
                dense: true,
                title: Text(
                  r.name,
                  style: const TextStyle(
                    fontFamily: 'Cairo',
                    fontWeight: FontWeight.bold,
                  ),
                ),
                subtitle: Text(
                  prior == null
                      ? 'الحالي: ${PhantomCustomerRating.formatScoreNumber(r.score)}'
                      : 'قبل 5 أيام: ${PhantomCustomerRating.formatScoreNumber(prior)} → الحالي: ${PhantomCustomerRating.formatScoreNumber(r.score)}'
                          '${delta == null || delta == 0 ? '' : ' (${delta > 0 ? '+' : ''}$delta)'}',
                  style: const TextStyle(fontFamily: 'Cairo', fontSize: 12),
                ),
                trailing: Text(
                  PhantomCustomerRating.formatScoreNumber(r.score),
                  style: const TextStyle(
                    fontFamily: 'Cairo',
                    fontWeight: FontWeight.w900,
                    fontSize: 18,
                    color: Colors.black,
                  ),
                ),
                onTap: () => onOpen(r),
              );
            }),
        ],
      ),
    );
  }
}

class RatedCustomer {
  RatedCustomer({
    required this.row,
    required this.score,
    this.scoreFiveDaysAgo,
  });
  final Map<String, dynamic> row;
  final int score;
  final int? scoreFiveDaysAgo;

  String get name => '${row['CustomerName'] ?? ''}';
  int get customerId => SafeNum.toInt(row['CustomerId']);
  Client get asClient => Client.fromDbRow(row);
}

class RatingCustomerDetailPage extends StatefulWidget {
  const RatingCustomerDetailPage({super.key, required this.item});
  final RatedCustomer item;

  @override
  State<RatingCustomerDetailPage> createState() =>
      _RatingCustomerDetailPageState();
}

class _RatingCustomerDetailPageState extends State<RatingCustomerDetailPage>
    with WidgetsBindingObserver {
  final _noteCtrl = TextEditingController();
  List<({String date, double amount})> _payments = [];
  bool _paymentsLoading = true;
  String? _paymentsHint;
  bool _busy = false;

  /// PDF waiting to be attached after the customer chat was opened by link.
  File? _pendingPdf;
  String? _pendingPhone;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    // Warm font bytes only (no UI) so first tap is faster.
    // ignore: unawaited_futures
    PdfAssetCache.precache();
    _bootPayments();
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    _noteCtrl.dispose();
    super.dispose();
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state != AppLifecycleState.resumed) return;
    if (_pendingPdf == null || _pendingPhone == null) return;
    // ignore: unawaited_futures
    _askAttachPendingPdf();
  }

  Future<void> _askAttachPendingPdf() async {
    final file = _pendingPdf;
    final phone = _pendingPhone;
    _pendingPdf = null;
    _pendingPhone = null;
    if (file == null || phone == null || !mounted) return;

    final send = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('إرسال ملف كشف الحساب',
            style: TextStyle(fontFamily: 'Cairo')),
        content: const Text(
          'إذا أرسلت رسالة «كشف حساب» للزبون، اضغط «إرسال الملف» '
          'ليُرفق الملف في نفس محادثته.',
          style: TextStyle(fontFamily: 'Cairo'),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx, false),
            child: const Text('إلغاء', style: TextStyle(fontFamily: 'Cairo')),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(ctx, true),
            child: const Text('إرسال الملف',
                style: TextStyle(fontFamily: 'Cairo')),
          ),
        ],
      ),
    );
    if (send != true || !mounted) return;

    await WhatsAppPdfShare.markChatReady(phone);
    final mode = await WhatsAppPdfShare.sendPdfToPhone(
      phone: phone,
      pdfFile: file,
      caption: 'كشف حساب',
    );
    // ignore: avoid_print
    print('shareStatement: attach after link mode=$mode');
    if (!mounted || mode != null) return;
    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(
        content: Text('تعذر فتح محادثة واتساب لهذا الرقم',
            style: TextStyle(fontFamily: 'Cairo')),
      ),
    );
  }

  Future<void> _bootPayments() async {
    final id = widget.item.customerId;
    final disk = await CustomerPaymentsCache.instance.loadDisk(id);
    if (disk != null && mounted) {
      setState(() {
        _payments = disk;
        _paymentsLoading = false;
      });
    }
    await _loadPayments(force: disk == null);
  }

  Future<void> _loadPayments({bool force = false}) async {
    if (force || _payments.isEmpty) {
      if (mounted) {
        setState(() {
          _paymentsLoading = _payments.isEmpty;
          _paymentsHint = null;
        });
      }
    }
    try {
      final rows = await CustomerPaymentsCache.instance
          .get(widget.item.customerId, forceRefresh: force);
      if (!mounted) return;
      setState(() {
        _payments = rows;
        _paymentsLoading = false;
        if (rows.isEmpty) {
          _paymentsHint =
              'لا توجد تسديدات في السجل (الواصل المجموع من السيرفر قد يكون أقدم من القائمة)';
        }
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _paymentsLoading = false;
        if (_payments.isEmpty) {
          _paymentsHint = 'تعذر تحميل سجل التسديدات';
        }
      });
    }
  }

  Future<void> _saveNote() async {
    final text = _noteCtrl.text.trim();
    if (text.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
            content: Text('نص الملاحظة مطلوب',
                style: TextStyle(fontFamily: 'Cairo'))),
      );
      return;
    }
    setState(() => _busy = true);
    try {
      final prefs = await SharedPreferences.getInstance();
      final link = prefs.getString('LinkDelegate') ?? '';
      final asyncId = prefs.getString('AsyncId') ?? '';
      final id = widget.item.customerId;
      final res = await http
          .post(
            Uri.parse('${link}customers/$id/notes'),
            headers: {'Content-Type': 'application/json'},
            body: json.encode({'asyncId': asyncId, 'noteText': text}),
          )
          .timeout(const Duration(seconds: 20));
      if (!mounted) return;
      if (res.statusCode >= 200 && res.statusCode < 300) {
        _noteCtrl.clear();
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
              content: Text('تم حفظ الملاحظة',
                  style: TextStyle(fontFamily: 'Cairo'))),
        );
      } else {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
              content: Text('تعذر حفظ الملاحظة',
                  style: TextStyle(fontFamily: 'Cairo'))),
        );
      }
    } catch (_) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
            content: Text('تعذر حفظ الملاحظة',
                style: TextStyle(fontFamily: 'Cairo'))),
      );
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  /// Scenario: tap → generate PDF → send to customer WhatsApp number.
  Future<void> _shareStatement() async {
    if (_busy) return;
    final c = widget.item.asClient;
    if (c.phone.trim().isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text(
            'لا يوجد رقم هاتف لهذا الزبون لفتح واتساب',
            style: TextStyle(fontFamily: 'Cairo'),
          ),
        ),
      );
      return;
    }

    setState(() => _busy = true);
    var dialogOpen = false;
    void dismissDialog() {
      if (!dialogOpen || !mounted) return;
      if (Navigator.of(context).canPop()) Navigator.of(context).pop();
      dialogOpen = false;
    }

    try {
      // Show spinner, then yield so UI paints (PDF runs in isolate — no ANR).
      if (mounted) {
        dialogOpen = true;
        showDialog<void>(
          context: context,
          barrierDismissible: false,
          builder: (_) => const Center(
            child: Card(
              child: Padding(
                padding: EdgeInsets.all(20),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    CircularProgressIndicator(),
                    SizedBox(height: 12),
                    Text('جاري إنشاء الملف...',
                        style: TextStyle(fontFamily: 'Cairo')),
                  ],
                ),
              ),
            ),
          ),
        );
        await Future<void>.delayed(const Duration(milliseconds: 16));
      }

      // 1) Generate PDF now (from payments already on screen).
      final rows = List<({String date, double amount})>.of(_payments);
      final bytes = await AccountStatementPdf(
        customerName: c.name,
        saleDate: Formatters.formatDate(c.dateSaleDevice),
        salePrice: c.salePrice,
        receiptsTotal: c.receipt,
        remaining: c.remaining,
        lastPaymentDate: c.lastPaymentDate.isEmpty
            ? ''
            : Formatters.formatDate(c.lastPaymentDate),
        paymentRows: rows,
      ).buildBytes().timeout(const Duration(seconds: 15));

      final dir = await getTemporaryDirectory();
      // Unique ASCII file per customer phone — avoids WhatsApp reusing last chat.
      final normalizedPhone = WhatsAppPdfShare.normalizeIraqPhone(c.phone);
      final file = File('${dir.path}/kashf_$normalizedPhone.pdf');
      await file.writeAsBytes(bytes, flush: false);

      dismissDialog();
      if (!mounted) return;

      // 2) Phone always from this customer's card.
      // ignore: avoid_print
      print('shareStatement: phone=${c.phone} → $normalizedPhone');
      final ready = await WhatsAppPdfShare.isChatReady(normalizedPhone);
      if (!mounted) return;

      if (ready) {
        final mode = await WhatsAppPdfShare.sendPdfToPhone(
          phone: normalizedPhone,
          pdfFile: file,
          caption: 'كشف حساب',
        );
        // ignore: avoid_print
        print('shareStatement: direct mode=$mode');
        if (!mounted) return;
        if (mode == null) {
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(
              content: Text(
                'تعذر فتح محادثة واتساب لهذا الرقم',
                style: TextStyle(fontFamily: 'Cairo'),
              ),
            ),
          );
        }
      } else {
        // No chat with this number yet: open it by link (works for numbers not
        // in contacts), then attach the PDF when the user returns.
        _pendingPdf = file;
        _pendingPhone = normalizedPhone;
        final opened = await WhatsAppPdfShare.openCustomerChat(
          normalizedPhone,
          text: 'كشف حساب — ${c.name}',
        );
        // ignore: avoid_print
        print('shareStatement: link opened=$opened');
        if (!opened) {
          _pendingPdf = null;
          _pendingPhone = null;
          if (!mounted) return;
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(
              content: Text(
                'تعذر فتح محادثة واتساب لهذا الرقم',
                style: TextStyle(fontFamily: 'Cairo'),
              ),
            ),
          );
        }
      }
    } catch (e, st) {
      // ignore: avoid_print
      print('shareStatement FAILED: $e\n$st');
      dismissDialog();
      if (!mounted) return;
      final short = '$e'.replaceAll('\n', ' ');
      final tip = short.length > 80 ? '${short.substring(0, 80)}…' : short;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            'تعذر إنشاء أو إرسال كشف الحساب\n$tip',
            style: const TextStyle(fontFamily: 'Cairo', fontSize: 12),
          ),
          duration: const Duration(seconds: 6),
        ),
      );
    } finally {
      dismissDialog();
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final c = widget.item.asClient;
    final nature =
        c.itemsNames.trim().isNotEmpty ? c.itemsNames : c.shopName;
    final prior = widget.item.scoreFiveDaysAgo;
    return Directionality(
      textDirection: TextDirection.rtl,
      child: AppSafeScaffold(
        backgroundColor: AppTheme.backgroundColor,
        appBar: AppBar(
          backgroundColor: AppTheme.primaryColor,
          title: Text(c.name,
              style: const TextStyle(fontFamily: 'Cairo', color: Colors.white)),
        ),
        body: ListView(
          padding: const EdgeInsets.all(16),
          children: [
            _line('الهاتف', c.phone.isEmpty ? '—' : c.phone),
            _line('طبيعة المحل', nature.isEmpty ? '—' : nature),
            _line('عنوان المحل',
                c.address.isEmpty ? (c.shopName.isEmpty ? '—' : c.shopName) : c.address),
            _line('التقييم الحالي',
                PhantomCustomerRating.formatScoreNumber(widget.item.score)),
            _line(
                'تقييم قبل 5 أيام',
                prior == null
                    ? '—'
                    : PhantomCustomerRating.formatScoreNumber(prior)),
            _line('تاريخ المبيع',
                c.dateSaleDevice.isEmpty
                    ? '—'
                    : Formatters.formatDate(c.dateSaleDevice)),
            _line(
                'آخر تسديد',
                c.lastPaymentDate.isEmpty
                    ? '—'
                    : Formatters.formatDate(c.lastPaymentDate)),
            _line('عدد التسديدات', c.countReceiptDevice),
            _line('عدد أيام التسديد', c.numberOfDayPayment),
            const SizedBox(height: 12),
            Row(
              children: [
                const Expanded(
                  child: Text('التسديدات',
                      style: TextStyle(
                          fontFamily: 'Cairo', fontWeight: FontWeight.bold)),
                ),
                if (_paymentsLoading)
                  const SizedBox(
                      width: 16,
                      height: 16,
                      child: CircularProgressIndicator(strokeWidth: 2))
                else if (_payments.isNotEmpty)
                  Text('${_payments.length}',
                      style: const TextStyle(
                          fontFamily: 'Cairo', color: Colors.grey)),
              ],
            ),
            const SizedBox(height: 8),
            if (_payments.isEmpty && _paymentsLoading)
              const Padding(
                padding: EdgeInsets.symmetric(vertical: 12),
                child: Center(
                    child: SizedBox(
                        width: 24,
                        height: 24,
                        child: CircularProgressIndicator(strokeWidth: 2))),
              )
            else if (_payments.isEmpty)
              Text(
                _paymentsHint ?? 'لا توجد تسديدات مسجّلة',
                style: const TextStyle(fontFamily: 'Cairo', color: Colors.grey),
              )
            else ...[
              if (_paymentsHint != null && _paymentsLoading)
                const Padding(
                  padding: EdgeInsets.only(bottom: 8),
                  child: LinearProgressIndicator(minHeight: 2),
                ),
              ..._payments.map((p) => ListTile(
                    dense: true,
                    contentPadding: EdgeInsets.zero,
                    title: Text(
                      Formatters.formatNumber(p.amount),
                      style: const TextStyle(
                          fontFamily: 'Cairo', fontWeight: FontWeight.bold),
                    ),
                    trailing: Text(p.date,
                        style: const TextStyle(
                            fontFamily: 'Cairo', color: Colors.grey)),
                  )),
            ],
            const SizedBox(height: 12),
            TextField(
              controller: _noteCtrl,
              maxLines: 3,
              style: const TextStyle(fontFamily: 'Cairo'),
              decoration: const InputDecoration(
                labelText: 'ملاحظة',
                labelStyle: TextStyle(fontFamily: 'Cairo'),
                border: OutlineInputBorder(),
              ),
            ),
            const SizedBox(height: 8),
            OutlinedButton(
              onPressed: _busy ? null : _saveNote,
              child: const Text('حفظ الملاحظة',
                  style: TextStyle(fontFamily: 'Cairo')),
            ),
            const SizedBox(height: 16),
            ElevatedButton.icon(
              onPressed: _busy
                  ? null
                  : () {
                      Navigator.pushNamed(
                        context,
                        '/DelegateSalesRequest',
                        arguments: {
                          'saleRequestType': 'Old',
                          'customer': {
                            'customerId': c.customerId,
                            'CustomerId': c.customerId,
                            'customerName': c.name,
                            'CustomerName': c.name,
                            'phoneNumber': c.phone,
                            'PhoneNumber': c.phone,
                          },
                        },
                      );
                    },
              icon: const Icon(Icons.add_shopping_cart),
              label: const Text('طلب مبيع',
                  style: TextStyle(fontFamily: 'Cairo')),
              style: ElevatedButton.styleFrom(
                backgroundColor: AppTheme.primaryColor,
                foregroundColor: Colors.white,
              ),
            ),
            const SizedBox(height: 8),
            ElevatedButton.icon(
              onPressed: _busy ? null : _shareStatement,
              icon: const Icon(Icons.send),
              label: const Text('إرسال كشف حساب',
                  style: TextStyle(fontFamily: 'Cairo')),
              style: ElevatedButton.styleFrom(
                backgroundColor: Colors.teal.shade700,
                foregroundColor: Colors.white,
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _line(String k, String v) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 8),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Expanded(
            child: Text(v,
                textAlign: TextAlign.left,
                style: const TextStyle(
                    fontFamily: 'Cairo', fontWeight: FontWeight.w600)),
          ),
          const SizedBox(width: 12),
          Text(k,
              style: const TextStyle(fontFamily: 'Cairo', color: Colors.grey)),
        ],
      ),
    );
  }
}
