import 'package:sales_employee_application/data/sales_models.dart';
import 'package:sales_employee_application/data/sales_repository.dart';
import 'package:sales_employee_application/services/api_client.dart';
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
    throw ApiException(salesApiMessage(e.statusCode, e.message), statusCode: e.statusCode);
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
      return _maps(raw).map(SalesInventoryItem.fromJson).toList();
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
  Future<SalesCompleteResult> completeSale(int id) async {
    try {
      final raw = await ApiClient.post('sales/$id/complete');
      return SalesCompleteResult.fromJson(Map<String, dynamic>.from(raw as Map));
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
    try {
      final path = document.downloadUrl.contains('/api/')
          ? document.downloadUrl.substring(document.downloadUrl.indexOf('/api/') + 5)
          : 'sales/$saleId/documents/${document.documentId}/download';
      return await ApiClient.getBytes(path);
    } on ApiException catch (e) {
      _throw(e);
    }
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
  Future<SalesWorkRequest> startSalesRequest(int id) async {
    try {
      final raw = await ApiClient.post('sales/requests/$id/start-processing');
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
}
