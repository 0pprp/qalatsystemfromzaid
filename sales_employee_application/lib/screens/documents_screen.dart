import 'package:flutter/material.dart';
import 'package:open_filex/open_filex.dart';
import 'package:sales_employee_application/data/sales_models.dart';
import 'package:sales_employee_application/data/sales_repository_factory.dart';
import 'package:sales_employee_application/services/sale_document_storage.dart';
import 'package:sales_employee_application/services/session.dart';
import 'package:sales_employee_application/utils/app_theme.dart';
import 'package:sales_employee_application/widgets/tappable_phone.dart';

class DocumentsScreen extends StatelessWidget {
  const DocumentsScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final sale = Session.lastSale;
    if (sale == null) {
      return const Center(
        child: Padding(
          padding: EdgeInsets.all(24),
          child: Text('احفظ مبيعاً أولاً ثم اطبع أو شارك عقد البيع + وصل الأمانة.'),
        ),
      );
    }

    Future<void> openDoc(SalesDocument doc) async {
      try {
        final saleId = int.tryParse('${sale['saleId'] ?? sale['SaleId'] ?? ''}');
        if (saleId == null) {
          throw Exception('لا يوجد رقم عملية لتنزيل المستند من الخادم.');
        }
        final bytes = await SalesRepositoryFactory.instance.downloadDocument(saleId, doc);
        final file = await SaleDocumentStorage.savePdf(doc.fileName, bytes);
        await OpenFilex.open(file.path);
      } catch (e) {
        if (context.mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(content: Text('تعذر تنزيل المستند من الخادم: $e')),
          );
        }
      }
    }

    Future<void> openPreferred() async {
      try {
        final saleId = int.tryParse('${sale['saleId'] ?? sale['SaleId'] ?? ''}');
        if (saleId == null) {
          throw Exception('لا يوجد رقم عملية لتنزيل المستند من الخادم.');
        }
        final docs = SalesDocument.preferDisplay(
          await SalesRepositoryFactory.instance.documents(saleId),
        );
        if (docs.isEmpty) {
          throw Exception('لا توجد مستندات.');
        }
        await openDoc(docs.first);
      } catch (e) {
        if (context.mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(content: Text('تعذر تنزيل المستند من الخادم: $e')),
          );
        }
      }
    }

    Future<void> openLegacy(bool contract) async {
      try {
        final saleId = int.tryParse('${sale['saleId'] ?? sale['SaleId'] ?? ''}');
        if (saleId == null) {
          throw Exception('لا يوجد رقم عملية لتنزيل المستند من الخادم.');
        }
        final docs = await SalesRepositoryFactory.instance.documents(saleId);
        final preferred = SalesDocument.preferDisplay(docs);
        if (preferred.any((d) => d.isCombined)) {
          await openDoc(preferred.first);
          return;
        }
        final doc = docs.firstWhere(
          (item) => contract
              ? item.type == 'Contract' || item.type == 'PreviewContract'
              : item.type == 'PromissoryNote' || item.type == 'PreviewPromissoryNote',
        );
        await openDoc(doc);
      } catch (e) {
        if (context.mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(content: Text('تعذر تنزيل المستند من الخادم: $e')),
          );
        }
      }
    }

    return FutureBuilder<List<SalesDocument>>(
      future: () async {
        final saleId = int.tryParse('${sale['saleId'] ?? sale['SaleId'] ?? ''}');
        if (saleId == null) return const <SalesDocument>[];
        return SalesDocument.preferDisplay(
          await SalesRepositoryFactory.instance.documents(saleId),
        );
      }(),
      builder: (context, snap) {
        final docs = snap.data ?? const <SalesDocument>[];
        final combined = docs.where((d) => d.isCombined).toList();
        return ListView(
          padding: const EdgeInsets.all(16),
          children: [
            Card(
              child: ListTile(
                title: Text('${sale['customerName'] ?? ''}'),
                subtitle: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    TappablePhone('${sale['phoneNumber'] ?? ''}'),
                    if ('${sale['ratingLabel'] ?? ''}'.trim().isNotEmpty)
                      Text('${sale['ratingLabel']}'),
                  ],
                ),
              ),
            ),
            const SizedBox(height: 12),
            if (combined.isNotEmpty) ...[
              ElevatedButton.icon(
                onPressed: openPreferred,
                icon: const Icon(Icons.picture_as_pdf),
                label: const Text('طباعة عقد البيع + وصل الأمانة'),
              ),
              const SizedBox(height: 8),
              OutlinedButton.icon(
                onPressed: openPreferred,
                icon: const Icon(Icons.share, color: AppTheme.primaryColor),
                label: const Text('مشاركة عقد البيع + وصل الأمانة'),
              ),
            ] else ...[
              ElevatedButton.icon(
                onPressed: () => openLegacy(true),
                icon: const Icon(Icons.picture_as_pdf),
                label: const Text('طباعة عقد البيع'),
              ),
              const SizedBox(height: 8),
              OutlinedButton.icon(
                onPressed: () => openLegacy(true),
                icon: const Icon(Icons.share, color: AppTheme.primaryColor),
                label: const Text('مشاركة عقد البيع'),
              ),
              const SizedBox(height: 20),
              ElevatedButton.icon(
                onPressed: () => openLegacy(false),
                icon: const Icon(Icons.receipt_long),
                label: const Text('طباعة وصل الأمانة'),
              ),
              const SizedBox(height: 8),
              OutlinedButton.icon(
                onPressed: () => openLegacy(false),
                icon: const Icon(Icons.share, color: AppTheme.primaryColor),
                label: const Text('مشاركة وصل الأمانة'),
              ),
            ],
          ],
        );
      },
    );
  }
}
