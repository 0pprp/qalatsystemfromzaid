import 'package:flutter/material.dart';
import 'package:sales_employee_application/services/sale_documents.dart';
import 'package:sales_employee_application/services/session.dart';
import 'package:sales_employee_application/utils/app_theme.dart';

class DocumentsScreen extends StatelessWidget {
  const DocumentsScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final sale = Session.lastSale;
    if (sale == null) {
      return const Center(
        child: Padding(
          padding: EdgeInsets.all(24),
          child: Text('احفظ مبيعاً أولاً ثم اطبع أو شارك عقد البيع ووصل الأمانة.'),
        ),
      );
    }

    Future<void> run(Future<void> Function(Map<String, dynamic>) action) async {
      try {
        await action(sale);
      } catch (e) {
        if (context.mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(content: Text('تعذر إنشاء المستند: $e')),
          );
        }
      }
    }

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        Card(
          child: ListTile(
            title: Text('${sale['customerName'] ?? ''}'),
            subtitle: Text('${sale['phoneNumber'] ?? ''} — ${sale['ratingLabel'] ?? ''}'),
          ),
        ),
        const SizedBox(height: 12),
        ElevatedButton.icon(
          onPressed: () => run(SaleDocuments.printContract),
          icon: const Icon(Icons.picture_as_pdf),
          label: const Text('طباعة عقد البيع'),
        ),
        const SizedBox(height: 8),
        OutlinedButton.icon(
          onPressed: () => run(SaleDocuments.shareContract),
          icon: const Icon(Icons.share, color: AppTheme.primaryColor),
          label: const Text('مشاركة عقد البيع'),
        ),
        const SizedBox(height: 20),
        ElevatedButton.icon(
          onPressed: () => run(SaleDocuments.printTrustReceipt),
          icon: const Icon(Icons.receipt_long),
          label: const Text('طباعة وصل الأمانة'),
        ),
        const SizedBox(height: 8),
        OutlinedButton.icon(
          onPressed: () => run(SaleDocuments.shareTrustReceipt),
          icon: const Icon(Icons.share, color: AppTheme.primaryColor),
          label: const Text('مشاركة وصل الأمانة'),
        ),
      ],
    );
  }
}
