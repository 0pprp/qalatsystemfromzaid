import 'package:flutter/material.dart';

import '../network/json.dart';
import 'skeleton.dart';
import 'state_views.dart';

typedef PageFetcher<T> = Future<Paged<T>> Function(int page);

/// Paged, infinitely scrolling list/grid with pull-to-refresh, skeletons,
/// empty and error states. Change [key] (e.g. `ValueKey(filter)`) to restart.
class PagedListView<T> extends StatefulWidget {
  const PagedListView({
    super.key,
    required this.fetch,
    required this.itemBuilder,
    required this.empty,
    this.headerSlivers = const [],
    this.gridDelegate,
    this.padding = const EdgeInsets.fromLTRB(16, 8, 16, 24),
    this.separator = 12,
    this.skeleton,
    this.onLoaded,
    this.onRefresh,
  });

  final PageFetcher<T> fetch;
  final Widget Function(BuildContext context, T item, int index) itemBuilder;
  final Widget empty;
  final List<Widget> headerSlivers;
  final SliverGridDelegate? gridDelegate;
  final EdgeInsets padding;
  final double separator;
  final Widget? skeleton;
  final void Function(Paged<T> page)? onLoaded;

  /// Extra work on pull-to-refresh (e.g. reload a summary header).
  final VoidCallback? onRefresh;

  @override
  State<PagedListView<T>> createState() => PagedListViewState<T>();
}

class PagedListViewState<T> extends State<PagedListView<T>> {
  final List<T> _items = [];
  int _page = 0;
  bool _hasMore = true;
  bool _loading = false;
  Object? _error;
  bool _initialLoaded = false;
  final ScrollController _scroll = ScrollController();

  @override
  void initState() {
    super.initState();
    _scroll.addListener(_onScroll);
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) _loadMore();
    });
  }

  @override
  void dispose() {
    _scroll.dispose();
    super.dispose();
  }

  void _onScroll() {
    if (!_scroll.hasClients) return;
    if (_scroll.position.pixels > _scroll.position.maxScrollExtent - 400) _loadMore();
  }

  Future<void> refresh() async {
    widget.onRefresh?.call();
    final first = await _fetch(1, replace: true);
    if (first == null && mounted && _items.isEmpty) setState(() {});
  }

  Future<void> _loadMore() async {
    if (_loading || !_hasMore || (_error != null && _initialLoaded)) return;
    await _fetch(_page + 1);
  }

  Future<Paged<T>?> _fetch(int page, {bool replace = false}) async {
    setState(() {
      _loading = true;
      if (replace) _error = null;
    });
    try {
      final result = await widget.fetch(page);
      if (!mounted) return result;
      setState(() {
        if (replace) _items.clear();
        _items.addAll(result.items);
        _page = result.page <= 0 ? page : result.page;
        _hasMore = result.hasMore && result.items.isNotEmpty;
        _error = null;
        _initialLoaded = true;
        _loading = false;
      });
      widget.onLoaded?.call(result);
      return result;
    } catch (e) {
      if (!mounted) return null;
      setState(() {
        _error = e;
        _loading = false;
      });
      return null;
    }
  }

  void _retry() {
    setState(() => _error = null);
    if (_items.isEmpty) {
      _fetch(1, replace: true);
    } else {
      _loadMore();
    }
  }

  @override
  Widget build(BuildContext context) {
    final slivers = <Widget>[...widget.headerSlivers];
    if (!_initialLoaded && _error == null) {
      slivers.add(SliverFillRemaining(
        hasScrollBody: true,
        child: widget.skeleton ?? const SkeletonList(),
      ));
    } else if (_items.isEmpty && _error != null) {
      slivers.add(SliverFillRemaining(
        hasScrollBody: false,
        child: ErrorView(error: _error!, onRetry: _retry),
      ));
    } else if (_items.isEmpty) {
      slivers.add(SliverFillRemaining(hasScrollBody: false, child: widget.empty));
    } else {
      final delegate = SliverChildBuilderDelegate(
        (context, i) {
          final child = widget.itemBuilder(context, _items[i], i);
          if (widget.gridDelegate != null) return child;
          return Padding(
            padding: EdgeInsets.only(bottom: i == _items.length - 1 ? 0 : widget.separator),
            child: child,
          );
        },
        childCount: _items.length,
      );
      slivers.add(SliverPadding(
        padding: widget.padding,
        sliver: widget.gridDelegate != null
            ? SliverGrid(delegate: delegate, gridDelegate: widget.gridDelegate!)
            : SliverList(delegate: delegate),
      ));
      if (_error != null) {
        slivers.add(SliverToBoxAdapter(child: ErrorView(error: _error!, onRetry: _retry, compact: true)));
      } else if (_hasMore) {
        slivers.add(const SliverToBoxAdapter(
          child: Padding(
            padding: EdgeInsets.all(20),
            child: Center(child: SizedBox(width: 24, height: 24, child: CircularProgressIndicator(strokeWidth: 2.5))),
          ),
        ));
      } else {
        slivers.add(const SliverToBoxAdapter(child: SizedBox(height: 24)));
      }
    }

    return RefreshIndicator(
      onRefresh: refresh,
      child: CustomScrollView(
        controller: _scroll,
        physics: const AlwaysScrollableScrollPhysics(),
        slivers: slivers,
      ),
    );
  }
}
