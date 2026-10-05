import 'package:main_follower_application/core/network/api_client.dart';
import 'package:main_follower_application/domain/branch_sale.dart';

class SalesRepository {
  static Future<List<BranchSale>> listForMissionCity({
    required int missionId,
    required String cityValue,
  }) async {
    final path =
        'main-follower/missions/$missionId/cities/${Uri.encodeComponent(cityValue)}/sales';
    final rows = await ApiClient.getList(path);
    return rows
        .whereType<Map>()
        .map((e) => BranchSale.fromJson(Map<String, dynamic>.from(e)))
        .where((s) => s.saleId > 0)
        .toList();
  }

  static Future<SaleReview> submitReview({
    required int missionId,
    required String cityValue,
    required int saleId,
    required String customerName,
    required int score,
    String? note,
    required double shopStockEstimatedValue,
    required double estimatedDailyRevenue,
  }) async {
    final json = await ApiClient.postJson('main-follower/sale-reviews', {
      'missionId': missionId,
      'cityValue': cityValue,
      'saleId': saleId,
      'customerName': customerName,
      'score': score,
      'note': note,
      'shopStockEstimatedValue': shopStockEstimatedValue,
      'estimatedDailyRevenue': estimatedDailyRevenue,
    });
    return SaleReview.fromJson(json);
  }

  static Future<List<SaleReview>> listReviews(int missionId) async {
    final rows = await ApiClient.getList('main-follower/missions/$missionId/sale-reviews');
    return rows
        .whereType<Map>()
        .map((e) => SaleReview.fromJson(Map<String, dynamic>.from(e)))
        .toList();
  }
}
