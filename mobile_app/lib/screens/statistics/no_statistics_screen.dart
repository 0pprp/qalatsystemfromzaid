import 'package:flutter/material.dart';
import 'agents_statistics_screen.dart';

class NoStatisticsScreen extends StatelessWidget {
  const NoStatisticsScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return const AgentsStatisticsScreen(excludeZeroed: true);
  }
}
