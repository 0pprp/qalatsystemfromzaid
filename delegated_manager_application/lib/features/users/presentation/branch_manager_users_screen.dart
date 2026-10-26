import 'package:delegated_manager_application/core/network/api_client.dart';
import 'package:delegated_manager_application/core/theme/app_theme.dart';
import 'package:delegated_manager_application/core/widgets/state_views.dart';
import 'package:flutter/material.dart';

class BranchManagerUsersRepository {
  static Future<List<Map<String, dynamic>>> listCities() async {
    final data =
        await ApiClient.get('delegated-manager/branch-manager-users/cities');
    final list = data is List ? data : <dynamic>[];
    return list.map((e) => Map<String, dynamic>.from(e as Map)).toList();
  }

  static Future<List<Map<String, dynamic>>> listUsers() async {
    final data = await ApiClient.get('delegated-manager/branch-manager-users');
    final list = data is List ? data : <dynamic>[];
    return list.map((e) => Map<String, dynamic>.from(e as Map)).toList();
  }

  static Future<Map<String, dynamic>> create({
    required String userName,
    required String password,
    required String displayName,
    required List<String> cityValues,
  }) async {
    final data =
        await ApiClient.post('delegated-manager/branch-manager-users', body: {
      'userName': userName,
      'password': password,
      'displayName': displayName,
      'cityValues': cityValues,
    });
    return data is Map ? Map<String, dynamic>.from(data) : {};
  }

  static Future<Map<String, dynamic>> update({
    required String id,
    required String userName,
    String? password,
    required String displayName,
    required bool isActive,
    required List<String> cityValues,
  }) async {
    final body = <String, dynamic>{
      'userName': userName,
      'displayName': displayName,
      'isActive': isActive,
      'cityValues': cityValues,
    };
    if ((password ?? '').isNotEmpty) body['password'] = password;
    final data = await ApiClient.put(
        'delegated-manager/branch-manager-users/$id',
        body: body);
    return data is Map ? Map<String, dynamic>.from(data) : {};
  }

  static Future<void> delete(String id) async {
    await ApiClient.delete('delegated-manager/branch-manager-users/$id');
  }
}

/// إدارة حسابات تطبيق إدارة الفرع فقط (إنشاء / تعديل / محافظات).
class BranchManagerUsersScreen extends StatefulWidget {
  const BranchManagerUsersScreen({super.key});

  @override
  State<BranchManagerUsersScreen> createState() =>
      BranchManagerUsersScreenState();
}

class BranchManagerUsersScreenState extends State<BranchManagerUsersScreen> {
  bool _loading = true;
  String? _error;
  List<Map<String, dynamic>> _users = [];
  List<Map<String, dynamic>> _cities = [];

  @override
  void initState() {
    super.initState();
    reload();
  }

