import 'package:sales_employee_application/data/sales_models.dart';
import 'package:sales_employee_application/services/official_contract/official_contract_data.dart';
import 'package:sales_employee_application/utils/iraqi_dinar_words.dart';

/// مصدر واحد لكل معلومة تظهر في العقد أو وصل الأمانة.
/// لا يُعاد إدخال القيمة في الواجهة إذا وُجدت في المصدر أدناه.
///
/// | حقل المستند | المصدر |
/// |---|---|
/// | customerName | Customer / SalesRequest / Sale.FullName |
/// | phone / whatsapp | Customer.phone — الواتساب نفس رقم الهاتف (لا حقل جديد) |
/// | nationalId | Customer.nationalCardNumber / Sale |
/// | province | Customer / SalesRequest / Sale.Province |
/// | address (قالب «العنوان وأقرب نقطة دالة») | Sale.Address + Sale.NearestLandmark |
/// | guarantorName | Sale.MukhtarName |
/// | rationCenterNumber | Sale.RationCenterNumber |
/// | goodsType | Sale.items |
/// | total / installment | Sale.FinalSalePrice / Sale.DailyInstallment |
/// | date | Sale.CompletedAt ?? CreatedAt |
/// | salesRepName | Sale.employeeName / UserName |
/// | cashierName | لا مصدر في مسار البيع — يبقى فارغاً كما في القالب |
/// | shop | SalesShopProfile — غير مطبوع في نص العقد الرسمي |
/// | customerList / evaluation | SaleDraft — غير مطبوعين في العقد |
class SaleDocumentMap {
  static OfficialContractData fromDraft(SalesDraft sale) {
    final total = sale.finalSalePrice;
    final installment = sale.dailyInstallment;
    return OfficialContractData(
      contractDate: sale.completedAt ?? sale.createdAt,
      customerName: sale.fullName,
      nationalId: sale.nationalCardNumber ?? '',
      province: sale.province ?? '',
      address: officialAddressField(sale.address, sale.nearestLandmark),
      phone: sale.phone ?? '',
      whatsapp: sale.phone ?? '',
      guarantorName: sale.mukhtarName ?? '',
      rationCenterNumber: sale.rationCenterNumber ?? '',
      goodsType: goodsType(sale.items),
      totalPriceNumeric: digitsOf(total),
      totalPriceWords: IraqiDinarWords.toArabic(total),
      dailyInstallmentNumeric: digitsOf(installment),
      dailyInstallmentWords: IraqiDinarWords.toArabic(installment),
      salesRepName: sale.employeeName ?? '',
      cashierName: '',
    );
  }

  static OfficialContractData fromLegacyMap(Map<String, dynamic> sale) {
    final items = (sale['contents'] as List?) ?? [];
    final goods = items.map((raw) {
      final item = raw is Map ? Map<String, dynamic>.from(raw) : <String, dynamic>{};
      return '${item['itemName'] ?? item['ItemName'] ?? ''} عدد ${item['quantity'] ?? item['Quantity'] ?? ''}';
    }).join('، ');
    final dateRaw = sale['dateCreate']?.toString();
    final total = _asNum(sale['amountPriceTotalFinal'] ?? sale['amountPriceTotal']);
    final installment = _asNum(sale['amountDayTotalFinal'] ?? sale['amountDayTotal']);
    final phone = '${sale['phoneNumber'] ?? sale['phone'] ?? ''}';
    return OfficialContractData(
      contractDate: DateTime.tryParse(dateRaw ?? '') ?? DateTime.now(),
      customerName: '${sale['customerName'] ?? ''}',
      nationalId: '${sale['nationalCardNumber'] ?? sale['cardNumber'] ?? ''}',
      province: '${sale['province'] ?? sale['address'] ?? ''}',
      address: officialAddressField(
        '${sale['address'] ?? ''}',
        '${sale['nearestFunctionPoint'] ?? sale['nearestLandmark'] ?? ''}',
      ),
      phone: phone,
      whatsapp: phone,
      guarantorName: '${sale['mukhtarName'] ?? sale['saleName'] ?? ''}',
      rationCenterNumber: '${sale['rationCenterNumber'] ?? ''}',
      goodsType: goods,
      totalPriceNumeric: digitsOf(total),
      totalPriceWords: IraqiDinarWords.toArabic(total),
      dailyInstallmentNumeric: digitsOf(installment),
      dailyInstallmentWords: IraqiDinarWords.toArabic(installment),
      salesRepName: sale['employeeName']?.toString() ?? '',
      cashierName: '',
    );
  }

  static String goodsType(List<SalesDraftItem> items) => items
      .map((i) => '${i.productName ?? i.productId} عدد ${i.quantity}')
      .join('، ');

  /// قالب الأداة الرسمية حقل واحد: «العنوان وأقرب نقطة دالة».
  static String officialAddressField(String? address, String? landmark) {
    final addr = address?.trim() ?? '';
    final point = landmark?.trim() ?? '';
    if (addr.isNotEmpty && point.isNotEmpty && addr != point) {
      return '$addr $point';
    }
    if (point.isNotEmpty) return point;
    return addr;
  }

  static String digitsOf(num value) => value.truncate().toString();

  static num _asNum(dynamic value) {
    if (value is num) return value;
    return num.tryParse('$value') ?? 0;
  }
}
