import 'package:flutter/material.dart';
import '../config/theme.dart';
import '../animations/widget_animations.dart';

class ModernStatCard extends StatelessWidget {
  final String title;
  final String value;
  final IconData icon;
  final String color;
  final String? gradient;
  final bool compact;
  final bool animateCount;

  const ModernStatCard({
    super.key,
    required this.title,
    required this.value,
    required this.icon,
    this.color = 'primary',
    this.gradient,
    this.compact = false,
    this.animateCount = true,
  });

  /// Compact version for horizontal scrollable stat rows.
  const ModernStatCard.compact({
    super.key,
    required this.title,
    required this.value,
    required this.icon,
    this.color = 'primary',
    this.gradient,
    this.animateCount = true,
  }) : compact = true;

  @override
  Widget build(BuildContext context) {
    if (compact) return _buildCompact(context);
    return _buildFull(context);
  }

  Widget _buildFull(BuildContext context) {
    final grad = gradient ?? 'linear-gradient(135deg, #8E2DE2 0%, #4A00E0 100%)';
    final colors = _parseGradient(grad);

    return Container(
      constraints: const BoxConstraints(minHeight: 130),
      decoration: BoxDecoration(
        gradient: LinearGradient(
          colors: colors,
          begin: AlignmentDirectional.topStart,
          end: AlignmentDirectional.bottomEnd,
        ),
        borderRadius: BorderRadius.circular(20),
        boxShadow: [
          BoxShadow(
            color: colors.first.withAlpha(77),
            blurRadius: 12,
            offset: const Offset(0, 6),
          ),
        ],
      ),
      child: Stack(
        children: [
          // Watermark icon — RTL: on the right side
          Positioned(
            right: -20,
            bottom: -20,
            child: Icon(icon, size: 120, color: Colors.white.withAlpha(25)),
          ),
          // Content
          Padding(
            padding: const EdgeInsets.all(20),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                Row(
                  children: [
                    Icon(icon, color: Colors.white.withAlpha(204), size: 28),
                    const Spacer(),
                  ],
                ),
                const Spacer(),
                _buildValue(),
                const SizedBox(height: 6),
                Text(
                  title,
                  style: TextStyle(color: Colors.white.withAlpha(217), fontSize: 13, fontWeight: FontWeight.w500, fontFamily: 'Cairo'),
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildValue() {
    final intVal = int.tryParse(value.replaceAll(RegExp(r'[^0-9]'), ''));
    if (animateCount && intVal != null && intVal > 0) {
      return AnimatedCounter(value: intVal, style: const TextStyle(color: Colors.white, fontSize: 24, fontWeight: FontWeight.bold, fontFamily: 'Cairo'));
    }
    return Text(value, style: const TextStyle(color: Colors.white, fontSize: 24, fontWeight: FontWeight.bold, fontFamily: 'Cairo'));
  }

  Widget _buildCompact(BuildContext context) {
    final gradColors = AppTheme.gradientColors(color);
    return Container(
      width: 150,
      margin: const EdgeInsets.symmetric(horizontal: 4),
      decoration: BoxDecoration(
        gradient: LinearGradient(colors: gradColors, begin: AlignmentDirectional.topStart, end: AlignmentDirectional.bottomEnd),
        borderRadius: BorderRadius.circular(16),
        boxShadow: [BoxShadow(color: gradColors.first.withAlpha(77), blurRadius: 8, offset: const Offset(0, 4))],
      ),
      padding: const EdgeInsets.all(14),
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(icon, color: Colors.white.withAlpha(179), size: 22),
          const SizedBox(height: 6),
          _buildCompactValue(),
          const SizedBox(height: 2),
          Text(title, style: TextStyle(color: Colors.white.withAlpha(217), fontSize: 11, fontFamily: 'Cairo'), maxLines: 1, overflow: TextOverflow.ellipsis),
        ],
      ),
    );
  }

  Widget _buildCompactValue() {
    final intVal = int.tryParse(value.replaceAll(RegExp(r'[^0-9]'), ''));
    if (animateCount && intVal != null && intVal > 0) {
      return AnimatedCounter(value: intVal, duration: const Duration(milliseconds: 800), style: const TextStyle(color: Colors.white, fontSize: 18, fontWeight: FontWeight.bold, fontFamily: 'Cairo'));
    }
    return Text(value, style: const TextStyle(color: Colors.white, fontSize: 18, fontWeight: FontWeight.bold, fontFamily: 'Cairo'));
  }

  List<Color> _parseGradient(String grad) {
    try {
      final regex = RegExp(r'#([0-9a-fA-F]{6})');
      final matches = regex.allMatches(grad);
      return matches.map((m) => Color(int.parse('FF${m.group(1)}', radix: 16))).toList();
    } catch (_) {
      return AppTheme.gradientColors(color);
    }
  }
}
