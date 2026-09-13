class FilterCity {
  FilterCity({required this.cityValue, this.cityName});
  final String cityValue;
  final String? cityName;

  String get label => (cityName ?? '').trim().isEmpty ? cityValue : cityName!.trim();

  factory FilterCity.fromJson(Map<String, dynamic> json) => FilterCity(
        cityValue: '${json['cityValue'] ?? json['CityValue'] ?? ''}',
        cityName: json['cityName']?.toString() ?? json['CityName']?.toString(),
      );
}

class FilterSalesEmployee {
  FilterSalesEmployee({
    required this.employeeId,
    required this.employeeName,
    this.cityValue,
  });

  final int employeeId;
  final String employeeName;
  final String? cityValue;

  factory FilterSalesEmployee.fromJson(Map<String, dynamic> json) => FilterSalesEmployee(
        employeeId: int.tryParse('${json['employeeId'] ?? json['EmployeeId'] ?? 0}') ?? 0,
        employeeName: '${json['employeeName'] ?? json['EmployeeName'] ?? ''}',
        cityValue: json['cityValue']?.toString() ?? json['CityValue']?.toString(),
      );
}

class FilterRequest {
  FilterRequest({
    required this.id,
    required this.customerName,
    this.customerPhone,
    this.cityValue,
    this.cityName,
    this.customerProvince,
    this.customerAddress,
    this.wantedDescription,
    required this.filterStatus,
    this.filterNote,
    this.rejectReason,
  });

  final int id;
  final String customerName;
  final String? customerPhone;
  final String? cityValue;
  final String? cityName;
  final String? customerProvince;
  final String? customerAddress;
  final String? wantedDescription;
  final String filterStatus;
  final String? filterNote;
  final String? rejectReason;

  factory FilterRequest.fromJson(Map<String, dynamic> json) => FilterRequest(
        id: int.tryParse('${json['id'] ?? json['Id'] ?? 0}') ?? 0,
        customerName: '${json['customerName'] ?? json['CustomerName'] ?? ''}',
        customerPhone: json['customerPhone']?.toString() ?? json['CustomerPhone']?.toString(),
        cityValue: json['cityValue']?.toString() ?? json['CityValue']?.toString(),
        cityName: json['cityName']?.toString() ?? json['CityName']?.toString(),
        customerProvince: json['customerProvince']?.toString() ?? json['CustomerProvince']?.toString(),
        customerAddress: json['customerAddress']?.toString() ?? json['CustomerAddress']?.toString(),
        wantedDescription: json['wantedDescription']?.toString() ?? json['WantedDescription']?.toString(),
        filterStatus: '${json['filterStatus'] ?? json['FilterStatus'] ?? ''}',
        filterNote: json['filterNote']?.toString() ?? json['FilterNote']?.toString(),
        rejectReason: json['rejectReason']?.toString() ?? json['RejectReason']?.toString(),
      );
}
