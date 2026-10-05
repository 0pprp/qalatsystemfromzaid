import 'package:delegate_application/client.dart';

/// Splits the delegate customer list into:
/// - «المبيعات الجديدة»: sale on the list with no first delegate تسديد yet
/// - «الأسماء التي في القائمة»: everyone else (routine installment names)
class NewSaleListLogic {
  /// True when this customer is a new sale awaiting the delegate's first تسديد.
  ///
  /// Store down-payment (مقدمة) alone must NOT remove them from المبيعات الجديدة.
  /// Only local تسديد by this delegate, a later LastPaymentDate, or a second
  /// ledger payment (مقدمة + تسديد) moves them to the regular list + evaluation.
  static bool isNewSaleAwaitingFirstCollection(
    Client client, {
    required Set<int> locallyCollectedCustomerIds,
  }) {
    if (client.customerId <= 0) return false;
    if (client.remaining <= 0) return false;
    if (client.dateSaleDevice.trim().isEmpty) return false;
    if (locallyCollectedCustomerIds.contains(client.customerId)) return false;
    if (_hasServerCollectionEvidence(client)) return false;
    return true;
  }

  static bool _hasServerCollectionEvidence(Client client) {
    // Two+ ledger payments ⇒ مقدمة (if any) plus at least one real تسديد.
    // A single payment on sale day is treated as store down-payment only.
    final paymentCount = _paymentCount(client.countReceiptDevice);
    if (paymentCount >= 2) return true;

    // LastPaymentDate strictly after sale day ⇒ someone already collected.
    final sale = _tryParseDay(client.dateSaleDevice);
    final last = _tryParseDay(client.lastPaymentDate);
    if (sale != null && last != null && last.isAfter(sale)) {
      return true;
    }

    // Do NOT treat ReceiptsTotal / week Amount1..7 alone as evidence:
    // new sales with مقدمة fill those on the sale day and must stay here.
    return false;
  }

  static int _paymentCount(String raw) {
    final t = raw.trim();
    if (t.isEmpty) return 0;
    return int.tryParse(t) ?? 0;
  }

  static DateTime? _tryParseDay(String raw) {
    final t = raw.trim();
    if (t.isEmpty) return null;
    try {
      final d = DateTime.parse(t);
      return DateTime(d.year, d.month, d.day);
    } catch (_) {
      return null;
    }
  }

  static List<Client> newSales(
    List<Client> clients, {
    required Set<int> locallyCollectedCustomerIds,
  }) {
    return clients
        .where(
          (c) => isNewSaleAwaitingFirstCollection(
            c,
            locallyCollectedCustomerIds: locallyCollectedCustomerIds,
          ),
        )
        .toList();
  }

  static List<Client> listNames(
    List<Client> clients, {
    required Set<int> locallyCollectedCustomerIds,
  }) {
    return clients
        .where(
          (c) => !isNewSaleAwaitingFirstCollection(
            c,
            locallyCollectedCustomerIds: locallyCollectedCustomerIds,
          ),
        )
        .toList();
  }
}
