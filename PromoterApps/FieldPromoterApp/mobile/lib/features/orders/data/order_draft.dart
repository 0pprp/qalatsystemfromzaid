import '../../../core/utils/validators.dart';
import 'order_models.dart';

/// What the agent sold (fixed list; «اخرى» lets them type the name).
enum SaleProduct {
  iphone('ايفون'),
  bicycle('دراجة'),
  massageChair('كرسي مساج'),
  screen('شاشة'),
  other('اخرى');

  const SaleProduct(this.label);

  final String label;
}

/// Pure, testable state of the one-page sale order form. Sales are daily installments only.
/// Required: product, customer's full name (three names at least), phone, branch and address.
/// Optional: shop photo and GPS location.
class OrderDraft {
  SaleProduct? product;
  String otherProduct = '';

  String fullName = '';
  String phone = '';

  int? cityValue;
  String address = '';

  /// Local file of the shop photo (uploaded on submit). Optional.
  String? shopPhotoPath;
  double? latitude;
  double? longitude;

  /// The item name sent to the sales department.
  String get itemName => product == SaleProduct.other ? otherProduct.trim().split(RegExp(r'\s+')).join(' ') : product?.label ?? '';

  bool get hasPhoto => shopPhotoPath != null && shopPhotoPath!.isNotEmpty;
  bool get hasLocation => latitude != null && longitude != null;

  Map<String, String> validate() {
    final e = <String, String>{};
    if (product == null) {
      e['product'] = 'اختر المنتج';
    } else if (product == SaleProduct.other) {
      final n = otherProduct.trim();
      if (n.length < 2) {
        e['otherProduct'] = 'اكتب اسم المنتج';
      } else if (n.length > 100) {
        e['otherProduct'] = 'اسم المنتج طويل جداً';
      }
    }
    final names = fullName.trim().split(RegExp(r'\s+')).where((w) => w.isNotEmpty).toList();
    if (names.isEmpty) {
      e['fullName'] = 'اكتب اسم الزبون';
    } else if (names.length < 3) {
      e['fullName'] = 'اكتب الاسم الثلاثي على الأقل';
    } else if (!RegExp(r'^[ء-يٱ-ۓ ]+$').hasMatch(names.join(' '))) {
      e['fullName'] = 'اكتب الاسم بالحروف العربية';
    }
    final phoneError = Validators.phone(phone);
    if (phoneError != null) e['phone'] = phoneError;
    if (cityValue == null) e['cityValue'] = 'اختر الفرع';
    if (address.trim().length < 3) e['address'] = 'اكتب عنوان الزبون';
    return e;
  }

  bool get isValid => validate().isEmpty;

  CreateOrderRequest toRequest({String? shopPhotoUrl}) => CreateOrderRequest(
        cityValue: cityValue!,
        fullName: fullName.trim().split(RegExp(r'\s+')).join(' '),
        phone: Validators.normalizePhone(phone),
        address: OrderAddress(area: '', details: address.trim(), latitude: latitude, longitude: longitude),
        items: [OrderItem(name: itemName)],
        shopPhotoUrl: shopPhotoUrl,
      );
}
