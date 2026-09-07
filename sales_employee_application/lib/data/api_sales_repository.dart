import 'package:flutter/foundation.dart';
import 'package:sales_employee_application/config/app_env.dart';
import 'package:sales_employee_application/data/sales_models.dart';
import 'package:sales_employee_application/data/sales_repository.dart';
import 'package:sales_employee_application/services/api_client.dart';
import 'package:sales_employee_application/services/global_customer_search.dart';
import 'package:sales_employee_application/tracking/work_shift.dart';
import 'package:sales_employee_application/utils/iraq_time.dart';
import 'package:sales_employee_application/utils/sales_format.dart';

class ApiSalesRepository implements SalesRepository {
  List<Map<String, dynamic>> _maps(dynamic raw) {
    if (raw is List) {
      return raw.whereType<Map>().map((e) => Map<String, dynamic>.from(e)).toList();
    }
    return const [];
  }

  Never _throw(ApiException e) {
    debugPrint('sales api error status=${e.statusCode} message=${e.message} body=${e.body}');
    throw ApiException(salesApiMessage(e.statusCode, e.message), statusCode: e.statusCode, body: e.body);
  }

  static bool _onIraqDay(DateTime value, DateTime iraqDate) {
    final iraq = value.isUtc ? value.add(const Duration(hours: 3)) : value;
    return iraq.year == iraqDate.year && iraq.month == iraqDate.month && iraq.day == iraqDate.day;
  }

  @override
  Future<SalesMe> me() async {
    try {
      final raw = await ApiClient.get('sales/me');
      return SalesMe.fromJson(Map<String, dynamic>.from(raw as Map));
    } on ApiException catch (e) {
      _throw(e);
    }
  }

  @override
  Future<List<SalesCustomer>> searchCustomers(String query) async {
    try {
      if (AppEnv.isProduction) {
        return await GlobalCustomerSearch.search(query);
      }
      final raw = await ApiClient.get('sales/customers/search', query: {'q': query});
      return _maps(raw).map(SalesCustomer.fromJson).toList();
    } on ApiException catch (e) {
      _throw(e);
    }
  }

  @override
  Future<List<SalesInventoryItem>> inventory() async {
    try {
      final raw = await ApiClient.get('sales/inventory');
      return _maps(raw)
          .map(SalesInventoryItem.fromJson)
          .where((i) => !SalesStaffInventoryFilter.isHidden(i.productName))
          .toList();
    } on ApiException catch (e) {
      _throw(e);
    }
  }

  @override
  Future<List<SalesCustomerList>> activeCustomerLists() async {
    try {
      final raw = await ApiClient.get('sales/active-customer-lists');
      return _maps(raw)
          .map(SalesCustomerList.fromJson)
          .where((e) => e.listId > 0 && e.listName.trim().isNotEmpty)
          .toList();
    } on ApiException catch (e) {
      _throw(e);
    }
  }

  @override
  Future<SalesDraft> createSale(SalesDraftCreateRequest request) async {
    try {
      final raw = await ApiClient.post('sales', body: request.toJson());
      return SalesDraft.fromJson(Map<String, dynamic>.from(raw as Map));
    } on ApiException catch (e) {
      _throw(e);
    }
  }

  @override
  Future<SalesDraft> saveSaleProgress(SalesDraftCreateRequest request) async {
    try {
      final raw = await ApiClient.post('sales/progress', body: request.toJson());
      return SalesDraft.fromJson(Map<String, dynamic>.from(raw as Map));
    } on ApiException catch (e) {
      _throw(e);
    }
  }

  @override
  Future<List<SalesDraft>> pending() async {
    try {
      final raw = await ApiClient.get('sales/pending');
      return _maps(raw).map(SalesDraft.fromJson).where((d) => !d.isCompleted).toList();
    } on ApiException catch (e) {
      _throw(e);
    }
  }

  @override
  Future<List<SalesDraft>> todayCompleted() async {
    try {
      final raw = await ApiClient.get('sales/today');
      return _maps(raw).map(SalesDraft.fromJson).toList();
    } on ApiException catch (e) {
      if (e.statusCode == 404) {
        final raw = await ApiClient.get('sales/pending');
        final iraq = IraqTime.now();
        final day = DateTime(iraq.year, iraq.month, iraq.day);
        return _maps(raw)
            .map(SalesDraft.fromJson)
            .where((d) => d.isCompleted && _onIraqDay(d.completedAt ?? d.createdAt, day))
            .toList();
      }
      _throw(e);
    }
  }

  @override
  Future<SalesDraft> byId(int id) async {
    try {
      final raw = await ApiClient.get('sales/$id');
      return SalesDraft.fromJson(Map<String, dynamic>.from(raw as Map));
    } on ApiException catch (e) {
      _throw(e);
    }
  }

  @override
  Future<String> uploadShopImage(int saleId, List<int> bytes, String fileName) async {
    try {
      final raw = await ApiClient.postMultipart(
        'sales/$saleId/shop-image',
        bytes: bytes,
        fileName: fileName,
      );
      if (raw is Map) {
        return '${raw['shopImageKey'] ?? raw['ShopImageKey'] ?? ''}';
      }
      throw ApiException('تعذر رفع صورة المحل');
    } on ApiException catch (e) {
      _throw(e);
    }
  }

