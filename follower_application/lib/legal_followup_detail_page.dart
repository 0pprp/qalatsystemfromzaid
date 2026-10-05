import 'dart:convert';
import 'dart:developer' as developer;
import 'dart:io';

import 'package:follower_application/config/app_env.dart';
import 'package:follower_application/ui/app_safe_scaffold.dart';
import 'package:follower_application/utils/AppTheme.dart';
import 'package:flutter/material.dart';
import 'package:geolocator/geolocator.dart';
import 'package:http/http.dart' as http;
import 'package:image_picker/image_picker.dart';
import 'package:shared_preferences/shared_preferences.dart';

class LegalFollowupDetailPage extends StatefulWidget {
  const LegalFollowupDetailPage({super.key, required this.caseId});

  final int caseId;

  @override
  State<LegalFollowupDetailPage> createState() =>
      _LegalFollowupDetailPageState();
}

class _DocSlot {
  _DocSlot({required this.type, required this.label});

  final String type;
  final String label;
  File? localFile;
  bool uploading = false;
  bool uploaded = false;
  String? remoteUrl;
}

class _LegalFollowupDetailPageState extends State<LegalFollowupDetailPage> {
  static const _green = Color(0xFF2E7D32);

  bool _loading = true;
  bool _submitting = false;
  bool _locating = false;
  String _error = '';
  Map<String, dynamic>? _item;
  final _note = TextEditingController();
  String _asyncId = '';
  String _link = '';

  double? _lat;
  double? _lng;
  bool _locationSelected = false;

