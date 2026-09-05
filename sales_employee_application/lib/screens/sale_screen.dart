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
  int _eval = 3;
  final Map<int, int> _qty = {};
  List<SalesInventoryItem> _stock = [];
  bool _loadingStock = false;
  bool _saving = false;
  SalesWorkRequest? _fromRequest;
  List<SalesCustomerList> _customerLists = [];
  int? _customerListId;
  int? _preferredListId;
  bool _lockName = false;
  bool _lockPhone = false;
  bool _lockProvince = false;
  bool _lockCard = false;
  bool _lockAddress = false;
  bool _lockLandmark = false;
  bool _lockMukhtar = false;
  bool _hydratedDirectory = false;

  bool get _blocksSale => _eval == 1 || _eval == 2;

  @override
  void initState() {
    super.initState();
    _loadCustomerLists();
  }

  Future<void> _loadCustomerLists() async {
    try {
      final rows = await SalesRepositoryFactory.instance.activeCustomerLists();
      if (!mounted) return;
      setState(() {
        _customerLists = rows;
        _applyPreferredList();
      });
    } catch (_) {
      if (!mounted) return;
      setState(() => _customerLists = []);
    }
  }

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
          delegateId: arg.delegateId,
          delegateName: arg.delegateName,
        );
      }
      _preferList(arg.delegateId);
      _applyPreferredList();
      _fillIfPresent(_name, arg.customerName, lock: (v) => _lockName = v);
      _fillIfPresent(_phone, arg.customerPhone, lock: (v) => _lockPhone = v);
      _fillIfPresent(_province, arg.customerProvince, lock: (v) => _lockProvince = v);
      _fillIfPresent(_address, arg.customerAddress, lock: (v) => _lockAddress = v);
      if (!_lockProvince && _province.text.trim().isEmpty) {
        _province.text = 'النجف';
      }
      WidgetsBinding.instance.addPostFrameCallback((_) => _hydrateFromDirectory());
    }
  }

  void _fillIfPresent(TextEditingController c, String? value, {required void Function(bool) lock}) {
    final text = value?.trim() ?? '';
    if (text.isEmpty) {
      lock(false);
      return;
    }
    c.text = text;
    lock(true);
  }

  void _applyCustomer(SalesCustomer c) {
    _picked = c;
    _existing = true;
    _fillIfPresent(_name, c.fullName, lock: (v) => _lockName = v);
    _fillIfPresent(_phone, c.phone, lock: (v) => _lockPhone = v);
    _fillIfPresent(_province, c.province, lock: (v) => _lockProvince = v);
    _fillIfPresent(_card, c.nationalCardNumber, lock: (v) => _lockCard = v);
    _fillIfPresent(_address, c.address, lock: (v) => _lockAddress = v);
    _fillIfPresent(_landmark, c.nearestLandmark, lock: (v) => _lockLandmark = v);
    _fillIfPresent(_mukhtar, c.mukhtarName, lock: (v) => _lockMukhtar = v);
    if ((c.rationCenterNumber ?? '').trim().isNotEmpty) {
      _ration.text = c.rationCenterNumber!.trim();
    }
    if (!_lockProvince && _province.text.trim().isEmpty) {
      _province.text = 'النجف';
    }
    _preferList(c.delegateId);
    _applyPreferredList();
  }

  void _preferList(int? id) {
    if (id == null || id <= 0) return;
    _preferredListId ??= id;
  }

  void _applyPreferredList() {
    if (_customerListId != null && _customerLists.any((e) => e.listId == _customerListId)) {
      return;
    }
    final preferred = _preferredListId;
    if (preferred != null && _customerLists.any((e) => e.listId == preferred)) {
      _customerListId = preferred;
      return;
    }
    if (_customerListId != null && !_customerLists.any((e) => e.listId == _customerListId)) {
      _customerListId = null;
    }
  }

  Future<void> _hydrateFromDirectory() async {
    if (_hydratedDirectory) return;
    _hydratedDirectory = true;
    final req = _fromRequest;
    if (req == null) return;
    try {
      final query = (req.customerPhone ?? req.customerName).trim();
      if (query.length < 2) return;
      final rows = await SalesRepositoryFactory.instance.searchCustomers(query);
      SalesCustomer? match;
      if (req.existingCustomerId != null && req.existingCustomerId! > 0) {
        for (final row in rows) {
          if (row.customerId == req.existingCustomerId) {
            match = row;
            break;
          }
        }
      }
      match ??= () {
        for (final row in rows) {
          if ((req.customerPhone ?? '').isNotEmpty && row.phone == req.customerPhone) {
            return row;
          }
        }
        return null;
      }();
      if (match == null || !mounted) return;
      setState(() {
        if (_picked == null && match!.customerId > 0) {
          _picked = match;
          _existing = true;
        }
        if (!_lockCard) _fillIfPresent(_card, match!.nationalCardNumber, lock: (v) => _lockCard = v);
        if (!_lockAddress) _fillIfPresent(_address, match!.address, lock: (v) => _lockAddress = v);
        if (!_lockLandmark) _fillIfPresent(_landmark, match!.nearestLandmark, lock: (v) => _lockLandmark = v);
        if (!_lockMukhtar) _fillIfPresent(_mukhtar, match!.mukhtarName, lock: (v) => _lockMukhtar = v);
        if (_ration.text.trim().isEmpty && (match!.rationCenterNumber ?? '').trim().isNotEmpty) {
          _ration.text = match.rationCenterNumber!.trim();
        }
        _preferList(match!.delegateId);
        _applyPreferredList();
      });
    } catch (_) {}
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
    super.dispose();
  }

  Future<void> _loadStock() async {
    setState(() => _loadingStock = true);
    try {
      _stock = (await SalesRepositoryFactory.instance.inventory())
          .where((i) => !SalesStaffInventoryFilter.isHidden(i.productName))
          .toList();
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
    if (_blocksSale) return 0;
    return _previewBase;
  }

  String _customerListName() {
    for (final list in _customerLists) {
      if (list.listId == _customerListId) return list.listName;
    }
    return '${_customerListId ?? ''}';
  }

  String? _req(String? v) => (v == null || v.trim().isEmpty) ? 'مطلوب' : null;

  bool _filled(TextEditingController c) => c.text.trim().isNotEmpty;

  bool get _fromKnownSource => _fromRequest != null || _picked != null;

  Future<void> _next() async {
    if (_step == 0) {
      if (!(_form.currentState?.validate() ?? false)) return;
      if (_customerLists.isEmpty) {
        _toast('لا توجد قوائم معرفة في هذا الفرع');
        return;
      }
      if (_customerListId == null || _customerListId! <= 0) {
        _toast('اختيار قائمة الزبون مطلوب');
        return;
      }
      if (!_filled(_name) ||
          !_filled(_phone) ||
          !_filled(_province) ||
          !_filled(_card) ||
          !_filled(_address) ||
          !_filled(_landmark) ||
          !_filled(_mukhtar)) {
        _toast('بيانات الزبون غير مكتملة');
        return;
      }
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
          customerId: (_existing && _picked != null && !_picked!.isForeignBranch)
              ? _picked!.customerId
              : null,
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
          customerListId: _customerListId,
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
                          : (_blocksSale ? 'حفظ الطلب المرفوض' : 'حفظ في المبيعات المعلقة')),
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
          if (_fromRequest == null) ...[
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
                  if (result is SalesCustomer) {
                    setState(() => _applyCustomer(result));
                  }
                },
                child: const Text('اختيار من البحث'),
              ),
            const SizedBox(height: AppSpacing.sm),
          ],
          if (_knownSummary().isNotEmpty) ...[
            const Text('بيانات الزبون الموجودة', style: TextStyle(fontWeight: FontWeight.w700)),
            const SizedBox(height: 6),
            for (final line in _knownSummary())
              Padding(
                padding: const EdgeInsets.only(bottom: 4),
                child: Text(line, style: const TextStyle(color: AppColors.muted)),
              ),
            const SizedBox(height: AppSpacing.sm),
          ],
          InputDecorator(
            decoration: const InputDecoration(labelText: 'قائمة الزبون *'),
            child: DropdownButtonHideUnderline(
              child: DropdownButton<int>(
                isExpanded: true,
                value: _customerLists.any((e) => e.listId == _customerListId) ? _customerListId : null,
                hint: const Text('اختر قائمة الزبون'),
                items: [
                  for (final list in _customerLists)
                    DropdownMenuItem(value: list.listId, child: Text(list.listName)),
                ],
                onChanged: (value) => setState(() => _customerListId = value),
              ),
            ),
          ),
          if (_customerLists.isEmpty)
            const Padding(
              padding: EdgeInsets.only(top: 6),
              child: Text('لا توجد قوائم معرفة في هذا الفرع',
                  style: TextStyle(color: AppColors.muted, fontSize: 12)),
            ),
          const SizedBox(height: AppSpacing.sm),
          if (!_lockName) _field(_name, 'الاسم الكامل *', validator: _req),
          if (!_lockPhone) _field(_phone, 'رقم الهاتف *', keyboard: TextInputType.phone, validator: _req),
          if (!_lockProvince) _field(_province, 'المحافظة *', validator: _req),
          if (!_lockCard) _field(_card, 'رقم البطاقة الوطنية *', keyboard: TextInputType.number, validator: _req),
          if (!_lockAddress) _field(_address, 'العنوان *', validator: _req),
          if (!_lockLandmark) _field(_landmark, 'أقرب نقطة دالة *', validator: _req),
          if (!_lockMukhtar) _field(_mukhtar, 'اسم المختار *', validator: _req),
          if (!_fromKnownSource) _field(_ration, 'رقم مركز التموين (اختياري)', keyboard: TextInputType.number, last: true),
        ],
      ),
    );
  }

  List<String> _knownSummary() {
    final lines = <String>[];
    if (_lockName && _filled(_name)) lines.add(_name.text.trim());
    if (_lockPhone && _filled(_phone)) lines.add(_phone.text.trim());
    if (_lockProvince && _filled(_province)) lines.add(_province.text.trim());
    if (_lockAddress && _filled(_address)) lines.add(_address.text.trim());
    if (_lockCard && _filled(_card)) lines.add('البطاقة: ${_card.text.trim()}');
    if (_lockLandmark && _filled(_landmark)) lines.add(_landmark.text.trim());
    if (_lockMukhtar && _filled(_mukhtar)) lines.add('المختار: ${_mukhtar.text.trim()}');
    if (_ration.text.trim().isNotEmpty && _fromKnownSource) {
      lines.add('التموين: ${_ration.text.trim()}');
    }
    return lines;
  }

  Widget _field(TextEditingController c, String label,
      {TextInputType? keyboard, String? Function(String?)? validator, bool last = false}) {
    return Padding(
      padding: const EdgeInsets.only(bottom: AppSpacing.sm),
      child: TextFormField(
        controller: c,
        keyboardType: keyboard,
        validator: validator,
        textInputAction: last ? TextInputAction.done : TextInputAction.next,
        enableSuggestions: false,
        autocorrect: false,
        smartDashesType: SmartDashesType.disabled,
        smartQuotesType: SmartQuotesType.disabled,
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
            labelText: 'الملاحظة *',
          ),
        ),
        if (_blocksSale) ...[
          const SizedBox(height: AppSpacing.sm),
          Text(
            _eval == 1
                ? 'تقييم مرفوض: ممنوع إتمام البيع. الملاحظة إجبارية.'
                : 'تقييم مقبول: ممنوع إتمام البيع. لا يوجد تضاعف سعر ولا عقد مكتمل.',
            style: const TextStyle(color: AppColors.danger),
          ),
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
        Text('قائمة الزبون: ${_customerListName()}'),
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
        if (!_blocksSale) ...[
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
