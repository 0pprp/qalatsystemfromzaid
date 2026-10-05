/// Client mirror of BE SaleReviewRules / follower auth:
/// score 1–10; note required when ≤6; optional when ≥7;
/// shop stock + daily revenue required.
class SaleReviewRules {
  static String? validate({
    required int score,
    String? note,
    double? shopStockEstimatedValue,
    double? estimatedDailyRevenue,
  }) {
    if (score < 1 || score > 10) {
      return 'التقييم يجب أن يكون من 1 إلى 10.';
    }
    final trimmed = (note ?? '').trim();
    if (score < 7 && trimmed.isEmpty) {
      return 'الملاحظة إجبارية عندما يكون التقييم أقل من 7.';
    }
    if (shopStockEstimatedValue == null || shopStockEstimatedValue <= 0) {
      return 'قيمة بضاعة المحل مطلوبة ويجب أن تكون أكبر من صفر.';
    }
    if (estimatedDailyRevenue == null || estimatedDailyRevenue <= 0) {
      return 'قيمة واردات المحل اليومية مطلوبة ويجب أن تكون أكبر من صفر.';
    }
    return null;
  }
}
