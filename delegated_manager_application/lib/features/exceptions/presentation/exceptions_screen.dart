import 'package:delegated_manager_application/core/network/api_client.dart';
import 'package:delegated_manager_application/core/theme/app_theme.dart';
import 'package:delegated_manager_application/core/utils/date_format.dart';
import 'package:delegated_manager_application/core/widgets/state_views.dart';
import 'package:delegated_manager_application/features/exceptions/data/exceptions_repository.dart';
import 'package:delegated_manager_application/features/exceptions/domain/sales_exception.dart';
import 'package:delegated_manager_application/features/exceptions/presentation/exception_detail_screen.dart';
import 'package:delegated_manager_application/features/exceptions/presentation/wrong_sale_detail_screen.dart';
import 'package:flutter/material.dart';

enum ExceptionInboxMode {
  /// Hold / classic exceptions — excludes WrongSale.
  hold,
  /// WrongSale only (مبيعات خاطئة).
  wrongSale,
}

/// WrongSale mailbox chip: unread vs read (everything stays stored).
enum WrongSaleMailboxFilter {
  unread,
  read,
}

/// Decision queue over `delegated-manager/exceptions`.
class ExceptionsScreen extends StatefulWidget {
  const ExceptionsScreen({
    super.key,
    this.onChanged,
    this.mode = ExceptionInboxMode.hold,
  });

  final VoidCallback? onChanged;
  final ExceptionInboxMode mode;

  @override
  State<ExceptionsScreen> createState() => ExceptionsScreenState();
}

class ExceptionsScreenState extends State<ExceptionsScreen> {
  static const int _pageSize = 20;
  static const int _wrongSalePageSize = 100;

  final _searchController = TextEditingController();
  final _scrollController = ScrollController();

  final List<ExceptionSummary> _items = [];
  String _status = ExceptionStatuses.pending;
  int _page = 1;
  int _totalPages = 1;
  bool _loading = true;
  bool _loadingMore = false;
  WrongSaleMailboxFilter _wrongSaleFilter = WrongSaleMailboxFilter.unread;
  String? _error;

  /// True when WrongSale «غير المقروءة» chip is selected.
  bool get unreadOnly => _wrongSaleFilter == WrongSaleMailboxFilter.unread;
  bool get _isWrongSaleTab => widget.mode == ExceptionInboxMode.wrongSale;
  int get _effectivePageSize =>
      _isWrongSaleTab ? _wrongSalePageSize : _pageSize;

  /// Used by dashboard card «مبيعات خاطئة غير المقروءة».
  void openUnreadOnly() {
    setState(() => _wrongSaleFilter = WrongSaleMailboxFilter.unread);
    reload();
  }

