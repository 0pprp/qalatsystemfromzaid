import 'package:sales_employee_application/data/sales_models.dart';
import 'package:sales_employee_application/data/sales_repository.dart';
import 'package:sales_employee_application/tracking/work_shift.dart';
import 'package:sales_employee_application/utils/iraq_time.dart';

class MockSalesRepository implements SalesRepository {
  MockSalesRepository() {
    _stock = {
      5: 3,
      8: 0,
      11: 6,
    };
  }

  final List<SalesDraft> _drafts = [];
  int _nextId = 101;
  late Map<int, int> _stock;
  int completeCalls = 0;
  int deductionCount = 0;

  static const _pdfBytes = <int>[
    37, 80, 68, 70, 45, 49, 46, 49, 10, 37, 226, 227, 207, 211, 10
  ];

  WorkShift? _shift;
  final Set<String> _sequences = {};
  int _shiftId = 1;

  @override
  Future<SalesMe> me() async {
    await Future<void>.delayed(const Duration(milliseconds: 200));
    return SalesMe(
      employeeId: 1,
      employeeName: 'موظف تجريبي',
      branchId: 'najaf-demo',
      branchName: 'النجف',
      role: 'SalesEmployee',
      isSalesShiftStarted: _shift?.isActive ?? false,
    );
  }

  @override
  Future<List<SalesCustomer>> searchCustomers(String query) async {
    await Future<void>.delayed(const Duration(milliseconds: 250));
    final q = query.trim();
    return [
      SalesCustomer(
        customerId: 12,
        fullName: 'أحمد علي محمد',
        phone: '07701234567',
        province: 'النجف',
        salePrice: 2000000,
        address: 'حي الأنصار',
        nearestLandmark: 'قرب جامع الأنصار',
        mukhtarName: 'حسن كاظم',
        rationCenterNumber: '4412',
        nationalCardNumber: 'N1234567',
      ),
      SalesCustomer(
        customerId: 18,
        fullName: 'حسين كاظم جاسم',
        phone: '07801112233',
        province: 'النجف',
        salePrice: 1500000,
      ),
    ].where((c) => c.fullName.contains(q) || (c.phone ?? '').contains(q)).toList();
  }

  @override
  Future<List<SalesInventoryItem>> inventory() async {
    await Future<void>.delayed(const Duration(milliseconds: 200));
    return [
      SalesInventoryItem(
        productId: 5,
        productName: 'ثلاجة سامسونج 18 قدم',
        availableQuantity: _stock[5] ?? 0,
        salePrice: 1500000,
        dailyInstallment: 25000,
        notes: 'S18',
      ),
      SalesInventoryItem(
        productId: 8,
        productName: 'غسالة إل جي 8 كغم',
        availableQuantity: _stock[8] ?? 0,
        salePrice: 850000,
        dailyInstallment: 15000,
        notes: 'LG8',
      ),
      SalesInventoryItem(
        productId: 11,
        productName: 'مكيف 1.5 طن',
        availableQuantity: _stock[11] ?? 0,
        salePrice: 500000,
        dailyInstallment: 10000,
      ),
    ];
  }

  @override
  Future<List<SalesCustomerList>> activeCustomerLists() async {
    await Future<void>.delayed(const Duration(milliseconds: 80));
    return [
      SalesCustomerList(listId: 1, listName: 'قائمة الأنصار'),
      SalesCustomerList(listId: 2, listName: 'قائمة الكوفة'),
    ];
  }

  @override
  Future<SalesDraft> createSale(SalesDraftCreateRequest request) async {
    await Future<void>.delayed(const Duration(milliseconds: 250));
    if (request.customerListId == null || request.customerListId! <= 0) {
      throw Exception('قائمة الزبون مطلوبة.');
    }
    if (request.salesRequestId != null) {
      if (_converted.contains(request.salesRequestId)) {
        throw Exception('الطلب مرتبط بعملية بيع أخرى.');
      }
      _converted.add(request.salesRequestId!);
    }
    final stock = await inventory();
    num base = 0;
    num defaultDaily = 0;
    final lines = <SalesDraftItem>[];
    for (final item in request.items) {
      final product = stock.firstWhere((p) => p.productId == item.productId);
      if (item.quantity > product.availableQuantity) {
        throw Exception('الكمية المطلوبة أكبر من المتوفر الحالي');
      }
      final line = product.salePrice * item.quantity;
      base += line;
      defaultDaily += (product.dailyInstallment ?? 0) * item.quantity;
      lines.add(SalesDraftItem(
        productId: product.productId,
        quantity: item.quantity,
        productName: product.productName,
        unitSalePrice: product.salePrice,
        lineSalePrice: line,
      ));
    }
    final finalPrice = request.overrideTotalSalePrice ?? base;
    final daily = request.overrideDailyInstallment ??
        (request.dailyInstallment > 0 ? request.dailyInstallment : defaultDaily);
    final down = request.overrideDownPayment ?? ((finalPrice * 0.05).round());
    final draft = SalesDraft(
      saleId: _nextId++,
      fullName: request.customer['fullName'] ?? '',
      phone: request.customer['phone'],
      province: request.customer['province'],
      nationalCardNumber: request.customer['nationalCardNumber'],
      address: request.customer['address'],
      nearestLandmark: request.customer['nearestLandmark'],
      mukhtarName: request.customer['mukhtarName'],
      rationCenterNumber: request.customer['rationCenterNumber'],
      employeeName: 'موظف تجريبي',
      status: 'Pending',
      evaluationLevel: request.evaluationLevel,
      evaluationNote: request.evaluationNote,
      baseSalePrice: base,
      finalSalePrice: finalPrice,
      dailyInstallment: daily,
      downPayment: down,
      defaultTotalSalePrice: base,
      defaultDailyInstallment: defaultDaily,
      defaultDownPayment: ((base * 0.05).round()),
      createdAt: DateTime.now(),
      items: lines,
    );
    _drafts.insert(0, draft);
    return draft;
  }

