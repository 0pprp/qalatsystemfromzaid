import 'package:flutter/material.dart';

import '../../../core/network/json.dart';
import '../../../core/theme/app_theme.dart';

/// A company branch: each sale order is handled by the branch the agent picks.
class Branch {
  const Branch({required this.cityValue, required this.name});

  final int cityValue;
  final String name;

  factory Branch.fromJson(Json j) => Branch(cityValue: asInt(j['cityValue']), name: asString(j['name']));

  @override
  bool operator ==(Object other) => other is Branch && other.cityValue == cityValue;

  @override
  int get hashCode => cityValue.hashCode;
}

enum PurchaseType {
  cash('Cash', 'نقد', Icons.payments_outlined),
  dailyInstallments('DailyInstallments', 'أقساط يومية', Icons.calendar_month_outlined);

  const PurchaseType(this.api, this.label, this.icon);

  final String api;
  final String label;
  final IconData icon;

  static PurchaseType? fromApi(String? v) {
    for (final t in values) {
      if (t.api == v) return t;
    }
    return null;
  }
}

enum OrderStatus {
  newOrder('New', 'طلب جديد', AppColors.info, Icons.fiber_new_rounded),
  underReview('UnderReview', 'قيد المعالجة', AppColors.warning, Icons.manage_search_rounded),
  transferred('Transferred', 'في قسم المبيعات', AppColors.purple, Icons.support_agent_rounded),
  fieldInspection('FieldInspection', 'قيد الكشف الميداني', Color(0xFF00838F), Icons.directions_car_rounded),
  sold('Sold', 'تم البيع - قيد التسديد', AppColors.success, Icons.verified_rounded),
  finished('Finished', 'مكتمل التسديد', Color(0xFF2E7D32), Icons.task_alt_rounded),
  notSold('NotSold', 'لم يتم البيع', Color(0xFF8D6E63), Icons.do_not_disturb_on_outlined),
  denied('Denied', 'مرفوض', AppColors.danger, Icons.cancel_rounded),
  cancelled('Cancelled', 'ملغى', AppColors.textMuted, Icons.block_rounded);

  const OrderStatus(this.api, this.label, this.color, this.icon);

  final String api;
  final String label;
  final Color color;
  final IconData icon;

  static OrderStatus fromApi(String? value) {
    for (final s in values) {
      if (s.api.toLowerCase() == value?.toLowerCase()) return s;
    }
    return OrderStatus.newOrder;
  }

  bool get isOpen => this == newOrder || this == underReview || this == transferred || this == fieldInspection;
  bool get isFinal => !isOpen;
  bool get isNegative => this == notSold || this == denied || this == cancelled;
  bool get isSale => this == sold || this == finished;

  /// Shows `publicReason` for these statuses (contract §5).
  bool get hasPublicReason => this == denied || this == notSold;

  /// Main progress path shown in the timeline stepper.
  /// Orders go straight to the sales department, so there is no review step (old «قيد المعالجة» counts as new).
  static const List<OrderStatus> happyPath = [newOrder, transferred, fieldInspection, sold, finished];

  /// Position on [happyPath] (final negative statuses replace the last reached step).
  int get progressIndex => switch (this) {
        newOrder => 0,
        underReview => 0,
        transferred => 1,
        fieldInspection => 2,
        sold => 3,
        finished => 4,
        _ => -1,
      };
}

class TimelineEntry {
  const TimelineEntry({required this.status, required this.label, this.at, this.note});

  final OrderStatus status;
  final String label;
  final DateTime? at;
  final String? note;

  factory TimelineEntry.fromJson(Json j) {
    final status = OrderStatus.fromApi(asStringOrNull(j['status']));
    return TimelineEntry(
      status: status,
      label: asStringOrNull(j['statusLabel']) ?? status.label,
      at: asDate(j['at']),
      note: asStringOrNull(j['note']),
    );
  }
}

