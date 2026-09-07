import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:image_picker/image_picker.dart';
import 'package:open_filex/open_filex.dart';
import 'package:sales_employee_application/data/sales_models.dart';
import 'package:sales_employee_application/data/sales_repository_factory.dart';
import 'package:sales_employee_application/screens/sale_complete_success_screen.dart';
import 'package:sales_employee_application/services/api_client.dart';
import 'package:sales_employee_application/services/sale_document_storage.dart';
import 'package:sales_employee_application/services/shop_gps.dart';
import 'package:sales_employee_application/utils/app_theme.dart';
import 'package:sales_employee_application/utils/iraq_phone.dart';
import 'package:sales_employee_application/utils/sales_format.dart';
import 'package:sales_employee_application/widgets/customer_document_slot.dart';
import 'package:sales_employee_application/widgets/inventory_item_info.dart';
import 'package:sales_employee_application/widgets/shop_location_button.dart';
import 'package:sales_employee_application/widgets/tappable_phone.dart';

class SaleScreen extends StatefulWidget {
  const SaleScreen({super.key});

  @visibleForTesting
  static List<int>? debugShopImageBytes;

  @override
  State<SaleScreen> createState() => _SaleScreenState();
}

class _SaleScreenState extends State<SaleScreen> {
  static const _lastStep = 4;
  static const _stepShop = 0;
  static const _stepCustomer = 1;
  static const _stepProducts = 2;
  static const _stepPrices = 3;
  static const _stepReview = 4;
  static const _stepNames = [
    'بيانات المحل',
    'بيانات الزبون',
    'المنتجات',
    'الأسعار',
    'المراجعة',
  ];
  int _step = 0;
  bool _existing = false;
  bool _resumeAttempted = false;
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
  SalesDraft? _created;
  List<SalesDocument> _previewDocs = const [];
  List<int>? _shopImageBytes;
  String _shopImageName = 'shop.jpg';
  String? _shopImageKey;
  String? _shopError;
  double? _shopLat;
  double? _shopLng;
  bool _locating = false;
  final List<_KycSlot> _kycSlots = [
    _KycSlot(SalesCustomerKycDocument.nationalIdFront, SalesCustomerKycDocument.labelFor(SalesCustomerKycDocument.nationalIdFront)),
    _KycSlot(SalesCustomerKycDocument.nationalIdBack, SalesCustomerKycDocument.labelFor(SalesCustomerKycDocument.nationalIdBack)),
    _KycSlot(SalesCustomerKycDocument.residenceCardFront, SalesCustomerKycDocument.labelFor(SalesCustomerKycDocument.residenceCardFront)),
    _KycSlot(SalesCustomerKycDocument.residenceCardBack, SalesCustomerKycDocument.labelFor(SalesCustomerKycDocument.residenceCardBack)),
    _KycSlot(SalesCustomerKycDocument.residenceCertificate, SalesCustomerKycDocument.labelFor(SalesCustomerKycDocument.residenceCertificate)),
  ];

  @override
  void initState() {
    super.initState();
    _loadCustomerLists();
    _shopLength.addListener(() => setState(() {}));
    _shopWidth.addListener(() => setState(() {}));
    for (final c in [_name, _phone, _card, _address, _landmark, _mukhtar, _ration, _shopName, _shopType, _shopStock, _shopDaily, _shopNote]) {
      c.addListener(_onVisitDataChanged);
    }
  }

