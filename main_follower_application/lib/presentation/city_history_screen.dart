import 'package:flutter/material.dart';
import 'package:main_follower_application/core/config/province_labels.dart';
import 'package:main_follower_application/data/history_repository.dart';

class CityHistoryScreen extends StatefulWidget {
  const CityHistoryScreen({
    super.key,
    required this.cityValue,
    this.embedded = false,
  });

  final String cityValue;
  final bool embedded;

  @override
  State<CityHistoryScreen> createState() => _CityHistoryScreenState();
}

class _CityHistoryScreenState extends State<CityHistoryScreen> {
  late Future<List<HistoryEntry>> _future;

  @override
  void initState() {
    super.initState();
    _future = HistoryRepository.cityHistory(widget.cityValue);
  }

  Future<void> _reload() async {
    setState(() => _future = HistoryRepository.cityHistory(widget.cityValue));
    await _future;
  }

  @override
  Widget build(BuildContext context) {
    final body = FutureBuilder<List<HistoryEntry>>(
      future: _future,
      builder: (context, snap) {
        if (snap.connectionState != ConnectionState.done) {
          return const Center(child: CircularProgressIndicator());
        }
        if (snap.hasError) {
          return Center(child: Text(snap.error.toString()));
        }
        final rows = snap.data ?? [];
        if (rows.isEmpty) {
          return const Center(child: Text('لا تقييمات سابقة لهذه المحافظة'));
        }
        return RefreshIndicator(
          onRefresh: _reload,
          child: ListView.separated(
            padding: const EdgeInsets.all(12),
            itemCount: rows.length,
            separatorBuilder: (_, __) => const Divider(height: 1),
            itemBuilder: (context, i) {
              final e = rows[i];
              return ListTile(
                leading: CircleAvatar(
                  backgroundColor: const Color(0xFF0F3D3E),
                  foregroundColor: Colors.white,
                  child: Text('${e.score}'),
                ),
                title: Text(e.title ?? (e.isSale ? 'مبيع' : 'موظف')),
                subtitle: Text(
                  [
                    if (e.committeeName != null && e.committeeName!.isNotEmpty)
                      e.committeeName!,
                    e.isSale ? 'تقييم مبيع' : 'تقييم موظف',
                    if (e.note != null && e.note!.isNotEmpty) e.note!,
                    if (e.atUtc.isNotEmpty) e.atUtc,
                  ].join('\n'),
                ),
                isThreeLine: true,
              );
            },
          ),
        );
      },
    );

    if (widget.embedded) return body;

    return Scaffold(
      appBar: AppBar(
        title: Text('سجل ${ProvinceLabels.display(widget.cityValue)}'),
        backgroundColor: const Color(0xFF0F3D3E),
        foregroundColor: Colors.white,
        actions: [
          IconButton(onPressed: _reload, icon: const Icon(Icons.refresh)),
        ],
      ),
      body: body,
    );
  }
}
