/// بيانات العقد ووصل الأمانة كما يستهلكها القالب الرسمي.
/// المصدر الوحيد لكل قيمة يُحدَّد في [SaleDocumentMap].
class OfficialContractData {
  const OfficialContractData({
    required this.contractDate,
    required this.customerName,
    required this.nationalId,
    required this.province,
    required this.address,
    required this.phone,
    required this.whatsapp,
    required this.guarantorName,
    required this.rationCenterNumber,
    required this.goodsType,
    required this.totalPriceNumeric,
    required this.totalPriceWords,
    required this.dailyInstallmentNumeric,
    required this.dailyInstallmentWords,
    required this.salesRepName,
    required this.cashierName,
  });

  final DateTime contractDate;
  final String customerName;
  final String nationalId;

  /// محافظة السكن في فقرة الطرف الثاني — من الزبون/الطلب/الفرع.
  final String province;

  /// حقل القالب الرسمي «العنوان وأقرب نقطة دالة» — يُطبع في العقد وفي الوصل.
  final String address;
  final String phone;
  final String whatsapp;
  final String guarantorName;
  final String rationCenterNumber;
  final String goodsType;

  /// رقم فقط أو بفواصل؛ القالب يعيد تنسيقه كما في الأداة المرجعية.
  final String totalPriceNumeric;
  final String totalPriceWords;
  final String dailyInstallmentNumeric;
  final String dailyInstallmentWords;
  final String salesRepName;
  final String cashierName;
}
