import 'package:flutter/material.dart';
import '../../services/api_service.dart';
import '../../utils/formatters.dart';
import '../../widgets/modern_stat_card.dart';
import '../../widgets/states.dart';

class UsersListScreen extends StatefulWidget {
  const UsersListScreen({super.key});
  @override
  State<UsersListScreen> createState() => _UsersListScreenState();
}

class _UsersListScreenState extends State<UsersListScreen> {
  final ApiService _api = ApiService();
  List<Map<String, dynamic>> _users = [];
  bool _isLoading = true;
  String _searchText = '';

  @override
  void initState() { super.initState(); _load(); }

  Future<void> _load() async {
    setState(() => _isLoading = true);
    try {
      final search = _searchText.isEmpty ? 'null' : _searchText;
      final res = await _api.get('Users/Users_GetAll/$search');
      if (res.data is List) {
        setState(() { _users = (res.data as List).cast<Map<String, dynamic>>(); _isLoading = false; });
      } else { setState(() => _isLoading = false); }
    } catch (_) { setState(() => _isLoading = false); }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('المستخدمين'),
        actions: [IconButton(icon: const Icon(Icons.refresh), onPressed: _load)],
      ),
      body: _isLoading
          ? const LoadingIndicator()
          : _users.isEmpty
              ? const EmptyState(message: 'لا يوجد مستخدمين')
              : Column(children: [
                  Padding(
                    padding: const EdgeInsets.all(12),
                    child: TextField(
                      decoration: InputDecoration(hintText: 'بحث...', prefixIcon: const Icon(Icons.search), border: OutlineInputBorder(borderRadius: BorderRadius.circular(12))),
                      onSubmitted: (v) { _searchText = v; _load(); },
                    ),
                  ),
                  SizedBox(
                    height: 80,
                    child: ListView(scrollDirection: Axis.horizontal, padding: const EdgeInsets.symmetric(horizontal: 8), children: [
                      ModernStatCard.compact(title: 'عدد المستخدمين', value: Formatters.formatNumber(_users.length), icon: Icons.people, color: 'primary'),
                    ]),
                  ),
                  Expanded(child: ListView.builder(
                    padding: const EdgeInsets.all(8),
                    itemCount: _users.length,
                    itemBuilder: (_, i) {
                      final u = _users[i];
                      return Card(
                        margin: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                        child: ListTile(
                          leading: CircleAvatar(backgroundColor: const Color(0xFF8E2DE2).withAlpha(51), child: const Icon(Icons.person, color: Color(0xFF8E2DE2))),
                          title: Text(u['userName'] ?? '', style: const TextStyle(fontWeight: FontWeight.bold)),
                          subtitle: Text(u['fullName'] ?? '', style: const TextStyle(fontSize: 12)),
                        ),
                      );
                    },
                  )),
                ]),
    );
  }
}
