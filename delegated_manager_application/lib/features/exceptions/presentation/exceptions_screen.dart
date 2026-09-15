import 'package:delegated_manager_application/core/network/api_client.dart';
import 'package:delegated_manager_application/core/theme/app_theme.dart';
import 'package:delegated_manager_application/core/utils/date_format.dart';
import 'package:delegated_manager_application/core/widgets/state_views.dart';
import 'package:delegated_manager_application/features/exceptions/data/exceptions_repository.dart';
import 'package:delegated_manager_application/features/exceptions/domain/sales_exception.dart';
import 'package:delegated_manager_application/features/exceptions/presentation/exception_detail_screen.dart';
import 'package:flutter/material.dart';

/// Decision queue over `delegated-manager/exceptions`, focused on Pending.
class ExceptionsScreen extends StatefulWidget {
  const ExceptionsScreen({super.key, this.onChanged});

  final VoidCallback? onChanged;

  @override
  State<ExceptionsScreen> createState() => ExceptionsScreenState();
}

class ExceptionsScreenState extends State<ExceptionsScreen> {
  static const int _pageSize = 20;

  final _searchController = TextEditingController();
  final _scrollController = ScrollController();

  final List<ExceptionSummary> _items = [];
  String _status = ExceptionStatuses.pending;
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
    _searchController.dispose();
    super.dispose();
  }

  void _onScroll() {
    if (_scrollController.position.pixels >=
        _scrollController.position.maxScrollExtent - 200) {
      _loadMore();
    }
  }

  /// The gateway list endpoint filters by status/city only, so the free-text
  /// query is applied on the loaded rows.
  List<ExceptionSummary> get _visibleItems {
    final query = _searchController.text.trim();
    if (query.isEmpty) return _items;
    return _items.where((item) {
      final haystack = [
        item.customerName,
        item.requestingManagerDisplayName ?? '',
        item.cityName ?? '',
      ].join(' ');
      return haystack.contains(query);
    }).toList();
  }

  Future<void> reload() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final result = await ExceptionsRepository.list(
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
      final result = await ExceptionsRepository.list(
        status: _status,
        page: _page + 1,
        pageSize: _pageSize,
      );
      if (!mounted) return;
      setState(() {
        _items.addAll(result.items);
        _page = result.page;
        _totalPages = result.totalPages;
      });
    } on ApiException catch (_) {
      // keep loaded rows; pull to refresh retries
    } finally {
      if (mounted) setState(() => _loadingMore = false);
    }
  }

  Future<void> _open(ExceptionSummary item) async {
    await Navigator.of(context).push<bool>(
      MaterialPageRoute(
        builder: (_) => ExceptionDetailScreen(requestId: item.id),
      ),
    );
    widget.onChanged?.call();
    await reload();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('طلبات الاستثناء'),
        actions: [
          IconButton(
            tooltip: 'تحديث',
            onPressed: reload,
            icon: const Icon(Icons.refresh),
          ),
        ],
      ),
      body: SafeArea(
        child: Column(
          children: [
            SingleChildScrollView(
              scrollDirection: Axis.horizontal,
              padding: const EdgeInsets.symmetric(
                  horizontal: AppSpacing.sm, vertical: AppSpacing.xs),
              child: Row(
                children: ExceptionStatuses.all.map((status) {
                  final selected = status == _status;
                  return Padding(
                    padding: const EdgeInsets.only(left: AppSpacing.xs),
                    child: ChoiceChip(
                      label: Text(ExceptionStatuses.arabic(status)),
                      selected: selected,
                      selectedColor: ExceptionStatuses.color(status)
                          .withValues(alpha: 0.16),
                      onSelected: (_) {
                        if (selected) return;
                        setState(() => _status = status);
                        reload();
                      },
                    ),
                  );
                }).toList(),
              ),
            ),
            Padding(
              padding: const EdgeInsets.symmetric(
                  horizontal: AppSpacing.sm, vertical: AppSpacing.xs),
              child: TextField(
                controller: _searchController,
                onChanged: (_) => setState(() {}),
                decoration: InputDecoration(
                  hintText: 'بحث باسم الزبون أو المحافظة',
                  prefixIcon: const Icon(Icons.search),
                  suffixIcon: _searchController.text.isEmpty
                      ? null
                      : IconButton(
                          icon: const Icon(Icons.close),
                          onPressed: () {
                            _searchController.clear();
                            setState(() {});
                          },
                        ),
                ),
              ),
            ),
            Expanded(child: _buildBody()),
          ],
        ),
      ),
    );
  }

  Widget _buildBody() {
    if (_loading) return const LoadingView();
    if (_error != null && _items.isEmpty) {
      return ErrorView(message: _error!, onRetry: reload);
    }
    final items = _visibleItems;
    if (items.isEmpty) {
      return EmptyView(
        message: 'لا توجد طلبات ${ExceptionStatuses.arabic(_status)}',
        icon: Icons.rule_folder_outlined,
      );
    }
    return RefreshIndicator(
      onRefresh: reload,
      child: ListView.builder(
        controller: _scrollController,
        padding: const EdgeInsets.all(AppSpacing.sm),
        itemCount: items.length + (_loadingMore ? 1 : 0),
        itemBuilder: (context, index) {
          if (index >= items.length) {
            return const Padding(
              padding: EdgeInsets.all(AppSpacing.md),
              child: Center(child: CircularProgressIndicator()),
            );
          }
          final item = items[index];
          return Padding(
            padding: const EdgeInsets.only(bottom: AppSpacing.sm),
            child: _ExceptionCard(item: item, onTap: () => _open(item)),
          );
        },
      ),
    );
  }
}

class _ExceptionCard extends StatelessWidget {
  const _ExceptionCard({required this.item, required this.onTap});

  final ExceptionSummary item;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: InkWell(
        borderRadius: BorderRadius.circular(AppRadius.md),
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.all(AppSpacing.md),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Expanded(
                    child: Text(
                      item.customerName,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(
                        fontSize: 16,
                        fontWeight: FontWeight.bold,
                        color: AppColors.text,
                      ),
                    ),
                  ),
                  StatusChip(
                    label: ExceptionStatuses.arabic(item.status),
                    color: ExceptionStatuses.color(item.status),
                  ),
                ],
              ),
              const SizedBox(height: AppSpacing.xs),
              Text(
                'طالب الاستثناء: ${item.requestingManagerDisplayName ?? '-'}',
                style: const TextStyle(color: AppColors.muted, fontSize: 13),
              ),
              const SizedBox(height: 2),
              Row(
                children: [
                  const Icon(Icons.place_outlined,
                      size: 15, color: AppColors.muted),
                  const SizedBox(width: 4),
                  Text(
                    item.cityName ?? 'غير محددة',
                    style:
                        const TextStyle(color: AppColors.muted, fontSize: 12),
                  ),
                  const Spacer(),
                  Text(
                    AppDate.relative(item.requestedAtUtc),
                    style:
                        const TextStyle(color: AppColors.muted, fontSize: 11),
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}