  late final List<_DocSlot> _slots = [
    _DocSlot(type: 'NationalIdFront', label: 'البطاقة الوطنية - أمامية'),
    _DocSlot(type: 'NationalIdBack', label: 'البطاقة الوطنية - خلفية'),
    _DocSlot(type: 'ResidenceCardFront', label: 'بطاقة السكن - أمامية'),
    _DocSlot(type: 'ResidenceCardBack', label: 'بطاقة السكن - خلفية'),
    _DocSlot(type: 'ResidenceCertificate', label: 'مضبطة تأييد سكن'),
  ];

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _note.dispose();
    super.dispose();
  }

  void _log(String msg, [Object? err]) {
    developer.log(msg, name: 'LegalFollowup', error: err);
    debugPrint('[LegalFollowup] $msg${err == null ? '' : ' :: $err'}');
  }

  String _pick(Map? m, List<String> keys) {
    if (m == null) return '';
    for (final k in keys) {
      final v = m[k];
      if (v != null && '$v'.trim().isNotEmpty) return '$v'.trim();
    }
    return '';
  }

  bool get _canSubmit {
    if (!_locationSelected || _lat == null || _lng == null) return false;
    return _slots.every((s) => s.uploaded || s.remoteUrl != null);
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = '';
    });
    try {
      final prefs = await SharedPreferences.getInstance();
      _asyncId = prefs.getString('AsyncId') ?? '';
      _link = AppEnv.apiBase(fallback: prefs.getString('LinkDelegate') ?? '');
      _log('load case=${widget.caseId} link=$_link');
      final uri = Uri.parse('${_link}Followers/LegalFollowups/${widget.caseId}')
          .replace(queryParameters: {'asyncId': _asyncId});
      final res = await http.get(uri).timeout(const Duration(seconds: 30));
      _log('GET detail status=${res.statusCode}');
      if (res.statusCode != 200) {
        setState(() {
          _loading = false;
          _error = 'تعذر تحميل التفاصيل';
        });
        return;
      }
      final decoded = json.decode(res.body);
      if (!mounted) return;
      final map = decoded is Map
          ? Map<String, dynamic>.from(decoded)
          : <String, dynamic>{};
      _applyServerState(map);
      setState(() {
        _item = map;
        _loading = false;
      });
    } catch (e) {
      _log('load failed', e);
      if (!mounted) return;
      setState(() {
        _loading = false;
        _error = 'تعذر الاتصال بالخادم';
      });
    }
  }

  void _applyServerState(Map<String, dynamic> map) {
    final lat = double.tryParse(_pick(map, ['latitude', 'Latitude']));
    final lng = double.tryParse(_pick(map, ['longitude', 'Longitude']));
    final selected = map['locationSelected'] == true ||
        map['LocationSelected'] == true ||
        (lat != null && lng != null);
    if (selected && lat != null && lng != null) {
      _lat = lat;
      _lng = lng;
      _locationSelected = true;
    }

    final docs = map['documents'] ?? map['Documents'];
    final docList = docs is List
        ? docs
            .whereType<Map>()
            .map((e) => Map<String, dynamic>.from(e))
            .toList()
        : <Map<String, dynamic>>[];

    for (final slot in _slots) {
      Map<String, dynamic>? match;
      for (final d in docList) {
        if (_pick(d, ['documentType', 'DocumentType']).toLowerCase() ==
            slot.type.toLowerCase()) {
          match = d;
          break;
        }
      }
      if (match != null) {
        slot.uploaded = true;
        slot.remoteUrl = _pick(match, ['downloadUrl', 'DownloadUrl']);
      }
    }
  }

  String _imageUrl(String path) {
    if (path.startsWith('http')) return path;
    final base =
        _link.endsWith('/') ? _link.substring(0, _link.length - 1) : _link;
    final p = path.startsWith('/') ? path : '/$path';
    final q = p.contains('?') ? '&' : '?';
    return '$base$p${q}asyncId=${Uri.encodeComponent(_asyncId)}';
  }

  Future<void> _captureLocation() async {
    if (_locating) return;
    setState(() => _locating = true);
    _log('capturing location…');
    try {
      var permission = await Geolocator.checkPermission();
      if (permission == LocationPermission.denied) {
        permission = await Geolocator.requestPermission();
      }
      if (permission == LocationPermission.denied ||
          permission == LocationPermission.deniedForever) {
        throw Exception('يجب السماح بصلاحية الموقع لتحديد عنوان المحل');
      }
      final enabled = await Geolocator.isLocationServiceEnabled();
      if (!enabled) {
        throw Exception('فعّل خدمة الموقع (GPS) ثم أعد المحاولة');
      }
      final pos = await Geolocator.getCurrentPosition(
        locationSettings: const LocationSettings(
          accuracy: LocationAccuracy.high,
          timeLimit: Duration(seconds: 25),
        ),
      );
      _log('gps lat=${pos.latitude} lng=${pos.longitude}');

      final uri =
          Uri.parse('${_link}Followers/LegalFollowups/${widget.caseId}/location');
      final res = await http
          .put(
            uri,
            headers: {'Content-Type': 'application/json'},
            body: json.encode({
              'asyncId': _asyncId,
              'latitude': pos.latitude,
              'longitude': pos.longitude,
            }),
          )
          .timeout(const Duration(seconds: 30));
      _log('PUT location status=${res.statusCode} body=${res.body}');
      if (res.statusCode < 200 || res.statusCode >= 300) {
        String msg = 'تعذر حفظ موقع المحل';
        try {
          final body = json.decode(res.body);
          if (body is Map && body['message'] != null) msg = '${body['message']}';
        } catch (_) {}
        throw Exception(msg);
      }

      if (!mounted) return;
      setState(() {
        _lat = pos.latitude;
        _lng = pos.longitude;
        _locationSelected = true;
        _locating = false;
      });
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(
          content: Text('تم تحديد عنوان المحل',
              style: TextStyle(fontFamily: 'Cairo'))));
    } catch (e) {
      _log('location failed', e);
      if (!mounted) return;
      setState(() => _locating = false);
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(
          content: Text(e.toString().replaceFirst('Exception: ', ''),
              style: const TextStyle(fontFamily: 'Cairo'))));
    }
  }

  Future<void> _pickDoc(_DocSlot slot) async {
    try {
      final picker = ImagePicker();
      final x = await picker.pickImage(
        source: ImageSource.camera,
        imageQuality: 85,
        maxWidth: 2000,
      );
      if (x == null) return;
      setState(() {
        slot.localFile = File(x.path);
        slot.uploading = true;
      });
      _log('upload ${slot.type} path=${x.path}');

      final uri = Uri.parse(
              '${_link}Followers/LegalFollowups/${widget.caseId}/documents')
          .replace(queryParameters: {'asyncId': _asyncId});
      final req = http.MultipartRequest('POST', uri);
      req.fields['documentType'] = slot.type;
      req.fields['asyncId'] = _asyncId;
      req.files.add(await http.MultipartFile.fromPath('file', x.path,
          filename: '${slot.type}.jpg'));
      final streamed =
          await req.send().timeout(const Duration(seconds: 60));
      final res = await http.Response.fromStream(streamed);
      _log('upload ${slot.type} status=${res.statusCode} body=${res.body}');
      if (res.statusCode < 200 || res.statusCode >= 300) {
        String msg = 'تعذر رفع ${slot.label}';
        try {
          final body = json.decode(res.body);
          if (body is Map && body['message'] != null) msg = '${body['message']}';
        } catch (_) {}
        throw Exception(msg);
      }
      final body = json.decode(res.body);
      if (!mounted) return;
      setState(() {
        slot.uploading = false;
        slot.uploaded = true;
        if (body is Map) {
          slot.remoteUrl =
              _pick(Map<String, dynamic>.from(body), ['downloadUrl', 'DownloadUrl']);
        }
      });
    } catch (e) {
      _log('upload failed ${slot.type}', e);
      if (!mounted) return;
      setState(() => slot.uploading = false);
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(
          content: Text(e.toString().replaceFirst('Exception: ', ''),
              style: const TextStyle(fontFamily: 'Cairo'))));
    }
  }

  Future<void> _submit() async {
    if (!_canSubmit || _submitting) return;
    setState(() => _submitting = true);
    _log('submit complete');
    try {
      final uri =
          Uri.parse('${_link}Followers/LegalFollowups/${widget.caseId}/complete');
      final res = await http
          .put(
            uri,
            headers: {'Content-Type': 'application/json'},
            body: json.encode({
              'asyncId': _asyncId,
              'note': _note.text.trim(),
            }),
          )
          .timeout(const Duration(seconds: 30));
      _log('complete status=${res.statusCode} body=${res.body}');
      if (!mounted) return;
      if (res.statusCode >= 200 && res.statusCode < 300) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(
            content: Text('تم إرسال المتابعة',
                style: TextStyle(fontFamily: 'Cairo'))));
        Navigator.pop(context);
      } else {
        String msg = 'تعذر الإرسال';
        try {
          final body = json.decode(res.body);
          if (body is Map && body['message'] != null) msg = '${body['message']}';
        } catch (_) {}
        ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(content: Text(msg, style: const TextStyle(fontFamily: 'Cairo'))));
      }
    } catch (e) {
      _log('submit failed', e);
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(
          content: Text('تعذر الاتصال', style: TextStyle(fontFamily: 'Cairo'))));
    } finally {
      if (mounted) setState(() => _submitting = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final status = _pick(_item, ['status', 'Status']);
    final canEdit = status == 'InProgress' || status.isEmpty;

    return AppSafeScaffold(
      appBar: AppBar(
        title: Text('متابعة #${widget.caseId}',
            style: const TextStyle(fontFamily: 'Cairo')),
        backgroundColor: AppTheme.primaryColor,
      ),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : _error.isNotEmpty
              ? Center(
                  child: Text(_error,
                      style: const TextStyle(fontFamily: 'Cairo')))
              : ListView(
                  padding: const EdgeInsets.all(16),
                  children: [
                    Text(_pick(_item, ['customerName', 'CustomerName']),
                        style: const TextStyle(
                            fontFamily: 'Cairo',
                            fontSize: 20,
                            fontWeight: FontWeight.bold)),
                    const SizedBox(height: 8),
                    Text(
                        'الهاتف: ${_pick(_item, ['customerPhone', 'CustomerPhone'])}',
                        style: const TextStyle(fontFamily: 'Cairo')),
                    Text(
                        'العنوان المسجّل: ${_pick(_item, ['customerAddress', 'CustomerAddress'])}',
                        style: const TextStyle(fontFamily: 'Cairo')),
                    Text(
                        'الحالة: ${_pick(_item, ['statusLabel', 'StatusLabel'])}',
                        style: const TextStyle(fontFamily: 'Cairo')),
                    if (_pick(_item, ['note', 'Note']).isNotEmpty)
                      Text('ملاحظة: ${_pick(_item, ['note', 'Note'])}',
                          style: const TextStyle(fontFamily: 'Cairo')),
                    const SizedBox(height: 16),
                    const Text('عنوان المحل',
                        style: TextStyle(
                            fontFamily: 'Cairo',
                            fontWeight: FontWeight.bold,
                            fontSize: 16)),
                    const SizedBox(height: 8),
                    ElevatedButton.icon(
                      onPressed: (!canEdit || _locating) ? null : _captureLocation,
                      icon: _locating
                          ? const SizedBox(
                              width: 18,
                              height: 18,
                              child: CircularProgressIndicator(
                                  strokeWidth: 2, color: Colors.white))
                          : Icon(_locationSelected
                              ? Icons.check_circle
                              : Icons.my_location),
                      label: Text(
                          _locationSelected ? 'تم التحديد' : 'تحديد عنوان المحل',
                          style: const TextStyle(fontFamily: 'Cairo')),
                      style: ElevatedButton.styleFrom(
                        backgroundColor:
                            _locationSelected ? _green : AppTheme.primaryColor,
                        foregroundColor: Colors.white,
                        disabledBackgroundColor:
                            _locationSelected ? _green : null,
                        disabledForegroundColor: Colors.white,
                        minimumSize: const Size.fromHeight(48),
                      ),
                    ),
                    if (_locationSelected && _lat != null && _lng != null)
                      Padding(
                        padding: const EdgeInsets.only(top: 6),
                        child: Text(
                          'الموقع: ${_lat!.toStringAsFixed(5)}, ${_lng!.toStringAsFixed(5)}',
                          style: const TextStyle(
                              fontFamily: 'Cairo', color: Colors.black54),
                        ),
                      ),
                    const SizedBox(height: 20),
                    const Text('المستمسكات (إجبارية)',
                        style: TextStyle(
                            fontFamily: 'Cairo',
                            fontWeight: FontWeight.bold,
                            fontSize: 16)),
                    const SizedBox(height: 8),
                    ..._slots.map((slot) => _DocTile(
                          slot: slot,
                          canEdit: canEdit,
                          imageUrl: slot.remoteUrl != null &&
                                  slot.remoteUrl!.isNotEmpty
                              ? _imageUrl(slot.remoteUrl!)
                              : null,
                          onPick: () => _pickDoc(slot),
                        )),
                    if (canEdit) ...[
                      const SizedBox(height: 12),
                      TextField(
                        controller: _note,
                        decoration: const InputDecoration(
                          labelText: 'ملاحظة (اختياري)',
                          border: OutlineInputBorder(),
                        ),
                        style: const TextStyle(fontFamily: 'Cairo'),
                        maxLines: 2,
                      ),
                      const SizedBox(height: 12),
                      ElevatedButton(
                        onPressed:
                            (_canSubmit && !_submitting) ? _submit : null,
                        style: ElevatedButton.styleFrom(
                          backgroundColor: AppTheme.primaryColor,
                          foregroundColor: Colors.white,
                          disabledBackgroundColor: Colors.grey.shade400,
                          minimumSize: const Size.fromHeight(52),
                        ),
                        child: _submitting
                            ? const SizedBox(
                                width: 22,
                                height: 22,
                                child: CircularProgressIndicator(
                                    strokeWidth: 2, color: Colors.white))
                            : const Text('إرسال المتابعة',
                                style: TextStyle(
                                    fontFamily: 'Cairo',
                                    fontWeight: FontWeight.bold)),
                      ),
                      if (!_canSubmit)
                        const Padding(
                          padding: EdgeInsets.only(top: 8),
                          child: Text(
                            'الزر يتفعّل بعد تحديد الموقع ورفع كل المستمسكات',
                            style: TextStyle(
                                fontFamily: 'Cairo',
                                color: Colors.black54,
                                fontSize: 12),
                          ),
                        ),
                    ],
                  ],
                ),
    );
  }
}

