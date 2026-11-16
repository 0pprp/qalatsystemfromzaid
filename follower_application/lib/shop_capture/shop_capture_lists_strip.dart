import 'package:flutter/material.dart';
import 'package:follower_application/shop_capture/shop_capture_api.dart';
import 'package:follower_application/shop_capture/shop_capture_list_page.dart';
import 'package:follower_application/utils/AppTheme.dart';

/// Home section: one card per assigned list with two progress bars (locations / photos).
class ShopCaptureListsStrip extends StatefulWidget {
  const ShopCaptureListsStrip({super.key, this.refresh});

  /// Bumped by the home page on pull-to-refresh.
  final Listenable? refresh;

  @override
  State<ShopCaptureListsStrip> createState() => _ShopCaptureListsStripState();
}

class _ShopCaptureListsStripState extends State<ShopCaptureListsStrip> {
  static const _cairo = TextStyle(fontFamily: 'Cairo');

  List<ShopCaptureList>? _lists;
  bool _failed = false;

  @override
  void initState() {
    super.initState();
    widget.refresh?.addListener(_load);
    _load();
  }

  @override
  void dispose() {
    widget.refresh?.removeListener(_load);
    super.dispose();
  }

  Future<void> _load() async {
    try {
      final lists = await ShopCaptureApi.lists();
      if (mounted) {
        setState(() {
          _lists = lists;
          _failed = false;
        });
      }
    } catch (_) {
      if (mounted) setState(() => _failed = _lists == null);
    }
  }

  Widget _bar(String label, int done, int total, Color color) {
    final v = total == 0 ? 1.0 : done / total;
    return Padding(
      padding: const EdgeInsets.only(top: 6),
      child: Row(
        children: [
          SizedBox(width: 64, child: Text(label, style: _cairo.copyWith(fontSize: 12))),
          Expanded(
            child: ClipRRect(
              borderRadius: BorderRadius.circular(6),
              child: LinearProgressIndicator(
                value: v,
                minHeight: 8,
                color: color,
                backgroundColor: color.withValues(alpha: 0.15),
              ),
            ),
          ),
          const SizedBox(width: 8),
          Text('$done/$total', style: _cairo.copyWith(fontSize: 12, fontWeight: FontWeight.w600)),
        ],
      ),
    );
  }

  Widget _card(ShopCaptureList l) => Material(
        color: Theme.of(context).cardColor,
        borderRadius: BorderRadius.circular(14),
        child: InkWell(
          borderRadius: BorderRadius.circular(14),
          onTap: () async {
            await Navigator.of(context).push(MaterialPageRoute(
              builder: (_) => ShopCaptureListPage(listId: l.listId, listName: l.listName),
            ));
            if (mounted) await _load();
          },
          child: Padding(
            padding: const EdgeInsets.fromLTRB(12, 10, 12, 12),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Row(children: [
                  Expanded(
                    child: Text(l.listName, style: _cairo.copyWith(fontWeight: FontWeight.bold, fontSize: 14)),
                  ),
                  const Icon(Icons.chevron_left, color: Colors.grey),
                ]),
                _bar('المواقع', l.withLocation, l.customerCount, AppTheme.primaryColor),
                _bar('الصور', l.withPhoto, l.customerCount, Colors.orange.shade700),
              ],
            ),
          ),
        ),
      );

  @override
  Widget build(BuildContext context) {
    final lists = _lists;
    if (lists == null) {
      return _failed
          ? TextButton(onPressed: _load, child: const Text('تعذر تحميل مواقع وصور المحلات — إعادة', style: _cairo))
          : const SizedBox(height: 4, child: LinearProgressIndicator());
    }
    if (lists.isEmpty) return const SizedBox.shrink();
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        const Text('مواقع وصور المحلات', style: TextStyle(fontFamily: 'Cairo', fontWeight: FontWeight.bold, fontSize: 15)),
        const SizedBox(height: 6),
        for (final l in lists) ...[_card(l), const SizedBox(height: 8)],
      ],
    );
  }
}
