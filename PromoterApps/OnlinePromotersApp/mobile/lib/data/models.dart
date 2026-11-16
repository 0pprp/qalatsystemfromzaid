import 'package:flutter/material.dart';

import '../core/network/json.dart';
import '../core/theme/app_theme.dart';

class Me {
  const Me({
    required this.id,
    required this.username,
    required this.phone,
    required this.isFull,
    required this.canCreateAccounts,
    this.createdAt,
  });

  final int id;
  final String username;
  final String phone;
  final bool isFull;
  final bool canCreateAccounts;
  final DateTime? createdAt;

  factory Me.fromJson(Json j) => Me(
        id: asInt(j['id']),
        username: asString(j['username']),
        phone: asString(j['phone']),
        isFull: asBool(j['isFull']),
        canCreateAccounts: asBool(j['canCreateAccounts']),
        createdAt: asDate(j['createdAt']),
      );

  Json toJson() => {
        'id': id,
        'username': username,
        'phone': phone,
        'isFull': isFull,
        'canCreateAccounts': canCreateAccounts,
        'createdAt': createdAt?.toIso8601String(),
      };
}

class Product {
  const Product({required this.id, required this.name, required this.price, required this.myCommission});

  final int id;
  final String name;
  final int price;
  final int myCommission;

  factory Product.fromJson(Json j) =>
      Product(id: asInt(j['id']), name: asString(j['name']), price: asInt(j['price']), myCommission: asInt(j['myCommission']));
}

class Branch {
  const Branch(this.cityValue, this.name);

  final int cityValue;
  final String name;

  factory Branch.fromJson(Json j) => Branch(asInt(j['cityValue']), asString(j['name']));
}

/// Order statuses and how they look in the app.
abstract final class OrderStatuses {
  static const open = 'New,Transferred,FieldInspection';
  static const sold = 'Sold';
  static const closed = 'NotSold,Cancelled';

  static Color color(String s) => switch (s) {
        'Sold' => AppColors.success,
        'NotSold' => AppColors.danger,
        'Cancelled' => AppColors.textMuted,
        'FieldInspection' => AppColors.purple,
        'Transferred' => AppColors.info,
        _ => AppColors.warning,
      };

  static IconData icon(String s) => switch (s) {
        'Sold' => Icons.check_circle_rounded,
        'NotSold' => Icons.cancel_rounded,
        'Cancelled' => Icons.block_rounded,
        'FieldInspection' => Icons.manage_search_rounded,
        'Transferred' => Icons.send_rounded,
        _ => Icons.schedule_rounded,
      };
}

class OrderSummary {
  const OrderSummary({
    required this.id,
    required this.orderNumber,
    required this.status,
    required this.statusLabel,
    required this.productName,
    required this.quantity,
    required this.customerName,
    required this.cityName,
    required this.myCommission,
    this.createdAt,
    this.soldAt,
  });

  final int id;
  final String orderNumber;
  final String status;
  final String statusLabel;
  final String productName;
  final int quantity;
  final String customerName;
  final String cityName;
  final int myCommission;
  final DateTime? createdAt;
  final DateTime? soldAt;

  factory OrderSummary.fromJson(Json j) => OrderSummary(
        id: asInt(j['id']),
        orderNumber: asString(j['orderNumber']),
        status: asString(j['status']),
        statusLabel: asString(j['statusLabel']),
        productName: asString(j['productName']),
        quantity: asInt(j['quantity'], 1),
        customerName: asString(j['customerName']),
        cityName: asString(j['cityName']),
        myCommission: asInt(j['myCommission']),
        createdAt: asDate(j['createdAt']),
        soldAt: asDate(j['soldAt']),
      );
}

class TimelineItem {
  const TimelineItem(this.status, this.label, this.at, this.note);

  final String status;
  final String label;
  final DateTime? at;
  final String? note;

  factory TimelineItem.fromJson(Json j) =>
      TimelineItem(asString(j['status']), asString(j['statusLabel']), asDate(j['at']), asStringOrNull(j['note']));
}

