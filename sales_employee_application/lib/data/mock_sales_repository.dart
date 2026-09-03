import 'package:sales_employee_application/data/sales_models.dart';
import 'package:sales_employee_application/data/sales_repository.dart';
import 'package:sales_employee_application/tracking/work_shift.dart';

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
  Future<SalesDraft> createSale(SalesDraftCreateRequest request) async {
    await Future<void>.delayed(const Duration(milliseconds: 250));
    if (request.salesRequestId != null) {
      if (_converted.contains(request.salesRequestId)) {
        throw Exception('الطلب مرتبط بعملية بيع أخرى.');
      }
      _converted.add(request.salesRequestId!);
    }
    final stock = await inventory();
    num base = 0;
    final lines = <SalesDraftItem>[];
    for (final item in request.items) {
      final product = stock.firstWhere((p) => p.productId == item.productId);
      if (item.quantity > product.availableQuantity) {
        throw Exception('الكمية المطلوبة أكبر من المتوفر الحالي');
      }
      final line = product.salePrice * item.quantity;
      base += line;
      lines.add(SalesDraftItem(
        productId: product.productId,
        quantity: item.quantity,
        productName: product.productName,
        unitSalePrice: product.salePrice,
        lineSalePrice: line,
      ));
    }
    final rejected = request.evaluationLevel == 1;
    final finalPrice = rejected
        ? 0
        : (request.evaluationLevel == 2 ? base * 2 : base);
    final draft = SalesDraft(
      saleId: _nextId++,
      fullName: request.customer['fullName'] ?? '',
      phone: request.customer['phone'],
      province: request.customer['province'],
      status: rejected ? 'Rejected' : 'Pending',
      evaluationLevel: request.evaluationLevel,
      evaluationNote: request.evaluationNote,
      baseSalePrice: base,
      finalSalePrice: finalPrice,
      dailyInstallment: request.dailyInstallment,
      createdAt: DateTime.now(),
      items: lines,
    );
    _drafts.insert(0, draft);
    return draft;
  }

  @override
  Future<List<SalesDraft>> pending() async => List.of(_drafts);

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

  @override
  Future<SalesCompleteResult> completeSale(int id) async {
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
      cutoffAtUtc: now.add(const Duration(hours: 18)),
      isNew: true,
    );
    return _shift!;
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
    final viewed = SalesWorkRequest(
      id: current.id,
      customerName: current.customerName,
      customerPhone: current.customerPhone,
      customerProvince: current.customerProvince,
      customerAddress: current.customerAddress,
      existingCustomerId: current.existingCustomerId,
      notes: current.notes,
      status: 'Viewed',
      createdAtUtc: current.createdAtUtc,
      convertedToSaleId: current.convertedToSaleId,
    );
    _replace(viewed);
    return viewed;
  }

  @override
  Future<SalesWorkRequest> startSalesRequest(int id) async {
    final current = await salesRequest(id);
    final row = SalesWorkRequest(
      id: current.id,
      customerName: current.customerName,
      customerPhone: current.customerPhone,
      customerProvince: current.customerProvince,
      notes: current.notes,
      status: 'InProgress',
      createdAtUtc: current.createdAtUtc,
      existingCustomerId: current.existingCustomerId,
      customerAddress: current.customerAddress,
    );
    _replace(row);
    return row;
  }

  @override
  Future<SalesWorkRequest> rejectSalesRequest(int id, String reason) async {
    if (reason.trim().isEmpty) throw Exception('سبب الرفض مطلوب');
    final current = await salesRequest(id);
    final row = SalesWorkRequest(
      id: current.id,
      customerName: current.customerName,
      customerPhone: current.customerPhone,
      customerProvince: current.customerProvince,
      notes: current.notes,
      status: 'Rejected',
      createdAtUtc: current.createdAtUtc,
      rejectionReason: reason.trim(),
      existingCustomerId: current.existingCustomerId,
      customerAddress: current.customerAddress,
    );
    _replace(row);
    return row;
  }

  void seedRequest(SalesWorkRequest row) => _requests.add(row);

  void _replace(SalesWorkRequest row) {
    _requests.removeWhere((r) => r.id == row.id);
    _requests.add(row);
  }
}
