import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:geolocator/geolocator.dart';
import 'package:image_picker/image_picker.dart';
import 'package:latlong2/latlong.dart';

import '../../../core/theme/app_theme.dart';
import '../../../core/widgets/state_views.dart';

/// OpenStreetMap tiles (no API key). The user agent identifies the app as the tile policy asks.
TileLayer osmTiles() => TileLayer(
      urlTemplate: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
      userAgentPackageName: 'iq.qalaa.customer_app',
      maxNativeZoom: 19,
    );

/// Asks for location permission and returns the current position, or null with a message shown.
Future<Position?> currentPosition(BuildContext context) async {
  try {
    if (!await Geolocator.isLocationServiceEnabled()) {
      if (context.mounted) context.showSnack('خدمة الموقع (GPS) متوقفة. فعّلها من إعدادات الهاتف.', error: true);
      return null;
    }
    var perm = await Geolocator.checkPermission();
    if (perm == LocationPermission.denied) perm = await Geolocator.requestPermission();
    if (perm == LocationPermission.denied || perm == LocationPermission.deniedForever) {
      if (context.mounted) {
        context.showSnack(
          perm == LocationPermission.deniedForever ? 'إذن الموقع مرفوض. فعّله من إعدادات التطبيق.' : 'يجب السماح بالوصول إلى الموقع.',
          error: true,
        );
      }
      if (perm == LocationPermission.deniedForever) await Geolocator.openAppSettings();
      return null;
    }
    return await Geolocator.getCurrentPosition(
      locationSettings: const LocationSettings(accuracy: LocationAccuracy.high, timeLimit: Duration(seconds: 25)),
    );
  } catch (_) {
    if (context.mounted) context.showSnack('تعذّر تحديد الموقع. تأكد من تشغيل GPS وحاول مجدداً.', error: true);
    return null;
  }
}

/// Optional shop photo: taken with the camera (gallery as a fallback), previewed, replaceable, removable.
class ShopPhotoCard extends StatelessWidget {
  const ShopPhotoCard({super.key, required this.path, required this.onChanged, this.errorText});

  final String? path;
  final ValueChanged<String?> onChanged;
  final String? errorText;

  Future<void> _pick(BuildContext context, ImageSource source) async {
    try {
      final file = await ImagePicker().pickImage(source: source, maxWidth: 1600, maxHeight: 1600, imageQuality: 72);
      if (file != null) onChanged(file.path);
    } catch (_) {
      if (context.mounted) context.showSnack('تعذّر فتح الكاميرا', error: true);
    }
  }

  @override
  Widget build(BuildContext context) {
    final hasPhoto = path != null && File(path!).existsSync();
    final borderColor = errorText != null ? AppColors.danger : AppColors.border;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        ClipRRect(
          borderRadius: BorderRadius.circular(16),
          child: Container(
            height: 200,
            decoration: BoxDecoration(
              color: hasPhoto ? Colors.black : AppColors.primaryLight,
              border: Border.all(color: borderColor, width: errorText != null ? 1.5 : 1),
              borderRadius: BorderRadius.circular(16),
            ),
            child: hasPhoto
                ? Stack(
                    fit: StackFit.expand,
                    children: [
                      Image.file(File(path!), fit: BoxFit.cover),
                      PositionedDirectional(
                        top: 8,
                        end: 8,
                        child: FilledButton.tonalIcon(
                          onPressed: () => _pick(context, ImageSource.camera),
                          icon: const Icon(Icons.camera_alt_rounded, size: 18),
                          label: const Text('إعادة التصوير'),
                        ),
                      ),
                    ],
                  )
                : InkWell(
                    onTap: () => _pick(context, ImageSource.camera),
                    child: const Column(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        Icon(Icons.add_a_photo_rounded, size: 48, color: AppColors.primary),
                        SizedBox(height: 8),
                        Text('التقط صورة واجهة المحل', style: TextStyle(fontWeight: FontWeight.w800, color: AppColors.primary)),
                        Text('اختياري', style: TextStyle(color: AppColors.textMuted, fontSize: 12)),
                      ],
                    ),
                  ),
          ),
        ),
        Row(
          children: [
            TextButton.icon(
              onPressed: () => _pick(context, ImageSource.gallery),
              icon: const Icon(Icons.photo_library_outlined, size: 18),
              label: const Text('اختيار من المعرض'),
            ),
            const Spacer(),
            if (hasPhoto)
              TextButton.icon(
                style: TextButton.styleFrom(foregroundColor: AppColors.danger),
                onPressed: () => onChanged(null),
                icon: const Icon(Icons.delete_outline_rounded, size: 18),
                label: const Text('إزالة'),
              ),
          ],
        ),
        if (errorText != null) Text(errorText!, style: const TextStyle(color: AppColors.danger, fontSize: 12.5)),
      ],
    );
  }
}

