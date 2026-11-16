import 'package:flutter/material.dart';
import 'package:follower_application/shop_capture/shop_capture_api.dart';
import 'package:follower_application/utils/AppTheme.dart';
import 'package:url_launcher/url_launcher.dart';

const _cairo = TextStyle(fontFamily: 'Cairo');

void showCaptureSnack(BuildContext context, String text, {bool error = false}) {
  ScaffoldMessenger.of(context).showSnackBar(SnackBar(
    content: Text(text, style: _cairo),
    backgroundColor: error ? Colors.red.shade700 : null,
  ));
}

String captureError(Object e) => '$e'.replaceFirst('Exception: ', '');

Future<void> openInMaps(double lat, double lng) =>
    launchUrl(Uri.parse('https://www.google.com/maps?q=$lat,$lng'), mode: LaunchMode.externalApplication);

/// Takes a fresh GPS fix with a small progress dialog. Returns null when cancelled/failed (error shown).
Future<CapturedPoint?> captureLocationWithDialog(BuildContext context) async {
  showDialog<void>(
    context: context,
    barrierDismissible: false,
    builder: (_) => const PopScope(
      canPop: false,
      child: AlertDialog(
        content: Row(children: [
          SizedBox(width: 22, height: 22, child: CircularProgressIndicator(strokeWidth: 2.5)),
          SizedBox(width: 16),
          Expanded(child: Text('جاري تحديد الموقع… قف قرب المحل', style: _cairo)),
        ]),
      ),
    ),
  );
  try {
    final p = await ShopCaptureApi.captureLocation();
    if (context.mounted) Navigator.of(context, rootNavigator: true).pop();
    return p;
  } catch (e) {
    if (context.mounted) {
      Navigator.of(context, rootNavigator: true).pop();
      showCaptureSnack(context, captureError(e), error: true);
    }
    return null;
  }
}

/// Location sheet: shows one location; tapping it reveals the other one (follower ↔ sales).
/// Returns true when a new follower location was saved.
Future<bool> showLocationSheet(BuildContext context, int listId, ShopCaptureCustomer c) async {
  final saved = await showModalBottomSheet<bool>(
    context: context,
    showDragHandle: true,
    builder: (ctx) => Directionality(
      textDirection: TextDirection.rtl,
      child: _LocationSheet(listId: listId, customer: c),
    ),
  );
  return saved == true;
}

class _LocationSheet extends StatefulWidget {
  const _LocationSheet({required this.listId, required this.customer});

  final int listId;
  final ShopCaptureCustomer customer;

  @override
  State<_LocationSheet> createState() => _LocationSheetState();
}

class _LocationSheetState extends State<_LocationSheet> {
  bool _showOther = false;
  bool _saving = false;

  ShopCaptureCustomer get c => widget.customer;

  Future<void> _capture() async {
    final p = await captureLocationWithDialog(context);
    if (p == null || !mounted) return;
    setState(() => _saving = true);
    try {
      await ShopCaptureApi.saveLocation(widget.listId, c.customerId, p);
      c
        ..followerLat = p.latitude
        ..followerLng = p.longitude
        ..followerAccuracy = p.accuracy;
      if (mounted) {
        showCaptureSnack(context, 'تم حفظ الموقع (دقة ${p.accuracy.round()} م)');
        Navigator.pop(context, true);
      }
    } catch (e) {
      if (mounted) {
        setState(() => _saving = false);
        showCaptureSnack(context, captureError(e), error: true);
      }
    }
  }

  Widget _row(String title, double lat, double lng, {String? sub, VoidCallback? onTap}) => ListTile(
        contentPadding: EdgeInsets.zero,
        leading: const Icon(Icons.place, color: AppTheme.primaryColor),
        title: Text(title, style: _cairo.copyWith(fontWeight: FontWeight.bold)),
        subtitle: Text(sub ?? '${lat.toStringAsFixed(6)}, ${lng.toStringAsFixed(6)}', style: _cairo.copyWith(fontSize: 12)),
        trailing: IconButton(
          tooltip: 'فتح في الخرائط',
          icon: const Icon(Icons.map_outlined),
          onPressed: () => openInMaps(lat, lng),
        ),
        onTap: onTap,
      );

