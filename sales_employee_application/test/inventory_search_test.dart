import 'package:flutter_test/flutter_test.dart';
import 'package:sales_employee_application/data/sales_models.dart';
import 'package:sales_employee_application/utils/inventory_search.dart';

void main() {
  final stock = [
    SalesInventoryItem(
      productId: 101,
      productName: 'ثلاجة سامسونج',
      availableQuantity: 3,
      salePrice: 500000,
      notes: 'S18',
    ),
    SalesInventoryItem(
      productId: 202,
      productName: 'غسالة ال جي',
      availableQuantity: 1,
      salePrice: 400000,
      notes: 'W10',
    ),
    SalesInventoryItem(
      productId: 303,
      productName: 'مكيف شباك',
      availableQuantity: 5,
      salePrice: 250000,
    ),
  ];

  test('search by Arabic name is case-insensitive and trims', () {
    final rows = filterInventoryItems(stock, '  ثلاجة  ');
    expect(rows, hasLength(1));
    expect(rows.single.productId, 101);
  });

  test('search by product code keeps real productId', () {
    final rows = filterInventoryItems(stock, '202');
    expect(rows, hasLength(1));
    expect(rows.single.productId, 202);
    expect(rows.single.productName, 'غسالة ال جي');
  });

  test('search by notes code', () {
    final rows = filterInventoryItems(stock, 's18');
    expect(rows, hasLength(1));
    expect(rows.single.productId, 101);
  });

  test('no results', () {
    expect(filterInventoryItems(stock, 'لا يوجد'), isEmpty);
  });

  test('selection after filter uses productId not filtered index', () {
    final filtered = filterInventoryItems(stock, 'مكيف');
    expect(filtered, hasLength(1));
    final qty = <int, int>{};
    qty[filtered.first.productId] = 2;
    expect(qty.keys.single, 303);
    expect(qty[stock.indexOf(filtered.first)], isNull);
  });
}