/// One stepper row: either reached (with time) or upcoming.
class TimelineStep {
  const TimelineStep({required this.status, required this.label, this.at, this.note, required this.reached, this.current = false});

  final OrderStatus status;
  final String label;
  final DateTime? at;
  final String? note;
  final bool reached;
  final bool current;
}

/// Builds the stepper: طلب جديد → في قسم المبيعات → قيد الكشف الميداني → (تم البيع | لم يتم البيع | مرفوض | ملغى).
List<TimelineStep> buildTimelineSteps(OrderStatus current, List<TimelineEntry> timeline) {
  TimelineEntry? find(OrderStatus s) {
    for (final e in timeline.reversed) {
      if (e.status == s) return e;
    }
    return null;
  }

  // Last happy-path step reached before a terminal negative status.
  var reachedIndex = current.progressIndex;
  if (reachedIndex < 0) {
    for (final e in timeline) {
      if (e.status.progressIndex > reachedIndex) reachedIndex = e.status.progressIndex;
    }
  }

  final steps = <TimelineStep>[];
  final path = current.isNegative
      ? [...OrderStatus.happyPath.take(reachedIndex + 1), current]
      : OrderStatus.happyPath;
  for (var i = 0; i < path.length; i++) {
    final s = path[i];
    final entry = find(s);
    final reached = current.isNegative ? true : i <= reachedIndex;
    steps.add(TimelineStep(
      status: s,
      label: entry?.label ?? s.label,
      at: entry?.at,
      note: entry?.note,
      reached: reached,
      current: s == current,
    ));
  }
  return steps;
}

/// Payment progress of a sold order in the company system, until it is fully paid.
class SaleProgress {
  const SaleProgress({
    this.saleId,
    required this.total,
    required this.paid,
    required this.remaining,
    this.paymentsCount = 0,
    this.lastPaymentAt,
    required this.progressPercent,
    this.soldAt,
    this.finishedAt,
  });

  final int? saleId;
  final int total;
  final int paid;
  final int remaining;
  final int paymentsCount;
  final DateTime? lastPaymentAt;
  final int progressPercent;
  final DateTime? soldAt;
  final DateTime? finishedAt;

  factory SaleProgress.fromJson(Json j) => SaleProgress(
        saleId: asIntOrNull(j['saleId']),
        total: asInt(j['total']),
        paid: asInt(j['paid']),
        remaining: asInt(j['remaining']),
        paymentsCount: asInt(j['paymentsCount']),
        lastPaymentAt: asDate(j['lastPaymentAt']),
        progressPercent: asInt(j['progressPercent']).clamp(0, 100),
        soldAt: asDate(j['soldAt']),
        finishedAt: asDate(j['finishedAt']),
      );
}

class OrderSummary {
  const OrderSummary({
    required this.id,
    required this.orderNumber,
    required this.status,
    required this.statusLabel,
    required this.purchaseType,
    required this.customerName,
    this.cityName,
    required this.itemsSummary,
    this.createdAt,
    this.updatedAt,
    this.sale,
  });

  final int id;
  final String orderNumber;
  final OrderStatus status;
  final String statusLabel;
  final PurchaseType purchaseType;
  final String customerName;
  final String? cityName;
  final String itemsSummary;
  final DateTime? createdAt;
  final DateTime? updatedAt;
  final SaleProgress? sale;

  factory OrderSummary.fromJson(Json j) {
    final status = OrderStatus.fromApi(asStringOrNull(j['status']));
    return OrderSummary(
      id: asInt(j['id']),
      orderNumber: asString(j['orderNumber']),
      status: status,
      statusLabel: asStringOrNull(j['statusLabel']) ?? status.label,
      purchaseType: PurchaseType.fromApi(asStringOrNull(j['purchaseType'])) ?? PurchaseType.cash,
      customerName: asString(j['customerName']),
      cityName: asStringOrNull(j['cityName']),
      itemsSummary: asString(j['itemsSummary']),
      createdAt: asDate(j['createdAt']),
      updatedAt: asDate(j['updatedAt']),
      sale: j['sale'] is Map ? SaleProgress.fromJson(asJson(j['sale'])) : null,
    );
  }
}