  void openReadMailbox() {
    setState(() => _wrongSaleFilter = WrongSaleMailboxFilter.read);
    reload();
  }

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
    Iterable<ExceptionSummary> rows = _items.where((item) =>
        _isWrongSaleTab ? item.isWrongSale : !item.isWrongSale);
    if (_isWrongSaleTab) {
      rows = _wrongSaleFilter == WrongSaleMailboxFilter.unread
          ? rows.where((item) => item.isUnread)
          : rows.where((item) => !item.isUnread);
    }
    final query = _searchController.text.trim();
    if (query.isEmpty) return rows.toList();
    return rows.where((item) {
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
      // WrongSale inbox: load all statuses (notice mailbox), filter client-side.
      final result = await ExceptionsRepository.list(
        status: _isWrongSaleTab ? null : _status,
        page: 1,
        pageSize: _effectivePageSize,
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
        status: _isWrongSaleTab ? null : _status,
        page: _page + 1,
        pageSize: _effectivePageSize,
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
        builder: (_) => _isWrongSaleTab
            ? WrongSaleDetailScreen(requestId: item.id)
            : ExceptionDetailScreen(requestId: item.id),
      ),
    );
    widget.onChanged?.call();
    await reload();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: SafeArea(
        child: Column(
          children: [
            if (!_isWrongSaleTab)
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
            if (_isWrongSaleTab)
              Padding(
                padding: const EdgeInsets.fromLTRB(
                    AppSpacing.sm, 0, AppSpacing.sm, AppSpacing.xs),
                child: Row(
                  children: [
                    Expanded(
                      child: _WrongSaleFilterChip(
                        label: 'المبيعات الخاطئة غير المقروءة',
                        selected: _wrongSaleFilter ==
                            WrongSaleMailboxFilter.unread,
                        color: AppColors.primary,
                        onTap: () {
                          if (_wrongSaleFilter ==
                              WrongSaleMailboxFilter.unread) {
                            return;
                          }
                          setState(() => _wrongSaleFilter =
                              WrongSaleMailboxFilter.unread);
                        },
                      ),
                    ),
                    const SizedBox(width: AppSpacing.xs),
                    Expanded(
                      child: _WrongSaleFilterChip(
                        label: 'المبيعات الخاطئة المقروءة',
                        selected:
                            _wrongSaleFilter == WrongSaleMailboxFilter.read,
                        color: Colors.orange.shade800,
                        onTap: () {
                          if (_wrongSaleFilter == WrongSaleMailboxFilter.read) {
                            return;
                          }
                          setState(() =>
                              _wrongSaleFilter = WrongSaleMailboxFilter.read);
                        },
                      ),
                    ),
                  ],
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
        message: _isWrongSaleTab
            ? (_wrongSaleFilter == WrongSaleMailboxFilter.unread
                ? 'لا توجد مبيعات خاطئة غير مقروءة'
                : 'لا توجد مبيعات خاطئة مقروءة')
            : 'لا توجد طلبات استثناء ${ExceptionStatuses.arabic(_status)}',
        icon: _isWrongSaleTab
            ? Icons.report_gmailerrorred_outlined
            : Icons.rule_folder_outlined,
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

class _WrongSaleFilterChip extends StatelessWidget {
  const _WrongSaleFilterChip({
    required this.label,
    required this.selected,
    required this.color,
    required this.onTap,
  });

  final String label;
  final bool selected;
  final Color color;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: selected ? color.withValues(alpha: 0.14) : Colors.white,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(AppRadius.md),
        side: BorderSide(
          color: selected ? color : AppColors.line,
          width: selected ? 1.5 : 1,
        ),
      ),
      child: InkWell(
        borderRadius: BorderRadius.circular(AppRadius.md),
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 10),
          child: Text(
            label,
            textAlign: TextAlign.center,
            maxLines: 2,
            style: TextStyle(
              fontFamily: 'Cairo',
              fontSize: 12,
              fontWeight: selected ? FontWeight.w700 : FontWeight.w500,
              color: selected ? color : AppColors.text,
              height: 1.25,
            ),
          ),
        ),
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
    final unread = item.isUnread;
    return Card(
      color: unread ? AppColors.unread : Colors.white,
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
                  CircleAvatar(
                    radius: 16,
                    backgroundColor:
                        unread ? AppColors.primary : AppColors.line,
                    child: Icon(
                      unread ? Icons.mail_outline : Icons.drafts_outlined,
                      size: 16,
                      color: unread ? Colors.white : AppColors.muted,
                    ),
                  ),
                  const SizedBox(width: 8),
                  Expanded(
                    child: Text(
                      item.customerName,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: TextStyle(
                        fontSize: 16,
                        fontWeight:
                            unread ? FontWeight.bold : FontWeight.w600,
                        color: AppColors.text,
                      ),
                    ),
                  ),
                  if (item.isWrongSale) ...[
                    StatusChip(
                      label: 'مبيع خاطئ',
                      color: Colors.orange.shade800,
                    ),
                    const SizedBox(width: 6),
                  ],
                  StatusChip(
                    label: unread ? 'غير مقروء' : 'مقروء',
                    color: unread ? AppColors.primary : AppColors.muted,
                  ),
                  if (!item.isWrongSale) ...[
                    const SizedBox(width: 6),
                    StatusChip(
                      label: ExceptionStatuses.arabic(item.status),
                      color: ExceptionStatuses.color(item.status),
                    ),
                  ],
                ],
              ),
              const SizedBox(height: AppSpacing.xs),
              Text(
                item.isWrongSale
                    ? 'إشعار اطلاع — مبيع خاطئ (بدون موافقة/رفض)'
                    : 'طالب الاستثناء: ${item.requestingManagerDisplayName ?? '-'}',
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