class OrderDetail {
  const OrderDetail({
    required this.summary,
    required this.unitPrice,
    required this.totalPrice,
    required this.customerPhone,
    required this.address,
    this.notes,
    this.publicReason,
    required this.timeline,
    required this.canCancel,
  });

  final OrderSummary summary;
  final int unitPrice;
  final int totalPrice;
  final String customerPhone;
  final String address;
  final String? notes;
  final String? publicReason;
  final List<TimelineItem> timeline;
  final bool canCancel;

  factory OrderDetail.fromJson(Json j) => OrderDetail(
        summary: OrderSummary.fromJson(j),
        unitPrice: asInt(j['unitPrice']),
        totalPrice: asInt(j['totalPrice']),
        customerPhone: asString(j['customerPhone']),
        address: asString(j['address']),
        notes: asStringOrNull(j['notes']),
        publicReason: asStringOrNull(j['publicReason']),
        timeline: asList(j['timeline'], TimelineItem.fromJson),
        canCancel: asBool(j['canCancel']),
      );
}

class Overview {
  const Overview({
    required this.balance,
    required this.earnedThisMonth,
    required this.ordersThisMonth,
    required this.soldThisMonth,
    required this.openOrders,
    required this.teamCount,
    required this.unreadNotifications,
    required this.recentOrders,
  });

  final int balance;
  final int earnedThisMonth;
  final int ordersThisMonth;
  final int soldThisMonth;
  final int openOrders;
  final int teamCount;
  final int unreadNotifications;
  final List<OrderSummary> recentOrders;

  factory Overview.fromJson(Json j) => Overview(
        balance: asInt(j['balance']),
        earnedThisMonth: asInt(j['earnedThisMonth']),
        ordersThisMonth: asInt(j['ordersThisMonth']),
        soldThisMonth: asInt(j['soldThisMonth']),
        openOrders: asInt(j['openOrders']),
        teamCount: asInt(j['teamCount']),
        unreadNotifications: asInt(j['unreadNotifications']),
        recentOrders: asList(j['recentOrders'], OrderSummary.fromJson),
      );
}

class WalletSummary {
  const WalletSummary({
    required this.balance,
    required this.totalEarned,
    required this.earnedThisMonth,
    required this.totalWithdrawn,
    required this.pendingWithdrawal,
    required this.minPayout,
  });

  final int balance;
  final int totalEarned;
  final int earnedThisMonth;
  final int totalWithdrawn;
  final int pendingWithdrawal;
  final int minPayout;

  factory WalletSummary.fromJson(Json j) => WalletSummary(
        balance: asInt(j['balance']),
        totalEarned: asInt(j['totalEarned']),
        earnedThisMonth: asInt(j['earnedThisMonth']),
        totalWithdrawn: asInt(j['totalWithdrawn']),
        pendingWithdrawal: asInt(j['pendingWithdrawal']),
        minPayout: asInt(j['minPayout']),
      );
}

class WalletEntry {
  const WalletEntry({
    required this.id,
    required this.amount,
    required this.type,
    required this.typeLabel,
    this.note,
    this.orderId,
    this.orderNumber,
    this.productName,
    this.fromUsername,
    this.payoutId,
    this.createdAt,
  });

  final int id;
  final int amount;
  final String type;
  final String typeLabel;
  final String? note;
  final int? orderId;
  final String? orderNumber;
  final String? productName;
  final String? fromUsername;
  final int? payoutId;
  final DateTime? createdAt;

  String get details {
    final parts = <String>[
      ?productName,
      if (fromUsername != null) 'من $fromUsername',
      ?note,
    ];
    return parts.join(' · ');
  }

  IconData get icon => switch (type) {
        'Sale' => Icons.sell_rounded,
        'Team' => Icons.groups_rounded,
        'SaleReversal' || 'TeamReversal' => Icons.undo_rounded,
        'Payout' => Icons.outbox_rounded,
        'PayoutReturned' => Icons.replay_rounded,
        _ => Icons.tune_rounded,
      };