  void _onVisitDataChanged() {
    if (mounted) setState(() {});
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
      _fillIfPresent(_name, arg.customerName);
      _fillIfPresent(_phone, IraqPhone.normalize(arg.customerPhone));
      _fillIfPresent(_province, arg.customerProvince);
      _fillIfPresent(_address, arg.customerAddress);
      if (_province.text.trim().isEmpty) {
        _province.text = 'النجف';
      }
      _resumeDraftIfAny();
    }
  }

  Future<void> _resumeDraftIfAny() async {
    if (_resumeAttempted) return;
    _resumeAttempted = true;
    final id = _fromRequest?.convertedToSaleId;
    if (id == null || id <= 0) return;
    try {
      await _loadStock();
      final draft = await SalesRepositoryFactory.instance.byId(id);
      if (!mounted || draft.isCompleted) return;
      _applyDraft(draft);
    } catch (e, st) {
      debugPrint('resume sale draft failed: $e\n$st');
    }
  }

  void _applyDraft(SalesDraft draft) {
    _created = draft;
    _previewDocs = const [];
    if (draft.fullName.trim().isNotEmpty) _name.text = draft.fullName.trim();
    if ((draft.phone ?? '').trim().isNotEmpty) {
      _phone.text = IraqPhone.normalize(draft.phone);
    }
    if ((draft.province ?? '').trim().isNotEmpty) _province.text = draft.province!.trim();
    if ((draft.address ?? '').trim().isNotEmpty) _address.text = draft.address!.trim();
    if (draft.nationalCardNumber != null && draft.nationalCardNumber!.trim().isNotEmpty) {
      _card.text = draft.nationalCardNumber!.trim();
    }
    if (draft.nearestLandmark != null && draft.nearestLandmark!.trim().isNotEmpty) {
      _landmark.text = draft.nearestLandmark!.trim();
    }
    if (draft.mukhtarName != null && draft.mukhtarName!.trim().isNotEmpty) {
      _mukhtar.text = draft.mukhtarName!.trim();
    }
    if (draft.rationCenterNumber != null && draft.rationCenterNumber!.trim().isNotEmpty) {
      _ration.text = draft.rationCenterNumber!.trim();
    }
    if (draft.customerListId != null && draft.customerListId! > 0) {
      _customerListId = draft.customerListId;
      _preferList(draft.customerListId);
      _applyPreferredList();
    }
    for (final item in draft.items) {
      if (item.productId > 0 && item.quantity > 0) {
        _qty[item.productId] = item.quantity;
      }
    }
    _defaultTotal = draft.defaultTotalSalePrice ?? draft.baseSalePrice;
    _defaultDaily = draft.defaultDailyInstallment ?? draft.dailyInstallment;
    _defaultDown = draft.defaultDownPayment ?? ((draft.finalSalePrice * 0.05).round());
    _fillOverride(_totalPrice, draft.overrideTotalSalePrice);
    _fillOverride(_installment, draft.overrideDailyInstallment);
    _fillOverride(_downPayment, draft.overrideDownPayment);
    final shop = draft.shop;
    if (shop != null) {
      if (shop.shopName.trim().isNotEmpty) _shopName.text = shop.shopName.trim();
      if (shop.shopBusinessType.trim().isNotEmpty) _shopType.text = shop.shopBusinessType.trim();
      if (shop.shopStockEstimatedValue > 0) {
        _shopStock.text = MoneyFormat.grouped(shop.shopStockEstimatedValue);
      }
      if (shop.estimatedDailyRevenue > 0) {
        _shopDaily.text = MoneyFormat.grouped(shop.estimatedDailyRevenue);
      }
      if (shop.shopLength > 0) _shopLength.text = _numText(shop.shopLength);
      if (shop.shopWidth > 0) _shopWidth.text = _numText(shop.shopWidth);
      if (shop.shopImageKey != null && shop.shopImageKey!.trim().isNotEmpty) {
        _shopImageKey = shop.shopImageKey!.trim();
      }
      if (ShopGps.isValid(shop.latitude, shop.longitude)) {
        _shopLat = shop.latitude;
        _shopLng = shop.longitude;
      }
    }
    setState(() => _step = _resumeStep(draft));
    final id = draft.saleId;
    if (id > 0) {
      _hydrateCustomerDocs(id);
    }
  }

  int _resumeStep(SalesDraft draft) {
    final saved = draft.wizardCurrentStep;
    if (saved != null && saved >= 0 && saved <= _lastStep) return saved;
    return _inferStep(draft);
  }

  int _inferStep(SalesDraft draft) {
    final shop = draft.shop;
    final shopMissing = shop == null ||
        shop.shopName.trim().isEmpty ||
        shop.shopBusinessType.trim().isEmpty ||
        !ShopGps.isValid(shop.latitude, shop.longitude);
    if (shopMissing) return _stepShop;
    if (draft.fullName.trim().isEmpty ||
        (draft.phone ?? '').trim().isEmpty ||
        (draft.nationalCardNumber ?? '').trim().isEmpty ||
        (draft.address ?? '').trim().isEmpty) {
      return _stepCustomer;
    }
    if (draft.items.every((item) => item.quantity <= 0)) return _stepProducts;
    if (draft.finalSalePrice <= 0) return _stepPrices;
    return _stepReview;
  }

  void _fillOverride(TextEditingController c, num? value) {
    if (value == null) {
      c.clear();
      return;
    }
    c.text = MoneyFormat.grouped(value);
  }

  String _numText(num value) => value % 1 == 0 ? value.round().toString() : value.toString();

  void _fillIfPresent(TextEditingController c, String? value) {
    final text = value?.trim() ?? '';
    if (text.isEmpty) return;
    c.text = text;
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
    if (_step == _stepShop) {
      if (!_validateShop()) return;
      final ok = await _persistProgress();
      if (!ok) return;
    }
    if (_step == _stepCustomer) {
      if (!_validateCustomer()) return;
      await _loadStock();
      final ok = await _persistProgress();
      if (!ok) return;
    }
    if (_step == _stepProducts && !_qty.values.any((q) => q > 0)) {
      _toast('أضف مادة واحدة على الأقل');
      return;
    }
    if (_step == _stepProducts) {
      _applyPriceDefaults();
      final ok = await _persistProgress();
      if (!ok) return;
    }
    if (_step == _stepPrices) {
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
      final ok = await _createDraftAndPreview();
      if (!ok) return;
    }
    setState(() => _step++);
  }

  bool _validateCustomer() {
    if (!(_form.currentState?.validate() ?? false)) return false;
    if (_customerLists.isEmpty) {
      _toast('لا توجد قوائم معرفة في هذا الفرع');
      return false;
    }
    if (_customerListId == null || _customerListId! <= 0) {
      _toast('اختيار القائمة/المندوب مطلوب');
      return false;
    }
    if (!_filled(_name) ||
        !_filled(_phone) ||
        !_filled(_province) ||
        !_filled(_card) ||
        !_filled(_address) ||
        !_filled(_landmark) ||
        !_filled(_mukhtar)) {
      _toast('بيانات الزبون غير مكتملة');
      return false;
    }
    return true;
  }

  bool get _hasVisitData {
    if (_filled(_shopName) ||
        _filled(_shopType) ||
        _filled(_shopStock) ||
        _filled(_shopDaily) ||
        _filled(_shopLength) ||
        _filled(_shopWidth) ||
        _filled(_shopNote) ||
        (_shopImageBytes != null && _shopImageBytes!.isNotEmpty) ||
        (_shopImageKey != null && _shopImageKey!.isNotEmpty) ||
        ShopGps.isValid(_shopLat, _shopLng)) {
      return true;
    }
    if (_filled(_name) ||
        _filled(_phone) ||
        _filled(_card) ||
        _filled(_address) ||
        _filled(_landmark) ||
        _filled(_mukhtar) ||
        _filled(_ration)) {
      return true;
    }
    return _qty.values.any((q) => q > 0);
  }

  Map<String, String> _customerPayload() => {
        'fullName': _name.text.trim(),
        'phone': IraqPhone.normalize(_phone.text),
        'province': _province.text.trim(),
        'nationalCardNumber': _card.text.trim(),
        'address': _address.text.trim(),
        'nearestLandmark': _landmark.text.trim(),
        'mukhtarName': _mukhtar.text.trim(),
        if (_ration.text.trim().isNotEmpty) 'rationCenterNumber': _ration.text.trim(),
      };

  List<SalesDraftItem> _selectedItems() => _qty.entries
      .where((e) => e.value > 0)
      .map((e) => SalesDraftItem(productId: e.key, quantity: e.value))
      .toList();

  SalesShopComplete? _shopProgressPayload() {
    if (!_hasVisitData) return null;
    return SalesShopComplete(
      shopName: _shopName.text.trim(),
      shopBusinessType: _shopType.text.trim(),
      shopStockEstimatedValue: _parsedOrNull(_shopStock) ?? 0,
      estimatedDailyRevenue: _parsedOrNull(_shopDaily) ?? 0,
      shopLength: _parsedOrNull(_shopLength) ?? 0,
      shopWidth: _parsedOrNull(_shopWidth) ?? 0,
      shopImageKey: _shopImageKey ?? '',
      employeeNote: _shopNote.text.trim(),
      overrideTotalSalePrice: _parsedOrNull(_totalPrice),
      overrideDailyInstallment: _parsedOrNull(_installment),
      overrideDownPayment: _parsedOrNull(_downPayment),
      latitude: _shopLat,
      longitude: _shopLng,
    );
  }

  SalesDraftCreateRequest _progressRequest({bool markInspected = false}) {
    return SalesDraftCreateRequest(
      customerId: (_existing && _picked != null && !_picked!.isForeignBranch) ? _picked!.customerId : null,
      customer: _customerPayload(),
      items: _selectedItems(),
      overrideTotalSalePrice: _parsedOrNull(_totalPrice),
      overrideDailyInstallment: _parsedOrNull(_installment),
      overrideDownPayment: _parsedOrNull(_downPayment),
      dailyInstallment: _parsedOrNull(_installment) ?? 0,
      salesRequestId: _fromRequest?.id,
      customerListId: _customerListId,
      wizardCurrentStep: _step,
      markInspected: markInspected,
      shop: _shopProgressPayload(),
    );
  }

  Future<bool> _persistProgress({bool silent = false, bool markInspected = false}) async {
    if (_fromRequest == null) return false;
    if (silent && !_hasVisitData) return true;
    try {
      var saved = await SalesRepositoryFactory.instance.saveSaleProgress(_progressRequest());
      _created = saved;
      _fromRequest = _fromRequest!.copyWith(convertedToSaleId: saved.saleId);
      if ((_shopImageKey == null || _shopImageKey!.isEmpty) &&
          _shopImageBytes != null &&
          _shopImageBytes!.isNotEmpty) {
        _shopImageKey = await SalesRepositoryFactory.instance.uploadShopImage(
          saved.saleId,
          _shopImageBytes!,
          _shopImageName,
        );
        saved = await SalesRepositoryFactory.instance.saveSaleProgress(_progressRequest());
        _created = saved;
      }
      if (markInspected) {
        final row = await SalesRepositoryFactory.instance.inspectSalesRequest(
          _fromRequest!.id,
          _progressRequest(markInspected: true),
        );
        _fromRequest = row;
      }
      return true;
    } on ApiException catch (e) {
      if (!silent) _toast(e.message);
      return false;
    } catch (e) {
      if (!silent) _toast(e.toString().replaceFirst('Exception: ', ''));
      return false;
    }
  }

  Future<void> _markInspected() async {
    if (_fromRequest == null || _saving || !_hasVisitData) return;
    setState(() => _saving = true);
    try {
      final ok = await _persistProgress(markInspected: true);
      if (!ok || !mounted) return;
      Navigator.pushNamedAndRemoveUntil(
        context,
        '/home',
        (_) => false,
        arguments: 'inspected',
      );
    } finally {
      if (mounted) setState(() => _saving = false);
    }
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
    if (!ShopGps.isValid(_shopLat, _shopLng)) {
      final debug = ShopGps.debugFix;
      if (debug != null && ShopGps.isValid(debug.latitude, debug.longitude)) {
        _shopLat = debug.latitude;
        _shopLng = debug.longitude;
      }
    }
    if (!ShopGps.isValid(_shopLat, _shopLng)) {
      setState(() => _shopError = 'يجب تحديد موقع المحل.');
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
      latitude: _shopLat,
      longitude: _shopLng,
    );
  }

  Future<bool> _createDraftAndPreview() async {
    setState(() => _saving = true);
    try {
      final created = await SalesRepositoryFactory.instance.createSale(
        SalesDraftCreateRequest(
          customerId: (_existing && _picked != null && !_picked!.isForeignBranch)
              ? _picked!.customerId
              : null,
          customer: {
            'fullName': _name.text.trim(),
            'phone': IraqPhone.normalize(_phone.text),
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
          wizardCurrentStep: _stepReview,
          shop: _shopPayload(),
        ),
      );
      _created = created;
      await _flushCustomerDocs(created.saleId);
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
      debugPrint('sale preview failed status=${e.statusCode} message=${e.message} body=${e.body}');
      _toast(e.message);
      return false;
    } catch (e, st) {
      debugPrint('sale preview failed: $e\n$st');
      _toast(e.toString());
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
    final toSave = SalesDocument.preferDisplay(docs);
    for (final doc in toSave) {
      try {
        final bytes = await SalesRepositoryFactory.instance.downloadDocument(_created?.saleId ?? 0, doc);
        final file = await SaleDocumentStorage.savePdf(doc.fileName, bytes);
        if (doc.isCombined) {
          contract = file.path;
          receipt = file.path;
        } else if (doc.isContract) {
          contract = file.path;
        } else if (doc.isPromissoryNote) {
          receipt = file.path;
        }
      } catch (_) {
        failed = true;
      }
    }
    if (toSave.isEmpty) failed = true;
    return _SaleDownloadBundle(
      contractPath: contract,
      receiptPath: receipt,
      failed: failed || contract == null || receipt == null,
    );
  }

  Future<void> _openOrDownload(SalesDocument doc) async {
    try {
      final bytes = await SalesRepositoryFactory.instance.downloadDocument(_created?.saleId ?? 0, doc);
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
        source: ImageSource.camera,
        imageQuality: 85,
        maxWidth: 1600,
        preferredCameraDevice: CameraDevice.rear,
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
      setState(() => _shopError = 'تعذر التقاط صورة المحل');
    }
  }

  Future<void> _hydrateCustomerDocs(int saleId) async {
    try {
      final rows = await SalesRepositoryFactory.instance.listCustomerDocuments(saleId);
      _kycSlots.removeWhere((s) => s.type == SalesCustomerKycDocument.residenceCardLegacy);
      for (final slot in _kycSlots) {
        final matches = rows.where((r) => r.documentType.toLowerCase() == slot.type.toLowerCase()).toList();
        if (matches.isEmpty) continue;
        final match = matches.last;
        slot.documentId = match.id;
        slot.pendingUpload = false;
        try {
          slot.bytes = await SalesRepositoryFactory.instance.customerDocumentBytes(match.id);
        } catch (_) {}
      }
      final legacy = rows
          .where((r) => r.documentType.toLowerCase() == SalesCustomerKycDocument.residenceCardLegacy.toLowerCase())
          .toList();
      if (legacy.isNotEmpty) {
        final match = legacy.last;
        final slot = _KycSlot(
          SalesCustomerKycDocument.residenceCardLegacy,
          match.typeLabel.isNotEmpty
              ? match.typeLabel
              : SalesCustomerKycDocument.labelFor(SalesCustomerKycDocument.residenceCardLegacy),
        );
        slot.documentId = match.id;
        slot.pendingUpload = false;
        try {
          slot.bytes = await SalesRepositoryFactory.instance.customerDocumentBytes(match.id);
        } catch (_) {}
        final certIndex = _kycSlots.indexWhere((s) => s.type == SalesCustomerKycDocument.residenceCertificate);
        if (certIndex >= 0) {
          _kycSlots.insert(certIndex, slot);
        } else {
          _kycSlots.add(slot);
        }
      }
      if (mounted) setState(() {});
    } catch (_) {}
  }

  Future<void> _flushCustomerDocs(int saleId) async {
    for (final slot in _kycSlots) {
      if (!slot.pendingUpload || slot.bytes == null || slot.bytes!.isEmpty) continue;
      slot.busy = true;
      if (mounted) setState(() {});
      try {
        final saved = await SalesRepositoryFactory.instance.uploadCustomerDocument(
          saleId,
          slot.type,
          slot.bytes!,
          slot.fileName,
        );
        slot.documentId = saved.id;
        slot.pendingUpload = false;
      } catch (_) {
        // Optional documents must not block the sale.
      } finally {
        slot.busy = false;
      }
    }
    if (mounted) setState(() {});
  }

  Future<void> _pickKyc(_KycSlot slot, ImageSource source) async {
    try {
      final picked = await ImagePicker().pickImage(
        source: source,
        imageQuality: 85,
        maxWidth: 1600,
        preferredCameraDevice: CameraDevice.rear,
      );
      if (picked == null) return;
      final bytes = await picked.readAsBytes();
      if (!mounted) return;
      setState(() {
        slot.bytes = bytes;
        slot.fileName = picked.name.isEmpty ? 'doc.jpg' : picked.name;
        slot.pendingUpload = true;
      });
      final saleId = _created?.saleId;
      if (saleId != null && saleId > 0) {
        await _flushCustomerDocs(saleId);
      }
    } catch (_) {
      if (!mounted) return;
      _toast('تعذر اختيار صورة المستند');
    }
  }

  Future<void> _deleteKyc(_KycSlot slot) async {
    final id = slot.documentId;
    if (id != null && id > 0) {
      try {
        await SalesRepositoryFactory.instance.deleteCustomerDocument(id);
      } catch (_) {}
    }
    if (!mounted) return;
    setState(() {
      slot.bytes = null;
      slot.documentId = null;
      slot.pendingUpload = false;
      slot.fileName = 'doc.jpg';
    });
  }

  Future<void> _captureShopLocation() async {
    if (_locating) return;
    setState(() {
      _locating = true;
      _shopError = null;
    });
    try {
      final fix = await ShopGps.capture();
      if (!mounted) return;
      setState(() {
        _shopLat = fix.latitude;
        _shopLng = fix.longitude;
        _locating = false;
        _shopError = null;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _locating = false;
        _shopError = e.toString().replaceFirst('Exception: ', '');
      });
    }
  }

  Future<void> _goBack() async {
    if (_step <= 0 || _saving) return;
    await _persistProgress(silent: true);
    if (!mounted) return;
    setState(() {
      _step--;
      if (_step < _stepReview) {
        _previewDocs = const [];
      }
    });
  }

  void _toast(String m) {
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(m)));
  }

  @override
  Widget build(BuildContext context) {
    return PopScope(
      canPop: !_saving,
      onPopInvokedWithResult: (didPop, result) {
        if (didPop) {
          _persistProgress(silent: true);
        }
      },
      child: Scaffold(
      resizeToAvoidBottomInset: true,
      appBar: AppBar(title: const Text('إتمام البيع')),
      body: SafeArea(
        child: Column(
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(AppSpacing.md, AppSpacing.md, AppSpacing.md, AppSpacing.sm),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Text(
                    'الخطوة ${_step + 1} من ${_lastStep + 1}',
                    style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 16),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    _stepNames[_step],
                    style: const TextStyle(color: AppColors.muted, fontWeight: FontWeight.w600),
                  ),
                ],
              ),
            ),
            Expanded(child: _stepBody()),
            Padding(
              padding: const EdgeInsets.fromLTRB(
                AppSpacing.md,
                AppSpacing.md,
                AppSpacing.md,
                AppSpacing.md,
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Row(
                    children: [
                      if (_step > 0) ...[
                        Expanded(
                          child: OutlinedButton(
                            onPressed: _saving ? null : _goBack,
                            child: const Text('رجوع'),
                          ),
                        ),
                        const SizedBox(width: AppSpacing.sm),
                      ],
                      Expanded(
                        flex: _step > 0 ? 2 : 1,
                        child: ElevatedButton(
                          onPressed: _saving ? null : (_step < _lastStep ? _next : _completeSale),
                          child: _saving
                              ? const SizedBox(
                                  height: 22,
                                  width: 22,
                                  child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white),
                                )
                              : Text(_step < _lastStep ? 'التالي' : 'تم البيع'),
                        ),
                      ),
                    ],
                  ),
                  if (_hasVisitData) ...[
                    const SizedBox(height: AppSpacing.sm),
                    OutlinedButton(
                      onPressed: _saving ? null : _markInspected,
                      child: const Text('تم الكشف'),
                    ),
                  ],
                ],
              ),
            ),
          ],
        ),
      ),
    ),
    );
  }

  Widget _stepBody() {
    final padding = const EdgeInsets.fromLTRB(AppSpacing.md, AppSpacing.md, AppSpacing.md, AppSpacing.lg);
    switch (_step) {
      case _stepShop:
        return SingleChildScrollView(
          keyboardDismissBehavior: ScrollViewKeyboardDismissBehavior.onDrag,
          padding: padding,
          child: _shopStep(),
        );
      case _stepCustomer:
        return SingleChildScrollView(
          keyboardDismissBehavior: ScrollViewKeyboardDismissBehavior.onDrag,
          padding: padding,
          child: _customerStep(),
        );
      case _stepProducts:
        return _itemsStep();
      case _stepPrices:
        return SingleChildScrollView(
          keyboardDismissBehavior: ScrollViewKeyboardDismissBehavior.onDrag,
          padding: padding,
          child: _priceStep(),
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
          const Text('القائمة/المندوب *', style: TextStyle(fontWeight: FontWeight.w600)),
          const SizedBox(height: 8),
          Padding(
            padding: const EdgeInsets.only(bottom: AppSpacing.md),
            child: InputDecorator(
              decoration: const InputDecoration(
                contentPadding: EdgeInsets.fromLTRB(12, 4, 12, 4),
              ),
              child: DropdownButtonHideUnderline(
                child: DropdownButton<int>(
                  isExpanded: true,
                  value: _customerLists.any((e) => e.listId == _customerListId) ? _customerListId : null,
                  hint: const Text('اختر القائمة/المندوب'),
                  items: [
                    for (final list in _customerLists)
                      DropdownMenuItem(value: list.listId, child: Text(list.listName)),
                  ],
                  onChanged: (value) => setState(() => _customerListId = value),
                ),
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
          _field(_name, 'الاسم الكامل *', validator: _req),
          _field(
            _phone,
            'رقم الهاتف *',
            keyboard: TextInputType.phone,
            validator: IraqPhone.validator,
            formatters: [
              FilteringTextInputFormatter.digitsOnly,
              LengthLimitingTextInputFormatter(11),
            ],
          ),
          _field(_province, 'المحافظة *', validator: _req),
          _field(_card, 'رقم البطاقة الوطنية *', keyboard: TextInputType.number, validator: _req),
          _field(_address, 'العنوان *', validator: _req),
          _field(_landmark, 'أقرب نقطة دالة *', validator: _req),
          _field(_mukhtar, 'اسم المختار *', validator: _req),
          if (!_fromKnownSource) _field(_ration, 'رقم مركز التموين (اختياري)', keyboard: TextInputType.number, last: true),
          const SizedBox(height: AppSpacing.md),
          const Text('مستندات الزبون', style: TextStyle(fontWeight: FontWeight.w700)),
          const SizedBox(height: 4),
          const Text(
            'اختيارية. يمكن التصوير أو الاختيار من المعرض.',
            style: TextStyle(color: AppColors.muted, fontSize: 12),
          ),
          const SizedBox(height: AppSpacing.sm),
          for (final slot in _kycSlots)
            CustomerDocumentSlot(
              label: slot.label,
              busy: slot.busy,
              bytes: slot.bytes,
              onCamera: () => _pickKyc(slot, ImageSource.camera),
              onGallery: () => _pickKyc(slot, ImageSource.gallery),
              onDelete: () => _deleteKyc(slot),
            ),
        ],
      ),
    );
  }

  Widget _field(TextEditingController c, String label,
      {TextInputType? keyboard, String? Function(String?)? validator, bool last = false,
      List<TextInputFormatter>? formatters}) {
    return Padding(
      padding: const EdgeInsets.only(bottom: AppSpacing.md),
      child: TextFormField(
        controller: c,
        keyboardType: keyboard,
        inputFormatters: formatters,
        validator: validator,
        textInputAction: last ? TextInputAction.done : TextInputAction.next,
        enableSuggestions: false,
        autocorrect: false,
        smartDashesType: SmartDashesType.disabled,
        smartQuotesType: SmartQuotesType.disabled,
        decoration: InputDecoration(
          labelText: label,
          alignLabelWithHint: true,
          floatingLabelBehavior: FloatingLabelBehavior.auto,
          contentPadding: const EdgeInsets.fromLTRB(16, 20, 16, 16),
        ),
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
                margin: const EdgeInsets.only(bottom: AppSpacing.md),
                child: Padding(
                  padding: const EdgeInsets.all(AppSpacing.md),
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
        const SizedBox(height: AppSpacing.sm),
        Text('القسط اليومي الافتراضي: ${MoneyFormat.iqd(_defaultDaily)}'),
        const SizedBox(height: AppSpacing.sm),
        Text('المقدمة الافتراضية (5%): ${MoneyFormat.iqd(_defaultDown)}'),
        const SizedBox(height: AppSpacing.lg),
        const Text(
          'حقول التعديل اختيارية. إذا بقي الحقل فارغاً يُعتمد الافتراضي.',
          style: TextStyle(color: AppColors.muted),
        ),
        const SizedBox(height: AppSpacing.md),
        _priceField(_totalPrice, 'السعر الإجمالي (اختياري)'),
        _priceField(_installment, 'القسط اليومي (اختياري)'),
        _priceField(_downPayment, 'المقدمة (اختياري)', last: true),
      ],
    );
  }

  Widget _priceField(TextEditingController c, String label, {bool last = false}) {
    return Padding(
      padding: const EdgeInsets.only(bottom: AppSpacing.md),
      child: TextField(
        controller: c,
        keyboardType: const TextInputType.numberWithOptions(decimal: false),
        inputFormatters: MoneyFormat.inputFormatters,
        textInputAction: last ? TextInputAction.done : TextInputAction.next,
        enableSuggestions: false,
        autocorrect: false,
        decoration: InputDecoration(
          labelText: label,
          suffixText: 'د.ع',
          alignLabelWithHint: true,
          floatingLabelBehavior: FloatingLabelBehavior.auto,
          contentPadding: const EdgeInsets.fromLTRB(16, 20, 16, 16),
        ),
      ),
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
        _field(_shopStock, 'تقدير قيمة بضاعة المحل *',
            keyboard: TextInputType.number, validator: _req, formatters: MoneyFormat.inputFormatters),
        _field(_shopDaily, 'تقدير الوارد اليومي *',
            keyboard: TextInputType.number, validator: _req, formatters: MoneyFormat.inputFormatters),
        _field(_shopLength, 'طول المحل بالمتر *', keyboard: TextInputType.number, validator: _req),
        _field(_shopWidth, 'عرض المحل بالمتر *', keyboard: TextInputType.number, validator: _req),
        Align(
          alignment: Alignment.centerRight,
          child: Padding(
            padding: const EdgeInsets.only(bottom: AppSpacing.md),
            child: Text(
              'المساحة: ${_shopArea.toStringAsFixed(_shopArea % 1 == 0 ? 0 : 2)} م²',
            ),
          ),
        ),
        _field(_shopNote, 'ملاحظة الموظف (اختياري)', last: true),
        ShopLocationButton(
          loading: _locating,
          captured: ShopGps.isValid(_shopLat, _shopLng),
          onPressed: _captureShopLocation,
        ),
        const SizedBox(height: AppSpacing.sm),
        OutlinedButton(
          onPressed: _pickShopImage,
          child: const Padding(
            padding: EdgeInsets.symmetric(vertical: 8),
            child: Text('التقاط صورة المحل'),
          ),
        ),
        if (_shopImageBytes != null && _shopImageBytes!.isNotEmpty) ...[
          const SizedBox(height: AppSpacing.md),
          Text(_shopImageName.isEmpty ? 'تم اختيار الصورة' : _shopImageName),
          if (SaleScreen.debugShopImageBytes == null) ...[
            const SizedBox(height: AppSpacing.sm),
            Image.memory(
              Uint8List.fromList(_shopImageBytes!),
              height: 120,
              fit: BoxFit.cover,
              errorBuilder: (_, _, _) => const Text('تم اختيار الصورة'),
            ),
          ],
        ] else if (_shopImageKey != null && _shopImageKey!.isNotEmpty) ...[
          const SizedBox(height: AppSpacing.md),
          const Text('تم حفظ صورة المحل سابقاً. يمكنك استبدالها.'),
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
        ? SalesDocument.preferDisplay(_previewDocs)
        : [
            SalesDocument(type: 'PreviewSaleDocuments', fileName: 'عقد البيع + وصل الأمانة', downloadUrl: ''),
          ];
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        const Text('بيانات الزبون', style: TextStyle(fontWeight: FontWeight.w700)),
        const SizedBox(height: AppSpacing.sm),
        Text(_name.text),
        const SizedBox(height: 6),
        TappablePhone(_phone.text),
        const SizedBox(height: 6),
        Text(_province.text),
        const SizedBox(height: 6),
        Text(_address.text),
        const SizedBox(height: 6),
        Text('القائمة/المندوب: ${_customerListName()}'),
        const SizedBox(height: AppSpacing.lg),
        const Text('المنتجات', style: TextStyle(fontWeight: FontWeight.w700)),
        ..._stock.where((i) => (_qty[i.productId] ?? 0) > 0).map(
              (i) => Text('${i.productName} × ${_qty[i.productId]}'),
            ),
        const SizedBox(height: AppSpacing.md),
        const Text('الأسعار', style: TextStyle(fontWeight: FontWeight.w700)),
        const SizedBox(height: AppSpacing.sm),
        Text('السعر الإجمالي: ${MoneyFormat.iqd(_previewFinal)}'),
        const SizedBox(height: 6),
        Text('القسط اليومي: ${MoneyFormat.iqd(_parsedOrDefault(_installment, _defaultDaily))}'),
        const SizedBox(height: 6),
        Text('المقدمة: ${MoneyFormat.iqd(_parsedOrDefault(_downPayment, _defaultDown))}'),
        const SizedBox(height: AppSpacing.lg),
        const Text('بيانات المحل', style: TextStyle(fontWeight: FontWeight.w700)),
        Text(_shopName.text),
        Text(_shopType.text),
        Text('قيمة البضاعة: ${MoneyFormat.iqd(_parsedOrNull(_shopStock) ?? 0)}'),
        Text('الوارد اليومي: ${MoneyFormat.iqd(_parsedOrNull(_shopDaily) ?? 0)}'),
        Text('المساحة: ${_shopArea.toStringAsFixed(_shopArea % 1 == 0 ? 0 : 2)} م²'),
        if (ShopGps.isValid(_shopLat, _shopLng))
          Text('الموقع: ${_shopLat!.toStringAsFixed(6)}, ${_shopLng!.toStringAsFixed(6)}'),
        if (_shopNote.text.trim().isNotEmpty) Text(_shopNote.text.trim()),
        const SizedBox(height: AppSpacing.md),
        const Text('المستندات', style: TextStyle(fontWeight: FontWeight.w700)),
        for (final doc in docs) _docTile(doc, doc.displayTitle),
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

class _KycSlot {
  _KycSlot(this.type, this.label);
  final String type;
  final String label;
  List<int>? bytes;
  String fileName = 'doc.jpg';
  int? documentId;
  bool pendingUpload = false;
  bool busy = false;
}

class _SaleDownloadBundle {
  const _SaleDownloadBundle({this.contractPath, this.receiptPath, required this.failed});
  final String? contractPath;
  final String? receiptPath;
  final bool failed;
}
