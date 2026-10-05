import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:main_follower_application/core/config/province_labels.dart';
import 'package:main_follower_application/core/ui/safe_dialogs.dart';
import 'package:main_follower_application/data/customer_profile_repository.dart';
import 'package:main_follower_application/data/sales_repository.dart';
import 'package:main_follower_application/domain/branch_sale.dart';
import 'package:main_follower_application/domain/customer_profile.dart';
import 'package:main_follower_application/presentation/sale_auth_dialog.dart';

class CustomerProfileScreen extends StatefulWidget {
  const CustomerProfileScreen({
    super.key,
    required this.missionId,
    required this.cityValue,
    this.customerId,
    this.customerName,
    this.phone,
    this.saleId,
    this.showAuthenticate = false,
    this.onAuthenticated,
  });

  final int missionId;
  final String cityValue;
  final int? customerId;
  final String? customerName;
  final String? phone;
  final int? saleId;
  final bool showAuthenticate;
  final Future<void> Function()? onAuthenticated;

  @override
  State<CustomerProfileScreen> createState() => _CustomerProfileScreenState();
}

class _CustomerProfileScreenState extends State<CustomerProfileScreen> {
  late Future<CustomerProfile> _future;

  @override
  void initState() {
    super.initState();
    _future = _load();
  }

  Future<CustomerProfile> _load() => CustomerProfileRepository.load(
        missionId: widget.missionId,
        cityValue: widget.cityValue,
        customerId: widget.customerId,
        name: widget.customerName,
        phone: widget.phone,
      );

  Future<void> _reload() async {
    setState(() => _future = _load());
    await _future;
  }