  @override
  Widget build(BuildContext context) {
    final rows = <({String title, double lat, double lng, String? sub})>[
      if (c.hasFollowerLocation)
        (
          title: 'موقعك (المتابع)',
          lat: c.followerLat!,
          lng: c.followerLng!,
          sub: c.followerAccuracy == null ? null : 'دقة ${c.followerAccuracy!.round()} م',
        ),
      if (c.hasSalesLocation)
        (title: 'موقع تطبيق المبيعات', lat: c.salesLat!, lng: c.salesLng!, sub: null),
    ];

    return SafeArea(
      child: Padding(
        padding: const EdgeInsets.fromLTRB(16, 0, 16, 16),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(c.customerName, style: _cairo.copyWith(fontWeight: FontWeight.bold, fontSize: 16)),
            const SizedBox(height: 4),
            if (rows.isEmpty)
              const Padding(
                padding: EdgeInsets.symmetric(vertical: 12),
                child: Text('لا يوجد موقع لهذا الزبون بعد', style: _cairo),
              )
            else ...[
              _row(rows.first.title, rows.first.lat, rows.first.lng,
                  sub: rows.length > 1
                      ? [if (rows.first.sub != null) rows.first.sub!, 'اضغط لعرض الموقع الآخر'].join(' — ')
                      : rows.first.sub,
                  onTap: rows.length > 1 ? () => setState(() => _showOther = !_showOther) : null),
              if (_showOther && rows.length > 1) _row(rows[1].title, rows[1].lat, rows[1].lng),
            ],
            const SizedBox(height: 8),
            FilledButton.icon(
              style: FilledButton.styleFrom(backgroundColor: AppTheme.primaryColor),
              onPressed: _saving ? null : _capture,
              icon: const Icon(Icons.my_location),
              label: Text(c.hasFollowerLocation ? 'تحديث موقعي الآن' : 'إضافة الموقع الآن', style: _cairo),
            ),
          ],
        ),
      ),
    );
  }
}

/// Photo: view the saved one (with «إعادة التقاط») or take a new one. Returns true when a photo was uploaded.
Future<bool> openPhoto(BuildContext context, int listId, ShopCaptureCustomer c) async {
  if (!c.hasPhoto) return takeAndUploadPhoto(context, listId, c);
  final url = await ShopCaptureApi.photoUrl(listId, c.customerId, version: c.photoAt);
  if (!context.mounted) return false;
  final changed = await Navigator.of(context).push<bool>(MaterialPageRoute(
    builder: (_) => _PhotoPage(listId: listId, customer: c, url: url),
  ));
  return changed == true;
}

Future<bool> takeAndUploadPhoto(BuildContext context, int listId, ShopCaptureCustomer c) async {
  final file = await ShopCaptureApi.takePhoto();
  if (file == null || !context.mounted) return false;
  showDialog<void>(
    context: context,
    barrierDismissible: false,
    builder: (_) => const PopScope(
      canPop: false,
      child: AlertDialog(
        content: Row(children: [
          SizedBox(width: 22, height: 22, child: CircularProgressIndicator(strokeWidth: 2.5)),
          SizedBox(width: 16),
          Text('جاري رفع الصورة…', style: _cairo),
        ]),
      ),
    ),
  );
  try {
    await ShopCaptureApi.uploadPhoto(listId, c.customerId, file);
    c
      ..hasPhoto = true
      ..photoAt = DateTime.now().toIso8601String();
    if (context.mounted) {
      Navigator.of(context, rootNavigator: true).pop();
      showCaptureSnack(context, 'تم حفظ صورة المحل');
    }
    return true;
  } catch (e) {
    if (context.mounted) {
      Navigator.of(context, rootNavigator: true).pop();
      showCaptureSnack(context, captureError(e), error: true);
    }
    return false;
  }
}

class _PhotoPage extends StatefulWidget {
  const _PhotoPage({required this.listId, required this.customer, required this.url});

  final int listId;
  final ShopCaptureCustomer customer;
  final String url;

  @override
  State<_PhotoPage> createState() => _PhotoPageState();
}

class _PhotoPageState extends State<_PhotoPage> {
  late String _url = widget.url;
  bool _changed = false;

  Future<void> _retake() async {
    if (await takeAndUploadPhoto(context, widget.listId, widget.customer)) {
      final url = await ShopCaptureApi.photoUrl(widget.listId, widget.customer.customerId);
      if (mounted) {
        setState(() {
          _url = url;
          _changed = true;
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return PopScope(
      canPop: false,
      onPopInvokedWithResult: (didPop, _) {
        if (!didPop) Navigator.pop(context, _changed);
      },
      child: Directionality(
        textDirection: TextDirection.rtl,
        child: Scaffold(
          backgroundColor: Colors.black,
          appBar: AppBar(
            backgroundColor: Colors.black,
            foregroundColor: Colors.white,
            title: Text(widget.customer.customerName, style: _cairo),
          ),
          body: Column(
            children: [
              Expanded(
                child: InteractiveViewer(
                  child: Center(
                    child: Image.network(
                      _url,
                      fit: BoxFit.contain,
                      loadingBuilder: (_, child, p) =>
                          p == null ? child : const CircularProgressIndicator(color: Colors.white),
                      errorBuilder: (_, __, ___) =>
                          const Text('تعذر تحميل الصورة', style: TextStyle(fontFamily: 'Cairo', color: Colors.white)),
                    ),
                  ),
                ),
              ),
              SafeArea(
                child: Padding(
                  padding: const EdgeInsets.all(12),
                  child: SizedBox(
                    width: double.infinity,
                    child: FilledButton.icon(
                      style: FilledButton.styleFrom(backgroundColor: AppTheme.primaryColor),
                      onPressed: _retake,
                      icon: const Icon(Icons.photo_camera),
                      label: const Text('إعادة التقاط الصورة', style: _cairo),
                    ),
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
