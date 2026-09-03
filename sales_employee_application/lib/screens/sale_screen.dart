import 'package:flutter/material.dart';
import 'package:sales_employee_application/data/sales_models.dart';
import 'package:sales_employee_application/data/sales_repository_factory.dart';
import 'package:sales_employee_application/services/api_client.dart';
import 'package:sales_employee_application/utils/app_theme.dart';
import 'package:sales_employee_application/utils/sales_format.dart';
import 'package:sales_employee_application/widgets/inventory_item_info.dart';

class SaleScreen extends StatefulWidget {
  const SaleScreen({super.key});

  @override
  State<SaleScreen> createState() => _SaleScreenState();
}

class _SaleScreenState extends State<SaleScreen> {
  int _step = 0;
  bool _existing = true;
  SalesCustomer? _picked;
  final _form = GlobalKey<FormState>();
  final _name = TextEditingController();
  final _phone = TextEditingController();
  final _province = TextEditingController(text: 'النجف');
  final _card = TextEditingController();
  final _address = TextEditingController();
  final _landmark = TextEditingController();
  final _mukhtar = TextEditingController();
  final _ration = TextEditingController();
  final _note = TextEditingController();
  final _installment = TextEditingController();
  late final List<FocusNode> _fieldFocus = List.generate(8, (_) => FocusNode());
  int _eval = 3;
  final Map<int, int> _qty = {};
  List<SalesInventoryItem> _stock = [];
  bool _loadingStock = false;
  bool _saving = false;
  SalesWorkRequest? _fromRequest;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    final arg = ModalRoute.of(context)?.settings.arguments;
    if (arg is SalesCustomer && _picked == null) {
      _applyCustomer(arg);
    }
    if (arg is SalesWorkRequest && _fromRequest == null) {
      _fromRequest = arg;
      _existing = arg.existingCustomerId != null && arg.existingCustomerId! > 0;
      if (_existing) {
        _picked = SalesCustomer(
          customerId: arg.existingCustomerId!,
          fullName: arg.customerName,
          phone: arg.customerPhone,
          province: arg.customerProvince,
          address: arg.customerAddress,
        );
      }
      _name.text = arg.customerName;
      _phone.text = arg.customerPhone ?? '';
      _province.text = arg.customerProvince ?? 'النجف';
      _address.text = arg.customerAddress ?? '';
    }
  }

  void _applyCustomer(SalesCustomer c) {
    _picked = c;
    _existing = true;
    _name.text = c.fullName;
    _phone.text = c.phone ?? '';
    _province.text = c.province ?? 'النجف';
    _card.text = c.nationalCardNumber ?? '';
    _address.text = c.address ?? '';
    _landmark.text = c.nearestLandmark ?? '';
    _mukhtar.text = c.mukhtarName ?? '';
    _ration.text = c.rationCenterNumber ?? '';
  }

  @override
  void dispose() {
    _name.dispose();
    _phone.dispose();
    _province.dispose();
    _card.dispose();
    _address.dispose();
    _landmark.dispose();
    _mukhtar.dispose();
    _ration.dispose();
    _note.dispose();
    _installment.dispose();
    for (final n in _fieldFocus) {
      n.dispose();
    }
    super.dispose();
  }

  Future<void> _loadStock() async {
    setState(() => _loadingStock = true);
    try {
      _stock = await SalesRepositoryFactory.instance.inventory();
    } finally {
      if (mounted) setState(() => _loadingStock = false);
    }
  }

  num get _previewBase {
    num total = 0;
    for (final item in _stock) {
      final q = _qty[item.productId] ?? 0;
      total += item.salePrice * q;
    }
    return total;
  }

  num get _previewFinal {
    if (_eval == 1) return 0;
    if (_eval == 2) return _previewBase * 2;
    return _previewBase;
  }

  String? _req(String? v) => (v == null || v.trim().isEmpty) ? 'مطلوب' : null;

  Future<void> _next() async {
    if (_step == 0) {
      if (!(_form.currentState?.validate() ?? false)) return;
      await _loadStock();
    }
    if (_step == 1 && !_qty.values.any((q) => q > 0)) {
      _toast('أضف مادة واحدة على الأقل');
      return;
    }
    if (_step == 2 && _note.text.trim().isEmpty) {
      _toast('الملاحظة إلزامية');
      return;
    }
    if (_step == 3) {
      final n = num.tryParse(_installment.text.trim()) ?? 0;
      if (n <= 0) {
        _toast('القسط اليومي يجب أن يكون أكبر من صفر');
        return;
      }
    }
    setState(() => _step++);
  }

  Future<void> _save() async {
    setState(() => _saving = true);
    try {
      final items = _qty.entries
          .where((e) => e.value > 0)
          .map((e) => SalesDraftItem(productId: e.key, quantity: e.value))
          .toList();
      final created = await SalesRepositoryFactory.instance.createSale(
        SalesDraftCreateRequest(
          customerId: _existing ? _picked?.customerId : null,
          customer: {
            'fullName': _name.text.trim(),
            'phone': _phone.text.trim(),
            'province': _province.text.trim(),
            'nationalCardNumber': _card.text.trim(),
            'address': _address.text.trim(),
            'nearestLandmark': _landmark.text.trim(),
            'mukhtarName': _mukhtar.text.trim(),
            if (_ration.text.trim().isNotEmpty) 'rationCenterNumber': _ration.text.trim(),
          },
          items: items,
          evaluationLevel: _eval,
          evaluationNote: _note.text.trim(),
          dailyInstallment: num.parse(_installment.text.trim()),
          salesRequestId: _fromRequest?.id,
        ),
      );
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('تم الحفظ. الحالة: ${SalesStatusLabels.of(created.status)}')),
      );
      Navigator.pushReplacementNamed(context, '/sale-details', arguments: created.saleId);
    } on ApiException catch (e) {
      _toast(e.message);
    } catch (e) {
      _toast('تعذر حفظ العملية');
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  void _toast(String m) {
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(m)));
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      resizeToAvoidBottomInset: true,
      appBar: AppBar(title: const Text('إنشاء عملية بيع')),
      body: SafeArea(
        child: Column(
          children: [
              Padding(
                padding: const EdgeInsets.fromLTRB(AppSpacing.md, AppSpacing.md, AppSpacing.md, 0),
                child: Text('الخطوة ${_step + 1} من 5',
                    style: const TextStyle(color: AppColors.muted)),
              ),
              Expanded(child: _stepBody()),
              Padding(
                padding: const EdgeInsets.fromLTRB(
                  AppSpacing.md,
                  AppSpacing.sm,
                  AppSpacing.md,
                  AppSpacing.md,
                ),
                child: Row(
                  children: [
                    if (_step > 0)
                      TextButton(
                        onPressed: () => setState(() => _step--),
                        child: const Text('رجوع'),
                      ),
                    const Spacer(),
                    ElevatedButton(
                      onPressed: _saving ? null : (_step < 4 ? _next : _save),
                      child: Text(_step < 4
                          ? 'التالي'
                          : (_eval == 1 ? 'حفظ الطلب المرفوض' : 'حفظ في المبيعات المعلقة')),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),
      );
  }

  Widget _stepBody() {
    final padding = const EdgeInsets.fromLTRB(AppSpacing.md, AppSpacing.md, AppSpacing.md, AppSpacing.lg);
    switch (_step) {
      case 0:
        return SingleChildScrollView(
          keyboardDismissBehavior: ScrollViewKeyboardDismissBehavior.onDrag,
          padding: padding,
          child: _customerStep(),
        );
      case 1:
        return _itemsStep();
      case 2:
        return SingleChildScrollView(
          keyboardDismissBehavior: ScrollViewKeyboardDismissBehavior.onDrag,
          padding: padding,
          child: _evalStep(),
        );
      case 3:
        return SingleChildScrollView(
          keyboardDismissBehavior: ScrollViewKeyboardDismissBehavior.onDrag,
          padding: padding,
          child: _payStep(),
        );
      default:
        return SingleChildScrollView(
          keyboardDismissBehavior: ScrollViewKeyboardDismissBehavior.onDrag,
          padding: padding,
          child: _reviewStep(),
        );
    }
  }

  Widget _customerStep() {
    return Form(
      key: _form,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          SegmentedButton<bool>(
            segments: const [
              ButtonSegment(value: true, label: Text('زبون موجود')),
              ButtonSegment(value: false, label: Text('زبون جديد')),
            ],
            selected: {_existing},
            onSelectionChanged: (s) => setState(() => _existing = s.first),
          ),
          if (_existing)
            TextButton(
              onPressed: () async {
                final result = await Navigator.pushNamed(context, '/search', arguments: 'pick');
                if (result is SalesCustomer) _applyCustomer(result);
              },
              child: const Text('اختيار من البحث'),
            ),
          const SizedBox(height: AppSpacing.sm),
          _field(_name, 'الاسم الكامل *', 0, validator: _req),
          _field(_phone, 'رقم الهاتف *', 1, keyboard: TextInputType.phone, validator: _req),
          _field(_province, 'المحافظة *', 2, validator: _req),
          _field(_card, 'رقم البطاقة الوطنية *', 3, keyboard: TextInputType.number, validator: _req),
          _field(_address, 'العنوان *', 4, validator: _req),
          _field(_landmark, 'أقرب نقطة دالة *', 5, validator: _req),
          _field(_mukhtar, 'اسم المختار *', 6, validator: _req),
          _field(_ration, 'رقم مركز التموين (اختياري)', 7, keyboard: TextInputType.number, last: true),
        ],
      ),
    );
  }

  Widget _field(TextEditingController c, String label, int index,
      {TextInputType? keyboard, String? Function(String?)? validator, bool last = false}) {
    return Padding(
      padding: const EdgeInsets.only(bottom: AppSpacing.sm),
      child: TextFormField(
        controller: c,
        focusNode: _fieldFocus[index],
        keyboardType: keyboard,
        validator: validator,
        textInputAction: last ? TextInputAction.done : TextInputAction.next,
        enableSuggestions: false,
        autocorrect: false,
        smartDashesType: SmartDashesType.disabled,
        smartQuotesType: SmartQuotesType.disabled,
        onFieldSubmitted: (_) {
          if (last) {
            _fieldFocus[index].unfocus();
          } else {
            _fieldFocus[index + 1].requestFocus();
          }
        },
        decoration: InputDecoration(labelText: label),
      ),
    );
  }

  Widget _itemsStep() {
    if (_loadingStock) {
      return const Center(child: CircularProgressIndicator());
    }
    return Column(
      children: [
        Expanded(
          child: ListView.builder(
            keyboardDismissBehavior: ScrollViewKeyboardDismissBehavior.onDrag,
            padding: const EdgeInsets.fromLTRB(AppSpacing.md, AppSpacing.md, AppSpacing.md, AppSpacing.sm),
            itemCount: _stock.length,
            itemBuilder: (context, index) {
              final item = _stock[index];
              final q = _qty[item.productId] ?? 0;
              final empty = item.availableQuantity <= 0;
              return Card(
                child: Padding(
                  padding: const EdgeInsets.all(AppSpacing.sm),
                  child: Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Expanded(child: InventoryItemInfo(item: item)),
                      if (!empty)
                        Row(
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            IconButton(
                              onPressed: q <= 0
                                  ? null
                                  : () => setState(() => _qty[item.productId] = q - 1),
                              icon: const Icon(Icons.remove),
                            ),
                            Text('$q'),
                            IconButton(
                              onPressed: q >= item.availableQuantity
                                  ? null
                                  : () => setState(() => _qty[item.productId] = q + 1),
                              icon: const Icon(Icons.add),
                            ),
                          ],
                        ),
                    ],
                  ),
                ),
              );
            },
          ),
        ),
        Padding(
          padding: const EdgeInsets.fromLTRB(AppSpacing.md, 0, AppSpacing.md, AppSpacing.sm),
          child: Align(
            alignment: Alignment.centerRight,
            child: Text('المجموع الأساسي: ${MoneyFormat.iqd(_previewBase)}',
                style: const TextStyle(fontWeight: FontWeight.w700)),
          ),
        ),
      ],
    );
  }

  Widget _evalStep() {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Wrap(
          spacing: AppSpacing.sm,
          children: EvaluationLabels.map.entries.map((e) {
            final selected = _eval == e.key;
            return ChoiceChip(
              label: Text(e.value),
              selected: selected,
              selectedColor: AppColors.lightGreen.withValues(alpha: 0.25),
              onSelected: (_) => setState(() => _eval = e.key),
            );
          }).toList(),
        ),
        TextField(
          controller: _note,
          maxLines: 3,
          decoration: InputDecoration(
            labelText: _eval == 1 ? 'سبب الرفض *' : 'الملاحظة *',
          ),
        ),
        if (_eval == 1) ...[
          const SizedBox(height: AppSpacing.sm),
          const Text(
            'سيتم حفظ الطلب كطلب مرفوض في الأرشيف، ولن يمكن إتمام عملية البيع.',
            style: TextStyle(color: AppColors.danger),
          ),
        ],
        if (_eval == 2) ...[
          const SizedBox(height: AppSpacing.md),
          Text('السعر الأساسي: ${MoneyFormat.iqd(_previewBase)}'),
          const Text('معامل التقييم: ×2'),
          Text('سعر البيع النهائي: ${MoneyFormat.iqd(_previewFinal)}',
              style: const TextStyle(fontWeight: FontWeight.w700)),
          const Text('معاينة واجهة فقط. السعر النهائي يأتي من الخادم بعد الحفظ.',
              style: TextStyle(color: AppColors.muted, fontSize: 12)),
        ],
        if (_eval >= 3) ...[
          const SizedBox(height: AppSpacing.md),
          Text('السعر النهائي = السعر الأساسي: ${MoneyFormat.iqd(_previewBase)}',
              style: const TextStyle(fontWeight: FontWeight.w700)),
        ],
      ],
    );
  }

  Widget _payStep() {
    return TextField(
      controller: _installment,
      keyboardType: const TextInputType.numberWithOptions(decimal: false),
      textInputAction: TextInputAction.done,
      enableSuggestions: false,
      autocorrect: false,
      decoration: const InputDecoration(
        labelText: 'القسط اليومي *',
        suffixText: 'د.ع',
      ),
    );
  }

  Widget _reviewStep() {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        const Text('بيانات الزبون', style: TextStyle(fontWeight: FontWeight.w700)),
        Text(_name.text),
        Text(_phone.text),
        Text(_province.text),
        const SizedBox(height: AppSpacing.md),
        const Text('البضاعة', style: TextStyle(fontWeight: FontWeight.w700)),
        ..._stock.where((i) => (_qty[i.productId] ?? 0) > 0).map(
              (i) => Text('${i.productName} × ${_qty[i.productId]}'),
            ),
        const SizedBox(height: AppSpacing.md),
        Text('التقييم: ${EvaluationLabels.of(_eval)}'),
        Text('الملاحظة: ${_note.text}'),
        Text('السعر الأساسي (معاينة): ${MoneyFormat.iqd(_previewBase)}'),
        Text('السعر النهائي (معاينة): ${MoneyFormat.iqd(_previewFinal)}'),
        Text('القسط اليومي: ${MoneyFormat.iqd(num.tryParse(_installment.text) ?? 0)}'),
        if (_eval != 1) ...[
          const SizedBox(height: AppSpacing.md),
          const ListTile(
            title: Text('عقد البيع'),
            subtitle: Text('سيتم إنشاؤه عند إتمام البيع'),
            enabled: false,
          ),
          const ListTile(
            title: Text('وصل الأمانة'),
            subtitle: Text('سيتم إنشاؤه عند إتمام البيع'),
            enabled: false,
          ),
        ],
      ],
    );
  }
}
