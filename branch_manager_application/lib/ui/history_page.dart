import 'package:branch_manager_application/core/api.dart';
import 'package:branch_manager_application/theme/bm_theme.dart';
import 'package:flutter/material.dart';
import 'package:intl/intl.dart' hide TextDirection;

class DecisionsHistoryPage extends StatefulWidget {
  const DecisionsHistoryPage({super.key});

  @override
  State<DecisionsHistoryPage> createState() => _DecisionsHistoryPageState();
}

class _DecisionsHistoryPageState extends State<DecisionsHistoryPage> {
  bool _loading = true;
  String? _error;
  List<Map<String, dynamic>> _items = [];
  final _date = DateFormat('yyyy/MM/dd HH:mm');

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final rows = await BmApi.instance.decisions();
      if (!mounted) return;
      setState(() {
        _items = rows;
        _loading = false;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _error = '$e';
      });
    }
  }

  String _s(Map m, List<String> keys) {
    for (final k in keys) {
      final v = m[k];
      if (v != null && '$v'.trim().isNotEmpty) return '$v'.trim();
    }
    return '';
  }

  Color _colorFor(String type) {
    switch (type) {
      case 'قانونية':
        return BmTheme.danger;
      case 'وهمي':
        return BmTheme.warn;
      case 'تنازل':
        return const Color(0xFF0F766E);
      default:
        return BmTheme.primary;
    }
  }

  @override
  Widget build(BuildContext context) {
    return RefreshIndicator(
      onRefresh: _load,
      child: _loading
          ? ListView(
              children: const [
                SizedBox(height: 120),
                Center(child: CircularProgressIndicator()),
              ],
            )
          : _error != null
              ? ListView(
                  padding: const EdgeInsets.all(24),
                  children: [
                    Text(_error!,
                        textAlign: TextAlign.center,
                        style: const TextStyle(fontFamily: 'Cairo')),
                    const SizedBox(height: 12),
                    Center(
                      child: FilledButton(
                          onPressed: _load, child: const Text('إعادة')),
                    ),
                  ],
                )
              : _items.isEmpty
                  ? ListView(
                      children: const [
                        SizedBox(height: 120),
                        Center(
                          child: Text(
                            'لا يوجد سجل قرارات بعد',
                            style: TextStyle(
                                fontFamily: 'Cairo', color: BmTheme.muted),
                          ),
                        ),
                      ],
                    )
                  : ListView.separated(
                      padding: const EdgeInsets.all(16),
                      itemCount: _items.length + 1,
                      separatorBuilder: (_, __) => const SizedBox(height: 10),
                      itemBuilder: (context, i) {
                        if (i == 0) {
                          return const Text(
                            'سجل القرارات المتخذة على زبائن الفرع',
                            style: TextStyle(
                              fontFamily: 'Cairo',
                              color: BmTheme.muted,
                              fontSize: 13,
                            ),
                          );
                        }
                        final item = _items[i - 1];
                        final type =
                            _s(item, ['decisionType', 'DecisionType']);
                        final name =
                            _s(item, ['customerName', 'CustomerName']);
                        final note = _s(item, ['note', 'Note']);
                        final whenRaw =
                            item['createdDate'] ?? item['CreatedDate'];
                        DateTime? when;
                        if (whenRaw != null) {
                          when = DateTime.tryParse('$whenRaw');
                        }
                        final color = _colorFor(type);
                        return Container(
                          padding: const EdgeInsets.all(14),
                          decoration: BoxDecoration(
                            color: Colors.white,
                            borderRadius: BorderRadius.circular(16),
                            border: Border.all(
                                color: color.withValues(alpha: 0.2)),
                          ),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Row(
                                children: [
                                  Expanded(
                                    child: Text(
                                      name.isEmpty ? 'زبون' : name,
                                      style: const TextStyle(
                                        fontFamily: 'Cairo',
                                        fontWeight: FontWeight.bold,
                                      ),
                                    ),
                                  ),
                                  Container(
                                    padding: const EdgeInsets.symmetric(
                                        horizontal: 10, vertical: 4),
                                    decoration: BoxDecoration(
                                      color: color.withValues(alpha: 0.12),
                                      borderRadius: BorderRadius.circular(99),
                                    ),
                                    child: Text(
                                      type,
                                      style: TextStyle(
                                        fontFamily: 'Cairo',
                                        color: color,
                                        fontWeight: FontWeight.bold,
                                        fontSize: 12,
                                      ),
                                    ),
                                  ),
                                ],
                              ),
                              if (note.isNotEmpty) ...[
                                const SizedBox(height: 6),
                                Text(note,
                                    style: const TextStyle(
                                        fontFamily: 'Cairo', height: 1.4)),
                              ],
                              if (when != null) ...[
                                const SizedBox(height: 8),
                                Text(
                                  _date.format(when.toLocal()),
                                  style: const TextStyle(
                                    fontFamily: 'Cairo',
                                    fontSize: 12,
                                    color: BmTheme.muted,
                                  ),
                                ),
                              ],
                            ],
                          ),
                        );
                      },
                    ),
    );
  }
}
