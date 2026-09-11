import 'package:delegate_application/utils/AppTheme.dart';
import 'package:flutter/material.dart';

/// RTL bottom bar — equal tabs (no FAB):
/// المبيعات | العملاء | الرئيسية | التسديدات
/// Indices: 0 sales, 1 customers, 2 home, 3 payments.
class MainBottomNavBar extends StatelessWidget {
  const MainBottomNavBar({
    super.key,
    required this.selectedIndex,
    required this.onTap,
  });

  final int selectedIndex;
  final ValueChanged<int> onTap;

  static const double barHeight = 68;

  static const _items = <_NavSpec>[
    _NavSpec(0, Icons.monetization_on_outlined, Icons.monetization_on, 'المبيعات'),
    _NavSpec(1, Icons.people_outline, Icons.people, 'العملاء'),
    _NavSpec(2, Icons.home_outlined, Icons.home, 'الرئيسية'),
    _NavSpec(3, Icons.account_balance_wallet_outlined, Icons.account_balance_wallet,
        'التسديدات'),
  ];

  @override
  Widget build(BuildContext context) {
    return Directionality(
      textDirection: TextDirection.rtl,
      child: Material(
        elevation: 8,
        color: Theme.of(context).cardColor,
        child: SizedBox(
          height: barHeight,
          child: LayoutBuilder(
            builder: (context, constraints) {
              final tabW = constraints.maxWidth / _items.length;
              return Stack(
                children: [
                  AnimatedPositioned(
                    duration: const Duration(milliseconds: 260),
                    curve: Curves.easeOutCubic,
                    // RTL Stack: left=0 is visual left; index 0 is rightmost.
                    right: selectedIndex * tabW + tabW * 0.18,
                    left: null,
                    top: 8,
                    width: tabW * 0.64,
                    height: 52,
                    child: DecoratedBox(
                      decoration: BoxDecoration(
                        color: AppTheme.primaryColor.withValues(alpha: 0.12),
                        borderRadius: BorderRadius.circular(26),
                      ),
                    ),
                  ),
                  Row(
                    children: [
                      for (final item in _items)
                        Expanded(
                          child: _NavItem(
                            icon: item.icon,
                            selectedIcon: item.selectedIcon,
                            label: item.label,
                            selected: selectedIndex == item.index,
                            onTap: () => onTap(item.index),
                          ),
                        ),
                    ],
                  ),
                ],
              );
            },
          ),
        ),
      ),
    );
  }
}

class _NavSpec {
  const _NavSpec(this.index, this.icon, this.selectedIcon, this.label);
  final int index;
  final IconData icon;
  final IconData selectedIcon;
  final String label;
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
        padding: const EdgeInsets.symmetric(vertical: 8),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          mainAxisSize: MainAxisSize.min,
          children: [
            AnimatedSwitcher(
              duration: const Duration(milliseconds: 200),
              child: Icon(
                selected ? selectedIcon : icon,
                key: ValueKey(selected),
                color: color,
                size: 24,
              ),
            ),
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
