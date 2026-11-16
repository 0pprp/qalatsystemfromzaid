import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/app_state.dart';
import '../../core/network/api_exception.dart';
import '../../core/theme/app_theme.dart';
import '../../core/utils/formatters.dart';
import '../../core/widgets/common.dart';
import '../../core/widgets/state_views.dart';
import '../../data/models.dart';
import '../../data/repository.dart';

/// One short page: product, quantity, customer, phone, governorate, address.
class NewOrderScreen extends ConsumerStatefulWidget {
  const NewOrderScreen({super.key, this.initialProduct});

  final Product? initialProduct;

  @override
  ConsumerState<NewOrderScreen> createState() => _NewOrderScreenState();
}

class _NewOrderScreenState extends ConsumerState<NewOrderScreen> {
  final _name = TextEditingController();
  final _phone = TextEditingController();
  final _address = TextEditingController();
  final _notes = TextEditingController();
  int? _productId;
  int _quantity = 1;
  int? _cityValue;
  bool _busy = false;
  Map<String, String> _errors = {};
  String? _error;
  // Same key while this form is open: a double tap or a retry after a timeout never creates two orders.
  String _idempotencyKey = Repository.newIdempotencyKey();

  @override
  void initState() {
    super.initState();
    _productId = widget.initialProduct?.id;
  }

  @override
  void dispose() {
    for (final c in [_name, _phone, _address, _notes]) {
      c.dispose();
    }
    super.dispose();
  }

  Map<String, String> _validate() {
    final e = <String, String>{};
    if (_productId == null) e['productId'] = 'اختر المنتج';
    final nameError = customerNameError(_name.text);
    if (nameError != null) e['customerName'] = nameError;
    final digits = _phone.text.replaceAll(RegExp(r'\D'), '');
    if (digits.length < 10) e['customerPhone'] = 'رقم الهاتف غير صحيح';
    if (_cityValue == null) e['cityValue'] = 'اختر المحافظة';
    if (_address.text.trim().length < 3) e['address'] = 'اكتب عنوان الزبون';
    return e;
  }

