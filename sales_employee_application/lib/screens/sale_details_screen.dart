import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';
import 'package:open_filex/open_filex.dart';
import 'package:sales_employee_application/data/sales_models.dart';
import 'package:sales_employee_application/data/sales_repository_factory.dart';
import 'package:sales_employee_application/screens/sale_complete_success_screen.dart';
import 'package:sales_employee_application/services/api_client.dart';
import 'package:sales_employee_application/services/sale_document_storage.dart';
import 'package:sales_employee_application/services/sale_documents.dart';
import 'package:sales_employee_application/utils/app_theme.dart';
import 'package:sales_employee_application/utils/sales_format.dart';

class SaleDetailsScreen extends StatefulWidget {
  const SaleDetailsScreen({super.key, this.saleId});

  final int? saleId;

  @visibleForTesting
  static List<int>? debugShopImageBytes;

  @override
  State<SaleDetailsScreen> createState() => _SaleDetailsScreenState();
}

class _SaleDetailsScreenState extends State<SaleDetailsScreen> {
  SalesDraft? _draft;
  String? _error;
  bool _loading = true;
  bool _started = false;
  bool _completing = false;
  bool _previewing = false;
  bool _previewed = false;
  List<SalesDocument> _previewDocs = const [];
  _ShopFormResult? _shopDraft;

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

  Future<void> _startReview() async {
    final debugBytes = SaleDetailsScreen.debugShopImageBytes;
    if (debugBytes != null && debugBytes.isNotEmpty) {
      _shopDraft = _ShopFormResult(
        shopName: 'محل اختبار',
        shopBusinessType: 'مواد غذائية',
        stockValue: 1500000,
        dailyRevenue: 80000,
        length: 8,
        width: 5,
        imageBytes: List<int>.from(debugBytes),
        imageName: 'shop.jpg',
      );
      await _runPreview(_shopDraft!);
      return;
    }

    final result = await showDialog<_ShopFormResult>(
      context: context,
      barrierDismissible: false,
      builder: (ctx) => _ShopCompleteDialog(initial: _shopDraft),
    );
    if (result == null) return;
    _shopDraft = result;
    await _runPreview(result);
  }

