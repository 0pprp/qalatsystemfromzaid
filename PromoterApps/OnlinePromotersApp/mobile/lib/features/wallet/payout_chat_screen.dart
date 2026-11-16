import 'dart:async';
import 'dart:io';
import 'dart:typed_data';

import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:open_filex/open_filex.dart';
import 'package:path_provider/path_provider.dart';

import '../../core/app_state.dart';
import '../../core/theme/app_theme.dart';
import '../../core/utils/formatters.dart';
import '../../core/widgets/common.dart';
import '../../core/widgets/state_views.dart';
import '../../data/models.dart';
import '../../data/repository.dart';

const _maxAttachmentBytes = 15 * 1024 * 1024;

/// Private files (chat photos, the receipt) downloaded once per screen visit, keyed by their API path.
final _privateBytesProvider = FutureProvider.autoDispose.family<Uint8List, String>((ref, key) {
  final repo = ref.watch(repositoryProvider);
  final parts = key.split(':'); // receipt:<payoutId> | msg:<payoutId>:<messageId>
  return parts.first == 'receipt' ? repo.receiptBytes(int.parse(parts[1])) : repo.attachmentBytes(int.parse(parts[1]), int.parse(parts[2]));
});

/// A withdrawal request and its follow-up chat with the administration (text, photos and documents).
/// New messages are fetched every 5 seconds.
class PayoutChatScreen extends ConsumerStatefulWidget {
  const PayoutChatScreen({super.key, required this.payoutId});

  final int payoutId;

  @override
  ConsumerState<PayoutChatScreen> createState() => _PayoutChatScreenState();
}

class _PayoutChatScreenState extends ConsumerState<PayoutChatScreen> {
  Payout? _payout;
  final List<ChatMessage> _messages = [];
  Object? _error;
  bool _sending = false;
  bool _uploading = false;
  final _input = TextEditingController();
  final _scroll = ScrollController();
  Timer? _timer;

  @override
  void initState() {
    super.initState();
    _load();
    _timer = Timer.periodic(const Duration(seconds: 5), (_) => _poll());
  }

  @override
  void dispose() {
    _timer?.cancel();
    _input.dispose();
    _scroll.dispose();
    super.dispose();
  }

  Repository get _repo => ref.read(repositoryProvider);

  Future<void> _load() async {
    try {
      final (p, m) = await _repo.payout(widget.payoutId);
      if (!mounted) return;
      setState(() {
        _payout = p;
        _messages
          ..clear()
          ..addAll(m);
        _error = null;
      });
      _toBottom();
      ref.read(dataVersionProvider.notifier).bump();
    } catch (e) {
      if (mounted) setState(() => _error = e);
    }
  }

  Future<void> _poll() async {
    if (_payout == null) return;
    try {
      final fresh = await _repo.messagesAfter(widget.payoutId, _messages.isEmpty ? 0 : _messages.last.id);
      if (!mounted || fresh.isEmpty) return;
      setState(() => _messages.addAll(fresh.where((m) => !_messages.any((x) => x.id == m.id))));
      _toBottom();
      // A dashboard reply may come with a decision (and a receipt): refresh the request.
      if (fresh.any((m) => m.fromAdmin)) {
        final (p, _) = await _repo.payout(widget.payoutId);
        if (mounted) setState(() => _payout = p);
      }
    } catch (_) {}
  }

