import 'package:sales_employee_application/data/sales_models.dart';
import 'package:sales_employee_application/tracking/work_shift.dart';

abstract class SalesRepository {
  Future<SalesMe> me();
  Future<List<SalesCustomer>> searchCustomers(String query);
  Future<List<SalesInventoryItem>> inventory();
  Future<SalesDraft> createSale(SalesDraftCreateRequest request);
  Future<List<SalesDraft>> pending();
  Future<SalesDraft> byId(int id);
  Future<SalesCompleteResult> completeSale(int id);
  Future<List<SalesDocument>> documents(int saleId);
  Future<List<int>> downloadDocument(int saleId, SalesDocument document);
  Future<WorkShift> startShift();
  Future<WorkShift?> currentShift();
  Future<LocationBatchResult> uploadLocationBatch(int shiftId, List<LocalLocationPoint> points);
  Future<void> recordTrackingEvent(int? shiftId, String eventType);
  Future<List<SalesWorkRequest>> salesRequests();
  Future<SalesWorkRequest> salesRequest(int id);
  Future<SalesWorkRequest> viewSalesRequest(int id);
  Future<SalesWorkRequest> startSalesRequest(int id);
  Future<SalesWorkRequest> rejectSalesRequest(int id, String reason);
}
