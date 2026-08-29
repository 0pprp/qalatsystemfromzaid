class DelegateModel {
  final int delegateID;
  final String delegateName;
  final String address;
  final String phoneNumber;
  final String receiptName;
  final String asyncID;
  final String notes;

  DelegateModel({
    required this.delegateID,
    required this.delegateName,
    required this.address,
    required this.phoneNumber,
    required this.receiptName,
    required this.asyncID,
    required this.notes,
  });

  factory DelegateModel.fromJson(Map<String, dynamic> json) {
    return DelegateModel(
      delegateID: json['delegateID'] ?? 0,
      delegateName: json['delegateName'] ?? '',
      address: json['address'] ?? '',
      phoneNumber: json['phoneNumber'] ?? '',
      receiptName: json['receiptName'] ?? '',
      asyncID: json['asyncID'] ?? '',
      notes: json['notes'] ?? '',
    );
  }
}