  factory WalletEntry.fromJson(Json j) => WalletEntry(
        id: asInt(j['id']),
        amount: asInt(j['amount']),
        type: asString(j['type']),
        typeLabel: asString(j['typeLabel']),
        note: asStringOrNull(j['note']),
        orderId: asIntOrNull(j['orderId']),
        orderNumber: asStringOrNull(j['orderNumber']),
        productName: asStringOrNull(j['productName']),
        fromUsername: asStringOrNull(j['fromUsername']),
        payoutId: asIntOrNull(j['payoutId']),
        createdAt: asDate(j['createdAt']),
      );
}

class Payout {
  const Payout({
    required this.id,
    required this.amount,
    required this.method,
    required this.methodLabel,
    required this.accountNumber,
    required this.status,
    required this.statusLabel,
    this.adminNote,
    this.decidedAt,
    this.createdAt,
    this.unreadMessages = 0,
    this.hasReceipt = false,
  });

  final int id;
  final int amount;
  final String method;
  final String methodLabel;
  final String accountNumber;
  final String status;
  final String statusLabel;
  final String? adminNote;
  final DateTime? decidedAt;
  final DateTime? createdAt;
  final int unreadMessages;

  /// The dashboard attached a picture of the transfer receipt (paid requests).
  final bool hasReceipt;

  bool get isPending => status == 'Pending';

  Color get color => switch (status) { 'Paid' => AppColors.success, 'Rejected' => AppColors.danger, _ => AppColors.warning };

  factory Payout.fromJson(Json j) => Payout(
        id: asInt(j['id']),
        amount: asInt(j['amount']),
        method: asString(j['method']),
        methodLabel: asString(j['methodLabel']),
        accountNumber: asString(j['accountNumber']),
        status: asString(j['status']),
        statusLabel: asString(j['statusLabel']),
        adminNote: asStringOrNull(j['adminNote']),
        decidedAt: asDate(j['decidedAt']),
        createdAt: asDate(j['createdAt']),
        unreadMessages: asInt(j['unreadMessages']),
        hasReceipt: asBool(j['hasReceipt']),
      );
}

class ChatMessage {
  const ChatMessage({
    required this.id,
    required this.fromAdmin,
    required this.senderName,
    required this.body,
    this.createdAt,
    this.attachmentName,
    this.attachmentKind,
    this.attachmentSize,
  });

  final int id;
  final bool fromAdmin;
  final String senderName;
  final String body;
  final DateTime? createdAt;

  /// A photo ("image") or document ("file") sent with the message.
  final String? attachmentName;
  final String? attachmentKind;
  final int? attachmentSize;

  bool get hasImage => attachmentKind == 'image';
  bool get hasFile => attachmentKind == 'file';

  String get sizeLabel {
    final n = attachmentSize ?? 0;
    return n < 1024 * 1024 ? '${(n / 1024).ceil()} KB' : '${(n / 1024 / 1024).toStringAsFixed(1)} MB';
  }

  factory ChatMessage.fromJson(Json j) => ChatMessage(
        id: asInt(j['id']),
        fromAdmin: asBool(j['fromAdmin']),
        senderName: asString(j['senderName']),
        body: asString(j['body']),
        createdAt: asDate(j['createdAt']),
        attachmentName: asStringOrNull(j['attachmentName']),
        attachmentKind: asStringOrNull(j['attachmentKind']),
        attachmentSize: asIntOrNull(j['attachmentSize']),
      );
}

class TeamMember {
  const TeamMember({
    required this.id,
    required this.username,
    required this.phone,
    required this.cutPercent,
    required this.isFull,
    required this.isSuspended,
    required this.ordersCount,
    required this.soldCount,
    required this.openCount,
    required this.earnedFromMember,
    this.createdAt,
    this.lastSaleAt,
  });

  final int id;
  final String username;
  final String phone;
  final double cutPercent;
  final bool isFull;
  final bool isSuspended;
  final int ordersCount;
  final int soldCount;
  final int openCount;
  final int earnedFromMember;
  final DateTime? createdAt;
  final DateTime? lastSaleAt;

