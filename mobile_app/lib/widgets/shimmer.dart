import 'package:flutter/material.dart';

class ShimmerBox extends StatefulWidget {
  final double width;
  final double height;
  final double borderRadius;

  const ShimmerBox({
    super.key,
    this.width = double.infinity,
    this.height = 16,
    this.borderRadius = 8,
  });

  @override
  State<ShimmerBox> createState() => _ShimmerBoxState();
}

class _ShimmerBoxState extends State<ShimmerBox> with SingleTickerProviderStateMixin {
  late AnimationController _controller;
  late Animation<double> _animation;

  @override
  void initState() {
    super.initState();
    _controller = AnimationController(vsync: this, duration: const Duration(milliseconds: 1500));
    _animation = Tween<double>(begin: -2, end: 2).animate(CurvedAnimation(parent: _controller, curve: Curves.easeInOut));
    _controller.repeat(reverse: true);
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return AnimatedBuilder(
      animation: _animation,
      builder: (context, child) {
        return Container(
          width: widget.width,
          height: widget.height,
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(widget.borderRadius),
            gradient: LinearGradient(
              begin: Alignment(-1.0 + _animation.value, 0),
              end: Alignment(1.0 + _animation.value, 0),
              colors: [
                isDark ? Colors.grey[850]! : Colors.grey[200]!,
                isDark ? Colors.grey[750]! : Colors.grey[100]!,
                isDark ? Colors.grey[850]! : Colors.grey[200]!,
              ],
            ),
          ),
        );
      },
    );
  }
}

class ShimmerStatCard extends StatelessWidget {
  const ShimmerStatCard({super.key});

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 150,
      margin: const EdgeInsets.symmetric(horizontal: 4),
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(16),
        color: Theme.of(context).brightness == Brightness.dark ? Colors.grey[850] : Colors.grey[100],
      ),
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        const ShimmerBox(width: 24, height: 24, borderRadius: 4),
        const SizedBox(height: 8),
        const ShimmerBox(height: 18, borderRadius: 4),
        const SizedBox(height: 6),
        const ShimmerBox(width: 80, height: 12, borderRadius: 4),
      ]),
    );
  }
}

class ShimmerListTile extends StatelessWidget {
  const ShimmerListTile({super.key});

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
      child: Row(children: [
        const ShimmerBox(width: 48, height: 48, borderRadius: 24),
        const SizedBox(width: 12),
        Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          const ShimmerBox(height: 14, borderRadius: 4),
          const SizedBox(height: 8),
          ShimmerBox(width: MediaQuery.of(context).size.width * 0.4, height: 12, borderRadius: 4),
        ])),
        const ShimmerBox(width: 40, height: 40, borderRadius: 20),
      ]),
    );
  }
}

class ShimmerDashboard extends StatelessWidget {
  const ShimmerDashboard({super.key});

  @override
  Widget build(BuildContext context) {
    return Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
      const SizedBox(height: 8),
      // Header shimmer
      Padding(
        padding: const EdgeInsets.all(16),
        child: Container(height: 120, decoration: BoxDecoration(borderRadius: BorderRadius.circular(20), color: Theme.of(context).brightness == Brightness.dark ? Colors.grey[850] : Colors.grey[100])),
      ),
      // Stat cards row
      SizedBox(height: 130, child: ListView(scrollDirection: Axis.horizontal, padding: const EdgeInsets.symmetric(horizontal: 12), children: List.generate(4, (_) => const ShimmerStatCard()))),
      const SizedBox(height: 16),
      // List items
      ...List.generate(6, (_) => const ShimmerListTile()),
    ]);
  }
}