  Future<void> _runPreview(_ShopFormResult shopForm) async {
    final draft = _draft;
    if (draft == null || _previewing || _completing) return;
    setState(() {
      _previewing = true;
      _error = null;
    });
    try {
      var imageKey = shopForm.imageKey;
      if (imageKey == null || imageKey.isEmpty) {
        imageKey = await SalesRepositoryFactory.instance.uploadShopImage(
          draft.saleId,
          shopForm.imageBytes,
          shopForm.imageName,
        );
        _shopDraft = shopForm.copyWith(imageKey: imageKey);
      }
      final shop = _shopPayload(_shopDraft!);
      final preview = await SalesRepositoryFactory.instance.previewDocuments(draft.saleId, shop);
      if (!mounted) return;
      setState(() {
        _previewing = false;
        _previewed = true;
        _previewDocs = preview.documents;
        _draft = draft.copyWith(documents: preview.documents);
      });
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() {
        _previewing = false;
        _error = e.message;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _previewing = false;
        _error = e.toString().replaceFirst('Exception: ', '');
      });
    }
  }

  SalesShopComplete _shopPayload(_ShopFormResult shopForm) => SalesShopComplete(
        shopName: shopForm.shopName,
        shopBusinessType: shopForm.shopBusinessType,
        shopStockEstimatedValue: shopForm.stockValue,
        estimatedDailyRevenue: shopForm.dailyRevenue,
        shopLength: shopForm.length,
        shopWidth: shopForm.width,
        shopImageKey: shopForm.imageKey ?? '',
        employeeNote: shopForm.employeeNote,
      );

  Future<void> _confirmComplete() async {
    final shop = _shopDraft;
    if (shop == null) {
      await _startReview();
      return;
    }
    await _complete(shop);
  }

  Future<void> _complete(_ShopFormResult shopForm) async {
    final draft = _draft;
    if (draft == null || _completing) return;
    setState(() => _completing = true);
    try {
      var imageKey = shopForm.imageKey;
      if (imageKey == null || imageKey.isEmpty) {
        imageKey = await SalesRepositoryFactory.instance.uploadShopImage(
          draft.saleId,
          shopForm.imageBytes,
          shopForm.imageName,
        );
        _shopDraft = shopForm.copyWith(imageKey: imageKey);
      }
      final shop = _shopPayload(_shopDraft!);
      final result = await SalesRepositoryFactory.instance.completeSale(draft.saleId, shop);
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
    try {
      final draft = _draft;
      if (draft != null && docs.isNotEmpty) {
        final contractFile = await SaleDocumentStorage.savePdf(
          'Sale_${draft.saleId}_Contract.pdf',
          await SaleDocuments.contractBytesFromDraft(draft),
        );
        final receiptFile = await SaleDocumentStorage.savePdf(
          'Sale_${draft.saleId}_PromissoryNote.pdf',
          await SaleDocuments.receiptBytesFromDraft(draft),
        );
        contract = contractFile.path;
        receipt = receiptFile.path;
      }
    } catch (_) {
      failed = true;
    }
    if (contract != null && receipt != null) {
      return _DownloadBundle(contractPath: contract, receiptPath: receipt, failed: false);
    }
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
      final draft = _draft;
      if (doc.documentId != null && (doc.downloadUrl.isNotEmpty || draft != null)) {
        final bytes = await SalesRepositoryFactory.instance.downloadDocument(draft?.saleId ?? 0, doc);
        final file = await SaleDocumentStorage.savePdf(doc.fileName, bytes);
        await OpenFilex.open(file.path);
        return;
      }
      if (draft != null) {
        final bytes = doc.isContract
            ? await SaleDocuments.contractBytesFromDraft(draft)
            : await SaleDocuments.receiptBytesFromDraft(draft);
        final file = await SaleDocumentStorage.savePdf(doc.fileName, bytes);
        await OpenFilex.open(file.path);
        return;
      }
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
                        const SizedBox(height: AppSpacing.md),
                        ...d.items.map((i) => Text('${i.productName ?? i.productId} × ${i.quantity}')),
                        const SizedBox(height: AppSpacing.md),
                        Text('السعر الافتراضي: ${MoneyFormat.iqd(d.defaultTotalSalePrice ?? d.baseSalePrice)}'),
                        Text('السعر النهائي: ${MoneyFormat.iqd(d.finalSalePrice)}'),
                        Text('القسط اليومي: ${MoneyFormat.iqd(d.dailyInstallment)}'),
                        Text('الدفعة المقدمة: ${MoneyFormat.iqd(d.downPayment)}'),
                        Text('التاريخ: ${d.createdAt}'),
                        if (_error != null)
                          Padding(
                            padding: const EdgeInsets.only(top: AppSpacing.sm),
                            child: Text(_error!, style: const TextStyle(color: AppColors.danger)),
                          ),
                        const SizedBox(height: AppSpacing.lg),
                        if (d.isRejected)
                          const Text('طلب مرفوض', style: TextStyle(color: AppColors.danger, fontWeight: FontWeight.w700))
                        else if (d.canComplete) ...[
                          if (_previewed) ...[
                            const Text('معاينة المستندات', style: TextStyle(fontWeight: FontWeight.w700)),
                            ..._documentTiles(d, docs: _previewDocs),
                            const SizedBox(height: AppSpacing.md),
                          ],
                          if (!_previewed)
                            ElevatedButton(
                              onPressed: _previewing || _completing ? null : _startReview,
                              child: _previewing
                                  ? const SizedBox(height: 22, width: 22, child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white))
                                  : const Text('مراجعة البيع'),
                            )
                          else
                            ElevatedButton(
                              onPressed: _completing ? null : _confirmComplete,
                              child: _completing
                                  ? const SizedBox(height: 22, width: 22, child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white))
                                  : const Text('تم البيع'),
                            ),
                        ]
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

  List<Widget> _documentTiles(SalesDraft d, {List<SalesDocument>? docs}) {
    final list = (docs != null && docs.isNotEmpty)
        ? docs
        : (d.documents.isNotEmpty
            ? d.documents
            : [
                SalesDocument(type: 'Contract', fileName: 'عقد البيع', downloadUrl: ''),
                SalesDocument(type: 'PromissoryNote', fileName: 'وصل الأمانة', downloadUrl: ''),
              ]);
    return [
      for (final doc in list)
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

class _ShopFormResult {
  const _ShopFormResult({
    required this.shopName,
    required this.shopBusinessType,
    required this.stockValue,
    required this.dailyRevenue,
    required this.length,
    required this.width,
    required this.imageBytes,
    required this.imageName,
    this.imageKey,
    this.employeeNote,
  });

  final String shopName;
  final String shopBusinessType;
  final num stockValue;
  final num dailyRevenue;
  final num length;
  final num width;
  final List<int> imageBytes;
  final String imageName;
  final String? imageKey;
  final String? employeeNote;

  _ShopFormResult copyWith({String? imageKey}) => _ShopFormResult(
        shopName: shopName,
        shopBusinessType: shopBusinessType,
        stockValue: stockValue,
        dailyRevenue: dailyRevenue,
        length: length,
        width: width,
        imageBytes: imageBytes,
        imageName: imageName,
        imageKey: imageKey ?? this.imageKey,
        employeeNote: employeeNote,
      );
}

class _ShopCompleteDialog extends StatefulWidget {
  const _ShopCompleteDialog({this.initial});

  final _ShopFormResult? initial;

  @override
  State<_ShopCompleteDialog> createState() => _ShopCompleteDialogState();
}

class _ShopCompleteDialogState extends State<_ShopCompleteDialog> {
  late final TextEditingController _name;
  late final TextEditingController _type;
  late final TextEditingController _stock;
  late final TextEditingController _daily;
  late final TextEditingController _length;
  late final TextEditingController _width;
  late final TextEditingController _note;
  List<int>? _imageBytes;
  String _imageName = '';
  String? _imageKey;
  String? _error;

  @override
  void initState() {
    super.initState();
    final initial = widget.initial;
    _name = TextEditingController(text: initial?.shopName ?? '');
    _type = TextEditingController(text: initial?.shopBusinessType ?? '');
    _stock = TextEditingController(text: initial == null ? '' : '${initial.stockValue}');
    _daily = TextEditingController(text: initial == null ? '' : '${initial.dailyRevenue}');
    _length = TextEditingController(text: initial == null ? '' : '${initial.length}');
    _width = TextEditingController(text: initial == null ? '' : '${initial.width}');
    _note = TextEditingController(text: initial?.employeeNote ?? '');
    _imageBytes = initial?.imageBytes;
    _imageName = initial?.imageName ?? '';
    _imageKey = initial?.imageKey;
    final debugBytes = SaleDetailsScreen.debugShopImageBytes;
    if ((_imageBytes == null || _imageBytes!.isEmpty) && debugBytes != null && debugBytes.isNotEmpty) {
      _imageBytes = List<int>.from(debugBytes);
      _imageName = _imageName.isEmpty ? 'shop.jpg' : _imageName;
    }
    _length.addListener(() => setState(() {}));
    _width.addListener(() => setState(() {}));
  }

  @override
  void dispose() {
    _name.dispose();
    _type.dispose();
    _stock.dispose();
    _daily.dispose();
    _length.dispose();
    _width.dispose();
    _note.dispose();
    super.dispose();
  }

  num? _num(TextEditingController c) => num.tryParse(c.text.trim().replaceAll(',', ''));

  num get _area {
    final length = _num(_length) ?? 0;
    final width = _num(_width) ?? 0;
    return length * width;
  }

  Future<void> _pickImage() async {
    final debugBytes = SaleDetailsScreen.debugShopImageBytes;
    if (debugBytes != null && debugBytes.isNotEmpty) {
      setState(() {
        _imageBytes = List<int>.from(debugBytes);
        _imageName = 'shop.jpg';
        _imageKey = null;
        _error = null;
      });
      return;
    }

    try {
      final picked = await ImagePicker().pickImage(
        source: ImageSource.gallery,
        imageQuality: 85,
        maxWidth: 1600,
      );
      if (picked == null) return;
      final bytes = await picked.readAsBytes();
      if (!mounted) return;
      setState(() {
        _imageBytes = bytes;
        _imageName = picked.name.isEmpty ? 'shop.jpg' : picked.name;
        _imageKey = null;
        _error = null;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() => _error = 'تعذر اختيار صورة المحل');
    }
  }

  void _submit() {
    final name = _name.text.trim();
    final type = _type.text.trim();
    final stock = _num(_stock);
    final daily = _num(_daily);
    final length = _num(_length);
    final width = _num(_width);
    var image = _imageBytes;
    if ((image == null || image.isEmpty) && SaleDetailsScreen.debugShopImageBytes != null) {
      image = List<int>.from(SaleDetailsScreen.debugShopImageBytes!);
    }
    if (name.isEmpty || type.isEmpty || stock == null || stock <= 0 || daily == null || daily <= 0 || length == null || length <= 0 || width == null || width <= 0) {
      setState(() => _error = 'أكمل كل حقول المحل المطلوبة.');
      return;
    }
    if ((image == null || image.isEmpty) && (_imageKey == null || _imageKey!.isEmpty)) {
      setState(() => _error = 'صورة المحل مطلوبة.');
      return;
    }
    Navigator.pop(
      context,
      _ShopFormResult(
        shopName: name,
        shopBusinessType: type,
        stockValue: stock,
        dailyRevenue: daily,
        length: length,
        width: width,
        imageBytes: image ?? const <int>[],
        imageName: _imageName.isEmpty ? 'shop.jpg' : _imageName,
        imageKey: _imageKey,
        employeeNote: _note.text.trim(),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: const Text('بيانات المحل قبل المراجعة'),
      content: SizedBox(
        width: 420,
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              TextField(
                key: const Key('shopName'),
                controller: _name,
                decoration: const InputDecoration(labelText: 'اسم المحل *'),
              ),
              TextField(
                key: const Key('shopBusinessType'),
                controller: _type,
                decoration: const InputDecoration(labelText: 'طبيعة عمل المحل *'),
              ),
              TextField(
                key: const Key('shopStockEstimatedValue'),
                controller: _stock,
                keyboardType: TextInputType.number,
                decoration: const InputDecoration(labelText: 'تقدير قيمة بضاعة المحل *'),
              ),
              TextField(
                key: const Key('estimatedDailyRevenue'),
                controller: _daily,
                keyboardType: TextInputType.number,
                decoration: const InputDecoration(labelText: 'تقدير الوارد اليومي *'),
              ),
              TextField(
                key: const Key('shopLength'),
                controller: _length,
                keyboardType: TextInputType.number,
                decoration: const InputDecoration(labelText: 'طول المحل بالمتر *'),
              ),
              TextField(
                key: const Key('shopWidth'),
                controller: _width,
                keyboardType: TextInputType.number,
                decoration: const InputDecoration(labelText: 'عرض المحل بالمتر *'),
              ),
              Align(
                alignment: Alignment.centerRight,
                child: Padding(
                  padding: const EdgeInsets.only(top: 8, bottom: 8),
                  child: Text(
                    'المساحة: ${_area.toStringAsFixed(_area % 1 == 0 ? 0 : 2)} م²',
                    key: const Key('shopArea'),
                  ),
                ),
              ),
              TextField(
                controller: _note,
                decoration: const InputDecoration(labelText: 'ملاحظة الموظف (اختياري)'),
              ),
              const SizedBox(height: 8),
              OutlinedButton(
                onPressed: _pickImage,
                child: const Text('اختيار صورة المحل'),
              ),
              if (_imageBytes != null && _imageBytes!.isNotEmpty) ...[
                const SizedBox(height: 8),
                Text(_imageName.isEmpty ? 'تم اختيار الصورة' : _imageName),
                if (SaleDetailsScreen.debugShopImageBytes == null) ...[
                  const SizedBox(height: 8),
                  Image.memory(
                    Uint8List.fromList(_imageBytes!),
                    height: 80,
                    fit: BoxFit.cover,
                    errorBuilder: (_, _, _) => const Text('تم اختيار الصورة'),
                  ),
                ],
              ],
              if (_error != null)
                Padding(
                  padding: const EdgeInsets.only(top: 8),
                  child: Text(_error!, style: const TextStyle(color: AppColors.danger)),
                ),
            ],
          ),
        ),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.pop(context), child: const Text('رجوع')),
        ElevatedButton(onPressed: _submit, child: const Text('متابعة للمراجعة')),
      ],
    );
  }
}

class _DownloadBundle {
  const _DownloadBundle({this.contractPath, this.receiptPath, required this.failed});
  final String? contractPath;
  final String? receiptPath;
  final bool failed;
}
