import 'package:delegated_manager_application/core/network/api_client.dart';
import 'package:delegated_manager_application/core/theme/app_theme.dart';
import 'package:delegated_manager_application/core/utils/date_format.dart';
import 'package:delegated_manager_application/core/widgets/state_views.dart';
import 'package:delegated_manager_application/features/waivers/data/waivers_repository.dart';
import 'package:delegated_manager_application/features/waivers/domain/customer_waiver.dart';
import 'package:delegated_manager_application/features/waivers/presentation/waiver_detail_screen.dart';
import 'package:flutter/material.dart';

class WaiversScreen extends StatefulWidget {
  const WaiversScreen({super.key, this.onChanged});

  final VoidCallback? onChanged;

  @override
  State<WaiversScreen> createState() => WaiversScreenState();
}

class WaiversScreenState extends State<WaiversScreen> {
  static const int _pageSize = 20;
  final _scrollController = ScrollController();
  final List<WaiverSummary> _items = [];
  String _status = WaiverStatuses.pending;
  int _page = 1;
  int _totalPages = 1;
  bool _loading = true;
  bool _loadingMore = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    _scrollController.addListener(_onScroll);
    reload();
  }

  @override
  void dispose() {
    _scrollController.removeListener(_onScroll);
    _scrollController.dispose();
    super.dispose();
  }

  void _onScroll() {
    if (_scrollController.position.pixels >=
        _scrollController.position.maxScrollExtent - 200) {
      _loadMore();
    }
  }

  Future<void> reload() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final result = await WaiversRepository.list(
        status: _status,
        page: 1,
        pageSize: _pageSize,
      );
      if (!mounted) return;
      setState(() {
        _items
          ..clear()
          ..addAll(result.items);
        _page = result.page;
        _totalPages = result.totalPages;
        _loading = false;
      });
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() {
        _error = e.message;
        _loading = false;
      });
    }
  }

  Future<void> _loadMore() async {
    if (_loadingMore || _loading || _page >= _totalPages) return;
    setState(() => _loadingMore = true);
    try {
      final result = await WaiversRepository.list(
        status: _status,
        page: _page + 1,
        pageSize: _pageSize,
      );
      if (!mounted) return;
      setState(() {
        _items.addAll(result.items);
        _page = result.page;
        _totalPages = result.totalPages;
        _loadingMore = false;
      });
    } on ApiException {
      if (mounted) setState(() => _loadingMore = false);
    }
  }

  Future<void> _open(WaiverSummary item) async {
    final changed = await Navigator.of(context).push<bool>(
      MaterialPageRoute(builder: (_) => WaiverDetailScreen(requestId: item.id)),
    );
    if (changed == true) {
      widget.onChanged?.call();
      await reload();
    }
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(12, 8, 12, 4),
          child: SegmentedButton<String>(
            segments: const [
              ButtonSegment(value: WaiverStatuses.pending, label: Text('قيد الانتظار')),
              ButtonSegment(value: WaiverStatuses.approved, label: Text('موافق')),
              ButtonSegment(value: WaiverStatuses.rejected, label: Text('مرفوض')),
            ],
            selected: {_status},
            onSelectionChanged: (s) {
              setState(() => _status = s.first);
              reload();
            },
          ),
        ),
        Expanded(
          child: _loading
              ? const LoadingView()
              : _error != null
                  ? ErrorView(message: _error!, onRetry: reload)
                  : _items.isEmpty
                      ? const EmptyView(message: 'لا توجد طلبات تنازل')
                      : RefreshIndicator(
                          onRefresh: reload,
                          child: ListView.separated(
                            controller: _scrollController,
                            padding: const EdgeInsets.all(AppSpacing.md),
                            itemCount: _items.length + (_loadingMore ? 1 : 0),
                            separatorBuilder: (_, __) =>
                                const SizedBox(height: AppSpacing.sm),
                            itemBuilder: (ctx, i) {
                              if (i >= _items.length) {
                                return const Padding(
                                  padding: EdgeInsets.all(16),
                                  child: Center(child: CircularProgressIndicator()),
                                );
                              }
                              final item = _items[i];
                              return Card(
                                child: ListTile(
                                  title: Text(
                                    item.customerName,
                                    style: const TextStyle(fontWeight: FontWeight.bold),
                                  ),
                                  subtitle: Text([
                                    item.branchManagerDisplayName ?? '',
                                    item.cityName ?? '',
                                    if (item.requestedAtUtc != null)
                                      AppDate.format(item.requestedAtUtc!),
                                  ].where((e) => e.isNotEmpty).join(' · ')),
                                  trailing: const Icon(Icons.chevron_left),
                                  onTap: () => _open(item),
                                ),
                              );
                            },
                          ),
                        ),
        ),
      ],
    );
  }
}