  Future<void> _submit() async {
    final local = _validate();
    setState(() {
      _errors = local;
      _error = null;
    });
    if (local.isNotEmpty) return;
    setState(() => _busy = true);
    try {
      final order = await ref.read(repositoryProvider).createOrder(
            productId: _productId!,
            quantity: _quantity,
            customerName: _name.text.trim(),
            customerPhone: _phone.text.trim(),
            cityValue: _cityValue,
            address: _address.text.trim(),
            notes: _notes.text.trim().isEmpty ? null : _notes.text.trim(),
            idempotencyKey: _idempotencyKey,
          );
      ref.read(dataVersionProvider.notifier).bump();
      if (!mounted) return;
      await showDialog<void>(
        context: context,
        builder: (ctx) => AlertDialog(
          icon: const Icon(Icons.check_circle_rounded, color: AppColors.success, size: 56),
          title: const Text('تم إرسال الطلب'),
          content: Text(
            'رقم الطلب ${order.summary.orderNumber}\nتُضاف عمولتك (${Fmt.money(order.summary.myCommission)}) إلى محفظتك عندما يتم البيع.',
            textAlign: TextAlign.center,
            style: const TextStyle(height: 1.7),
          ),
          actions: [FilledButton(onPressed: () => Navigator.pop(ctx), child: const Text('تم'))],
        ),
      );
      if (mounted) context.pop();
    } catch (e) {
      final ex = ApiException.from(e);
      if (ex.code != 'network' && ex.code != 'timeout') _idempotencyKey = Repository.newIdempotencyKey();
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

  String? _err(String key) => _errors[key];

  void _clear(String key) {
    if (_errors.containsKey(key)) setState(() => _errors = {..._errors}..remove(key));
  }

  @override
  Widget build(BuildContext context) {
    final products = ref.watch(productsProvider);
    final branches = ref.watch(branchesProvider);
    final selected = products.value?.where((p) => p.id == _productId).firstOrNull;

    return Scaffold(
      appBar: AppBar(title: const Text('طلب بيع جديد')),
      body: SafeArea(
        child: ListView(
          padding: const EdgeInsets.fromLTRB(16, 8, 16, 32),
          children: [
            AsyncView<List<Product>>(
              value: products,
              onRetry: () => ref.invalidate(productsProvider),
              loading: const LinearProgressIndicator(),
              data: (list) => DropdownButtonFormField<int>(
                initialValue: list.any((p) => p.id == _productId) ? _productId : null,
                isExpanded: true,
                decoration: InputDecoration(labelText: 'المنتج', errorText: _err('productId'), prefixIcon: const Icon(Icons.inventory_2_outlined)),
                items: [
                  for (final p in list)
                    DropdownMenuItem(value: p.id, child: Text('${p.name} — ${Fmt.money(p.price)}', overflow: TextOverflow.ellipsis)),
                ],
                onChanged: (v) {
                  setState(() => _productId = v);
                  _clear('productId');
                },
              ),
            ),
            const SizedBox(height: 12),
            Row(
              children: [
                const Text('الكمية', style: TextStyle(fontWeight: FontWeight.w700)),
                const Spacer(),
                IconButton.filledTonal(
                  onPressed: _quantity > 1 ? () => setState(() => _quantity--) : null,
                  icon: const Icon(Icons.remove_rounded),
                ),
                SizedBox(width: 44, child: Text('$_quantity', textAlign: TextAlign.center, style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w800))),
                IconButton.filledTonal(
                  onPressed: _quantity < 10 ? () => setState(() => _quantity++) : null,
                  icon: const Icon(Icons.add_rounded),
                ),
              ],
            ),
            if (selected != null) ...[
              const SizedBox(height: 8),
              NoticeBanner(
                color: AppColors.success,
                icon: Icons.payments_outlined,
                message: 'السعر ${Fmt.money(selected.price * _quantity)} · عمولتك ${Fmt.money(selected.myCommission * _quantity)} عند إتمام البيع',
              ),
            ],
            const SizedBox(height: 20),
            TextField(
              controller: _name,
              onChanged: (_) => _clear('customerName'),
              textInputAction: TextInputAction.next,
              decoration: InputDecoration(labelText: 'اسم الزبون الثلاثي', errorText: _err('customerName'), prefixIcon: const Icon(Icons.person_outline_rounded)),
            ),
            const SizedBox(height: 12),
            TextField(
              controller: _phone,
              onChanged: (_) => _clear('customerPhone'),
              keyboardType: TextInputType.phone,
              textInputAction: TextInputAction.next,
              inputFormatters: [FilteringTextInputFormatter.allow(RegExp(r'[0-9٠-٩+ ]')), LengthLimitingTextInputFormatter(16)],
              textDirection: TextDirection.ltr,
              decoration: InputDecoration(
                labelText: 'رقم هاتف الزبون',
                hintText: '07XXXXXXXXX',
                errorText: _err('customerPhone'),
                prefixIcon: const Icon(Icons.phone_outlined),
              ),
            ),
            const SizedBox(height: 12),
            DropdownButtonFormField<int>(
              initialValue: _cityValue,
              isExpanded: true,
              decoration: InputDecoration(labelText: 'المحافظة', errorText: _err('cityValue'), prefixIcon: const Icon(Icons.location_city_outlined)),
              items: [for (final b in branches.value ?? const <Branch>[]) DropdownMenuItem(value: b.cityValue, child: Text(b.name))],
              onChanged: (v) {
                setState(() => _cityValue = v);
                _clear('cityValue');
              },
            ),
            const SizedBox(height: 12),
            TextField(
              controller: _address,
              onChanged: (_) => _clear('address'),
              maxLines: 2,
              minLines: 1,
              decoration: InputDecoration(labelText: 'العنوان (المنطقة، أقرب نقطة دالة)', errorText: _err('address'), prefixIcon: const Icon(Icons.place_outlined)),
            ),
            const SizedBox(height: 12),
            TextField(
              controller: _notes,
              maxLines: 3,
              minLines: 1,
              maxLength: 500,
              decoration: InputDecoration(labelText: 'ملاحظات (اختياري)', errorText: _err('notes'), prefixIcon: const Icon(Icons.notes_rounded)),
            ),
            if (_error != null) ...[
              const SizedBox(height: 8),
              NoticeBanner(message: _error!, color: AppColors.danger, icon: Icons.error_outline_rounded),
            ],
            const SizedBox(height: 16),
            LoadingButton(label: 'إرسال الطلب', icon: Icons.send_rounded, loading: _busy, onPressed: _submit),
          ],
        ),
      ),
    );
  }
}

/// The same rule as the server: at least three Arabic names, «عبد …» / «أبو …» count as one, and diacritics,
/// tatweel and invisible keyboard marks are ignored. Returns the message to show, or null when the name is fine.
String? customerNameError(String input) {
  final stripped = input.replaceAll(RegExp('[\u064B-\u065F\u0670\u0640\u200B-\u200F\u202A-\u202E\u2066-\u2069\uFEFF]'), '');
  final words = stripped.trim().split(RegExp(r'\s+')).where((w) => w.isNotEmpty).toList();
  const tooShort = 'اكتب اسم الزبون الثلاثي (ثلاثة أسماء على الأقل، و«عبد الحسن» تُحسب اسماً واحداً)';
  if (words.isEmpty) return tooShort;
  final arabic = RegExp(r'^[\u0621-\u064A\u0671-\u06D3]+$');
  if (words.any((w) => !arabic.hasMatch(w))) return 'اكتب اسم الزبون بالحروف العربية فقط';
  const prefixes = {'عبد', 'ابو', 'أبو', 'ابي', 'أبي'};
  var names = 0;
  for (var i = 0; i < words.length; i++) {
    if (prefixes.contains(words[i]) && i + 1 < words.length) i++;
    names++;
  }
  return names < 3 ? tooShort : null;
}
