import 'package:sales_employee_application/data/sales_models.dart';

/// Local inventory filter for sale/warehouse lists (name, notes, product code).
bool inventoryItemMatches(SalesInventoryItem item, String rawQuery) {
  final q = rawQuery.trim().toLowerCase();
  if (q.isEmpty) return true;
  if (item.productName.toLowerCase().contains(q)) return true;
  if ((item.notes ?? '').toLowerCase().contains(q)) return true;
  if ('${item.productId}'.contains(q)) return true;
  return false;
}

List<SalesInventoryItem> filterInventoryItems(
  Iterable<SalesInventoryItem> items,
  String rawQuery,
) {
  final q = rawQuery.trim();
  if (q.isEmpty) return List<SalesInventoryItem>.from(items);
  return items.where((i) => inventoryItemMatches(i, q)).toList();
}
