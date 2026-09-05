import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';
import 'package:open_filex/open_filex.dart';
import 'package:sales_employee_application/data/sales_models.dart';
import 'package:sales_employee_application/data/sales_repository_factory.dart';
import 'package:sales_employee_application/screens/sale_complete_success_screen.dart';
import 'package:sales_employee_application/services/api_client.dart';
import 'package:sales_employee_application/services/sale_document_storage.dart';
import 'package:sales_employee_application/services/sale_documents.dart';
import 'package:sales_employee_application/utils/app_theme.dart';
import 'package:sales_employee_application/utils/sales_format.dart';
import 'package:sales_employee_application/widgets/inventory_item_info.dart';

class SaleScreen extends StatefulWidget {
  const SaleScreen({super.key});

  @visibleForTesting
  static List<int>? debugShopImageBytes;

  @override
  State<SaleScreen> createState() => _SaleScreenState();
}

class _SaleScreenState extends State<SaleScreen> {
  static const _lastStep = 4;
  int _step = 0;
  bool _existing = false;
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
  final _installment = TextEditingController();
  final _totalPrice = TextEditingController();
  final _downPayment = TextEditingController();
  final _shopName = TextEditingController();
  final _shopType = TextEditingController();
  final _shopStock = TextEditingController();
  final _shopDaily = TextEditingController();
  final _shopLength = TextEditingController();
  final _shopWidth = TextEditingController();
  final _shopNote = TextEditingController();
  num _defaultTotal = 0;
  num _defaultDaily = 0;
  num _defaultDown = 0;
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
  bool _lockAddress = false;
  SalesDraft? _created;
  List<SalesDocument> _previewDocs = const [];
  List<int>? _shopImageBytes;
  String _shopImageName = 'shop.jpg';
  String? _shopImageKey;
  String? _shopError;

