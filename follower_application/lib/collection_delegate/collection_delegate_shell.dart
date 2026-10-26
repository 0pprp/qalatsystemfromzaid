import 'package:follower_application/collection_delegate/collection_customers_tab.dart';
import 'package:follower_application/collection_delegate/collection_home_tab.dart';
import 'package:follower_application/collection_delegate/collection_sales_tab.dart';
import 'package:follower_application/collection_delegate/collection_today_tab.dart';
import 'package:follower_application/collection_delegate/ui/main_bottom_nav.dart';
import 'package:follower_application/ui/app_safe_scaffold.dart';
import 'package:follower_application/ui/day_rating_gate.dart';
import 'package:follower_application/utils/AppTheme.dart';
import 'package:flutter/material.dart';

/// Full mini-delegate shell for follower collection (same 4 tabs as المندوب).
class CollectionDelegateShell extends StatefulWidget {
  final int listId;
  final String listName;

  const CollectionDelegateShell({
    super.key,
    required this.listId,
    required this.listName,
  });

  @override
  State<CollectionDelegateShell> createState() =>
      _CollectionDelegateShellState();
}

class _CollectionDelegateShellState extends State<CollectionDelegateShell> {
  int _index = 1; // العملاء — default like collection work
  bool _ending = false;
  final GlobalKey<CollectionCustomersTabState> _customersKey =
      GlobalKey<CollectionCustomersTabState>();
  final GlobalKey<CollectionTodayTabState> _todayKey =
      GlobalKey<CollectionTodayTabState>();
  final GlobalKey<CollectionHomeTabState> _homeKey =
      GlobalKey<CollectionHomeTabState>();

  void _refreshAll() {
    _customersKey.currentState?.reload();
    _todayKey.currentState?.reload();
    _homeKey.currentState?.reload();
    setState(() {});
  }

  Future<void> _endCollection() async {
    if (_ending) return;
    setState(() => _ending = true);
    try {
      await endCollectionWithDayRating(
        context,
        listId: widget.listId,
        listName: widget.listName,
      );
    } finally {
      if (mounted) setState(() => _ending = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Directionality(
      textDirection: TextDirection.rtl,
      child: AppSafeScaffold(
        backgroundColor: AppTheme.backgroundColor,
        appBar: AppBar(
          title: Text(
            'جباية — ${widget.listName}',
            style: const TextStyle(fontFamily: 'Cairo', fontWeight: FontWeight.bold),
          ),
          backgroundColor: AppTheme.primaryColor,
          foregroundColor: Colors.white,
          actions: [
            TextButton(
              onPressed: _ending ? null : _endCollection,
              child: _ending
                  ? const SizedBox(
                      width: 18,
                      height: 18,
                      child: CircularProgressIndicator(
                        strokeWidth: 2,
                        color: Colors.white,
                      ),
                    )
                  : const Text(
                      'إنهاء الجباية',
                      style: TextStyle(
                        fontFamily: 'Cairo',
                        fontWeight: FontWeight.bold,
                        color: Colors.white,
                      ),
                    ),
            ),
            IconButton(
              tooltip: 'تحديث',
              onPressed: _refreshAll,
              icon: const Icon(Icons.refresh),
            ),
          ],
        ),
        body: IndexedStack(
          index: _index,
          children: [
            CollectionSalesTab(listId: widget.listId, listName: widget.listName),
            CollectionCustomersTab(
              key: _customersKey,
              listId: widget.listId,
              listName: widget.listName,
              onPaid: () {
                _todayKey.currentState?.reload();
                _homeKey.currentState?.reload();
              },
            ),
            CollectionHomeTab(
              key: _homeKey,
              listId: widget.listId,
              listName: widget.listName,
            ),
            CollectionTodayTab(
              key: _todayKey,
              listId: widget.listId,
              listName: widget.listName,
            ),
          ],
        ),
        bottomNavigationBar: MainBottomNavBar(
          selectedIndex: _index,
          onTap: (i) => setState(() => _index = i),
        ),
      ),
    );
  }
}
