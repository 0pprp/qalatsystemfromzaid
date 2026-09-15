import 'package:delegated_manager_application/core/models/paged_result.dart';
import 'package:delegated_manager_application/core/network/api_client.dart';
import 'package:delegated_manager_application/core/theme/app_theme.dart';
import 'package:delegated_manager_application/core/utils/date_format.dart';
import 'package:delegated_manager_application/core/widgets/state_views.dart';
import 'package:delegated_manager_application/features/complaints/data/complaints_repository.dart';
import 'package:delegated_manager_application/features/complaints/domain/complaint.dart';
import 'package:delegated_manager_application/features/complaints/presentation/complaint_detail_screen.dart';
import 'package:flutter/material.dart';

/// Email-like inbox over `delegated-manager/complaints`.
class ComplaintsScreen extends StatefulWidget {
  const ComplaintsScreen({super.key, this.onChanged});

  /// Called after a mark-read action so the dashboard counters stay in sync.
  final VoidCallback? onChanged;

  @override
  State<ComplaintsScreen> createState() => ComplaintsScreenState();
}

class ComplaintsScreenState extends State<ComplaintsScreen> {
  static const int _pageSize = 20;

  final _searchController = TextEditingController();
  final _scrollController = ScrollController();

  final List<ComplaintSummary> _items = [];
  int _page = 1;
  int _totalPages = 1;
  bool _loading = true;
  bool _loadingMore = false;
  bool _unreadOnly = false;
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

  Future<PagedResult<ComplaintSummary>> _fetch(int page) =>
      ComplaintsRepository.inbox(
        page: page,
        pageSize: _pageSize,
        unreadOnly: _unreadOnly,
        search: _searchController.text,
      );

  Future<void> reload() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final result = await _fetch(1);
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
      final result = await _fetch(_page + 1);
      if (!mounted) return;
      setState(() {
        _items.addAll(result.items);
        _page = result.page;
        _totalPages = result.totalPages;
      });
    } on ApiException catch (_) {
      // keep the already loaded page; the user can pull to refresh
    } finally {
      if (mounted) setState(() => _loadingMore = false);
    }
  }

  Future<void> _markAllRead() async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('تحديد الكل كمقروء'),
        content: const Text('هل تريد تحديد جميع الرسائل كمقروءة؟'),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(false),
            child: const Text('إلغاء'),
          ),
          FilledButton(
            onPressed: () => Navigator.of(context).pop(true),
            child: const Text('تأكيد'),
          ),
        ],
      ),
    );
    if (confirmed != true) return;
    try {
      await ComplaintsRepository.markAllRead();
      widget.onChanged?.call();
      await reload();
    } on ApiException catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context)
          .showSnackBar(SnackBar(content: Text(e.message)));
    }
  }

  Future<void> _open(ComplaintSummary item) async {
    await Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => ComplaintDetailScreen(complaintId: item.id),
      ),
    );
    widget.onChanged?.call();
    await reload();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('الرسائل والشكاوى'),
        actions: [
          IconButton(
            tooltip: _unreadOnly ? 'عرض الكل' : 'غير المقروءة فقط',
            onPressed: () {
              setState(() => _unreadOnly = !_unreadOnly);
              reload();
            },
            icon: Icon(_unreadOnly
                ? Icons.filter_alt
                : Icons.filter_alt_outlined),
          ),
          IconButton(
            tooltip: 'تحديد الكل كمقروء',
            onPressed: _markAllRead,
            icon: const Icon(Icons.done_all),
          ),
        ],
      ),
      body: SafeArea(
        child: Column(
          children: [
            Padding(
              padding: const EdgeInsets.all(AppSpacing.sm),
              child: TextField(
                controller: _searchController,
                textInputAction: TextInputAction.search,
                onSubmitted: (_) => reload(),
                decoration: InputDecoration(
                  hintText: 'بحث في العنوان أو المرسل',
                  prefixIcon: const Icon(Icons.search),
                  suffixIcon: _searchController.text.isEmpty
                      ? null
                      : IconButton(
                          icon: const Icon(Icons.close),
                          onPressed: () {
                            _searchController.clear();
                            reload();
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
    if (_items.isEmpty) {
      return const EmptyView(
        message: 'لا توجد رسائل أو شكاوى',
        icon: Icons.mark_email_read_outlined,
      );
    }
    return RefreshIndicator(
      onRefresh: reload,
      child: ListView.separated(
        controller: _scrollController,
        padding: const EdgeInsets.only(bottom: AppSpacing.lg),
        itemCount: _items.length + (_loadingMore ? 1 : 0),
        separatorBuilder: (_, __) => const Divider(height: 1),
        itemBuilder: (context, index) {
          if (index >= _items.length) {
            return const Padding(
              padding: EdgeInsets.all(AppSpacing.md),
              child: Center(child: CircularProgressIndicator()),
            );
          }
          return _ComplaintTile(
            item: _items[index],
            onTap: () => _open(_items[index]),
          );
        },
      ),
    );
  }
}

class _ComplaintTile extends StatelessWidget {
  const _ComplaintTile({required this.item, required this.onTap});

  final ComplaintSummary item;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final unread = item.isUnread;
    return ListTile(
      tileColor: unread ? AppColors.unread : Colors.white,
      onTap: onTap,
      leading: CircleAvatar(
        backgroundColor:
            unread ? AppColors.primary : AppColors.line,
        child: Icon(
          unread ? Icons.mail_outline : Icons.drafts_outlined,
          color: unread ? Colors.white : AppColors.muted,
          size: 20,
        ),
      ),
      title: Text(
        item.subject,
        maxLines: 1,
        overflow: TextOverflow.ellipsis,
        style: TextStyle(
          fontSize: 15,
          color: AppColors.text,
          fontWeight: unread ? FontWeight.bold : FontWeight.w500,
        ),
      ),
      subtitle: Padding(
        padding: const EdgeInsets.only(top: 4),
        child: Text(
          '${item.senderDisplayName}'
          '${item.cityName == null ? '' : ' • ${item.cityName}'}'
          ' • ${item.friendlySource}',
          maxLines: 1,
          overflow: TextOverflow.ellipsis,
          style: const TextStyle(color: AppColors.muted, fontSize: 12),
        ),
      ),
      trailing: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        crossAxisAlignment: CrossAxisAlignment.end,
        children: [
          Text(
            AppDate.relative(item.createdAtUtc),
            style: const TextStyle(color: AppColors.muted, fontSize: 11),
          ),
          const SizedBox(height: 4),
          if (unread)
            const StatusChip(label: 'جديدة', color: AppColors.accent),
        ],
      ),
    );
  }
}