class _DocTile extends StatelessWidget {
  const _DocTile({
    required this.slot,
    required this.canEdit,
    required this.onPick,
    this.imageUrl,
  });

  final _DocSlot slot;
  final bool canEdit;
  final VoidCallback onPick;
  final String? imageUrl;

  @override
  Widget build(BuildContext context) {
    final done = slot.uploaded || slot.remoteUrl != null;
    return Card(
      margin: const EdgeInsets.only(bottom: 10),
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Row(
              children: [
                Icon(
                  done ? Icons.check_circle : Icons.photo_camera_outlined,
                  color: done ? const Color(0xFF2E7D32) : Colors.grey,
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: Text(slot.label,
                      style: const TextStyle(
                          fontFamily: 'Cairo', fontWeight: FontWeight.w600)),
                ),
                if (slot.uploading)
                  const SizedBox(
                      width: 18,
                      height: 18,
                      child: CircularProgressIndicator(strokeWidth: 2)),
              ],
            ),
            const SizedBox(height: 8),
            if (slot.localFile != null)
              ClipRRect(
                borderRadius: BorderRadius.circular(8),
                child: Image.file(slot.localFile!,
                    height: 140, fit: BoxFit.cover),
              )
            else if (imageUrl != null && imageUrl!.isNotEmpty)
              ClipRRect(
                borderRadius: BorderRadius.circular(8),
                child: Image.network(
                  imageUrl!,
                  height: 140,
                  fit: BoxFit.cover,
                  errorBuilder: (_, __, ___) => const Text(
                      'تعذر عرض الصورة',
                      style: TextStyle(fontFamily: 'Cairo')),
                ),
              ),
            if (canEdit) ...[
              const SizedBox(height: 8),
              OutlinedButton.icon(
                onPressed: slot.uploading ? null : onPick,
                icon: const Icon(Icons.camera_alt_outlined),
                label: Text(done ? 'إعادة التصوير' : 'تصوير ورفع',
                    style: const TextStyle(fontFamily: 'Cairo')),
              ),
            ],
          ],
        ),
      ),
    );
  }
}
