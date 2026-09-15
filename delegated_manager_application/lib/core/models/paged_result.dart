import 'package:delegated_manager_application/core/utils/json_read.dart';

/// Shared shape of gateway list endpoints:
/// `{ page, pageSize, totalCount, totalPages, items[] }`.
class PagedResult<T> {
  const PagedResult({
    required this.page,
    required this.pageSize,
    required this.totalCount,
    required this.totalPages,
    required this.items,
  });

  final int page;
  final int pageSize;
  final int totalCount;
  final int totalPages;
  final List<T> items;

  bool get hasMore => page < totalPages;

  static PagedResult<T> fromJson<T>(
    dynamic json,
    T Function(Map<String, dynamic> item) parse,
  ) {
    final map = JsonRead.map(json);
    return PagedResult<T>(
      page: JsonRead.number(map['page'], fallback: 1),
      pageSize: JsonRead.number(map['pageSize'], fallback: 20),
      totalCount: JsonRead.number(map['totalCount']),
      totalPages: JsonRead.number(map['totalPages']),
      items: JsonRead.list(map['items']).map(parse).toList(),
    );
  }

  PagedResult<T> merge(PagedResult<T> next) => PagedResult<T>(
        page: next.page,
        pageSize: next.pageSize,
        totalCount: next.totalCount,
        totalPages: next.totalPages,
        items: [...items, ...next.items],
      );
}
