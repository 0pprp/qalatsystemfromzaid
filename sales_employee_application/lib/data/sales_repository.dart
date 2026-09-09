import 'package:sales_employee_application/data/sales_models.dart';
import 'package:sales_employee_application/tracking/work_shift.dart';

abstract class SalesRepository {
  Future<SalesMe> me();
  Future<List<SalesCustomer>> searchCustomers(String query);
  Future<List<SalesInventoryItem>> inventory();
  Future<List<SalesCustomerList>> activeCustomerLists();
  Future<SalesDraft> createSale(SalesDraftCreateRequest request);
  Future<SalesDraft> saveSaleProgress(SalesDraftCreateRequest request);
  Future<List<SalesDraft>> pending();
  Future<List<SalesDraft>> todayCompleted();
  Future<SalesDraft> byId(int id);
  Future<SalesCompleteResult> completeSale(int id, [SalesShopComplete? shop]);
  Future<SalesPreviewDocuments> previewDocuments(int id, [SalesShopComplete? shop]);
  Future<String> uploadShopImage(int saleId, List<int> bytes, String fileName);
  Future<List<SalesCustomerKycDocument>> listCustomerDocuments(int saleId);
  Future<SalesCustomerKycDocument> uploadCustomerDocument(int saleId, String type, List<int> bytes, String fileName);
  Future<void> deleteCustomerDocument(int documentId);
  Future<List<int>> customerDocumentBytes(int documentId);
  Future<List<SalesDocument>> documents(int saleId);
  Future<List<int>> downloadDocument(int saleId, SalesDocument document);
  Future<WorkShift> startShift();
  Future<void> endShift();
  Future<WorkShift?> currentShift();
  Future<LocationBatchResult> uploadLocationBatch(int shiftId, List<LocalLocationPoint> points);
  Future<void> uploadLiveLocation({
    required int shiftId,
    required double latitude,
    required double longitude,
    double? accuracy,
    DateTime? capturedAtUtc,
  });
  Future<void> recordTrackingEvent(int? shiftId, String eventType);
  Future<List<SalesWorkRequest>> salesRequests();
  Future<SalesWorkRequest> salesRequest(int id);
  Future<SalesWorkRequest> viewSalesRequest(int id);
  Future<SalesWorkRequest> startSalesRequest(int id);
  Future<SalesWorkRequest> prepareSalesRequest(int id, String note);
  Future<SalesWorkRequest> inspectSalesRequest(int id, SalesDraftCreateRequest progress);
  Future<SalesWorkRequest> pendSalesRequest(int id, String note);
  Future<SalesWorkRequest> rejectSalesRequest(int id, String reason);
  Future<SalesWorkRequest> submitSalesRequest({
    required String fullName,
    required String phone,
    required String province,
    required String address,
    String? notes,
  });
}