  void _toBottom() {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (_scroll.hasClients) _scroll.animateTo(_scroll.position.maxScrollExtent, duration: const Duration(milliseconds: 250), curve: Curves.easeOut);
    });
  }

  Future<void> _send() async {
    final text = _input.text.trim();
    if (text.isEmpty) return;
    setState(() => _sending = true);
    try {
      final m = await _repo.sendMessage(widget.payoutId, text);
      _input.clear();
      setState(() => _messages.add(m));
      _toBottom();
    } catch (e) {
      if (mounted) context.showError(e);
    } finally {
      if (mounted) setState(() => _sending = false);
    }
  }

  /// Photo from the gallery or a document (PDF, Word, Excel), up to 15 MB. Typed text goes with it.
  Future<void> _attach() async {
    final kind = await showModalBottomSheet<String>(
      context: context,
      builder: (ctx) => SafeArea(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            ListTile(
              leading: const Icon(Icons.photo_library_outlined),
              title: const Text('صورة'),
              subtitle: const Text('من معرض الصور'),
              onTap: () => Navigator.pop(ctx, 'image'),
            ),
            ListTile(
              leading: const Icon(Icons.description_outlined),
              title: const Text('مستند'),
              subtitle: const Text('PDF أو Word أو Excel'),
              onTap: () => Navigator.pop(ctx, 'file'),
            ),
            const SizedBox(height: 8),
          ],
        ),
      ),
    );
    if (kind == null || !mounted) return;
    PlatformFile? file;
    try {
      file = kind == 'image'
          ? await FilePicker.pickFile(type: FileType.image)
          : await FilePicker.pickFile(type: FileType.custom, allowedExtensions: const ['pdf', 'doc', 'docx', 'xls', 'xlsx']);
    } catch (_) {
      if (mounted) context.showSnack('تعذّر فتح الملفات', error: true);
      return;
    }
    if (file == null || !mounted) return;
    final length = await file.length();
    if (!mounted) return;
    if (length != null && length > _maxAttachmentBytes) {
      context.showSnack('حجم الملف يجب ألا يتجاوز 15 ميغابايت', error: true);
      return;
    }
    setState(() => _uploading = true);
    try {
      final bytes = await file.readAsBytes();
      if (bytes.length > _maxAttachmentBytes) throw const FormatException();
      final m = await _repo.sendAttachment(widget.payoutId, bytes: bytes, fileName: file.name, body: _input.text.trim());
      _input.clear();
      setState(() => _messages.add(m));
      _toBottom();
    } on FormatException {
      if (mounted) context.showSnack('حجم الملف يجب ألا يتجاوز 15 ميغابايت', error: true);
    } catch (e) {
      if (mounted) context.showError(e);
    } finally {
      if (mounted) setState(() => _uploading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final p = _payout;
    return Scaffold(
      appBar: AppBar(title: const Text('طلب السحب')),
      body: p == null
          ? (_error != null ? ErrorView(error: _error!, onRetry: _load) : const Center(child: CircularProgressIndicator()))
          : Column(
              children: [
                Padding(
                  padding: const EdgeInsets.fromLTRB(16, 4, 16, 8),
                  child: AppCard(
                    padding: const EdgeInsets.all(14),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(
                          children: [
                            Expanded(child: Text(Fmt.money(p.amount), style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 20))),
                            StatusChip(label: p.statusLabel, color: p.color),
                          ],
                        ),
                        const SizedBox(height: 4),
                        Text('${p.methodLabel}: ${p.accountNumber}', textDirection: TextDirection.rtl, style: const TextStyle(color: AppColors.textMuted)),
                        Text('طُلب في ${Fmt.dateTime(p.createdAt)}', style: const TextStyle(color: AppColors.textMuted, fontSize: 12.5)),
                        if (p.adminNote != null) ...[
                          const SizedBox(height: 6),
                          Text('ملاحظة الإدارة: ${p.adminNote}', style: TextStyle(color: p.color, fontWeight: FontWeight.w600)),
                        ],
                        if (p.hasReceipt) ...[
                          const SizedBox(height: 10),
                          _ReceiptTile(payoutId: p.id),
                        ],
                      ],
                    ),
                  ),
                ),
                const Divider(height: 1),
                Expanded(
                  child: _messages.isEmpty
                      ? const EmptyState(
                          icon: Icons.forum_outlined,
                          title: 'تابع طلبك هنا',
                          message: 'اكتب للإدارة أي استفسار عن طلب السحب، أو أرسل صورة أو مستنداً، وسيظهر ردهم في هذه المحادثة.',
                        )
                      : ListView.builder(
                          controller: _scroll,
                          padding: const EdgeInsets.all(16),
                          itemCount: _messages.length,
                          itemBuilder: (_, i) => _Bubble(payoutId: p.id, message: _messages[i]),
                        ),
                ),
                SafeArea(
                  top: false,
                  child: Padding(
                    padding: const EdgeInsets.fromLTRB(8, 6, 12, 10),
                    child: Row(
                      crossAxisAlignment: CrossAxisAlignment.end,
                      children: [
                        IconButton(
                          tooltip: 'إرسال صورة أو مستند',
                          onPressed: _uploading ? null : _attach,
                          icon: _uploading
                              ? const SizedBox(width: 20, height: 20, child: CircularProgressIndicator(strokeWidth: 2))
                              : const Icon(Icons.attach_file_rounded),
                        ),
                        Expanded(
                          child: TextField(
                            controller: _input,
                            minLines: 1,
                            maxLines: 4,
                            maxLength: 1000,
                            textInputAction: TextInputAction.newline,
                            decoration: const InputDecoration(hintText: 'اكتب رسالة…', counterText: ''),
                          ),
                        ),
                        const SizedBox(width: 8),
                        IconButton.filled(
                          onPressed: _sending ? null : _send,
                          icon: _sending
                              ? const SizedBox(width: 20, height: 20, child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white))
                              : const Icon(Icons.send_rounded),
                        ),
                      ],
                    ),
                  ),
                ),
              ],
            ),
    );
  }
}