  Future<void> reload() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final cities = await BranchManagerUsersRepository.listCities();
      final users = await BranchManagerUsersRepository.listUsers();
      if (!mounted) return;
      setState(() {
        _cities = cities;
        _users = users;
        _loading = false;
      });
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _error = e.message;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _error = '$e';
      });
    }
  }

  /// Called from AppBar «إنشاء».
  Future<void> openCreate() => _openEditor();

  Future<void> _openEditor({Map<String, dynamic>? existing}) async {
    var cities = _cities;
    if (cities.isEmpty) {
      try {
        cities = await BranchManagerUsersRepository.listCities();
        if (mounted) setState(() => _cities = cities);
      } catch (_) {}
    }
    if (!mounted) return;
    if (cities.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('تعذر تحميل المحافظات — حدّث الصفحة وحاول مرة أخرى'),
        ),
      );
      return;
    }
    final changed = await Navigator.of(context).push<bool>(
      MaterialPageRoute(
        builder: (_) => BranchManagerUserEditorPage(
          cities: cities,
          existing: existing,
        ),
      ),
    );
    if (changed == true && mounted) await reload();
  }

  Future<void> _delete(Map<String, dynamic> user) async {
    final id = '${user['id'] ?? user['Id'] ?? ''}';
    if (id.isEmpty) return;
    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('حذف مستخدم إدارة الفرع'),
        content: Text(
          'حذف «${user['displayName'] ?? user['userName']}»؟ لن يستطيع دخول تطبيق إدارة الفرع.',
        ),
        actions: [
          TextButton(
              onPressed: () => Navigator.pop(ctx, false),
              child: const Text('إلغاء')),
          FilledButton(
              onPressed: () => Navigator.pop(ctx, true),
              child: const Text('حذف')),
        ],
      ),
    );
    if (ok != true) return;
    try {
      await BranchManagerUsersRepository.delete(id);
      if (mounted) await reload();
    } on ApiException catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context)
          .showSnackBar(SnackBar(content: Text(e.message)));
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) return const LoadingView();

    return Scaffold(
      floatingActionButton: FloatingActionButton.extended(
        onPressed: openCreate,
        icon: const Icon(Icons.person_add_alt_1),
        label: const Text('إنشاء مستخدم إدارة الفرع'),
      ),
      body: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Container(
            width: double.infinity,
            margin: const EdgeInsets.fromLTRB(
                AppSpacing.sm, AppSpacing.sm, AppSpacing.sm, 0),
            padding: const EdgeInsets.all(AppSpacing.md),
            decoration: BoxDecoration(
              color: AppColors.primary.withValues(alpha: 0.08),
              borderRadius: BorderRadius.circular(AppRadius.md),
              border: Border.all(color: AppColors.primary.withValues(alpha: 0.25)),
            ),
            child: const Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'حسابات تطبيق إدارة الفرع',
                  style: TextStyle(
                    fontWeight: FontWeight.w800,
                    fontSize: 16,
                  ),
                ),
                SizedBox(height: 6),
                Text(
                  'من هنا فقط تُنشأ أسماء الدخول وكلمات المرور والمحافظات المسموحة لتطبيق إدارة الفرع. بدون حساب هنا لن يفتح التطبيق.',
                  style: TextStyle(fontSize: 13, color: AppColors.muted, height: 1.35),
                ),
              ],
            ),
          ),
          if (_error != null)
            Padding(
              padding: const EdgeInsets.all(AppSpacing.sm),
              child: Material(
                color: Colors.red.shade50,
                borderRadius: BorderRadius.circular(AppRadius.md),
                child: ListTile(
                  leading: const Icon(Icons.error_outline, color: Colors.red),
                  title: Text(_error!, style: const TextStyle(fontSize: 13)),
                  trailing: TextButton(onPressed: reload, child: const Text('إعادة')),
                ),
              ),
            ),
          Expanded(
            child: _users.isEmpty
                ? Center(
                    child: Padding(
                      padding: const EdgeInsets.all(AppSpacing.lg),
                      child: Column(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Icon(Icons.manage_accounts_outlined,
                              size: 56, color: AppColors.muted),
                          const SizedBox(height: 12),
                          const Text(
                            'لا يوجد مستخدمون بعد',
                            style: TextStyle(
                              fontSize: 17,
                              fontWeight: FontWeight.w700,
                            ),
                          ),
                          const SizedBox(height: 8),
                          const Text(
                            'أنشئ أول حساب لمدير فرع ليتمكن من فتح تطبيق إدارة الفرع واختيار محافظاته.',
                            textAlign: TextAlign.center,
                            style: TextStyle(color: AppColors.muted, height: 1.4),
                          ),
                          const SizedBox(height: 20),
                          FilledButton.icon(
                            onPressed: openCreate,
                            icon: const Icon(Icons.person_add_alt_1),
                            label: const Text('إنشاء مستخدم إدارة الفرع'),
                          ),
                        ],
                      ),
                    ),
                  )
                : RefreshIndicator(
                    onRefresh: reload,
                    child: ListView.separated(
                      padding: const EdgeInsets.fromLTRB(
                          AppSpacing.sm, AppSpacing.sm, AppSpacing.sm, 96),
                      itemCount: _users.length,
                      separatorBuilder: (context, index) =>
                          const SizedBox(height: 8),
                      itemBuilder: (ctx, i) {
                        final u = _users[i];
                        final cities = (u['cities'] as List?) ?? [];
                        final active = u['isActive'] != false;
                        final cityNames = cities
                            .map((c) =>
                                '${(c as Map)['cityName'] ?? c['cityValue'] ?? ''}')
                            .where((s) => s.isNotEmpty)
                            .join(' · ');
                        return Card(
                          child: ListTile(
                            leading: CircleAvatar(
                              backgroundColor: active
                                  ? AppColors.primary
                                  : AppColors.line,
                              child: const Icon(Icons.storefront,
                                  color: Colors.white, size: 18),
                            ),
                            title: Text(
                              '${u['displayName'] ?? u['userName'] ?? ''}',
                              style: TextStyle(
                                fontWeight: FontWeight.w700,
                                color:
                                    active ? AppColors.text : AppColors.muted,
                              ),
                            ),
                            subtitle: Text(
                              [
                                'اسم الدخول: ${u['userName'] ?? ''}',
                                if (!active) 'الحساب موقوف',
                                if (cityNames.isNotEmpty)
                                  'المحافظات: $cityNames',
                              ].join('\n'),
                            ),
                            isThreeLine: true,
                            trailing: PopupMenuButton<String>(
                              onSelected: (v) {
                                if (v == 'edit') _openEditor(existing: u);
                                if (v == 'delete') _delete(u);
                              },
                              itemBuilder: (_) => const [
                                PopupMenuItem(
                                    value: 'edit', child: Text('تعديل')),
                                PopupMenuItem(
                                    value: 'delete', child: Text('حذف')),
                              ],
                            ),
                            onTap: () => _openEditor(existing: u),
                          ),
                        );
                      },
                    ),
                  ),
          ),
        ],
      ),
    );
  }
}