  @override
  Future<List<SalesDraft>> pending() async =>
      _drafts.where((d) => !d.isCompleted).toList();

  @override
  Future<List<SalesDraft>> todayCompleted() async {
    final iraq = DateTime.now().toUtc().add(const Duration(hours: 3));
    final day = DateTime(iraq.year, iraq.month, iraq.day);
    return _drafts.where((d) {
      if (!d.isCompleted) return false;
      final at = d.completedAt ?? d.createdAt;
      final local = at.isUtc ? at.add(const Duration(hours: 3)) : at;
      return local.year == day.year && local.month == day.month && local.day == day.day;
    }).toList();
  }

  @override
  Future<SalesDraft> byId(int id) async =>
      _drafts.firstWhere((d) => d.saleId == id);

  List<SalesDocument> _docsFor(int saleId) => [
        SalesDocument(
          documentId: saleId * 10,
          type: 'Contract',
          fileName: 'Sale_${saleId}_Contract.pdf',
          downloadUrl: 'sales/$saleId/documents/${saleId * 10}/download',
        ),
        SalesDocument(
          documentId: saleId * 10 + 1,
          type: 'PromissoryNote',
          fileName: 'Sale_${saleId}_PromissoryNote.pdf',
          downloadUrl: 'sales/$saleId/documents/${saleId * 10 + 1}/download',
        ),
      ];

  List<SalesDocument> _previewDocsFor(int saleId) => [
        SalesDocument(
          documentId: saleId * 10 + 8,
          type: 'PreviewContract',
          fileName: 'Sale_${saleId}_Preview_Contract.pdf',
          downloadUrl: 'sales/$saleId/documents/${saleId * 10 + 8}/download',
        ),
        SalesDocument(
          documentId: saleId * 10 + 9,
          type: 'PreviewPromissoryNote',
          fileName: 'Sale_${saleId}_Preview_PromissoryNote.pdf',
          downloadUrl: 'sales/$saleId/documents/${saleId * 10 + 9}/download',
        ),
      ];

  @override
  Future<String> uploadShopImage(int saleId, List<int> bytes, String fileName) async {
    if (bytes.isEmpty) throw Exception('صورة المحل مطلوبة');
    return 'sales/$saleId/shop.jpg';
  }

  @override
  Future<SalesCompleteResult> completeSale(int id, [SalesShopComplete? shop]) async {
    completeCalls++;
    await Future<void>.delayed(const Duration(milliseconds: 200));
    final index = _drafts.indexWhere((d) => d.saleId == id);
    if (index < 0) {
      throw Exception('العملية غير موجودة');
    }
    final current = _drafts[index];
    if (current.isRejected) {
      throw Exception('لا يمكن إتمام عملية بيع مرفوضة.');
    }
    if (current.isCompleted) {
      return SalesCompleteResult(
        saleId: current.saleId,
        status: 'Completed',
        documentsStatus: 'DocumentsReady',
        completedAt: current.completedAt,
        finalSalePrice: current.finalSalePrice,
        documents: current.documents,
      );
    }
    for (final item in current.items) {
      final available = _stock[item.productId] ?? 0;
      if (item.quantity > available) {
        throw Exception('الكمية المطلوبة غير متوفرة حالياً.');
      }
    }
    for (final item in current.items) {
      _stock[item.productId] = (_stock[item.productId] ?? 0) - item.quantity;
    }
    deductionCount++;
    final completed = current.copyWith(
      status: 'Completed',
      completedAt: DateTime.now(),
      documentsStatus: 'DocumentsReady',
      documents: _docsFor(current.saleId),
    );
    _drafts[index] = completed;
    return SalesCompleteResult(
      saleId: completed.saleId,
      status: completed.status,
      documentsStatus: completed.documentsStatus,
      completedAt: completed.completedAt,
      finalSalePrice: completed.finalSalePrice,
      documents: completed.documents,
    );
  }

