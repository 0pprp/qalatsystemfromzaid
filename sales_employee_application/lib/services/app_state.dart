import 'package:flutter/foundation.dart';

class AppState extends ChangeNotifier {
  AppState._();
  static final AppState instance = AppState._();

  Map<String, dynamic>? selectedCustomer;
  int? ratingLevel;
  String ratingNotes = '';
  String rejectionReason = '';
  bool isNewSale = true;
  int homeIndex = 0;
  int? storeId;
  String storeName = '';
  List<dynamic> warehouseItems = [];
  List<dynamic> delegates = [];

  bool get isRejected => ratingLevel == 1;
  bool get isDoublePrice => ratingLevel == 2;
  double get priceMultiplier => isDoublePrice ? 2 : 1;

  String get ratingLabel {
    switch (ratingLevel) {
      case 1:
        return 'مرفوض';
      case 2:
        return 'مقبول ×2';
      case 3:
        return 'مستوى 3';
      case 4:
        return 'مستوى 4';
      case 5:
        return 'مستوى 5';
      default:
        return 'بدون تقييم';
    }
  }

  void selectCustomer(Map<String, dynamic> customer, {bool previousSale = true}) {
    selectedCustomer = customer;
    isNewSale = !previousSale;
    notifyListeners();
  }

  void setRating({
    required int level,
    required String notes,
    String reason = '',
  }) {
    ratingLevel = level;
    ratingNotes = notes;
    rejectionReason = reason;
    notifyListeners();
  }

  void goTo(int index) {
    homeIndex = index.clamp(0, 3);
    notifyListeners();
  }

  void setWarehouse({
    required int storeId,
    required String storeName,
    required List<dynamic> items,
    List<dynamic> delegates = const [],
  }) {
    this.storeId = storeId;
    this.storeName = storeName;
    warehouseItems = items;
    if (delegates.isNotEmpty) {
      this.delegates = delegates;
    }
    notifyListeners();
  }

  void setDelegates(List<dynamic> list) {
    delegates = list;
    notifyListeners();
  }

  void clearCustomer() {
    selectedCustomer = null;
    isNewSale = true;
    notifyListeners();
  }
}
