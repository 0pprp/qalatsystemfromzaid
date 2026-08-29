import 'package:flutter/material.dart';
import 'cash_history_screen.dart';

class CashTransfersScreen extends StatelessWidget {
  const CashTransfersScreen({super.key});
  @override
  Widget build(BuildContext context) {
    return CashHistoryScreen(
      title: 'التحويلات',
      listEndpoint: 'Accounts/TransferBoxs_GetByDate/{from}&&{to}',
      idKey: 'transferBoxID',
      amountKey: 'amountDenar',
      boxNameKey: 'fromBoxName',
      icon: Icons.swap_horiz,
    );
  }
}