  @override
  Future<SalesPreviewDocuments> previewDocuments(int id, [SalesShopComplete? shop]) async {
    final current = await byId(id);
    if (current.isRejected) {
      throw Exception('لا يمكن إنشاء معاينة لعملية مرفوضة.');
    }
    return SalesPreviewDocuments(
      saleId: current.saleId,
      finalSalePrice: current.finalSalePrice,
      dailyInstallment: current.dailyInstallment,
      downPayment: current.downPayment,
      defaultTotalSalePrice: current.defaultTotalSalePrice ?? current.baseSalePrice,
      defaultDailyInstallment: current.defaultDailyInstallment ?? current.dailyInstallment,
      defaultDownPayment: current.defaultDownPayment ?? current.downPayment,
      documents: _previewDocsFor(current.saleId),
    );
  }

  @override
  Future<List<SalesDocument>> documents(int saleId) async {
    final draft = await byId(saleId);
    if (draft.documents.isNotEmpty) return draft.documents;
    if (draft.isCompleted) return _docsFor(saleId);
    return const [];
  }

  @override
  Future<List<int>> downloadDocument(int saleId, SalesDocument document) async {
    await Future<void>.delayed(const Duration(milliseconds: 80));
    return List<int>.from(_pdfBytes);
  }

  @override
  Future<WorkShift> startShift() async {
    await Future<void>.delayed(const Duration(milliseconds: 150));
    if (_shift != null && _shift!.isActive) {
      return WorkShift(
        shiftId: _shift!.shiftId,
        status: _shift!.status,
        startedAtUtc: _shift!.startedAtUtc,
        cutoffAtUtc: _shift!.cutoffAtUtc,
        isNew: false,
      );
    }
    final now = DateTime.now().toUtc();
    _shift = WorkShift(
      shiftId: _shiftId++,
      status: 'Active',
      startedAtUtc: now,
      cutoffAtUtc: IraqTime.shiftEndUtc(now),
      isNew: true,
    );
    return _shift!;
  }

  @override
  Future<void> endShift() async {
    await Future<void>.delayed(const Duration(milliseconds: 80));
    final current = _shift;
    if (current == null || !current.isActive) return;
    _shift = WorkShift(
      shiftId: current.shiftId,
      status: 'Closed',
      startedAtUtc: current.startedAtUtc,
      cutoffAtUtc: current.cutoffAtUtc,
      isNew: false,
      hasActiveShift: false,
      closeReason: 'ManualEnd',
    );
  }

  @override
  Future<WorkShift?> currentShift() async {
    if (_shift == null || !_shift!.isActive) return null;
    return _shift;
  }

  @override
  Future<LocationBatchResult> uploadLocationBatch(int shiftId, List<LocalLocationPoint> points) async {
    var accepted = 0;
    var duplicates = 0;
    for (final p in points) {
      final key = '$shiftId:${p.deviceSequence}';
      if (_sequences.contains(key)) {
        duplicates++;
      } else {
        _sequences.add(key);
        accepted++;
      }
    }
    return LocationBatchResult(accepted: accepted, duplicates: duplicates);
  }

  @override
  Future<void> recordTrackingEvent(int? shiftId, String eventType) async {}

  final List<SalesWorkRequest> _requests = [];
  final Set<int> _converted = {};

  @override
  Future<List<SalesWorkRequest>> salesRequests() async => List.of(_requests);

  @override
  Future<SalesWorkRequest> salesRequest(int id) async =>
      _requests.firstWhere((r) => r.id == id);

  @override
  Future<SalesWorkRequest> viewSalesRequest(int id) async {
    final current = await salesRequest(id);
    if (current.status != 'New') return current;
    final viewed = current.copyWith(status: 'Viewed');
    _replace(viewed);
    return viewed;
  }

  @override
  Future<SalesWorkRequest> startSalesRequest(int id) => prepareSalesRequest(id);

  @override
  Future<SalesWorkRequest> prepareSalesRequest(int id) async {
    final current = await salesRequest(id);
    if (current.status == 'Completed') {
      throw Exception('لا يمكن تعديل طلب مكتمل.');
    }
    if (current.status == 'Rejected') {
      throw Exception('لا يمكن تجهيز هذا الطلب.');
    }
    final row = current.copyWith(status: 'PreparedForSale');
    _replace(row);
    return row;
  }

  @override
  Future<SalesWorkRequest> pendSalesRequest(int id, String note) async {
    if (note.trim().isEmpty) throw Exception('الملاحظة مطلوبة');
    final current = await salesRequest(id);
    if (current.status == 'Completed') {
      throw Exception('لا يمكن تعديل طلب مكتمل.');
    }
    final row = current.copyWith(status: 'Pending', pendingNote: note.trim());
    _replace(row);
    return row;
  }

  @override
  Future<SalesWorkRequest> rejectSalesRequest(int id, String reason) async {
    if (reason.trim().isEmpty) throw Exception('سبب الرفض مطلوب');
    final current = await salesRequest(id);
    if (current.status == 'Completed') {
      throw Exception('لا يمكن تعديل طلب مكتمل.');
    }
    if (current.status == 'Rejected') return current;
    final row = current.copyWith(status: 'Rejected', rejectionReason: reason.trim());
    _replace(row);
    return row;
  }

  void seedRequest(SalesWorkRequest row) => _requests.add(row);

  void _replace(SalesWorkRequest row) {
    _requests.removeWhere((r) => r.id == row.id);
    _requests.add(row);
  }
}