  @override
  Future<List<SalesCustomerKycDocument>> listCustomerDocuments(int saleId) async {
    try {
      final raw = await ApiClient.get('sales/$saleId/customer-documents');
      return _maps(raw).map(SalesCustomerKycDocument.fromJson).toList();
    } on ApiException catch (e) {
      _throw(e);
    }
  }

  @override
  Future<SalesCustomerKycDocument> uploadCustomerDocument(
    int saleId,
    String type,
    List<int> bytes,
    String fileName,
  ) async {
    try {
      final raw = await ApiClient.postMultipart(
        'sales/$saleId/customer-documents?type=${Uri.encodeQueryComponent(type)}',
        bytes: bytes,
        fileName: fileName,
      );
      if (raw is Map) {
        return SalesCustomerKycDocument.fromJson(Map<String, dynamic>.from(raw));
      }
      throw ApiException('تعذر رفع مستند الزبون');
    } on ApiException catch (e) {
      _throw(e);
    }
  }

  @override
  Future<void> deleteCustomerDocument(int documentId) async {
    try {
      await ApiClient.delete('sales/customer-documents/$documentId');
    } on ApiException catch (e) {
      _throw(e);
    }
  }

  @override
  Future<List<int>> customerDocumentBytes(int documentId) async {
    try {
      return await ApiClient.getBytes('sales/customer-documents/$documentId/file');
    } on ApiException catch (e) {
      _throw(e);
    }
  }

  @override
  Future<SalesCompleteResult> completeSale(int id, [SalesShopComplete? shop]) async {
    try {
      final raw = await ApiClient.post('sales/$id/complete', body: shop?.toJson() ?? {});
      return SalesCompleteResult.fromJson(Map<String, dynamic>.from(raw as Map));
    } on ApiException catch (e) {
      _throw(e);
    }
  }

  @override
  Future<SalesPreviewDocuments> previewDocuments(int id, [SalesShopComplete? shop]) async {
    try {
      final raw = await ApiClient.post('sales/$id/preview-documents', body: shop?.toJson() ?? {});
      return SalesPreviewDocuments.fromJson(Map<String, dynamic>.from(raw as Map));
    } on ApiException catch (e) {
      _throw(e);
    }
  }

  @override
  Future<List<SalesDocument>> documents(int saleId) async {
    try {
      final raw = await ApiClient.get('sales/$saleId/documents');
      return _maps(raw).map(SalesDocument.fromJson).toList();
    } on ApiException catch (e) {
      _throw(e);
    }
  }

  @override
  Future<List<int>> downloadDocument(int saleId, SalesDocument document) async {
    final path = _documentDownloadPath(saleId, document);
    try {
      debugPrint('DOCUMENT_DOWNLOAD_START saleId=$saleId type=${document.type} url=$path');
      final bytes = await ApiClient.getBytes(path);
      if (bytes.isEmpty) {
        debugPrint('DOCUMENT_DOWNLOAD_FAILED status=empty url=$path');
        throw ApiException('المستند غير موجود.', statusCode: 404);
      }
      final header = String.fromCharCodes(bytes.take(5));
      if (header != '%PDF-') {
        debugPrint('DOCUMENT_DOWNLOAD_FAILED status=not-pdf url=$path');
        throw ApiException('تعذر تنزيل المستند', statusCode: 502);
      }
      debugPrint('DOCUMENT_DOWNLOAD_SUCCESS saleId=$saleId bytes=${bytes.length}');
      return bytes;
    } on ApiException catch (e) {
      debugPrint('DOCUMENT_DOWNLOAD_FAILED status=${e.statusCode} url=$path error=${e.message}');
      _throw(e);
    }
  }

  String _documentDownloadPath(int saleId, SalesDocument document) {
    final raw = document.downloadUrl.trim();
    if (raw.isNotEmpty) {
      if (raw.startsWith('http://') || raw.startsWith('https://')) {
        final uri = Uri.parse(raw);
        var path = uri.path;
        while (path.toLowerCase().startsWith('/api/')) {
          path = path.substring(4);
        }
        if (path.startsWith('/')) path = path.substring(1);
        return path;
      }
      var relative = raw;
      if (relative.contains('/api/')) {
        relative = relative.substring(relative.indexOf('/api/') + 5);
      }
      while (relative.toLowerCase().startsWith('api/')) {
        relative = relative.substring(4);
      }
      if (relative.startsWith('/')) relative = relative.substring(1);
      return relative;
    }
    final id = document.documentId;
    if (saleId <= 0 || id == null || id <= 0) {
      throw ApiException('المستند غير متوفر.', statusCode: 404);
    }
    return 'sales/$saleId/documents/$id/download';
  }

  @override
  Future<WorkShift> startShift() async {
    try {
      final raw = await ApiClient.post('sales/shifts/start');
      return WorkShift.fromJson(Map<String, dynamic>.from(raw as Map));
    } on ApiException {
      rethrow;
    }
  }

