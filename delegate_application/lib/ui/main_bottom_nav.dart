import 'package:delegate_application/utils/AppTheme.dart';
import 'package:flutter/material.dart';

/// RTL bottom bar: المبيعات | العملاء | [Home FAB] | التسديدات
/// Indices match [HomePage] navigation: 0 sales, 1 customers, 2 home, 3 payments.
class MainBottomNavBar extends StatelessWidget {
  const MainBottomNavBar({
    super.key,
    required this.selectedIndex,
    required this.onTap,
  });

  final int selectedIndex;
  final ValueChanged<int> onTap;

  static const double barHeight = 64;
  static const double fabGap = 56;

  @override
  Widget build(BuildContext context) {
    return Directionality(
      textDirection: TextDirection.rtl,
      child: BottomAppBar(
        shape: const CircularNotchedRectangle(),
        notchMargin: 8,
        elevation: 8,
        padding: EdgeInsets.zero,
        color: Theme.of(context).cardColor,
        child: SizedBox(
          height: barHeight,
          child: Row(
            children: [
              // RTL: first child = rightmost
              Expanded(
                child: _NavItem(
                  icon: Icons.monetization_on_outlined,
                  selectedIcon: Icons.monetization_on,
                  label: 'المبيعات',
                  selected: selectedIndex == 0,
                  onTap: () => onTap(0),
                ),
              ),
              Expanded(
                child: _NavItem(
                  icon: Icons.people_outline,
                  selectedIcon: Icons.people,
                  label: 'العملاء',
                  selected: selectedIndex == 1,
                  onTap: () => onTap(1),
                ),
              ),
              const SizedBox(width: fabGap),
              Expanded(
                child: _NavItem(
                  icon: Icons.account_balance_wallet_outlined,
                  selectedIcon: Icons.account_balance_wallet,
                  label: 'التسديدات',
                  selected: selectedIndex == 3,
                  onTap: () => onTap(3),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _NavItem extends StatelessWidget {
  const _NavItem({
    required this.icon,
    required this.selectedIcon,
    required this.label,
    required this.selected,
    required this.onTap,
  });

  final IconData icon;
  final IconData selectedIcon;
  final String label;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final color = selected ? AppTheme.primaryColor : Colors.grey;
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(12),
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: 6),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(selected ? selectedIcon : icon, color: color, size: 24),
            const SizedBox(height: 2),
            Flexible(
              child: Text(
                label,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                textAlign: TextAlign.center,
                style: TextStyle(
                  fontFamily: 'Cairo',
                  fontSize: 11,
                  height: 1.1,
                  color: color,
                  fontWeight: selected ? FontWeight.bold : FontWeight.normal,
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
