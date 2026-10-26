import 'package:follower_application/ui/stripe_container.dart';
import 'package:flutter/material.dart';

/// Previous | arrow | current — same visual language as delegate week card.
class RatingTrendRow extends StatelessWidget {
  const RatingTrendRow({
    super.key,
    required this.previousLabel,
    required this.currentLabel,
    required this.previousScore,
    required this.currentScore,
    required this.trend,
    this.compact = false,
    this.stripeIndex = 0,
  });

  final String previousLabel;
  final String currentLabel;
  final double? previousScore;
  final double currentScore;
  final String trend;
  final bool compact;
  final int stripeIndex;

  static String formatScore(double? v) {
    if (v == null) return '—';
    return v == v.roundToDouble()
        ? v.toStringAsFixed(0)
        : v.toStringAsFixed(1);
  }

  static IconData trendIcon(String trend) {
    switch (trend.toLowerCase()) {
      case 'up':
        return Icons.arrow_upward;
      case 'down':
        return Icons.arrow_downward;
      default:
        return Icons.drag_handle;
    }
  }

  static Color trendColor(String trend) {
    switch (trend.toLowerCase()) {
      case 'up':
        return Colors.green.shade700;
      case 'down':
        return Colors.red.shade700;
      default:
        return Colors.grey.shade600;
    }
  }

  @override
  Widget build(BuildContext context) {
    final pad = compact ? 10.0 : 16.0;
    final iconSize = compact ? 24.0 : 32.0;
    final scoreSize = compact ? 18.0 : 22.0;
    return Container(
      width: double.infinity,
      padding: EdgeInsets.all(pad),
      decoration: StripeContainer.decoration(
        stripeIndex,
        radius: compact ? 14 : 20,
      ),
      child: Row(
        children: [
          Expanded(
            child: _metric(previousLabel, formatScore(previousScore), scoreSize),
          ),
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 6),
            child: Icon(
              trendIcon(trend),
              color: trendColor(trend),
              size: iconSize,
            ),
          ),
          Expanded(
            child: _metric(currentLabel, formatScore(currentScore), scoreSize),
          ),
        ],
      ),
    );
  }

  Widget _metric(String label, String value, double scoreSize) {
    return Column(
      children: [
        Text(
          label,
          textAlign: TextAlign.center,
          style: const TextStyle(
            fontFamily: 'Cairo',
            fontSize: 12,
            color: Colors.grey,
          ),
        ),
        const SizedBox(height: 4),
        Text(
          value,
          textAlign: TextAlign.center,
          style: TextStyle(
            fontFamily: 'Cairo',
            fontWeight: FontWeight.w900,
            fontSize: scoreSize,
            color: Colors.black,
          ),
        ),
      ],
    );
  }
}