class OrderCustomer {
  const OrderCustomer({
    required this.firstName,
    required this.fatherName,
    required this.grandfatherName,
    this.surname,
    required this.phone,
    this.secondaryPhone,
  });

  final String firstName;
  final String fatherName;
  final String grandfatherName;
  final String? surname;
  final String phone;
  final String? secondaryPhone;

  String get fullName => [firstName, fatherName, grandfatherName, surname ?? ''].where((e) => e.isNotEmpty).join(' ');

  factory OrderCustomer.fromJson(Json j) => OrderCustomer(
        firstName: asString(j['firstName']),
        fatherName: asString(j['fatherName']),
        grandfatherName: asString(j['grandfatherName']),
        surname: asStringOrNull(j['surname']),
        phone: asString(j['phone']),
        secondaryPhone: asStringOrNull(j['secondaryPhone']),
      );

  Json toJson() => {
        'firstName': firstName.trim(),
        'fatherName': fatherName.trim(),
        'grandfatherName': grandfatherName.trim(),
        'surname': (surname?.trim().isEmpty ?? true) ? null : surname!.trim(),
        'phone': phone,
        'secondaryPhone': (secondaryPhone?.trim().isEmpty ?? true) ? null : secondaryPhone,
      };
}

class OrderAddress {
  const OrderAddress({required this.area, required this.details, this.nearestLandmark, this.latitude, this.longitude});

  final String area;
  final String details;
  final String? nearestLandmark;
  final double? latitude;
  final double? longitude;

  factory OrderAddress.fromJson(Json j) => OrderAddress(
        area: asString(j['area']),
        details: asString(j['details']),
        nearestLandmark: asStringOrNull(j['nearestLandmark']),
        latitude: asDoubleOrNull(j['latitude']),
        longitude: asDoubleOrNull(j['longitude']),
      );

  Json toJson() => {
        'area': area.trim(),
        'details': details.trim(),
        'nearestLandmark': (nearestLandmark?.trim().isEmpty ?? true) ? null : nearestLandmark!.trim(),
        'latitude': latitude,
        'longitude': longitude,
      };
}

class OrderShop {
  const OrderShop({required this.name, required this.businessType, this.address, this.ownsShop = true});

  final String name;
  final String businessType;
  final String? address;
  final bool ownsShop;

  factory OrderShop.fromJson(Json j) => OrderShop(
        name: asString(j['name']),
        businessType: asString(j['businessType']),
        address: asStringOrNull(j['address']),
        ownsShop: asBool(j['ownsShop'], true),
      );

  Json toJson() => {
        'name': name.trim(),
        'businessType': businessType.trim(),
        'address': (address?.trim().isEmpty ?? true) ? null : address!.trim(),
        'ownsShop': ownsShop,
      };
}

/// What was sold, as the agent typed it.
class OrderItem {
  const OrderItem({required this.name, this.quantity = 1});

  final String name;
  final int quantity;

  factory OrderItem.fromJson(Json j) => OrderItem(name: asString(j['name']), quantity: asInt(j['quantity'], 1));

  Json toJson() => {'name': name.trim(), 'quantity': quantity};
}

class OrderDetail {
  const OrderDetail({
    required this.summary,
    this.customer,
    this.address,
    this.shop,
    this.notes,
    this.items = const [],
    this.publicReason,
    this.timeline = const [],
    this.canCancel = false,
    this.shopPhotoUrl,
  });

  final OrderSummary summary;
  final OrderCustomer? customer;
  final OrderAddress? address;
  final OrderShop? shop;
  final String? notes;
  final List<OrderItem> items;
  final String? publicReason;
  final List<TimelineEntry> timeline;
  final bool canCancel;
  final String? shopPhotoUrl;

