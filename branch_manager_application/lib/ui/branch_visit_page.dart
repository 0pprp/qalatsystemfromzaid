import 'package:branch_manager_application/theme/bm_theme.dart';
import 'package:flutter/material.dart';
import 'package:intl/intl.dart' hide TextDirection;

/// Shown right after opening a province — arrival, members, assigned tasks.
/// Delegate 0–10 ratings happen on leave (مغادرة الفرع), not here.
class BranchVisitPage extends StatelessWidget {
  const BranchVisitPage({
    super.key,
    required this.visit,
    required this.onContinue,
  });

  final Map<String, dynamic> visit;
  final VoidCallback onContinue;

  @override
  Widget build(BuildContext context) {
    final arrived = DateTime.tryParse(
            '${visit['arrivedAtUtc'] ?? visit['ArrivedAtUtc'] ?? ''}')
        ?.toLocal();
    final city = '${visit['cityName'] ?? visit['CityName'] ?? ''}';
    final members =
        (visit['members'] ?? visit['Members'] ?? []) as List? ?? [];
    final assigned =
        (visit['assignedTasks'] ?? visit['AssignedTasks'] ?? []) as List? ??
            [];
    // Only en_US date symbols ship without initializeDateFormatting().
    final fmt = DateFormat('yyyy/MM/dd — hh:mm a', 'en_US');

    return PopScope(
      canPop: false,
      child: Directionality(
        textDirection: TextDirection.rtl,
        child: Scaffold(
          appBar: AppBar(
            title: const Text('استمارة دخول الفرع',
                style: TextStyle(fontFamily: 'Cairo')),
            automaticallyImplyLeading: false,
          ),
          body: ListView(
            padding: const EdgeInsets.all(16),
            children: [
              Card(
                child: ListTile(
                  leading: const Icon(Icons.login, color: BmTheme.primary),
                  title: Text(
                    city.isEmpty ? 'المحافظة' : city,
                    style: const TextStyle(
                        fontFamily: 'Cairo', fontWeight: FontWeight.bold),
                  ),
                  subtitle: Text(
                    arrived == null
                        ? 'وقت الوصول: —'
                        : 'وقت الوصول: ${fmt.format(arrived)}',
                    style: const TextStyle(fontFamily: 'Cairo'),
                  ),
                ),
              ),
              const SizedBox(height: 12),
              Text(
                'أعضاء اللجنة الحاضرون (${members.length})',
                style: const TextStyle(
                    fontFamily: 'Cairo',
                    fontWeight: FontWeight.w700,
                    fontSize: 16),
              ),
              const SizedBox(height: 8),
              if (members.isEmpty)
                const Text('لا توجد حسابات مكلّفة بهذه المحافظة',
                    style: TextStyle(fontFamily: 'Cairo', color: Colors.grey))
              else
                ...members.map((m) {
                  final map = Map<String, dynamic>.from(m as Map);
                  return ListTile(
                    dense: true,
                    leading: const Icon(Icons.person_outline),
                    title: Text(
                      '${map['displayName'] ?? map['DisplayName'] ?? '—'}',
                      style: const TextStyle(fontFamily: 'Cairo'),
                    ),
                  );
                }),
              const Divider(height: 28),
              const Text(
                'المهام المكلَّفة في المحافظة',
                style: TextStyle(
                    fontFamily: 'Cairo',
                    fontWeight: FontWeight.w700,
                    fontSize: 16),
              ),
              const SizedBox(height: 8),
              if (assigned.isEmpty)
                const Text('لا توجد مهام مكلَّفة حالياً',
                    style: TextStyle(fontFamily: 'Cairo', color: Colors.grey))
              else
                ...assigned.map((t) {
                  final map = Map<String, dynamic>.from(t as Map);
                  return ListTile(
                    leading: const Icon(Icons.assignment_outlined),
                    title: Text('${map['title'] ?? map['Title'] ?? 'مهمة'}',
                        style: const TextStyle(fontFamily: 'Cairo')),
                    subtitle: Text('${map['status'] ?? map['Status'] ?? ''}',
                        style: const TextStyle(
                            fontFamily: 'Cairo', fontSize: 12)),
                  );
                }),
              const SizedBox(height: 16),
              const Text(
                'تقييم المندوبين يتم عند مغادرة الفرع.',
                textAlign: TextAlign.center,
                style: TextStyle(
                    fontFamily: 'Cairo', fontSize: 13, color: Colors.black54),
              ),
              const SizedBox(height: 20),
              FilledButton(
                onPressed: onContinue,
                style: FilledButton.styleFrom(
                  backgroundColor: BmTheme.primary,
                  padding: const EdgeInsets.symmetric(vertical: 14),
                ),
                child: const Text(
                  'متابعة العمل في المحافظة',
                  style: TextStyle(
                      fontFamily: 'Cairo', fontWeight: FontWeight.bold),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
