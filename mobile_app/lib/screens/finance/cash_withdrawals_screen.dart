import 'package:flutter/material.dart';
import 'cash_history_screen.dart';

class CashWithdrawalsScreen extends StatelessWidget {
  const CashWithdrawalsScreen({super.key});
  @override
  Widget build(BuildContext context) {
    return const CashHistoryScreen(
      title: 'السحوبات',
      listEndpoint: 'Accounts/WithdrawalFromBoxs_GetByDateByboxID/{from}&&{to}&&{boxID}',
      idKey: 'withdrawalFromBoxID',
      amountKey: 'amountDenar',
      icon: Icons.remove_circle_outline,
    );
  }
}
