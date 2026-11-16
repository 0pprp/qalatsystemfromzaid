import 'package:sales_employee_application/data/archive_models.dart';
import 'package:sales_employee_application/services/api_client.dart';

class ArchiveApi {
  static Future<List<ArchiveBundle>> list() async {
    final raw = await ApiClient.get('sales/archive');
    final map = raw is Map ? Map<String, dynamic>.from(raw) : const <String, dynamic>{};
    final rows = map['bundles'] ?? map['Bundles'] ?? const [];
    if (rows is! List) return const [];
    return [
      for (final row in rows)
        if (row is Map) ArchiveBundle.fromJson(Map<String, dynamic>.from(row)),
    ];
  }

  /// مسؤول / مساعد مسؤول لجنة المراقبة of the agent's branch.
  static Future<List<ArchiveRecipient>> recipients() async {
    final raw = await ApiClient.get('sales/archive/recipients');
    final map = raw is Map ? Map<String, dynamic>.from(raw) : const <String, dynamic>{};
    final rows = map['recipients'] ?? map['Recipients'] ?? const [];
    if (rows is! List) return const [];
    return [
      for (final row in rows)
        if (row is Map) ArchiveRecipient.fromJson(Map<String, dynamic>.from(row)),
    ];
  }

  static Future<ArchiveBundle> getBundle(int bundleId) async {
    final raw = await ApiClient.get('sales/archive/$bundleId');
    return ArchiveBundle.fromJson(Map<String, dynamic>.from(raw as Map));
  }

  static Future<ArchiveBundle> send({
    required int bundleId,
    required String recipientName,
    required String note,
    List<({List<int> bytes, String name})> photos = const [],
  }) async {
    final raw = await ApiClient.postMultipart(
      'sales/archive/$bundleId/send',
      files: [
        for (final photo in photos)
          (bytes: photo.bytes, fileName: photo.name),
      ],
      fields: {
        'recipientName': recipientName,
        'intermediateName': '',
        'intermediatePhone': '',
        'note': note,
      },
    );
    return ArchiveBundle.fromJson(Map<String, dynamic>.from(raw as Map));
  }

  static Future<List<int>> photo(int bundleId) => ApiClient.getBytes('sales/archive/$bundleId/photo');
}
