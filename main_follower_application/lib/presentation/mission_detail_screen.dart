import 'package:flutter/material.dart';
import 'package:main_follower_application/core/config/province_labels.dart';
import 'package:main_follower_application/data/sales_repository.dart';
import 'package:main_follower_application/data/staff_repository.dart';
import 'package:main_follower_application/domain/branch_sale.dart';
import 'package:main_follower_application/domain/mission.dart';
import 'package:main_follower_application/domain/staff_member.dart';
import 'package:main_follower_application/presentation/city_hub_screen.dart';

class MissionDetailScreen extends StatefulWidget {
  const MissionDetailScreen({super.key, required this.mission});

  final Mission mission;

  @override
  State<MissionDetailScreen> createState() => _MissionDetailScreenState();
}

class _MissionDetailScreenState extends State<MissionDetailScreen> {
  late Future<List<SaleReview>> _saleReviews;
  late Future<List<StaffReview>> _staffReviews;

  @override
  void initState() {
    super.initState();
    _reload();
  }

  void _reload() {
    _saleReviews = SalesRepository.listReviews(widget.mission.id);
    _staffReviews = StaffRepository.listMissionReviews(widget.mission.id);
  }

  Future<void> _pickProvince() async {
    final m = widget.mission;
    if (m.cityValues.isEmpty) return;
    if (m.cityValues.length == 1) {
      await _openCity(m.cityValues.first);
      return;
    }
    final selected = await showModalBottomSheet<String>(
      context: context,
      builder: (ctx) => SafeArea(
        minimum: const EdgeInsets.only(bottom: 12),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Padding(
              padding: EdgeInsets.all(16),
              child: Text(
                'اختر المحافظة',
                style: TextStyle(fontWeight: FontWeight.bold, fontSize: 16),
              ),
            ),
            ...m.cityValues.map(
              (city) => ListTile(
                leading: const Icon(Icons.location_city_outlined),
                title: Text(ProvinceLabels.display(city)),
                onTap: () => Navigator.pop(ctx, city),
              ),
            ),
            const SizedBox(height: 8),
          ],
        ),
      ),
    );
    if (selected != null && mounted) await _openCity(selected);
  }

  Future<void> _openCity(String city) async {
    await Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => CityHubScreen(mission: widget.mission, cityValue: city),
      ),
    );
    if (mounted) setState(_reload);
  }

  @override
  Widget build(BuildContext context) {
    final m = widget.mission;
    return Scaffold(
      appBar: AppBar(
        title: Text(m.committeeName.isEmpty ? 'مهمة #${m.id}' : m.committeeName),
        backgroundColor: const Color(0xFF0F3D3E),
        foregroundColor: Colors.white,
      ),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(16, 16, 16, 32),
        children: [
          if (m.note != null && m.note!.isNotEmpty)
            Text('ملاحظة التكليف: ${m.note}'),
          const SizedBox(height: 12),
          Row(
            children: [
              Text('المحافظات', style: Theme.of(context).textTheme.titleMedium),
              const Spacer(),
              TextButton.icon(
                onPressed: _pickProvince,
                icon: const Icon(Icons.list_alt),
                label: const Text('اختر من القائمة'),
              ),
            ],
          ),
          const SizedBox(height: 8),
          ...m.cityValues.map(
            (city) => Card(
              child: ListTile(
                leading: const Icon(Icons.map_outlined, color: Color(0xFF0F3D3E)),
                title: Text(ProvinceLabels.display(city)),
                subtitle: const Text('مبيعات · مندوبون · متابعون · موظفون · ملف زبون'),
                trailing: const Icon(Icons.chevron_left),
                onTap: () => _openCity(city),
              ),
            ),
          ),
          const SizedBox(height: 16),
          Text('تقييمات المبيعات', style: Theme.of(context).textTheme.titleMedium),
          const SizedBox(height: 8),
          FutureBuilder<List<SaleReview>>(
            future: _saleReviews,
            builder: (context, snap) {
              if (snap.connectionState != ConnectionState.done) {
                return const Center(child: CircularProgressIndicator());
              }
              if (snap.hasError) return Text(snap.error.toString());
              final rows = snap.data ?? [];
              if (rows.isEmpty) return const Text('لا تقييمات مبيعات بعد.');
              return Column(
                children: rows
                    .map(
                      (r) => ListTile(
                        title: Text('${r.customerName ?? 'مبيع'} #${r.saleId}'),
                        subtitle: Text(
                          '${ProvinceLabels.display(r.cityValue)} — تقييم ${r.score}/10'
                          '${(r.note != null && r.note!.isNotEmpty) ? '\n${r.note}' : ''}',
                        ),
                      ),
                    )
                    .toList(),
              );
            },
          ),
          const SizedBox(height: 16),
          Text('تقييمات الموظفين', style: Theme.of(context).textTheme.titleMedium),
          const SizedBox(height: 8),
          FutureBuilder<List<StaffReview>>(
            future: _staffReviews,
            builder: (context, snap) {
              if (snap.connectionState != ConnectionState.done) {
                return const Center(child: CircularProgressIndicator());
              }
              if (snap.hasError) return Text(snap.error.toString());
              final rows = snap.data ?? [];
              if (rows.isEmpty) {
                return const Text('لا تقييمات موظفين بعد — افتح محافظة وقيّم.');
              }
              return Column(
                children: rows
                    .map(
                      (r) => ListTile(
                        title: Text('${r.typeLabel}: ${r.targetName ?? '#${r.targetId}'}'),
                        subtitle: Text(
                          '${ProvinceLabels.display(r.cityValue)} — ${r.score}/10'
                          '${(r.note != null && r.note!.isNotEmpty) ? '\n${r.note}' : ''}',
                        ),
                      ),
                    )
                    .toList(),
              );
            },
          ),
        ],
      ),
    );
  }
}
