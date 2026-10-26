import 'package:follower_application/utils/AppTheme.dart';
import 'package:flutter/material.dart';

/// Alternating container tones (0,2,4… teal; 1,3,5… cool slate) for visual scanning.
class StripeContainer {
  static Color fill(int index) => index.isEven
      ? AppTheme.primaryColor.withValues(alpha: 0.10)
      : const Color(0xFFE8EEF5);

  static Color border(int index) => index.isEven
      ? AppTheme.primaryColor.withValues(alpha: 0.38)
      : const Color(0xFF5B7C99).withValues(alpha: 0.40);

  static BoxDecoration decoration(int index, {double radius = 16}) =>
      BoxDecoration(
        color: fill(index),
        borderRadius: BorderRadius.circular(radius),
        border: Border.all(color: border(index)),
      );
}
