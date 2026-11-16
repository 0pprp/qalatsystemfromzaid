import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:uuid/uuid.dart';

import '../../../core/network/api_exception.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/widgets/common.dart';
import '../../../core/widgets/state_views.dart';
import '../../auth/presentation/auth_widgets.dart';
import '../../work/presentation/auto_start.dart';
import '../data/order_draft.dart';
import '../data/orders_repository.dart';
import 'shop_capture_widgets.dart';

/// «طلب بيع» (the app's first tab): one short page with only what the sales request needs.
/// Sales are daily installments. The shop photo and location are optional extras.
class NewOrderScreen extends ConsumerStatefulWidget {
  const NewOrderScreen({super.key});

  @override
  ConsumerState<NewOrderScreen> createState() => _NewOrderScreenState();
}

class _NewOrderScreenState extends ConsumerState<NewOrderScreen> {
  var _draft = OrderDraft();
  var _formKey = UniqueKey();
  Map<String, String> _errors = {};
  bool _submitting = false;
  bool _extrasOpen = false;
  String? _idempotencyKey;

  /// The uploaded photo's URL, kept across retries of the same photo.
  String? _uploadedPhotoFor;
  String? _uploadedPhotoUrl;

  final _otherProduct = TextEditingController();
  final _fullName = TextEditingController();
  final _phone = TextEditingController();
  final _address = TextEditingController();

  @override
  void dispose() {
    for (final c in [_otherProduct, _fullName, _phone, _address]) {
      c.dispose();
    }
    super.dispose();
  }

  void _changed(String key) {
    _idempotencyKey = null; // the content changed → a new submit attempt
    if (_errors.containsKey(key)) setState(() => _errors.remove(key));
  }

  void _reset() {
    for (final c in [_otherProduct, _fullName, _phone, _address]) {
      c.clear();
    }
    setState(() {
      _draft = OrderDraft();
      _formKey = UniqueKey();
      _errors = {};
      _extrasOpen = false;
      _idempotencyKey = null;
      _uploadedPhotoFor = null;
      _uploadedPhotoUrl = null;
    });
  }

