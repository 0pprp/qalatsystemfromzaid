import 'package:flutter/material.dart';
import 'package:open_filex/open_filex.dart';
import 'package:sales_employee_application/utils/app_theme.dart';

class SaleCompleteSuccessScreen extends StatelessWidget {
  const SaleCompleteSuccessScreen({
    super.key,
    required this.saleId,
    required this.finalSalePrice,
    this.completedAt,
    this.contractPath,
    this.receiptPath,
    this.downloadFailed = false,
    this.onRetryDownload,
  });

  final int saleId;
  final num finalSalePrice;
  final DateTime? completedAt;
  final String? contractPath;
  final String? receiptPath;
  final bool downloadFailed;
  final Future<void> Function()? onRetryDownload;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('تم البيع')),
      body: Padding(
        padding: const EdgeInsets.all(AppSpacing.md),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            const Text(
              '✓ تمت عملية البيع بنجاح',
              style: TextStyle(fontSize: 22, fontWeight: FontWeight.w800, color: AppColors.darkGreen),
            ),
            const SizedBox(height: AppSpacing.sm),
            const Text('تم تسجيل البيع وخصم المواد من المخزن.'),
            if (completedAt != null) Text('التاريخ: $completedAt'),
            Text('السعر النهائي: $finalSalePrice'),
            const SizedBox(height: AppSpacing.md),
            if (downloadFailed)
              const Text(
                'تم البيع بنجاح، لكن تعذر تنزيل أحد المستندات.',
                style: TextStyle(color: AppColors.danger, fontWeight: FontWeight.w700),
              )
            else ...[
              const Text('تم تنزيل:'),
              Text(contractPath != null ? '✓ عقد البيع' : 'عقد البيع (غير متوفر)'),
              Text(receiptPath != null ? '✓ وصل الأمانة' : 'وصل الأمانة (غير متوفر)'),
            ],
            const Spacer(),
            ElevatedButton(
              onPressed: contractPath == null ? null : () => OpenFilex.open(contractPath!),
              child: const Text('فتح عقد البيع'),
            ),
            const SizedBox(height: AppSpacing.sm),
            ElevatedButton(
              onPressed: receiptPath == null ? null : () => OpenFilex.open(receiptPath!),
              child: const Text('فتح وصل الأمانة'),
            ),
            if (downloadFailed && onRetryDownload != null) ...[
              const SizedBox(height: AppSpacing.sm),
              OutlinedButton(
                onPressed: onRetryDownload,
                child: const Text('إعادة تنزيل المستندات'),
              ),
            ],
            const SizedBox(height: AppSpacing.sm),
            TextButton(
              onPressed: () => Navigator.pushNamedAndRemoveUntil(
                context,
                '/home',
                (_) => false,
                arguments: 'today',
              ),
              child: const Text('العودة للمبيعات'),
            ),
          ],
        ),
      ),
    );
  }
}
