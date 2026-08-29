import 'package:flutter/material.dart';
import '../../services/api_service.dart';
import '../../utils/formatters.dart';
import '../../widgets/modern_stat_card.dart';
import '../../widgets/states.dart';
import '../../widgets/shimmer.dart';
import '../../utils/excel_export.dart';

class PermissionListScreen extends StatefulWidget {
  const PermissionListScreen({super.key});

  @override
  State<PermissionListScreen> createState() => _PermissionListScreenState();
}

class _PermissionListScreenState extends State<PermissionListScreen> {
  final ApiService _api = ApiService();
  List<Map<String, dynamic>> _permissions = [];
  List<Map<String, dynamic>> _permissionTypes = [];
  List<Map<String, dynamic>> _users = [];
  bool _isLoading = true;

  @override
  void initState() { super.initState(); _load(); }

  Future<void> _load() async {
    setState(() => _isLoading = true);
    try { await Future.wait([_loadPermissions(), _loadTypes(), _loadUsers()]); } catch (_) {}
    setState(() => _isLoading = false);
  }

  Future<void> _loadPermissions() async {
    try { final r = await _api.get('Permissions/Permissions_GetAll'); if (r.data is List) _permissions = (r.data as List).cast<Map<String, dynamic>>(); } catch (_) {}
  }

  Future<void> _loadTypes() async {
    try { final r = await _api.get('Permissions/PermissionsTypes_GetAll'); if (r.data is List) _permissionTypes = (r.data as List).cast<Map<String, dynamic>>(); } catch (_) {}
  }

  Future<void> _loadUsers() async {
    try { final r = await _api.get('Users/Users_GetAll/null'); if (r.data is List) _users = (r.data as List).cast<Map<String, dynamic>>(); } catch (_) {}
  }

  Future<void> _assignPermission(Map<String, dynamic> user, List<int> typeIds) async {
    try {
      await _api.post('Permissions/SetUsersPermissionsTypes', data: {'userID': user['userID'], 'permissionTypeIDs': typeIds});
      _load();
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('تم حفظ الصلاحيات', style: TextStyle())));
    } catch (e) {
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('فشل: $e')));
    }
  }

  Future<void> _showPermissionDialog(Map<String, dynamic> user) async {
    final localSelected = <int>{};
    try {
      final res = await _api.get('Users/UserSelectCityByUserID/${user['userID']}');
      if (res.data is List) {
        for (final s in (res.data as List)) {
          if (s is Map) localSelected.add(s['permissionTypeID']);
        }
      }
    } catch (_) {}

    final result = await showDialog<List<int>>(
      context: context,
      builder: (ctx) => StatefulBuilder(builder: (ctx, setSt) {
        final setLocal = Set<int>.from(localSelected);
        return AlertDialog(
          title: Text('صلاحيات ${user['userName'] ?? user['fullName'] ?? ''}', style: const TextStyle()),
          content: SizedBox(width: double.maxFinite, child: Column(mainAxisSize: MainAxisSize.min, children:
            _permissionTypes.map((t) {
              final tid = t['permissionTypeID'] as int;
              return CheckboxListTile(title: Text(t['permissionTypeName'] ?? 'صلاحية ${tid}', style: const TextStyle(fontSize: 14)), value: setLocal.contains(tid),
                onChanged: (v) => setSt(() => v == true ? setLocal.add(tid) : setLocal.remove(tid)));
            }).toList(),
          )),
          actions: [
            TextButton(onPressed: () => Navigator.pop(ctx), child: const Text('إلغاء')),
            ElevatedButton(onPressed: () => Navigator.pop(ctx, setLocal.toList()), child: const Text('حفظ', style: TextStyle())),
          ],
        );
      }),
    );
    if (result != null && mounted) await _assignPermission(user, result);
  }

  Future<void> _export() async {
    try {
      await ExcelExport.exportList(data: _permissions, columns: ['المستخدم', 'الصلاحية'], keys: ['userName', 'permissionTypeName'], fileName: 'الصلاحيات_${Formatters.todayEnCA()}');
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('تم التصدير', style: TextStyle())));
    } catch (_) {}
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('الصلاحيات', style: TextStyle()), actions: [
        IconButton(icon: const Icon(Icons.table_chart_outlined), tooltip: 'تصدير Excel', onPressed: _export),
        IconButton(icon: const Icon(Icons.refresh), onPressed: _load),
      ]),
      body: _isLoading ? const ShimmerDashboard() : Column(children: [
        SizedBox(height: 90, child: ListView(scrollDirection: Axis.horizontal, padding: const EdgeInsets.symmetric(horizontal: 8), children: [
          ModernStatCard.compact(title: 'المستخدمين', value: Formatters.formatNumber(_users.length), icon: Icons.people, color: 'primary'),
          ModernStatCard.compact(title: 'الصلاحيات', value: Formatters.formatNumber(_permissionTypes.length), icon: Icons.security, color: 'info'),
        ])),
        Expanded(child: _users.isEmpty ? const EmptyState(message: 'لا يوجد مستخدمين') : ListView.builder(padding: const EdgeInsets.all(8), itemCount: _users.length, itemBuilder: (_, i) {
          final u = _users[i];
          final userPerms = _permissions.where((p) => p['userID'] == u['userID']).toList();
          return Card(margin: const EdgeInsets.symmetric(horizontal: 8, vertical: 4), child: ListTile(
            leading: CircleAvatar(backgroundColor: const Color(0xFF8E2DE2).withValues(alpha: 0.2), child: const Icon(Icons.person, color: Color(0xFF8E2DE2))),
            title: Text(u['userName'] ?? u['fullName'] ?? '', style: const TextStyle(fontWeight: FontWeight.bold)),
            subtitle: Text(userPerms.isEmpty ? 'لا صلاحيات' : userPerms.map((p) => p['permissionTypeName'] ?? '').join('، '), style: const TextStyle(fontSize: 12), maxLines: 2),
            trailing: IconButton(icon: const Icon(Icons.edit, color: Color(0xFF1e5799)), onPressed: () => _showPermissionDialog(u)),
          ));
        })),
      ]),
    );
  }
}