  int get id => summary.id;
  SaleProgress? get sale => summary.sale;
  OrderStatus get status => summary.status;

  factory OrderDetail.fromJson(Json j) => OrderDetail(
        summary: OrderSummary.fromJson(j),
        customer: j['customer'] is Map ? OrderCustomer.fromJson(asJson(j['customer'])) : null,
        address: j['address'] is Map ? OrderAddress.fromJson(asJson(j['address'])) : null,
        shop: j['shop'] is Map ? OrderShop.fromJson(asJson(j['shop'])) : null,
        notes: asStringOrNull(j['notes']),
        items: asList(j['items'], OrderItem.fromJson),
        publicReason: asStringOrNull(j['publicReason']),
        timeline: asList(j['timeline'], TimelineEntry.fromJson),
        canCancel: asBool(j['canCancel']),
        shopPhotoUrl: asStringOrNull(j['shopPhotoUrl']),
      );
}

/// A new sale order: daily installments, one product, the customer, branch and address.
/// The shop photo and GPS location are optional.
class CreateOrderRequest {
  const CreateOrderRequest({
    required this.cityValue,
    required this.fullName,
    required this.phone,
    required this.address,
    required this.items,
    this.shopPhotoUrl,
    this.notes,
  });

  final int cityValue;
  final String fullName;
  final String phone;
  final OrderAddress address;
  final List<OrderItem> items;
  final String? shopPhotoUrl;
  final String? notes;

  Json toJson() => {
        'cityValue': cityValue,
        'purchaseType': PurchaseType.dailyInstallments.api,
        'customer': {'fullName': fullName.trim(), 'phone': phone},
        'address': address.toJson(),
        'items': [for (final i in items) i.toJson()],
        'shopPhotoUrl': shopPhotoUrl,
        'notes': (notes?.trim().isEmpty ?? true) ? null : notes!.trim(),
      };
}

/// «الرئيسية»: the agent's numbers and latest orders.
class AgentOverview {
  const AgentOverview({
    this.ordersToday = 0,
    this.ordersThisMonth = 0,
    this.soldThisMonth = 0,
    this.openOrders = 0,
    this.waitingPayment = 0,
    this.fullyPaid = 0,
    this.notSoldThisMonth = 0,
    this.pinsThisMonth = 0,
    this.recentOrders = const [],
    this.pinsTotal = 0,
    this.pinsByInterest = const {},
  });

  /// The agent's pinned shops, all time.
  final int pinsTotal;

  /// Interest level (1 … 5) → how many of the agent's pinned shops are at that level.
  final Map<int, int> pinsByInterest;

  final int ordersToday;
  final int ordersThisMonth;
  final int soldThisMonth;
  final int openOrders;
  final int waitingPayment;
  final int fullyPaid;
  final int notSoldThisMonth;
  final int pinsThisMonth;
  final List<OrderSummary> recentOrders;

  factory AgentOverview.fromJson(Json j) => AgentOverview(
        ordersToday: asInt(j['ordersToday']),
        ordersThisMonth: asInt(j['ordersThisMonth']),
        soldThisMonth: asInt(j['soldThisMonth']),
        openOrders: asInt(j['openOrders']),
        waitingPayment: asInt(j['waitingPayment']),
        fullyPaid: asInt(j['fullyPaid']),
        notSoldThisMonth: asInt(j['notSoldThisMonth']),
        pinsThisMonth: asInt(j['pinsThisMonth']),
        recentOrders: asList(j['recentOrders'], OrderSummary.fromJson),
        pinsTotal: asInt(j['pinsTotal']),
        pinsByInterest: {
          for (final l in (j['pinsByInterest'] as List? ?? const []))
            if (l is Map) asInt(l['interest']): asInt(l['count']),
        },
      );
}