/// «وصل التحويل»: the picture the administration attached when it marked the request paid.
class _ReceiptTile extends ConsumerWidget {
  const _ReceiptTile({required this.payoutId});

  final int payoutId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final key = 'receipt:$payoutId';
    final bytes = ref.watch(_privateBytesProvider(key));
    return InkWell(
      borderRadius: BorderRadius.circular(12),
      onTap: bytes.hasValue ? () => _openImage(context, bytes.requireValue, 'وصل التحويل') : null,
      child: Container(
        padding: const EdgeInsets.all(8),
        decoration: BoxDecoration(color: AppColors.success.withValues(alpha: 0.08), borderRadius: BorderRadius.circular(12)),
        child: Row(
          children: [
            ClipRRect(
              borderRadius: BorderRadius.circular(8),
              child: SizedBox(
                width: 52,
                height: 52,
                child: bytes.when(
                  data: (b) => Image.memory(b, fit: BoxFit.cover, cacheWidth: 200),
                  loading: () => const Center(child: SizedBox(width: 18, height: 18, child: CircularProgressIndicator(strokeWidth: 2))),
                  error: (_, _) => const Icon(Icons.broken_image_outlined),
                ),
              ),
            ),
            const SizedBox(width: 12),
            const Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('وصل التحويل', style: TextStyle(fontWeight: FontWeight.w800, color: AppColors.success)),
                  Text('اضغط لعرض الصورة', style: TextStyle(color: AppColors.textMuted, fontSize: 12.5)),
                ],
              ),
            ),
            if (bytes.hasError)
              IconButton(icon: const Icon(Icons.refresh_rounded), onPressed: () => ref.invalidate(_privateBytesProvider(key))),
          ],
        ),
      ),
    );
  }
}

class _Bubble extends ConsumerWidget {
  const _Bubble({required this.payoutId, required this.message});

  final int payoutId;
  final ChatMessage message;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final mine = !message.fromAdmin;
    final fg = mine ? Colors.white : AppColors.text;
    return Align(
      // RTL: start = right. My messages on the right, the administration on the left.
      alignment: mine ? AlignmentDirectional.centerStart : AlignmentDirectional.centerEnd,
      child: Container(
        margin: const EdgeInsets.only(bottom: 8),
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
        constraints: BoxConstraints(maxWidth: MediaQuery.sizeOf(context).width * 0.78),
        decoration: BoxDecoration(
          color: mine ? AppColors.primary : Colors.white,
          border: mine ? null : Border.all(color: AppColors.border),
          borderRadius: BorderRadius.circular(16),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            if (!mine) const Text('الإدارة', style: TextStyle(fontWeight: FontWeight.w800, color: AppColors.primary, fontSize: 12)),
            if (message.hasImage) _ChatImage(payoutId: payoutId, message: message),
            if (message.hasFile) _ChatFile(payoutId: payoutId, message: message, mine: mine),
            if (message.body.isNotEmpty)
              Padding(
                padding: EdgeInsets.only(top: message.attachmentKind == null ? 0 : 6),
                child: Text(message.body, style: TextStyle(color: fg, height: 1.5)),
              ),
            const SizedBox(height: 2),
            Text(Fmt.time(message.createdAt), style: TextStyle(fontSize: 11, color: mine ? Colors.white70 : AppColors.textMuted)),
          ],
        ),
      ),
    );
  }
}

class _ChatImage extends ConsumerWidget {
  const _ChatImage({required this.payoutId, required this.message});

