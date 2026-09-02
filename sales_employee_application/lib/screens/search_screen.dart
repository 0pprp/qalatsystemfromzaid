import 'dart:async';

import 'package:flutter/material.dart';
import 'package:sales_employee_application/data/sales_models.dart';
import 'package:sales_employee_application/data/sales_repository_factory.dart';
import 'package:sales_employee_application/services/api_client.dart';
import 'package:sales_employee_application/utils/app_theme.dart';
import 'package:sales_employee_application/utils/sales_format.dart';

class SearchScreen extends StatefulWidget {
  const SearchScreen({super.key});

  @override
  State<SearchScreen> createState() => _SearchScreenState();
}

class _SearchScreenState extends State<SearchScreen> {
  final _ctrl = TextEditingController();
  Timer? _debounce;
  bool _loading = false;
  String? _error;
  List<SalesCustomer> _rows = [];

  @override
  void dispose() {
    _debounce?.cancel();
    _ctrl.dispose();
    super.dispose();
  }

  void _onChanged(String value) {
    _debounce?.cancel();
    _debounce = Timer(const Duration(milliseconds: 400), () => _search(value));
  }

  Future<void> _search(String raw) async {
    final q = raw.trim();
    if (q.length < 2) {
      setState(() {
        _rows = [];
        _error = null;
        _loading = false;
      });
      return;
    }
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final rows = await SalesRepositoryFactory.instance.searchCustomers(q);
      if (!mounted) return;
      setState(() {
        _rows = rows;
        _loading = false;
      });
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _error = e.message;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _error = 'تعذر إكمال البحث';
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('بحث عن زبون')),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.all(AppSpacing.md),
            child: TextField(
              controller: _ctrl,
              autofocus: true,
              onChanged: _onChanged,
              decoration: const InputDecoration(
                hintText: 'اكتب حرفين على الأقل',
                prefixIcon: Icon(Icons.search),
              ),
            ),
          ),
          if (_loading) const LinearProgressIndicator(minHeight: 2),
          Expanded(child: _body()),
        ],
      ),
    );
  }

  Widget _body() {
    if (_error != null) {
      return Center(child: Text(_error!, style: const TextStyle(color: AppColors.danger)));
    }
    if (_ctrl.text.trim().length < 2) {
      return const Center(child: Text('ابدأ الكتابة للبحث', style: TextStyle(color: AppColors.muted)));
    }
    if (!_loading && _rows.isEmpty) {
      return const Center(child: Text('لا توجد نتائج', style: TextStyle(color: AppColors.muted)));
    }
    return ListView.builder(
      padding: const EdgeInsets.all(AppSpacing.md),
      itemCount: _rows.length,
      itemBuilder: (context, i) {
        final c = _rows[i];
        return Card(
          child: ListTile(
            title: Text(c.fullName, style: const TextStyle(fontWeight: FontWeight.w700)),
            subtitle: Text('${c.phone ?? '-'}\n${c.province ?? '-'}\nسعر البيع: ${MoneyFormat.iqd(c.salePrice)}'),
            isThreeLine: true,
            onTap: () {
              if (ModalRoute.of(context)?.settings.arguments == 'pick') {
                Navigator.pop(context, c);
              } else {
                Navigator.pushNamed(context, '/sale', arguments: c);
              }
            },
          ),
        );
      },
    );
  }
}
