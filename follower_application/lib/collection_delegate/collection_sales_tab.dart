import 'package:follower_application/utils/AppTheme.dart';
import 'package:follower_application/utils/display_name.dart';
import 'package:flutter/material.dart';

/// Sales tab inside collection mini-delegate — mirrors المبيعات entry points.
class CollectionSalesTab extends StatelessWidget {
  final int listId;
  final String listName;

  const CollectionSalesTab({
    super.key,
    required this.listId,
    required this.listName,
  });

  @override
  Widget build(BuildContext context) {
    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        Text('المبيعات — ${listName}',
            style: const TextStyle(
                fontFamily: 'Cairo', fontWeight: FontWeight.bold, fontSize: 18)),
        const SizedBox(height: 12),
        Card(
          child: ListTile(
            leading: const Icon(Icons.add_shopping_cart, color: AppTheme.primaryColor),
            title: const Text('طلب مبيع جديد',
                style: TextStyle(fontFamily: 'Cairo', fontWeight: FontWeight.bold)),
            subtitle: const Text('نفس مسار مسؤول اللجنة لطلب مبيع على هذه القائمة',
                style: TextStyle(fontFamily: 'Cairo')),
            trailing: const Icon(Icons.chevron_left),
            onTap: () {
              Navigator.pushNamed(
                context,
                '/FollowerSalesRequest',
                arguments: {'listId': listId},
              );
            },
          ),
        ),
        Card(
          child: ListTile(
            leading: const Icon(Icons.pending_actions, color: AppTheme.primaryColor),
            title: const Text('المبيعات بانتظار التوثيق',
                style: TextStyle(fontFamily: 'Cairo', fontWeight: FontWeight.bold)),
            subtitle: const Text('شاشة مبيعات مسؤول اللجنة',
                style: TextStyle(fontFamily: 'Cairo')),
            trailing: const Icon(Icons.chevron_left),
            onTap: () => Navigator.pushNamed(context, '/PendingSales'),
          ),
        ),
      ],
    );
  }
}