  final int payoutId;
  final ChatMessage message;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final key = 'msg:$payoutId:${message.id}';
    final bytes = ref.watch(_privateBytesProvider(key));
    return GestureDetector(
      onTap: bytes.hasValue ? () => _openImage(context, bytes.requireValue, message.attachmentName ?? 'صورة') : null,
      child: ClipRRect(
        borderRadius: BorderRadius.circular(10),
        child: SizedBox(
          width: 220,
          height: 170,
          child: bytes.when(
            data: (b) => Image.memory(b, fit: BoxFit.cover, cacheWidth: 600),
            loading: () => const ColoredBox(color: Color(0x14000000), child: Center(child: CircularProgressIndicator(strokeWidth: 2))),
            error: (_, _) => ColoredBox(
              color: const Color(0x14000000),
              child: IconButton(icon: const Icon(Icons.refresh_rounded), onPressed: () => ref.invalidate(_privateBytesProvider(key))),
            ),
          ),
        ),
      ),
    );
  }
}

class _ChatFile extends ConsumerStatefulWidget {
  const _ChatFile({required this.payoutId, required this.message, required this.mine});

  final int payoutId;
  final ChatMessage message;
  final bool mine;

  @override
  ConsumerState<_ChatFile> createState() => _ChatFileState();
}

class _ChatFileState extends ConsumerState<_ChatFile> {
  bool _opening = false;

  /// Downloads the document into the app's cache and opens it with the phone's viewer.
  Future<void> _open() async {
    setState(() => _opening = true);
    try {
      final m = widget.message;
      final bytes = await ref.read(repositoryProvider).attachmentBytes(widget.payoutId, m.id);
      final dir = await getTemporaryDirectory();
      final safe = (m.attachmentName ?? 'file-${m.id}').replaceAll(RegExp(r'[\\/:*?"<>|]'), '_');
      final file = File('${dir.path}/chat/${m.id}-$safe');
      await file.parent.create(recursive: true);
      await file.writeAsBytes(bytes, flush: true);
      final result = await OpenFilex.open(file.path);
      if (result.type != ResultType.done && mounted) context.showSnack('لا يوجد تطبيق لفتح هذا الملف على الهاتف', error: true);
    } catch (e) {
      if (mounted) context.showError(e);
    } finally {
      if (mounted) setState(() => _opening = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final m = widget.message;
    final fg = widget.mine ? Colors.white : AppColors.text;
    final name = m.attachmentName ?? 'ملف';
    final icon = name.toLowerCase().endsWith('.pdf')
        ? Icons.picture_as_pdf_rounded
        : RegExp(r'\.xlsx?$').hasMatch(name.toLowerCase())
            ? Icons.table_chart_rounded
            : Icons.description_rounded;
    return InkWell(
      onTap: _opening ? null : _open,
      borderRadius: BorderRadius.circular(10),
      child: Container(
        padding: const EdgeInsets.all(10),
        decoration: BoxDecoration(color: fg.withValues(alpha: 0.1), borderRadius: BorderRadius.circular(10)),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(icon, color: fg),
            const SizedBox(width: 8),
            Flexible(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(name, maxLines: 2, overflow: TextOverflow.ellipsis, style: TextStyle(color: fg, fontWeight: FontWeight.w700)),
                  Text(m.sizeLabel, style: TextStyle(color: fg.withValues(alpha: 0.75), fontSize: 11.5)),
                ],
              ),
            ),
            const SizedBox(width: 8),
            _opening
                ? SizedBox(width: 18, height: 18, child: CircularProgressIndicator(strokeWidth: 2, color: fg))
                : Icon(Icons.download_rounded, color: fg, size: 20),
          ],
        ),
      ),
    );
  }
}

void _openImage(BuildContext context, Uint8List bytes, String title) {
  Navigator.of(context).push(MaterialPageRoute(
    builder: (_) => Scaffold(
      backgroundColor: Colors.black,
      appBar: AppBar(backgroundColor: Colors.black, foregroundColor: Colors.white, title: Text(title, maxLines: 1, overflow: TextOverflow.ellipsis)),
      body: InteractiveViewer(maxScale: 5, child: Center(child: Image.memory(bytes, fit: BoxFit.contain))),
    ),
  ));
}
