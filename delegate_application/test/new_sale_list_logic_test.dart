import 'package:delegate_application/client.dart';
import 'package:delegate_application/services/new_sale_list_logic.dart';
import 'package:flutter_test/flutter_test.dart';

Client _client({
  int id = 1,
  double remaining = 100000,
  double receipt = 0,
  String dateSale = '2026-09-20',
  String lastPay = '',
  double a1 = 0,
  String countReceipt = '',
}) {
  return Client(
    name: 'زبون',
    phone: '',
    address: '',
    shopName: '',
    salePrice: 500000,
    receipt: receipt,
    dailyInstallment: 10000,
    remaining: remaining,
    itemsNames: '',
    amount1: a1,
    amount2: 0,
    amount3: 0,
    amount4: 0,
    amount5: 0,
    amount6: 0,
    amount7: 0,
    phoneNumberCompany: '',
    countReceiptDevice: countReceipt,
    numberOfDayPayment: '',
    isLegal: '',
    lastPaymentDate: lastPay,
    dateSaleDevice: dateSale,
    customerId: id,
  );
}

void main() {
  test('new sale with no receipts stays in المبيعات الجديدة', () {
    final c = _client();
    expect(
      NewSaleListLogic.isNewSaleAwaitingFirstCollection(
        c,
        locallyCollectedCustomerIds: {},
      ),
      isTrue,
    );
  });

  test('after local تسديد moves out of المبيعات الجديدة', () {
    final c = _client();
    expect(
      NewSaleListLogic.isNewSaleAwaitingFirstCollection(
        c,
        locallyCollectedCustomerIds: {1},
      ),
      isFalse,
    );
  });

  test('مقدمة alone (ReceiptsTotal on sale day) stays in المبيعات الجديدة', () {
    final c = _client(
      receipt: 150000,
      a1: 150000,
      lastPay: '2026-09-20',
      countReceipt: '1',
    );
    expect(
      NewSaleListLogic.isNewSaleAwaitingFirstCollection(
        c,
        locallyCollectedCustomerIds: {},
      ),
      isTrue,
    );
  });

  test('week Amount1 alone does not move out (sale-day مقدمة)', () {
    final c = _client(a1: 5000, lastPay: '2026-09-20', countReceipt: '1');
    expect(
      NewSaleListLogic.isNewSaleAwaitingFirstCollection(
        c,
        locallyCollectedCustomerIds: {},
      ),
      isTrue,
    );
  });

  test('second ledger payment (مقدمة + تسديد) moves to القائمة', () {
    final c = _client(
      receipt: 170000,
      a1: 20000,
      lastPay: '2026-09-20',
      countReceipt: '2',
    );
    expect(
      NewSaleListLogic.isNewSaleAwaitingFirstCollection(
        c,
        locallyCollectedCustomerIds: {},
      ),
      isFalse,
    );
  });

  test('LastPaymentDate after sale day is not new-sale', () {
    final c = _client(
      dateSale: '2026-09-01',
      lastPay: '2026-09-10',
      receipt: 10000,
      countReceipt: '1',
    );
    expect(
      NewSaleListLogic.isNewSaleAwaitingFirstCollection(
        c,
        locallyCollectedCustomerIds: {},
      ),
      isFalse,
    );
  });

  test('zero remaining never listed as new sale', () {
    final c = _client(remaining: 0);
    expect(
      NewSaleListLogic.isNewSaleAwaitingFirstCollection(
        c,
        locallyCollectedCustomerIds: {},
      ),
      isFalse,
    );
  });

  test('split helpers partition list', () {
    final clients = [
      _client(id: 1),
      _client(
        id: 2,
        receipt: 150000,
        lastPay: '2026-09-20',
        countReceipt: '1',
      ),
      _client(id: 3),
      _client(
        id: 4,
        receipt: 170000,
        lastPay: '2026-09-20',
        countReceipt: '2',
      ),
    ];
    final neu = NewSaleListLogic.newSales(
      clients,
      locallyCollectedCustomerIds: {3},
    );
    final list = NewSaleListLogic.listNames(
      clients,
      locallyCollectedCustomerIds: {3},
    );
    expect(neu.map((c) => c.customerId), [1, 2]);
    expect(list.map((c) => c.customerId), [3, 4]);
  });
}