  Future<void> _openDoc(CustomerDocument doc) async {
    try {
      final (bytes, type) = await CustomerProfileRepository.documentBytes(
        missionId: widget.missionId,
        cityValue: widget.cityValue,
        documentId: doc.id,
      );
      if (!mounted) return;
      final isImage = type.startsWith('image/');
      await showSafeDialog<void>(
        context: context,
        builder: (ctx) => AlertDialog(
          title: Text(doc.displayLabel),
          content: SizedBox(
            width: double.maxFinite,
            child: isImage
                ? Image.memory(Uint8List.fromList(bytes), fit: BoxFit.contain)
                : Text('تم تحميل الملف (${bytes.length} بايت) — النوع: $type'),
          ),
          actions: [
            TextButton(onPressed: () => Navigator.pop(ctx), child: const Text('إغلاق')),
          ],
        ),
      );
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('$e')));
    }
  }

  Future<void> _authenticate(CustomerProfile p) async {
    final saleId = widget.saleId ??
        (p.sales.isNotEmpty ? p.sales.first.saleId : 0);
    if (saleId <= 0) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('لا يوجد مبيع للمصادقة')),
      );
      return;
    }
    final sale = BranchSale(
      saleId: saleId,
      customerId: (p.customerId != null && p.customerId! > 0)
          ? p.customerId
          : widget.customerId,
      customerName: p.customerName.isNotEmpty
          ? p.customerName
          : (widget.customerName ?? '—'),
      phone: p.phone ?? widget.phone,
    );
    final saved = await showSaleReviewDialog(
      context,
      sale: sale,
      onSubmit: (score, note, shopStock, dailyRevenue) async {
        await SalesRepository.submitReview(
          missionId: widget.missionId,
          cityValue: widget.cityValue,
          saleId: sale.saleId,
          customerName: sale.customerName,
          score: score,
          note: note,
          shopStockEstimatedValue: shopStock,
          estimatedDailyRevenue: dailyRevenue,
        );
      },
    );
    if (saved && mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('تمت مصادقة المبيع بنجاح')),
      );
      await widget.onAuthenticated?.call();
      await _reload();
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('ملف الزبون'),
        backgroundColor: const Color(0xFF0F3D3E),
        foregroundColor: Colors.white,
        actions: [
          IconButton(onPressed: _reload, icon: const Icon(Icons.refresh)),
        ],
      ),
      body: FutureBuilder<CustomerProfile>(
        future: _future,
        builder: (context, snap) {
          if (snap.connectionState != ConnectionState.done) {
            return const Center(child: CircularProgressIndicator());
          }
          if (snap.hasError) {
            return Center(
              child: Padding(
                padding: const EdgeInsets.all(16),
                child: Text(snap.error.toString()),
              ),
            );
          }
          final p = snap.data!;
          return ListView(
            padding: const EdgeInsets.fromLTRB(16, 16, 16, 32),
            children: [
              Text(
                p.customerName,
                style: Theme.of(context).textTheme.headlineSmall?.copyWith(
                      fontWeight: FontWeight.bold,
                      color: const Color(0xFF0F3D3E),
                    ),
              ),
              const SizedBox(height: 8),
              _line('الهاتف', p.phone),
              _line('العنوان', p.address),
              _line('المحافظة', p.province ?? ProvinceLabels.display(widget.cityValue)),
              _line('البطاقة الوطنية', p.nationalCardNumber),
              _line('المندوب / القائمة', p.delegateName),
              if (widget.showAuthenticate) ...[
                const SizedBox(height: 12),
                FilledButton.icon(
                  style: FilledButton.styleFrom(
                    backgroundColor: const Color(0xFF0F3D3E),
                  ),
                  onPressed: () => _authenticate(p),
                  icon: const Icon(Icons.verified_user),
                  label: const Text('تمت المصادقة'),
                ),
              ],
              const SizedBox(height: 20),
              _section(
                'المستمسكات',
                p.documents.isEmpty
                    ? [const Text('لا مستمسكات')]
                    : p.documents
                        .map((d) => ListTile(
                              contentPadding: EdgeInsets.zero,
                              leading: const Icon(Icons.badge_outlined),
                              title: Text(d.displayLabel),
                              trailing: const Icon(Icons.visibility_outlined),
                              onTap: () => _openDoc(d),
                            ))
                        .toList(),
              ),
              _section(
                'الملاحظات',
                p.notes.isEmpty
                    ? [const Text('لا ملاحظات')]
                    : p.notes
                        .map((n) => Card(
                              child: ListTile(
                                title: Text(n.body.isEmpty ? '—' : n.body),
                                subtitle: Text([
                                  if (n.authorRole != null && n.authorRole!.isNotEmpty)
                                    n.authorRole!,
                                  if (n.authorName != null && n.authorName!.isNotEmpty)
                                    n.authorName!,
                                  if (n.createdAt != null) n.createdAt!,
                                ].join(' · ')),
                              ),
                            ))
                        .toList(),
              ),
              _section(
                'تقييم المبيع (متابع / مدير)',
                p.evaluations.isEmpty
                    ? [const Text('لا تقييمات مسجّلة')]
                    : p.evaluations
                        .map((e) => ListTile(
                              contentPadding: EdgeInsets.zero,
                              title: Text(
                                  'مبيع #${e.saleId} — ${e.name.isEmpty ? e.level : e.name}'),
                              subtitle: Text(
                                  e.note?.isNotEmpty == true ? e.note! : 'بدون ملاحظة'),
                            ))
                        .toList(),
              ),
              _section(
                'المبيعات والمصادقة',
                p.sales.isEmpty
                    ? [const Text('لا مبيعات')]
                    : p.sales
                        .map((s) => ListTile(
                              contentPadding: EdgeInsets.zero,
                              title: Text('مبيع #${s.saleId}'),
                              subtitle: Text([
                                s.statusLabel,
                                if (s.employeeName != null) 'موظف: ${s.employeeName}',
                                if (s.finalPrice != null) '${s.finalPrice}',
                              ].join(' · ')),
                            ))
                        .toList(),
              ),
              if (p.payments.isNotEmpty)
                _section(
                  'القبوضات',
                  p.payments
                      .take(20)
                      .map((pay) => ListTile(
                            contentPadding: EdgeInsets.zero,
                            title: Text(pay.amount == null ? '—' : '${pay.amount} د.ع'),
                            subtitle: Text([
                              if (pay.date != null) pay.date!,
                              if (pay.note != null) pay.note!,
                            ].join(' · ')),
                          ))
                      .toList(),
                ),
              if (p.history.isNotEmpty)
                _section(
                  'سجل الإجراءات',
                  p.history
                      .take(30)
                      .map((h) => ListTile(
                            contentPadding: EdgeInsets.zero,
                            title: Text(h.title),
                            subtitle: Text([
                              if (h.actor != null) h.actor!,
                              if (h.detail != null) h.detail!,
                              if (h.at != null) h.at!,
                            ].join(' · ')),
                          ))
                      .toList(),
                ),
            ],
          );
        },
      ),
    );
  }

  Widget _line(String label, String? value) {
    if (value == null || value.trim().isEmpty) return const SizedBox.shrink();
    return Padding(
      padding: const EdgeInsets.only(bottom: 4),
      child: Text('$label: ${value.trim()}'),
    );
  }

  Widget _section(String title, List<Widget> children) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(
            title,
            style: const TextStyle(
              fontWeight: FontWeight.bold,
              fontSize: 16,
              color: Color(0xFF0F3D3E),
            ),
          ),
          const SizedBox(height: 8),
          ...children,
        ],
      ),
    );
  }
}