  @override
  void initState() {
    super.initState();
    _loadCustomerLists();
    _shopLength.addListener(() => setState(() {}));
    _shopWidth.addListener(() => setState(() {}));
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
    _installment.dispose();
    _totalPrice.dispose();
    _downPayment.dispose();
    _shopName.dispose();
    _shopType.dispose();
    _shopStock.dispose();
    _shopDaily.dispose();
    _shopLength.dispose();
    _shopWidth.dispose();
    _shopNote.dispose();
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

  num get _previewDaily {
    num total = 0;
    for (final item in _stock) {
      final q = _qty[item.productId] ?? 0;
      total += (item.dailyInstallment ?? 0) * q;
    }
    return total;
  }

  num get _previewFinal => _parsedOrDefault(_totalPrice, _defaultTotal);

  num get _shopArea {
    final length = _parsedOrNull(_shopLength) ?? 0;
    final width = _parsedOrNull(_shopWidth) ?? 0;
    return length * width;
  }

  num _parsedOrDefault(TextEditingController c, num fallback) {
    final text = c.text.trim().replaceAll(',', '');
    if (text.isEmpty) return fallback;
    return num.tryParse(text) ?? fallback;
  }

  num? _parsedOrNull(TextEditingController c) {
    final text = c.text.trim().replaceAll(',', '');
    if (text.isEmpty) return null;
    return num.tryParse(text);
  }

  void _applyPriceDefaults() {
    _defaultTotal = _previewBase;
    _defaultDaily = _previewDaily;
    _defaultDown = (_defaultTotal * 0.05).round();
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
    if (_fromRequest == null) {
      _toast('لا يمكن إنشاء بيع بدون طلب مبيعات');
      return;
    }
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
    if (_step == 1) {
      _applyPriceDefaults();
    }
    if (_step == 2) {
      final daily = _parsedOrNull(_installment) ?? _defaultDaily;
      final total = _parsedOrNull(_totalPrice) ?? _defaultTotal;
      if (total <= 0) {
        _toast('سعر البيع الكلي يجب أن يكون أكبر من صفر');
        return;
      }
      if (daily <= 0) {
        _toast('القسط اليومي غير متوفر لهذه المواد. أدخل القسط اليومي.');
        return;
      }
    }
    if (_step == 3) {
      if (!_validateShop()) return;
      final ok = await _createDraftAndPreview();
      if (!ok) return;
    }
    setState(() => _step++);
  }

  bool _validateShop() {
    final name = _shopName.text.trim();
    final type = _shopType.text.trim();
    final stock = _parsedOrNull(_shopStock);
    final daily = _parsedOrNull(_shopDaily);
    final length = _parsedOrNull(_shopLength);
    final width = _parsedOrNull(_shopWidth);
    var image = _shopImageBytes;
    if ((image == null || image.isEmpty) && SaleScreen.debugShopImageBytes != null) {
      image = List<int>.from(SaleScreen.debugShopImageBytes!);
      _shopImageBytes = image;
      if (_shopImageName.isEmpty) _shopImageName = 'shop.jpg';
    }
    if (name.isEmpty ||
        type.isEmpty ||
        stock == null ||
        stock <= 0 ||
        daily == null ||
        daily <= 0 ||
        length == null ||
        length <= 0 ||
        width == null ||
        width <= 0) {
      setState(() => _shopError = 'أكمل كل حقول المحل المطلوبة.');
      return false;
    }
    if ((image == null || image.isEmpty) && (_shopImageKey == null || _shopImageKey!.isEmpty)) {
      setState(() => _shopError = 'صورة المحل مطلوبة.');
      return false;
    }
    setState(() => _shopError = null);
    return true;
  }

  SalesShopComplete? _shopPayload() {
    final imageKey = _shopImageKey;
    if (imageKey == null || imageKey.isEmpty) return null;
    return SalesShopComplete(
      shopName: _shopName.text.trim(),
      shopBusinessType: _shopType.text.trim(),
      shopStockEstimatedValue: _parsedOrNull(_shopStock) ?? 0,
      estimatedDailyRevenue: _parsedOrNull(_shopDaily) ?? 0,
      shopLength: _parsedOrNull(_shopLength) ?? 0,
      shopWidth: _parsedOrNull(_shopWidth) ?? 0,
      shopImageKey: imageKey,
      employeeNote: _shopNote.text.trim(),
      overrideTotalSalePrice: _parsedOrNull(_totalPrice),
      overrideDailyInstallment: _parsedOrNull(_installment),
      overrideDownPayment: _parsedOrNull(_downPayment),
    );
  }

  Future<bool> _createDraftAndPreview() async {
    setState(() => _saving = true);
    try {
      _created ??= await SalesRepositoryFactory.instance.createSale(
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
          items: _qty.entries
              .where((e) => e.value > 0)
              .map((e) => SalesDraftItem(productId: e.key, quantity: e.value))
              .toList(),
          overrideTotalSalePrice: _parsedOrNull(_totalPrice),
          overrideDailyInstallment: _parsedOrNull(_installment),
          overrideDownPayment: _parsedOrNull(_downPayment),
          dailyInstallment: _parsedOrNull(_installment) ?? 0,
          salesRequestId: _fromRequest?.id,
          customerListId: _customerListId,
        ),
      );
      final created = _created!;
      if (_shopImageKey == null || _shopImageKey!.isEmpty) {
        final bytes = _shopImageBytes ?? const <int>[];
        _shopImageKey = await SalesRepositoryFactory.instance.uploadShopImage(
          created.saleId,
          bytes,
          _shopImageName,
        );
      }
      final shop = _shopPayload();
      if (shop == null) {
        _toast('صورة المحل مطلوبة.');
        return false;
      }
      final preview = await SalesRepositoryFactory.instance.previewDocuments(created.saleId, shop);
      if (!mounted) return false;
      setState(() {
        _previewDocs = preview.documents;
        _created = created.copyWith(documents: preview.documents);
      });
      return true;
    } on ApiException catch (e) {
      _toast(e.message);
      return false;
    } catch (_) {
      _toast('تعذر تجهيز معاينة البيع');
      return false;
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  Future<void> _completeSale() async {
    final created = _created;
    final shop = _shopPayload();
    if (created == null || shop == null || _saving) return;
    setState(() => _saving = true);
    try {
      final result = await SalesRepositoryFactory.instance.completeSale(created.saleId, shop);
      final download = await _downloadAll(result.documents);
      if (!mounted) return;
      await Navigator.pushReplacement(
        context,
        MaterialPageRoute(
          builder: (_) => SaleCompleteSuccessScreen(
            saleId: result.saleId,
            finalSalePrice: result.finalSalePrice,
            completedAt: result.completedAt,
            contractPath: download.contractPath,
            receiptPath: download.receiptPath,
            downloadFailed: download.failed,
          ),
        ),
      );
    } on ApiException catch (e) {
      _toast(e.message);
    } catch (_) {
      _toast('تعذر إتمام البيع');
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  Future<_SaleDownloadBundle> _downloadAll(List<SalesDocument> docs) async {
    String? contract;
    String? receipt;
    var failed = false;
    try {
      final draft = _created;
      if (draft != null && docs.isNotEmpty) {
        final contractFile = await SaleDocumentStorage.savePdf(
          'Sale_${draft.saleId}_Contract.pdf',
          await SaleDocuments.contractBytesFromDraft(draft),
        );
        final receiptFile = await SaleDocumentStorage.savePdf(
          'Sale_${draft.saleId}_PromissoryNote.pdf',
          await SaleDocuments.receiptBytesFromDraft(draft),
        );
        contract = contractFile.path;
        receipt = receiptFile.path;
      }
    } catch (_) {
      failed = true;
    }
    if (contract != null && receipt != null) {
      return _SaleDownloadBundle(contractPath: contract, receiptPath: receipt, failed: false);
    }
    for (final doc in docs) {
      try {
        final bytes = await SalesRepositoryFactory.instance.downloadDocument(_created?.saleId ?? 0, doc);
        final file = await SaleDocumentStorage.savePdf(doc.fileName, bytes);
        if (doc.isContract) {
          contract = file.path;
        } else if (doc.isPromissoryNote) {
          receipt = file.path;
        }
      } catch (_) {
        failed = true;
      }
    }
    if (docs.isEmpty) failed = true;
    return _SaleDownloadBundle(
      contractPath: contract,
      receiptPath: receipt,
      failed: failed || contract == null || receipt == null,
    );
  }

  Future<void> _openOrDownload(SalesDocument doc) async {
    try {
      final draft = _created;
      if (draft != null) {
        final bytes = doc.isContract
            ? await SaleDocuments.contractBytesFromDraft(draft)
            : await SaleDocuments.receiptBytesFromDraft(draft);
        final file = await SaleDocumentStorage.savePdf(doc.fileName, bytes);
        await OpenFilex.open(file.path);
        return;
      }
      final bytes = await SalesRepositoryFactory.instance.downloadDocument(0, doc);
      final file = await SaleDocumentStorage.savePdf(doc.fileName, bytes);
      await OpenFilex.open(file.path);
    } catch (_) {
      if (!mounted) return;
      _toast('تعذر تنزيل المستند');
    }
  }

  Future<void> _pickShopImage() async {
    final debugBytes = SaleScreen.debugShopImageBytes;
    if (debugBytes != null && debugBytes.isNotEmpty) {
      setState(() {
        _shopImageBytes = List<int>.from(debugBytes);
        _shopImageName = 'shop.jpg';
        _shopImageKey = null;
        _shopError = null;
      });
      return;
    }
    try {
      final picked = await ImagePicker().pickImage(
        source: ImageSource.gallery,
        imageQuality: 85,
        maxWidth: 1600,
      );
      if (picked == null) return;
      final bytes = await picked.readAsBytes();
      if (!mounted) return;
      setState(() {
        _shopImageBytes = bytes;
        _shopImageName = picked.name.isEmpty ? 'shop.jpg' : picked.name;
        _shopImageKey = null;
        _shopError = null;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() => _shopError = 'تعذر اختيار صورة المحل');
    }
  }

  void _toast(String m) {
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(m)));
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      resizeToAvoidBottomInset: true,
      appBar: AppBar(title: const Text('إتمام البيع')),
      body: SafeArea(
        child: Column(
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(AppSpacing.md, AppSpacing.md, AppSpacing.md, 0),
              child: Text('الخطوة ${_step + 1} من ${_lastStep + 1}',
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
                      onPressed: _saving
                          ? null
                          : () => setState(() {
                                if (_created != null && _step > 3) {
                                  _step = 3;
                                } else if (_created == null) {
                                  _step--;
                                }
                              }),
                      child: const Text('رجوع'),
                    ),
                  const Spacer(),
                  ElevatedButton(
                    onPressed: _saving ? null : (_step < _lastStep ? _next : _completeSale),
                    child: _saving
                        ? const SizedBox(
                            height: 22,
                            width: 22,
                            child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white),
                          )
                        : Text(_step < _lastStep ? 'التالي' : 'تم البيع'),
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
          child: _priceStep(),
        );
      case 3:
        return SingleChildScrollView(
          keyboardDismissBehavior: ScrollViewKeyboardDismissBehavior.onDrag,
          padding: padding,
          child: _shopStep(),
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
    if (_fromRequest == null) {
      return const Text(
        'لا يمكن إنشاء بيع بدون طلب مبيعات مرسل للموظف.',
        style: TextStyle(color: AppColors.danger, fontWeight: FontWeight.w700),
      );
    }
    return Form(
      key: _form,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
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
          _field(_card, 'رقم البطاقة الوطنية *', keyboard: TextInputType.number, validator: _req),
          if (!_lockAddress) _field(_address, 'العنوان *', validator: _req),
          _field(_landmark, 'أقرب نقطة دالة *', validator: _req),
          _field(_mukhtar, 'اسم المختار *', validator: _req),
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

  Widget _priceStep() {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text('السعر الإجمالي الافتراضي: ${MoneyFormat.iqd(_defaultTotal)}',
            style: const TextStyle(fontWeight: FontWeight.w700)),
        Text('القسط اليومي الافتراضي: ${MoneyFormat.iqd(_defaultDaily)}'),
        Text('المقدمة الافتراضية (5%): ${MoneyFormat.iqd(_defaultDown)}'),
        const SizedBox(height: AppSpacing.md),
        const Text(
          'حقول التعديل اختيارية. إذا بقي الحقل فارغاً يُعتمد الافتراضي.',
          style: TextStyle(color: AppColors.muted),
        ),
        const SizedBox(height: AppSpacing.sm),
        TextField(
          controller: _totalPrice,
          keyboardType: const TextInputType.numberWithOptions(decimal: false),
          enableSuggestions: false,
          autocorrect: false,
          decoration: const InputDecoration(
            labelText: 'السعر الإجمالي (اختياري)',
            suffixText: 'د.ع',
          ),
        ),
        const SizedBox(height: AppSpacing.sm),
        TextField(
          controller: _installment,
          keyboardType: const TextInputType.numberWithOptions(decimal: false),
          enableSuggestions: false,
          autocorrect: false,
          decoration: const InputDecoration(
            labelText: 'القسط اليومي (اختياري)',
            suffixText: 'د.ع',
          ),
        ),
        const SizedBox(height: AppSpacing.sm),
        TextField(
          controller: _downPayment,
          keyboardType: const TextInputType.numberWithOptions(decimal: false),
          textInputAction: TextInputAction.done,
          enableSuggestions: false,
          autocorrect: false,
          decoration: const InputDecoration(
            labelText: 'المقدمة (اختياري)',
            suffixText: 'د.ع',
          ),
        ),
      ],
    );
  }

  Widget _shopStep() {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        const Text('بيانات المحل', style: TextStyle(fontWeight: FontWeight.w700)),
        const SizedBox(height: AppSpacing.sm),
        _field(_shopName, 'اسم المحل *', validator: _req),
        _field(_shopType, 'طبيعة عمل المحل *', validator: _req),
        _field(_shopStock, 'تقدير قيمة بضاعة المحل *', keyboard: TextInputType.number, validator: _req),
        _field(_shopDaily, 'تقدير الوارد اليومي *', keyboard: TextInputType.number, validator: _req),
        _field(_shopLength, 'طول المحل بالمتر *', keyboard: TextInputType.number, validator: _req),
        _field(_shopWidth, 'عرض المحل بالمتر *', keyboard: TextInputType.number, validator: _req),
        Align(
          alignment: Alignment.centerRight,
          child: Text(
            'المساحة: ${_shopArea.toStringAsFixed(_shopArea % 1 == 0 ? 0 : 2)} م²',
          ),
        ),
        const SizedBox(height: AppSpacing.sm),
        _field(_shopNote, 'ملاحظة الموظف (اختياري)', last: true),
        OutlinedButton(
          onPressed: _pickShopImage,
          child: const Text('اختيار صورة المحل'),
        ),
        if (_shopImageBytes != null && _shopImageBytes!.isNotEmpty) ...[
          const SizedBox(height: 8),
          Text(_shopImageName.isEmpty ? 'تم اختيار الصورة' : _shopImageName),
          if (SaleScreen.debugShopImageBytes == null) ...[
            const SizedBox(height: 8),
            Image.memory(
              Uint8List.fromList(_shopImageBytes!),
              height: 80,
              fit: BoxFit.cover,
              errorBuilder: (_, _, _) => const Text('تم اختيار الصورة'),
            ),
          ],
        ],
        if (_shopError != null)
          Padding(
            padding: const EdgeInsets.only(top: 8),
            child: Text(_shopError!, style: const TextStyle(color: AppColors.danger)),
          ),
      ],
    );
  }

  Widget _reviewStep() {
    final docs = _previewDocs.isNotEmpty
        ? _previewDocs
        : [
            SalesDocument(type: 'PreviewContract', fileName: 'عقد البيع', downloadUrl: ''),
            SalesDocument(type: 'PreviewPromissoryNote', fileName: 'وصل الأمانة', downloadUrl: ''),
          ];
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        const Text('بيانات الزبون', style: TextStyle(fontWeight: FontWeight.w700)),
        Text(_name.text),
        Text(_phone.text),
        Text(_province.text),
        Text(_address.text),
        Text('قائمة الزبون: ${_customerListName()}'),
        const SizedBox(height: AppSpacing.md),
        const Text('المنتجات', style: TextStyle(fontWeight: FontWeight.w700)),
        ..._stock.where((i) => (_qty[i.productId] ?? 0) > 0).map(
              (i) => Text('${i.productName} × ${_qty[i.productId]}'),
            ),
        const SizedBox(height: AppSpacing.md),
        const Text('الأسعار', style: TextStyle(fontWeight: FontWeight.w700)),
        Text('السعر الإجمالي: ${MoneyFormat.iqd(_previewFinal)}'),
        Text('القسط اليومي: ${MoneyFormat.iqd(_parsedOrDefault(_installment, _defaultDaily))}'),
        Text('المقدمة: ${MoneyFormat.iqd(_parsedOrDefault(_downPayment, _defaultDown))}'),
        const SizedBox(height: AppSpacing.md),
        const Text('بيانات المحل', style: TextStyle(fontWeight: FontWeight.w700)),
        Text(_shopName.text),
        Text(_shopType.text),
        Text('قيمة البضاعة: ${MoneyFormat.iqd(_parsedOrNull(_shopStock) ?? 0)}'),
        Text('الوارد اليومي: ${MoneyFormat.iqd(_parsedOrNull(_shopDaily) ?? 0)}'),
        Text('المساحة: ${_shopArea.toStringAsFixed(_shopArea % 1 == 0 ? 0 : 2)} م²'),
        if (_shopNote.text.trim().isNotEmpty) Text(_shopNote.text.trim()),
        const SizedBox(height: AppSpacing.md),
        const Text('العقد', style: TextStyle(fontWeight: FontWeight.w700)),
        for (final doc in docs.where((d) => d.isContract)) _docTile(doc, 'عقد البيع'),
        const SizedBox(height: AppSpacing.sm),
        const Text('وصل الأمانة', style: TextStyle(fontWeight: FontWeight.w700)),
        for (final doc in docs.where((d) => d.isPromissoryNote)) _docTile(doc, 'وصل الأمانة'),
      ],
    );
  }

  Widget _docTile(SalesDocument doc, String title) {
    return Card(
      child: ListTile(
        title: Text(title),
        subtitle: Text(doc.fileName),
        trailing: Wrap(
          spacing: 8,
          children: [
            TextButton(onPressed: () => _openOrDownload(doc), child: const Text('فتح')),
            TextButton(onPressed: () => _openOrDownload(doc), child: const Text('تنزيل')),
          ],
        ),
      ),
    );
  }
}

class _SaleDownloadBundle {
  const _SaleDownloadBundle({this.contractPath, this.receiptPath, required this.failed});
  final String? contractPath;
  final String? receiptPath;
  final bool failed;
}
