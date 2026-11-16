import 'package:flutter/material.dart';

/// Lightweight shimmer effect (no extra dependency).
class Shimmer extends StatefulWidget {
  const Shimmer({super.key, required this.child});

  final Widget child;

  @override
  State<Shimmer> createState() => _ShimmerState();
}

class _ShimmerState extends State<Shimmer> with SingleTickerProviderStateMixin {
  late final AnimationController _c =
      AnimationController(vsync: this, duration: const Duration(milliseconds: 1300))..repeat();

  @override
  void dispose() {
    _c.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: _c,
      child: widget.child,
      builder: (context, child) {
        final t = _c.value;
        return ShaderMask(
          blendMode: BlendMode.srcATop,
          shaderCallback: (rect) => LinearGradient(
            begin: Alignment(-1.5 + 3 * t, -0.3),
            end: Alignment(-0.5 + 3 * t, 0.3),
            colors: const [Color(0xFFE8ECEC), Color(0xFFF7F9F9), Color(0xFFE8ECEC)],
            stops: const [0.1, 0.5, 0.9],
          ).createShader(rect),
          child: child,
        );
      },
    );
  }
}

class SkeletonBox extends StatelessWidget {
  const SkeletonBox({super.key, this.width, this.height = 14, this.radius = 10});

  final double? width;
  final double height;
  final double radius;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: width,
      height: height,
      decoration: BoxDecoration(
        color: const Color(0xFFE8ECEC),
        borderRadius: BorderRadius.circular(radius),
      ),
    );
  }
}

/// Generic list skeleton: [count] rows of cards.
class SkeletonList extends StatelessWidget {
  const SkeletonList({super.key, this.count = 6, this.itemHeight = 96, this.padding = const EdgeInsets.all(16), this.shrinkWrap = false});

  final int count;
  final double itemHeight;
  final EdgeInsets padding;

  /// True when placed inside another scrolling list.
  final bool shrinkWrap;

  @override
  Widget build(BuildContext context) {
    return Shimmer(
      child: ListView.separated(
        physics: const NeverScrollableScrollPhysics(),
        shrinkWrap: shrinkWrap,
        padding: padding,
        itemCount: count,
        separatorBuilder: (_, _) => const SizedBox(height: 12),
        itemBuilder: (_, _) => SkeletonCard(height: itemHeight),
      ),
    );
  }
}

class SkeletonCard extends StatelessWidget {
  const SkeletonCard({super.key, this.height = 96});

  final double height;

  @override
  Widget build(BuildContext context) {
    return Container(
      height: height,
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(18),
      ),
      child: const Row(
        children: [
          SkeletonBox(width: 64, height: 64, radius: 14),
          SizedBox(width: 12),
          Expanded(
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                SkeletonBox(width: 160),
                SizedBox(height: 10),
                SkeletonBox(width: 100, height: 12),
                SizedBox(height: 10),
                SkeletonBox(width: 70, height: 12),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

/// Product grid tile skeleton.
class SkeletonProductTile extends StatelessWidget {
  const SkeletonProductTile({super.key});

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(color: Colors.white, borderRadius: BorderRadius.circular(18)),
      padding: const EdgeInsets.all(10),
      child: const Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Expanded(child: SkeletonBox(width: double.infinity, height: double.infinity, radius: 14)),
          SizedBox(height: 10),
          SkeletonBox(width: 120),
          SizedBox(height: 8),
          SkeletonBox(width: 80, height: 12),
        ],
      ),
    );
  }
}
