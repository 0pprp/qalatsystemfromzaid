import 'dart:convert';

import 'package:follower_application/config/app_env.dart';
import 'package:http/http.dart' as http;

class CommitteeTask {
  const CommitteeTask({
    required this.id,
    required this.body,
  });

  final int id;
  final String body;

  String get preview {
    final compact = body.trim().replaceAll(RegExp(r'\s+'), ' ');
    if (compact.length <= 90) return compact;
    return '${compact.substring(0, 90)}…';
  }
}

class CommitteeTaskService {
  static Future<List<CommitteeTask>> fetch() async {
    try {
      final base = AppEnv.gatewayApiBase();
      final uri = Uri.parse('${base}mobile-committee-tasks/follower');
      final response = await http.get(uri).timeout(const Duration(seconds: 12));
      if (response.statusCode != 200) return const [];
      final decoded = jsonDecode(response.body);
      if (decoded is! List) return const [];
      final out = <CommitteeTask>[];
      for (final raw in decoded) {
        if (raw is! Map) continue;
        final id = int.tryParse('${raw['id'] ?? raw['Id'] ?? 0}') ?? 0;
        final body = '${raw['body'] ?? raw['Body'] ?? ''}'.trim();
        if (body.isEmpty) continue;
        out.add(CommitteeTask(id: id, body: body));
      }
      return out;
    } catch (_) {
      return const [];
    }
  }
}
