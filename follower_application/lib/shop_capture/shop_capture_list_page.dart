import 'package:flutter/material.dart';
import 'package:follower_application/shop_capture/shop_capture_api.dart';
import 'package:follower_application/shop_capture/shop_capture_widgets.dart';
import 'package:follower_application/ui/app_safe_scaffold.dart';
import 'package:follower_application/utils/AppTheme.dart';

enum _Filter { all, noLocation, noPhoto }

/// Customers of one list: search + filter, one button for the shop location and one for the shop photo.
class ShopCaptureListPage extends StatefulWidget {
  const ShopCaptureListPage({super.key, required this.listId, required this.listName});

  final int listId;
  final String listName;

  @override
  State<ShopCaptureListPage> createState() => _ShopCaptureListPageState();
}

class _ShopCaptureListPageState extends State<ShopCaptureListPage> {
  static const _cairo = TextStyle(fontFamily: 'Cairo');

  bool _loading = true;
  String _error = '';
  List<ShopCaptureCustomer> _all = [];
  String _query = '';
  _Filter _filter = _Filter.all;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = _all.isEmpty;
      _error = '';
    });
    try {
      final rows = await ShopCaptureApi.customers(widget.listId);
      if (mounted) {
        setState(() {
          _all = rows;
          _loading = false;
        });
      }
    } catch (e) {
      if (mounted) {
        setState(() {
          _loading = false;
          _error = captureError(e);
        });
      }
    }
  }

  List<ShopCaptureCustomer> get _shown {
    final q = _query.trim().toLowerCase();
    return _all.where((c) {
      if (_filter == _Filter.noLocation && c.hasFollowerLocation) return false;
      if (_filter == _Filter.noPhoto && c.hasPhoto) return false;
      if (q.isEmpty) return true;
      return c.customerName.toLowerCase().contains(q) ||
          (c.shopName ?? '').toLowerCase().contains(q) ||
          (c.phoneNumber ?? '').contains(q);
    }).toList();
  }

  Widget _chip(_Filter f, String label, int count) => Padding(
        padding: const EdgeInsetsDirectional.only(end: 8),
        child: ChoiceChip(
          label: Text('$label ($count)', style: _cairo.copyWith(fontSize: 13)),
          selected: _filter == f,
          onSelected: (_) => setState(() => _filter = f),
        ),
      );

  Widget _actionButton({
    required bool done,
    required IconData icon,
    required String todo,
    required String doneLabel,
    required VoidCallback onTap,
  }) =>
      Expanded(
        child: done
            ? OutlinedButton.icon(
                onPressed: onTap,
                icon: Icon(Icons.check_circle, size: 18, color: Colors.green.shade700),
                label: Text(doneLabel, style: _cairo.copyWith(fontSize: 13, color: Colors.green.shade800)),
              )
            : FilledButton.icon(
                style: FilledButton.styleFrom(backgroundColor: AppTheme.primaryColor),
                onPressed: onTap,
                icon: Icon(icon, size: 18),
                label: Text(todo, style: _cairo.copyWith(fontSize: 13)),
              ),
      );

  Widget _customerTile(ShopCaptureCustomer c) {
    final sub = [
      if (c.shopName != null) c.shopName!,
      if (c.phoneNumber != null) c.phoneNumber!,
    ].join(' • ');
    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: Theme.of(context).cardColor,
        borderRadius: BorderRadius.circular(14),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(c.customerName, style: _cairo.copyWith(fontWeight: FontWeight.bold, fontSize: 15)),
          if (sub.isNotEmpty) Text(sub, style: _cairo.copyWith(fontSize: 12, color: Colors.grey)),
          if (!c.hasFollowerLocation && c.hasSalesLocation)
            Text('يوجد موقع من تطبيق المبيعات', style: _cairo.copyWith(fontSize: 12, color: Colors.orange.shade800)),
          const SizedBox(height: 8),
          Row(
            children: [
              _actionButton(
                done: c.hasFollowerLocation,
                icon: Icons.add_location_alt,
                todo: 'إضافة الموقع',
                doneLabel: 'الموقع',
                onTap: () async {
                  final hasAny = c.hasFollowerLocation || c.hasSalesLocation;
                  final changed = hasAny
                      ? await showLocationSheet(context, widget.listId, c)
                      : await _captureDirect(c);
                  if (changed && mounted) setState(() {});
                },
              ),
              const SizedBox(width: 8),
              _actionButton(
                done: c.hasPhoto,
                icon: Icons.photo_camera,
                todo: 'صورة المحل',
                doneLabel: 'الصورة',
                onTap: () async {
                  if (await openPhoto(context, widget.listId, c) && mounted) setState(() {});
                },
              ),
            ],
          ),
        ],
      ),
    );
  }

  /// No location at all yet → take it straight away (no sheet).
  Future<bool> _captureDirect(ShopCaptureCustomer c) async {
    final p = await captureLocationWithDialog(context);
    if (p == null || !mounted) return false;
    try {
      await ShopCaptureApi.saveLocation(widget.listId, c.customerId, p);
      c
        ..followerLat = p.latitude
        ..followerLng = p.longitude
        ..followerAccuracy = p.accuracy;
      if (mounted) showCaptureSnack(context, 'تم حفظ الموقع (دقة ${p.accuracy.round()} م)');
      return true;
    } catch (e) {
      if (mounted) showCaptureSnack(context, captureError(e), error: true);
      return false;
    }
  }

  @override
  Widget build(BuildContext context) {
    final shown = _shown;
    final noLoc = _all.where((c) => !c.hasFollowerLocation).length;
    final noPhoto = _all.where((c) => !c.hasPhoto).length;
    return Directionality(
      textDirection: TextDirection.rtl,
      child: AppSafeScaffold(
        appBar: AppBar(
          backgroundColor: AppTheme.primaryColor,
          foregroundColor: Colors.white,
          title: Text(widget.listName, style: _cairo),
        ),
        body: _loading
            ? const Center(child: CircularProgressIndicator())
            : _error.isNotEmpty && _all.isEmpty
                ? Center(
                    child: Column(mainAxisSize: MainAxisSize.min, children: [
                      Text(_error, style: _cairo),
                      TextButton(onPressed: _load, child: const Text('إعادة المحاولة', style: _cairo)),
                    ]),
                  )
                : Column(
                    children: [
                      Padding(
                        padding: const EdgeInsets.fromLTRB(12, 12, 12, 4),
                        child: TextField(
                          onChanged: (v) => setState(() => _query = v),
                          style: _cairo,
                          decoration: InputDecoration(
                            hintText: 'بحث بالاسم أو المحل أو الهاتف',
                            hintStyle: _cairo,
                            prefixIcon: const Icon(Icons.search),
                            isDense: true,
                            border: OutlineInputBorder(borderRadius: BorderRadius.circular(12)),
                          ),
                        ),
                      ),
                      SingleChildScrollView(
                        scrollDirection: Axis.horizontal,
                        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
                        child: Row(children: [
                          _chip(_Filter.all, 'الكل', _all.length),
                          _chip(_Filter.noLocation, 'بدون موقع', noLoc),
                          _chip(_Filter.noPhoto, 'بدون صورة', noPhoto),
                        ]),
                      ),
                      Expanded(
                        child: RefreshIndicator(
                          onRefresh: _load,
                          child: shown.isEmpty
                              ? ListView(children: const [
                                  SizedBox(height: 80),
                                  Center(child: Text('لا يوجد زبائن', style: TextStyle(fontFamily: 'Cairo', color: Colors.grey))),
                                ])
                              : ListView.separated(
                                  padding: const EdgeInsets.fromLTRB(12, 4, 12, 24),
                                  itemCount: shown.length,
                                  separatorBuilder: (_, __) => const SizedBox(height: 8),
                                  itemBuilder: (_, i) => _customerTile(shown[i]),
                                ),
                        ),
                      ),
                    ],
                  ),
      ),
    );
  }
}
