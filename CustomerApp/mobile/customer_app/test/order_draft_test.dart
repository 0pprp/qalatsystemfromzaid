import 'package:customer_app/features/orders/data/order_draft.dart';
import 'package:flutter_test/flutter_test.dart';

OrderDraft validDraft() => OrderDraft()
  ..product = SaleProduct.iphone
  ..fullName = 'علي حسين محمد'
  ..phone = '07701234567'
  ..cityValue = 1
  ..address = 'الكوفة - حي ميثم';

void main() {
  test('only product, full name, phone, branch and address are required', () {
    expect(validDraft().validate(), isEmpty);
    final e = OrderDraft().validate();
    expect(e.keys, containsAll(['product', 'fullName', 'phone', 'cityValue', 'address']));
    expect(e.containsKey('shopPhoto'), isFalse);
    expect(e.containsKey('latitude'), isFalse);
  });

  test('products come from the list; «اخرى» needs a typed name', () {
    final d = validDraft();
    expect(d.itemName, 'ايفون');
    d.product = SaleProduct.massageChair;
    expect(d.itemName, 'كرسي مساج');

    d.product = SaleProduct.other;
    expect(d.validate()['otherProduct'], isNotNull);
    d.otherProduct = '  ثلاجة   سامسونج ';
    expect(d.validate(), isEmpty);
    expect(d.itemName, 'ثلاجة سامسونج');
    expect(SaleProduct.values.map((p) => p.label), ['ايفون', 'دراجة', 'كرسي مساج', 'شاشة', 'اخرى']);
  });

  test('the customer name needs three Arabic names', () {
    final d = validDraft()..fullName = 'علي حسين';
    expect(d.validate()['fullName'], contains('الثلاثي'));
    d.fullName = 'Ali Hussein Mohammed';
    expect(d.validate()['fullName'], contains('العربية'));
    d.fullName = 'عبد الله حسين علي الكعبي';
    expect(d.validate(), isEmpty);
  });

  test('the request is daily installments with one item; photo and location only when added', () {
    final d = validDraft();
    var json = d.toRequest().toJson();
    expect(json['purchaseType'], 'DailyInstallments');
    expect(json['items'], [
      {'name': 'ايفون', 'quantity': 1},
    ]);
    expect(json['customer'], {'fullName': 'علي حسين محمد', 'phone': '07701234567'});
    expect((json['address'] as Map)['latitude'], isNull);
    expect(json['shopPhotoUrl'], isNull);

    d
      ..latitude = 32.02
      ..longitude = 44.33;
    json = d.toRequest(shopPhotoUrl: 'http://x/media/shops/a.jpg').toJson();
    expect((json['address'] as Map)['latitude'], 32.02);
    expect(json['shopPhotoUrl'], 'http://x/media/shops/a.jpg');
  });
}