class BranchManagerUserEditorPage extends StatefulWidget {
  const BranchManagerUserEditorPage({
    super.key,
    required this.cities,
    this.existing,
  });

  final List<Map<String, dynamic>> cities;
  final Map<String, dynamic>? existing;

  @override
  State<BranchManagerUserEditorPage> createState() =>
      _BranchManagerUserEditorPageState();
}

class _BranchManagerUserEditorPageState
    extends State<BranchManagerUserEditorPage> {
  late final TextEditingController _user;
  late final TextEditingController _pass;
  late final TextEditingController _display;
  late bool _active;
  late Set<String> _selected;
  bool _saving = false;
  bool _obscure = true;

  @override
  void initState() {
    super.initState();
    final e = widget.existing;
    _user = TextEditingController(text: '${e?['userName'] ?? ''}');
    _pass = TextEditingController();
    _display = TextEditingController(text: '${e?['displayName'] ?? ''}');
    _active = e == null || e['isActive'] != false;
    final cities = (e?['cities'] as List?) ?? [];
    _selected = {
      for (final c in cities)
        '${(c as Map)['cityValue'] ?? c['value'] ?? ''}'.trim(),
    }..removeWhere((s) => s.isEmpty);
  }

  @override
  void dispose() {
    _user.dispose();
    _pass.dispose();
    _display.dispose();
    super.dispose();
  }

  String _cityValue(Map<String, dynamic> c) =>
      '${c['value'] ?? c['cityValue'] ?? c['database'] ?? ''}'.trim();

  String _cityName(Map<String, dynamic> c) =>
      '${c['name'] ?? c['cityName'] ?? _cityValue(c)}';

  Future<void> _save() async {
    final userName = _user.text.trim();
    final displayRaw = _display.text.trim();
    final pass = _pass.text;
    final isNew = widget.existing == null;
    if (displayRaw.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
            content: Text('أدخل الاسم الظاهر — يظهر في ترحيب تطبيق إدارة الفرع')),
      );
      return;
    }
    final display = displayRaw;
    if (userName.length < 3) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('اسم المستخدم 3 أحرف على الأقل')),
      );
      return;
    }
    if (isNew && pass.length < 4) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('كلمة المرور 4 أحرف على الأقل')),
      );
      return;
    }
    if (_selected.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('اختر محافظة واحدةً على الأقل')),
      );
      return;
    }
    setState(() => _saving = true);
    try {
      if (isNew) {
        await BranchManagerUsersRepository.create(
          userName: userName,
          password: pass,
          displayName: display,
          cityValues: _selected.toList(),
        );
      } else {
        await BranchManagerUsersRepository.update(
          id: '${widget.existing!['id'] ?? widget.existing!['Id']}',
          userName: userName,
          password: pass.isEmpty ? null : pass,
          displayName: display,
          isActive: _active,
          cityValues: _selected.toList(),
        );
      }
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(isNew
              ? 'تم إنشاء مستخدم تطبيق إدارة الفرع'
              : 'تم حفظ التعديلات'),
        ),
      );
      Navigator.of(context).pop(true);
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() => _saving = false);
      ScaffoldMessenger.of(context)
          .showSnackBar(SnackBar(content: Text(e.message)));
    } catch (e) {
      if (!mounted) return;
      setState(() => _saving = false);
      ScaffoldMessenger.of(context)
          .showSnackBar(SnackBar(content: Text('$e')));
    }
  }

  @override
  Widget build(BuildContext context) {
    final isNew = widget.existing == null;
    return Scaffold(
      appBar: AppBar(
        title: Text(isNew
            ? 'إنشاء مستخدم إدارة الفرع'
            : 'تعديل مستخدم إدارة الفرع'),
      ),
      bottomNavigationBar: SafeArea(
        child: Padding(
          padding: const EdgeInsets.fromLTRB(16, 8, 16, 12),
          child: SizedBox(
            width: double.infinity,
            height: 48,
            child: FilledButton(
              onPressed: _saving ? null : _save,
              child: _saving
                  ? const SizedBox(
                      width: 22,
                      height: 22,
                      child: CircularProgressIndicator(
                          strokeWidth: 2, color: Colors.white),
                    )
                  : Text(isNew ? 'إنشاء الحساب' : 'حفظ التعديلات'),
            ),
          ),
        ),
      ),
      body: ListView(
        padding: const EdgeInsets.all(AppSpacing.md),
        children: [
          const Text(
            'بيانات الدخول لتطبيق إدارة الفرع',
            style: TextStyle(fontWeight: FontWeight.w800, fontSize: 15),
          ),
          const SizedBox(height: 10),
          TextField(
            controller: _display,
            textInputAction: TextInputAction.next,
            decoration: const InputDecoration(
              labelText: 'الاسم الظاهر',
              hintText: 'مثال: مدير فرع النجف',
              border: OutlineInputBorder(),
            ),
          ),
          const SizedBox(height: 12),
          TextField(
            controller: _user,
            textInputAction: TextInputAction.next,
            decoration: const InputDecoration(
              labelText: 'اسم المستخدم *',
              hintText: 'يُستخدم لتسجيل الدخول في التطبيق',
              border: OutlineInputBorder(),
            ),
          ),
          const SizedBox(height: 12),
          TextField(
            controller: _pass,
            obscureText: _obscure,
            decoration: InputDecoration(
              labelText: isNew ? 'كلمة المرور *' : 'كلمة مرور جديدة (اختياري)',
              border: const OutlineInputBorder(),
              suffixIcon: IconButton(
                onPressed: () => setState(() => _obscure = !_obscure),
                icon: Icon(_obscure ? Icons.visibility : Icons.visibility_off),
              ),
            ),
          ),
          if (!isNew) ...[
            const SizedBox(height: 8),
            SwitchListTile(
              contentPadding: EdgeInsets.zero,
              title: const Text('الحساب مفعّل'),
              subtitle: const Text('إذا أوقفته لن يدخل تطبيق إدارة الفرع'),
              value: _active,
              onChanged: (v) => setState(() => _active = v),
            ),
          ],
          const SizedBox(height: 20),
          const Text(
            'المحافظات المسموحة',
            style: TextStyle(fontWeight: FontWeight.w800, fontSize: 15),
          ),
          const SizedBox(height: 4),
          const Text(
            'اختر المحافظات التي يظهرها له تطبيق إدارة الفرع بعد تسجيل الدخول.',
            style: TextStyle(fontSize: 12, color: AppColors.muted),
          ),
          const SizedBox(height: 8),
          if (widget.cities.isEmpty)
            const Padding(
              padding: EdgeInsets.all(12),
              child: Text('لا توجد محافظات في الكتالوج'),
            )
          else
            ...widget.cities.map((c) {
              final value = _cityValue(c);
              if (value.isEmpty) return const SizedBox.shrink();
              final selected = _selected.contains(value);
              return CheckboxListTile(
                value: selected,
                title: Text(_cityName(c),
                    style: const TextStyle(fontWeight: FontWeight.w600)),
                subtitle: Text(value, style: const TextStyle(fontSize: 11)),
                controlAffinity: ListTileControlAffinity.leading,
                onChanged: (v) {
                  setState(() {
                    if (v == true) {
                      _selected.add(value);
                    } else {
                      _selected.remove(value);
                    }
                  });
                },
              );
            }),
          const SizedBox(height: 24),
        ],
      ),
    );
  }
}
