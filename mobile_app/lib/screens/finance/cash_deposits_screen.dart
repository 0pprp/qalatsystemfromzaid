import 'package:flutter/material.dart';
import 'cash_history_screen.dart';

class CashDepositsScreen extends StatelessWidget {
  const CashDepositsScreen({super.key});
  @override
  Widget build(BuildContext context) {
    return const CashHistoryScreen(
      title: 'الإضافات',
      listEndpoint: 'Accounts/AddToBoxs_GetByDateByboxID/{from}&&{to}&&{boxID}',
      idKey: 'addToBoxID',
      amountKey: 'amountDenar',
      icon: Icons.add_circle_outline,
    );
  }
}