/// Optional shop location: GPS fix shown on a map; tapping the map moves the pin.
class ShopLocationCard extends StatefulWidget {
  const ShopLocationCard({super.key, this.latitude, this.longitude, required this.onChanged, this.errorText});

  final double? latitude;
  final double? longitude;
  final void Function(double lat, double lng) onChanged;
  final String? errorText;

  @override
  State<ShopLocationCard> createState() => _ShopLocationCardState();
}

class _ShopLocationCardState extends State<ShopLocationCard> {
  final _map = MapController();
  bool _locating = false;

  /// The map exists only once a location is set; before that its first camera is the location itself.
  bool _mapReady = false;

  Future<void> _locate() async {
    setState(() => _locating = true);
    final pos = await currentPosition(context);
    if (!mounted) return;
    setState(() => _locating = false);
    if (pos == null) return;
    widget.onChanged(pos.latitude, pos.longitude);
    if (_mapReady) _map.move(LatLng(pos.latitude, pos.longitude), 17);
  }

  @override
  Widget build(BuildContext context) {
    final point = widget.latitude == null ? null : LatLng(widget.latitude!, widget.longitude!);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        ClipRRect(
          borderRadius: BorderRadius.circular(16),
          child: Container(
            height: 220,
            decoration: BoxDecoration(
              border: Border.all(color: widget.errorText != null ? AppColors.danger : AppColors.border),
              borderRadius: BorderRadius.circular(16),
            ),
            child: point == null
                ? InkWell(
                    onTap: _locating ? null : _locate,
                    child: Container(
                      color: AppColors.primaryLight,
                      alignment: Alignment.center,
                      child: _locating
                          ? const CircularProgressIndicator()
                          : const Column(
                              mainAxisSize: MainAxisSize.min,
                              children: [
                                Icon(Icons.add_location_alt_rounded, size: 48, color: AppColors.primary),
                                SizedBox(height: 8),
                                Text('حدد موقع المحل', style: TextStyle(fontWeight: FontWeight.w800, color: AppColors.primary)),
                                Text('قف عند المحل واضغط هنا', style: TextStyle(color: AppColors.textMuted, fontSize: 12)),
                              ],
                            ),
                    ),
                  )
                : FlutterMap(
                    mapController: _map,
                    options: MapOptions(
                      initialCenter: point,
                      initialZoom: 17,
                      onMapReady: () => _mapReady = true,
                      onTap: (_, p) => widget.onChanged(p.latitude, p.longitude),
                    ),
                    children: [
                      osmTiles(),
                      MarkerLayer(markers: [
                        Marker(
                          point: point,
                          width: 44,
                          height: 44,
                          alignment: Alignment.topCenter,
                          child: const Icon(Icons.location_on_rounded, color: AppColors.danger, size: 44),
                        ),
                      ]),
                    ],
                  ),
          ),
        ),
        const SizedBox(height: 6),
        Row(
          children: [
            Expanded(
              child: Text(
                point == null
                    ? 'يُرسل الموقع مع الطلب ليصل مندوب المبيعات إلى المحل مباشرة.'
                    : 'اضغط على الخريطة لتعديل مكان الدبوس إن لزم.',
                style: const TextStyle(color: AppColors.textMuted, fontSize: 12),
              ),
            ),
            if (point != null)
              TextButton.icon(
                onPressed: _locating ? null : _locate,
                icon: _locating
                    ? const SizedBox(width: 16, height: 16, child: CircularProgressIndicator(strokeWidth: 2))
                    : const Icon(Icons.my_location_rounded, size: 18),
                label: const Text('موقعي الحالي'),
              ),
          ],
        ),
        if (widget.errorText != null) Text(widget.errorText!, style: const TextStyle(color: AppColors.danger, fontSize: 12.5)),
      ],
    );
  }
}

/// Small non-interactive map with one pin (order detail / review).
class StaticPinMap extends StatelessWidget {
  const StaticPinMap({super.key, required this.latitude, required this.longitude, this.height = 160});

  final double latitude;
  final double longitude;
  final double height;

  @override
  Widget build(BuildContext context) {
    final p = LatLng(latitude, longitude);
    return ClipRRect(
      borderRadius: BorderRadius.circular(14),
      child: SizedBox(
        height: height,
        child: FlutterMap(
          options: MapOptions(initialCenter: p, initialZoom: 16, interactionOptions: const InteractionOptions(flags: InteractiveFlag.none)),
          children: [
            osmTiles(),
            MarkerLayer(markers: [
              Marker(point: p, width: 40, height: 40, alignment: Alignment.topCenter,
                  child: const Icon(Icons.location_on_rounded, color: AppColors.danger, size: 40)),
            ]),
          ],
        ),
      ),
    );
  }
}
