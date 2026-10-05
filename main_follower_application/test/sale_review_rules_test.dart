import 'package:flutter_test/flutter_test.dart';
import 'package:main_follower_application/domain/sale_review_rules.dart';

void main() {
  test('note optional at 7+ when shop values present', () {
    expect(
      SaleReviewRules.validate(
        score: 7,
        note: null,
        shopStockEstimatedValue: 1000,
        estimatedDailyRevenue: 100,
      ),
      isNull,
    );
    expect(
      SaleReviewRules.validate(
        score: 10,
        note: '  ',
        shopStockEstimatedValue: 1000,
        estimatedDailyRevenue: 100,
      ),
      isNull,
    );
  });

  test('note required at 6 or below', () {
    expect(
      SaleReviewRules.validate(
        score: 6,
        note: null,
        shopStockEstimatedValue: 1000,
        estimatedDailyRevenue: 100,
      ),
      isNotNull,
    );
    expect(
      SaleReviewRules.validate(
        score: 5,
        note: 'ضعيف',
        shopStockEstimatedValue: 1000,
        estimatedDailyRevenue: 100,
      ),
      isNull,
    );
  });

  test('shop money required', () {
    expect(
      SaleReviewRules.validate(
        score: 8,
        note: null,
        shopStockEstimatedValue: null,
        estimatedDailyRevenue: 100,
      ),
      isNotNull,
    );
    expect(
      SaleReviewRules.validate(
        score: 8,
        note: null,
        shopStockEstimatedValue: 1000,
        estimatedDailyRevenue: 0,
      ),
      isNotNull,
    );
  });
}