  factory TeamMember.fromJson(Json j) => TeamMember(
        id: asInt(j['id']),
        username: asString(j['username']),
        phone: asString(j['phone']),
        cutPercent: asDoubleOrNull(j['cutPercent']) ?? 0,
        isFull: asBool(j['isFull']),
        isSuspended: asBool(j['isSuspended']),
        ordersCount: asInt(j['ordersCount']),
        soldCount: asInt(j['soldCount']),
        openCount: asInt(j['openCount']),
        earnedFromMember: asInt(j['earnedFromMember']),
        createdAt: asDate(j['createdAt']),
        lastSaleAt: asDate(j['lastSaleAt']),
      );
}

class Team {
  const Team(this.items, this.totalEarned, this.canCreateAccounts);

  final List<TeamMember> items;
  final int totalEarned;
  final bool canCreateAccounts;

  factory Team.fromJson(Json j) =>
      Team(asList(j['items'], TeamMember.fromJson), asInt(j['totalEarned']), asBool(j['canCreateAccounts']));
}

class MarketingMaterial {
  const MarketingMaterial({
    required this.id,
    required this.title,
    required this.kind,
    required this.url,
    required this.fileName,
    required this.contentType,
    required this.sizeBytes,
    this.createdAt,
  });

  final int id;
  final String title;
  final String kind; // image | video | pdf
  final String url;
  final String fileName;
  final String contentType;
  final int sizeBytes;
  final DateTime? createdAt;

  bool get isImage => kind == 'image';
  bool get isVideo => kind == 'video';
  bool get isPdf => kind == 'pdf';

  String get extension {
    final dot = url.lastIndexOf('.');
    return dot < 0 ? (isPdf ? '.pdf' : isVideo ? '.mp4' : '.jpg') : url.substring(dot);
  }

  String get sizeLabel => sizeBytes < 1024 * 1024
      ? '${(sizeBytes / 1024).ceil()} KB'
      : '${(sizeBytes / 1024 / 1024).toStringAsFixed(1)} MB';

  factory MarketingMaterial.fromJson(Json j) => MarketingMaterial(
        id: asInt(j['id']),
        title: asString(j['title']),
        kind: asString(j['kind']),
        url: asString(j['url']),
        fileName: asString(j['fileName']),
        contentType: asString(j['contentType']),
        sizeBytes: asInt(j['sizeBytes']),
        createdAt: asDate(j['createdAt']),
      );
}

class AppNotification {
  const AppNotification({required this.id, required this.type, required this.title, required this.body, this.data = const {}, this.readAt, this.createdAt});

  final int id;
  final String type;
  final String title;
  final String body;
  final Json data;
  final DateTime? readAt;
  final DateTime? createdAt;

  bool get isRead => readAt != null;

  factory AppNotification.fromJson(Json j) => AppNotification(
        id: asInt(j['id']),
        type: asString(j['type']),
        title: asString(j['title']),
        body: asString(j['body']),
        data: asJson(j['data']),
        readAt: asDate(j['readAt']),
        createdAt: asDate(j['createdAt']),
      );
}

class RemoteConfig {
  const RemoteConfig({this.minSupportedVersion = '0.0.0', this.latestVersion = '0.0.0', this.supportPhone, this.supportWhatsapp, this.apkUrl, this.minPayout = 5000});

  final String minSupportedVersion;
  final String latestVersion;
  final String? supportPhone;
  final String? supportWhatsapp;
  final String? apkUrl;
  final int minPayout;

  factory RemoteConfig.fromJson(Json j) => RemoteConfig(
        minSupportedVersion: asString(j['minSupportedVersion'], '0.0.0'),
        latestVersion: asString(j['latestVersion'], '0.0.0'),
        supportPhone: asStringOrNull(j['supportPhone']),
        supportWhatsapp: asStringOrNull(j['supportWhatsapp']),
        apkUrl: asStringOrNull(j['apkUrl']),
        minPayout: asInt(j['minPayout'], 5000),
      );
}