  Future<void> _submit() async {
    FocusScope.of(context).unfocus();
    // A message bar would cover the send button: errors are shown under their fields instead.
    ScaffoldMessenger.of(context).hideCurrentSnackBar();
    final d = _draft;
    final errors = d.validate();
    if (errors.isNotEmpty) {
      setState(() => _errors = errors);
      return;
    }
    // A sale request means the agent is working: «الدوام» starts by itself (alongside sending, never blocking it).
    unawaited(autoStartWorkDay(context, ref));
    // One key per submit attempt; reused if the agent retries after a network failure.
    _idempotencyKey ??= const Uuid().v4();
    setState(() => _submitting = true);
    try {
      final repo = ref.read(ordersRepositoryProvider);
      if (d.hasPhoto && _uploadedPhotoFor != d.shopPhotoPath) {
        _uploadedPhotoUrl = await repo.uploadShopPhoto(d.shopPhotoPath!);
        _uploadedPhotoFor = d.shopPhotoPath;
      }
      final order = await repo.create(d.toRequest(shopPhotoUrl: d.hasPhoto ? _uploadedPhotoUrl : null), idempotencyKey: _idempotencyKey!);
      if (!mounted) return;
      ref.read(ordersVersionProvider.notifier).bump();
      setState(() => _submitting = false);
      _reset();
      await context.push('/order-success', extra: order);
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() => _submitting = false);
      if (!e.isNetwork && (e.statusCode ?? 0) < 500) _idempotencyKey = null;
      if (e.code == 'duplicate_open_order') {
        await _showDuplicate(e);
      } else if (e.code == 'validation' && e.fields.isNotEmpty) {
        const keys = {
          'items': 'product',
          'fullName': 'fullName',
          'firstName': 'fullName',
          'phone': 'phone',
          'cityValue': 'cityValue',
          'details': 'address',
          'area': 'address',
        };
        final mapped = <String, String>{};
        for (final f in e.fields.entries) {
          final k = keys[f.key.split('.').last];
          if (k != null) mapped[k] = f.value;
        }
        setState(() => _errors = mapped);
        if (mapped.isEmpty) context.showSnack(e.fields.values.first, error: true);
      } else {
        context.showError(e);
      }
    }
  }

  Future<void> _showDuplicate(ApiException e) async {
    final number = e.stringData('orderNumber');
    await showDialog<void>(
      context: context,
      builder: (ctx) => AlertDialog(
        icon: const Icon(Icons.info_outline_rounded, color: AppColors.warning, size: 40),
        title: const Text('الطلب مرسل مسبقاً'),
        content: Text(
          'يوجد طلب مفتوح لنفس الزبون ونفس المنتج${number != null ? ' (رقم $number)' : ''}. تابعه من «الطلبات».',
          style: const TextStyle(height: 1.6),
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx), child: const Text('حسناً')),
          FilledButton(
            onPressed: () {
              Navigator.pop(ctx);
              context.go('/orders');
            },
            child: const Text('عرض الطلبات'),
          ),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final d = _draft;
    final extras = [if (d.hasPhoto) 'صورة', if (d.hasLocation) 'موقع'];
    return Scaffold(
      appBar: AppBar(title: const Text('طلب بيع جديد')),
      body: ListView(
        key: _formKey,
        padding: const EdgeInsets.fromLTRB(16, 8, 16, 24),
        children: [
          const _Section('المنتج'),
          DropdownButtonFormField<SaleProduct>(
            initialValue: d.product,
            isExpanded: true,
            decoration: InputDecoration(
              labelText: 'اختر المنتج',
              prefixIcon: const Icon(Icons.inventory_2_outlined),
              errorText: _errors['product'],
            ),
            items: [for (final p in SaleProduct.values) DropdownMenuItem(value: p, child: Text(p.label))],
            onChanged: (p) {
              setState(() => d.product = p);
              _changed('product');
            },
          ),
          if (d.product == SaleProduct.other) ...[
            const SizedBox(height: 12),
            TextField(
              controller: _otherProduct,
              autofocus: true,
              maxLength: 100,
              decoration: InputDecoration(labelText: 'اسم المنتج', counterText: '', errorText: _errors['otherProduct']),
              onChanged: (v) {
                d.otherProduct = v;
                _changed('otherProduct');
              },
            ),
          ],
          const _Section('الزبون'),
          TextField(
            controller: _fullName,
            textInputAction: TextInputAction.next,
            decoration: InputDecoration(
              labelText: 'اسم الزبون الثلاثي',
              hintText: 'مثال: محمد جاسم علي',
              prefixIcon: const Icon(Icons.person_outline_rounded),
              errorText: _errors['fullName'],
            ),
            onChanged: (v) {
              d.fullName = v;
              _changed('fullName');
            },
          ),
          const SizedBox(height: 12),
          PhoneField(
            controller: _phone,
            label: 'رقم الهاتف',
            errorText: _errors['phone'],
            onChanged: (v) {
              d.phone = v;
              _changed('phone');
            },
          ),
          const _Section('العنوان'),
          BranchDropdown(
            value: d.cityValue,
            errorText: _errors['cityValue'],
            onChanged: (v) {
              setState(() => d.cityValue = v);
              _changed('cityValue');
            },
          ),
          const SizedBox(height: 12),
          TextField(
            controller: _address,
            minLines: 1,
            maxLines: 3,
            decoration: InputDecoration(
              labelText: 'العنوان',
              hintText: 'المنطقة، الشارع، أقرب نقطة دالة',
              prefixIcon: const Icon(Icons.place_outlined),
              errorText: _errors['address'],
            ),
            onChanged: (v) {
              d.address = v;
              _changed('address');
            },
          ),
          const SizedBox(height: 16),
          AppCard(
            padding: EdgeInsets.zero,
            child: Theme(
              data: Theme.of(context).copyWith(dividerColor: Colors.transparent),
              child: ExpansionTile(
                initiallyExpanded: _extrasOpen,
                onExpansionChanged: (v) => _extrasOpen = v,
                leading: const Icon(Icons.add_a_photo_outlined, color: AppColors.primary),
                title: const Text('صورة المحل وموقعه', style: TextStyle(fontWeight: FontWeight.w700)),
                subtitle: Text(extras.isEmpty ? 'اختياري' : 'تمت إضافة: ${extras.join(' و')}',
                    style: const TextStyle(color: AppColors.textMuted, fontSize: 12.5)),
                childrenPadding: const EdgeInsets.fromLTRB(12, 0, 12, 12),
                children: [
                  ShopPhotoCard(
                    path: d.shopPhotoPath,
                    onChanged: (p) => setState(() => d.shopPhotoPath = p),
                  ),
                  const SizedBox(height: 8),
                  ShopLocationCard(
                    latitude: d.latitude,
                    longitude: d.longitude,
                    onChanged: (lat, lng) => setState(() {
                      d
                        ..latitude = lat
                        ..longitude = lng;
                    }),
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
      bottomNavigationBar: Container(
        padding: const EdgeInsets.fromLTRB(16, 10, 16, 10),
        decoration: const BoxDecoration(color: Colors.white, border: Border(top: BorderSide(color: AppColors.border))),
        child: LoadingButton(
          label: 'إرسال الطلب',
          icon: Icons.send_rounded,
          loading: _submitting,
          onPressed: _submit,
        ),
      ),
    );
  }
}

class _Section extends StatelessWidget {
  const _Section(this.title);

  final String title;

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.only(top: 18, bottom: 10),
        child: Text(title, style: Theme.of(context).textTheme.titleMedium),
      );
}