  @override
  Future<void> endShift() async {
    try {
      await ApiClient.post('sales/shifts/end');
    } on ApiException {
      rethrow;
    }
  }

  @override
  Future<WorkShift?> currentShift() async {
    try {
      final raw = await ApiClient.get('sales/shifts/current');
      if (raw is Map && (raw['hasActiveShift'] == false || raw['HasActiveShift'] == false)) {
        return null;
      }
      if (raw is Map) {
        final shift = WorkShift.fromJson(Map<String, dynamic>.from(raw));
        return shift.hasActiveShift && shift.shiftId > 0 ? shift : null;
      }
      return null;
    } on ApiException catch (e) {
      _throw(e);
    }
  }

  @override
  Future<LocationBatchResult> uploadLocationBatch(int shiftId, List<LocalLocationPoint> points) async {
    try {
      final raw = await ApiClient.post('sales/location/batch', body: {
        'shiftId': shiftId,
        'points': points.map((e) => e.toBatchJson()).toList(),
      });
      return LocationBatchResult.fromJson(Map<String, dynamic>.from(raw as Map));
    } on ApiException catch (e) {
      _throw(e);
    }
  }

  @override
  Future<void> uploadLiveLocation({
    required int shiftId,
    required double latitude,
    required double longitude,
    double? accuracy,
    DateTime? capturedAtUtc,
  }) async {
    try {
      await ApiClient.post('sales/location/live', body: {
        'shiftId': shiftId,
        'latitude': latitude,
        'longitude': longitude,
        if (accuracy != null) 'accuracy': accuracy,
        'capturedAtUtc': (capturedAtUtc ?? DateTime.now().toUtc()).toIso8601String(),
      });
    } on ApiException {
      return;
    }
  }

  @override
  Future<void> recordTrackingEvent(int? shiftId, String eventType) async {
    try {
      await ApiClient.post('sales/tracking/events', body: {
        'shiftId': ?shiftId,
        'eventType': eventType,
        'occurredAtUtc': DateTime.now().toUtc().toIso8601String(),
      });
    }     on ApiException {
      return;
    }
  }

  @override
  Future<List<SalesWorkRequest>> salesRequests() async {
    try {
      final raw = await ApiClient.get('sales/requests');
      return _maps(raw).map(SalesWorkRequest.fromJson).toList();
    } on ApiException catch (e) {
      _throw(e);
    }
  }

  @override
  Future<SalesWorkRequest> salesRequest(int id) async {
    try {
      final raw = await ApiClient.get('sales/requests/$id');
      return SalesWorkRequest.fromJson(Map<String, dynamic>.from(raw as Map));
    } on ApiException catch (e) {
      _throw(e);
    }
  }

  @override
  Future<SalesWorkRequest> viewSalesRequest(int id) async {
    try {
      final raw = await ApiClient.post('sales/requests/$id/view');
      return SalesWorkRequest.fromJson(Map<String, dynamic>.from(raw as Map));
    } on ApiException catch (e) {
      _throw(e);
    }
  }

  @override
  Future<SalesWorkRequest> startSalesRequest(int id) => prepareSalesRequest(id);

  @override
  Future<SalesWorkRequest> prepareSalesRequest(int id) async {
    try {
      final raw = await ApiClient.post('sales/requests/$id/prepare');
      return SalesWorkRequest.fromJson(Map<String, dynamic>.from(raw as Map));
    } on ApiException catch (e) {
      _throw(e);
    }
  }

  @override
  Future<SalesWorkRequest> inspectSalesRequest(int id, SalesDraftCreateRequest progress) async {
    try {
      final raw = await ApiClient.post('sales/requests/$id/inspect', body: progress.toJson());
      return SalesWorkRequest.fromJson(Map<String, dynamic>.from(raw as Map));
    } on ApiException catch (e) {
      _throw(e);
    }
  }

  @override
  Future<SalesWorkRequest> pendSalesRequest(int id, String note) async {
    try {
      final raw = await ApiClient.post('sales/requests/$id/pending', body: {'note': note});
      return SalesWorkRequest.fromJson(Map<String, dynamic>.from(raw as Map));
    } on ApiException catch (e) {
      _throw(e);
    }
  }

  @override
  Future<SalesWorkRequest> rejectSalesRequest(int id, String reason) async {
    try {
      final raw = await ApiClient.post('sales/requests/$id/reject', body: {'reason': reason});
      return SalesWorkRequest.fromJson(Map<String, dynamic>.from(raw as Map));
    } on ApiException catch (e) {
      _throw(e);
    }
  }

  @override
  Future<SalesWorkRequest> submitSalesRequest({
    required String fullName,
    required String phone,
    required String province,
    required String address,
    String? notes,
  }) async {
    try {
      final raw = await ApiClient.post('sales/requests/submit', body: {
        'customer': {
          'fullName': fullName,
          'phone': phone,
          'province': province,
          'address': address,
        },
        if (notes != null && notes.trim().isNotEmpty) 'notes': notes.trim(),
      });
      return SalesWorkRequest.fromJson(Map<String, dynamic>.from(raw as Map));
    } on ApiException catch (e) {
      _throw(e);
    }
  }
}
