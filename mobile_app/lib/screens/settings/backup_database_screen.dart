import 'package:flutter/material.dart';
import '../../services/api_service.dart';

class BackupDatabaseScreen extends StatefulWidget {
  const BackupDatabaseScreen({super.key});

  @override
  State<BackupDatabaseScreen> createState() => _BackupDatabaseScreenState();
}

class _BackupDatabaseScreenState extends State<BackupDatabaseScreen> {
  final ApiService _api = ApiService();
  bool _isLoading = false;
  String? _result;

  Future<void> _backup() async {
    setState(() => _isLoading = true);
    try {
      final res = await _api.post('BackupDatabase/BackupDatabase');
      setState(() { _result = 'تم النسخ الاحتياطي بنجاح'; _isLoading = false; });
    } catch (e) {
      setState(() { _result = 'فشل: $e'; _isLoading = false; });
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('النسخ الاحتياطي', style: TextStyle())),
      body: Center(child: Column(mainAxisSize: MainAxisSize.min, children: [
        const Icon(Icons.cloud_upload, size: 80, color: Color(0xFF1e5799)),
        const SizedBox(height: 24),
        const Text('نسخ احتياطي لقاعدة البيانات', style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold)),
        const SizedBox(height: 24),
        if (_result != null) Padding(padding: const EdgeInsets.all(16), child: Text(_result!, style: TextStyle(color: _result!.contains('نجاح') ? Colors.green : Colors.red))),
        ElevatedButton.icon(onPressed: _isLoading ? null : _backup, icon: _isLoading ? const SizedBox(width: 20, height: 20, child: CircularProgressIndicator(strokeWidth: 2)) : const Icon(Icons.backup), label: const Text('بدء النسخ الاحتياطي', style: TextStyle())),
      ])),
    );
  }
}
