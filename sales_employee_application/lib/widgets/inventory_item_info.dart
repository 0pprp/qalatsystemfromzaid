import 'package:flutter/material.dart';
import 'package:sales_employee_application/data/sales_models.dart';
import 'package:sales_employee_application/utils/app_theme.dart';
import 'package:sales_employee_application/utils/sales_format.dart';

class InventoryItemInfo extends StatelessWidget {
  const InventoryItemInfo({
    super.key,
    required this.item,
    this.availabilityOverride,
  });

  final SalesInventoryItem item;
  final String? availabilityOverride;

  @override
  Widget build(BuildContext context) {
    final empty = item.availableQuantity <= 0;
    final details = <String>[
      if (item.productId > 0) 'الكود: ${item.productId}',
      if (item.notes != null && item.notes!.trim().isNotEmpty) item.notes!.trim(),
    ];
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text(item.productName, style: const TextStyle(fontWeight: FontWeight.w700)),
        const SizedBox(height: 4),
        Text(availabilityOverride ?? (empty ? 'غير متوفر' : 'المتوفر: ${item.availableQuantity}')),
        Text('سعر البيع: ${MoneyFormat.iqd(item.salePrice)}'),
        Text(
          item.dailyInstallment == null
              ? 'القسط اليومي: غير متوفر من الخادم'
              : 'القسط اليومي: ${MoneyFormat.iqd(item.dailyInstallment!)}',
          style: item.dailyInstallment == null
              ? const TextStyle(color: AppColors.muted)
              : null,
        ),
        if (details.isNotEmpty)
          Text(details.join(' · '), style: const TextStyle(color: AppColors.muted, fontSize: 13)),
      ],
    );
  }
}
