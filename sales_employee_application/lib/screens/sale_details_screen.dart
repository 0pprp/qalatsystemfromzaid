import 'package:flutter/material.dart';
import 'package:open_filex/open_filex.dart';
import 'package:sales_employee_application/data/sales_models.dart';
import 'package:sales_employee_application/data/sales_repository_factory.dart';
import 'package:sales_employee_application/screens/sale_complete_success_screen.dart';
import 'package:sales_employee_application/services/api_client.dart';
import 'package:sales_employee_application/services/sale_document_storage.dart';
import 'package:sales_employee_application/utils/app_theme.dart';
import 'package:sales_employee_application/utils/sales_format.dart';

class SaleDetailsScreen extends StatefulWidget {
  const SaleDetailsScreen({super.key, this.saleId});

  final int? saleId;

  @override
  State<SaleDetailsScreen> createState() => _SaleDetailsScreenState();
}

class _SaleDetailsScreenState extends State<SaleDetailsScreen> {
  SalesDraft? _draft;
  String? _error;
  bool _loading = true;
  bool _started = false;
  bool _completing = false;

  int? get _id => widget.saleId ?? ModalRoute.of(context)?.settings.arguments as int?;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (_started) return;
    _started = true;
    final id = _id;
    if (id != null) {
      _load(id);
    } else {
      setState(() {
        _loading = false;
        _error = 'رقم العملية غير صالح';
      });
    }
  }

  Future<void> _load(int id) async {
    try {
      final draft = await SalesRepositoryFactory.instance.byId(id);
      if (!mounted) return;
      setState(() {
        _draft = draft;
        _loading = false;
      });
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() {
        _error = e.message;
        _loading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _error = 'تعذر فتح العملية';
        _loading = false;
      });
    }
  }

  Future<void> _confirmComplete() async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('هل أنت متأكد من إتمام عملية البيع؟'),
        content: const Text(
          'سيتم:\n'
          '• تسجيل البيع\n'
          '• خصم المواد من مخزن الفرع\n'
          '• اعتماد العقد ووصل الأمانة\n'
          '• إنشاء ملفات PDF للعقد ووصل الأمانة\n'
          '• تنزيل الملفات إلى جهازك',
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('رجوع')),
          ElevatedButton(onPressed: () => Navigator.pop(ctx, true), child: const Text('نعم، تم البيع')),
        ],
      ),
    );
    if (ok == true) {
      await _complete();
    }
  }

  Future<void> _complete() async {
    final draft = _draft;
    if (draft == null || _completing) return;
    setState(() => _completing = true);
    try {
      final result = await SalesRepositoryFactory.instance.completeSale(draft.saleId);
      final download = await _downloadAll(result.documents);
      if (!mounted) return;
      await Navigator.pushReplacement(
        context,
        MaterialPageRoute(
          builder: (_) => SaleCompleteSuccessScreen(
            saleId: result.saleId,
            finalSalePrice: result.finalSalePrice,
            completedAt: result.completedAt,
            contractPath: download.contractPath,
            receiptPath: download.receiptPath,
            downloadFailed: download.failed,
            onRetryDownload: () async {
              final again = await _downloadAll(result.documents);
              if (!mounted) return;
              await Navigator.pushReplacement(
                context,
                MaterialPageRoute(
                  builder: (_) => SaleCompleteSuccessScreen(
                    saleId: result.saleId,
                    finalSalePrice: result.finalSalePrice,
                    completedAt: result.completedAt,
                    contractPath: again.contractPath,
                    receiptPath: again.receiptPath,
                    downloadFailed: again.failed,
                  ),
                ),
              );
            },
          ),
        ),
      );
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() {
        _completing = false;
        _error = e.message;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _completing = false;
        _error = e.toString().replaceFirst('Exception: ', '');
      });
    }
  }

  Future<_DownloadBundle> _downloadAll(List<SalesDocument> docs) async {
    String? contract;
    String? receipt;
    var failed = false;
    for (final doc in docs) {
      try {
        final bytes = await SalesRepositoryFactory.instance.downloadDocument(_draft?.saleId ?? doc.documentId ?? 0, doc);
        final file = await SaleDocumentStorage.savePdf(doc.fileName, bytes);
        if (doc.isContract) {
          contract = file.path;
        } else if (doc.isPromissoryNote) {
          receipt = file.path;
        }
      } catch (_) {
        failed = true;
      }
    }
    if (docs.isEmpty) {
      failed = true;
    }
    return _DownloadBundle(contractPath: contract, receiptPath: receipt, failed: failed || contract == null || receipt == null);
  }

  Future<void> _openOrDownload(SalesDocument doc) async {
    try {
      final bytes = await SalesRepositoryFactory.instance.downloadDocument(_draft!.saleId, doc);
      final file = await SaleDocumentStorage.savePdf(doc.fileName, bytes);
      await OpenFilex.open(file.path);
    } catch (_) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('تعذر تنزيل المستند')));
    }
  }

  @override
  Widget build(BuildContext context) {
    final d = _draft;
    return Scaffold(
      appBar: AppBar(title: const Text('تفاصيل العملية')),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : _error != null && d == null
              ? Center(child: Text(_error!, style: const TextStyle(color: AppColors.danger)))
              : d == null
                  ? const SizedBox.shrink()
                  : ListView(
                      padding: const EdgeInsets.all(AppSpacing.md),
                      children: [
                        Text(d.fullName, style: const TextStyle(fontSize: 20, fontWeight: FontWeight.w700)),
                        Text(d.phone ?? ''),
                        Text(d.province ?? ''),
                        const SizedBox(height: AppSpacing.md),
                        Text('الحالة: ${SalesStatusLabels.of(d.status)}'),
                        Text('التقييم: ${EvaluationLabels.of(d.evaluationLevel)}'),
                        Text(d.isRejected ? 'سبب الرفض: ${d.evaluationNote}' : 'الملاحظة: ${d.evaluationNote}'),
                        const SizedBox(height: AppSpacing.md),
                        ...d.items.map((i) => Text('${i.productName ?? i.productId} × ${i.quantity}')),
                        const SizedBox(height: AppSpacing.md),
                        Text('السعر الأساسي: ${MoneyFormat.iqd(d.baseSalePrice)}'),
                        Text('السعر النهائي: ${MoneyFormat.iqd(d.finalSalePrice)}'),
                        Text('القسط اليومي: ${MoneyFormat.iqd(d.dailyInstallment)}'),
                        Text('التاريخ: ${d.createdAt}'),
                        if (_error != null)
                          Padding(
                            padding: const EdgeInsets.only(top: AppSpacing.sm),
                            child: Text(_error!, style: const TextStyle(color: AppColors.danger)),
                          ),
                        const SizedBox(height: AppSpacing.lg),
                        if (d.isRejected)
                          const Text('طلب مرفوض', style: TextStyle(color: AppColors.danger, fontWeight: FontWeight.w700))
                        else if (d.canComplete)
                          ElevatedButton(
                            onPressed: _completing ? null : _confirmComplete,
                            child: _completing
                                ? const SizedBox(height: 22, width: 22, child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white))
                                : const Text('تم البيع'),
                          )
                        else if (d.isCompleted) ...[
                          const Text('تم البيع', style: TextStyle(fontSize: 18, fontWeight: FontWeight.w800, color: AppColors.darkGreen)),
                          if (d.completedAt != null) Text('التاريخ: ${d.completedAt}'),
                          Text('السعر النهائي: ${MoneyFormat.iqd(d.finalSalePrice)}'),
                          const SizedBox(height: AppSpacing.md),
                          const Text('المستندات', style: TextStyle(fontWeight: FontWeight.w700)),
                          ..._documentTiles(d),
                        ],
                      ],
                    ),
    );
  }

  List<Widget> _documentTiles(SalesDraft d) {
    final docs = d.documents.isNotEmpty
        ? d.documents
        : [
            SalesDocument(type: 'Contract', fileName: 'عقد البيع', downloadUrl: ''),
            SalesDocument(type: 'PromissoryNote', fileName: 'وصل الأمانة', downloadUrl: ''),
          ];
    return [
      for (final doc in docs)
        Card(
          child: ListTile(
            title: Text(doc.isContract ? 'عقد البيع' : 'وصل الأمانة'),
            subtitle: Text(doc.fileName),
            trailing: Wrap(
              spacing: 8,
              children: [
                TextButton(onPressed: () => _openOrDownload(doc), child: const Text('فتح')),
                TextButton(onPressed: () => _openOrDownload(doc), child: const Text('تنزيل')),
              ],
            ),
          ),
        ),
    ];
  }
}

class _DownloadBundle {
  const _DownloadBundle({this.contractPath, this.receiptPath, required this.failed});
  final String? contractPath;
  final String? receiptPath;
  final bool failed;
}
