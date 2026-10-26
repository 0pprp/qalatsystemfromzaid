import 'package:flutter/material.dart';
import '../utils/app_theme.dart';

/// One branded loading treatment for pages and compact action buttons.
class CompanyLoader extends StatefulWidget {
  const CompanyLoader({super.key, this.size = 64, this.label});
  final double size;
  final String? label;

  @override
  State<CompanyLoader> createState() => _CompanyLoaderState();
}

class _CompanyLoaderState extends State<CompanyLoader>
    with SingleTickerProviderStateMixin {
  late final AnimationController _pulse = AnimationController(
    vsync: this,
    duration: const Duration(milliseconds: 1100),
  );

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (MediaQuery.disableAnimationsOf(context) || !TickerMode.valuesOf(context).enabled) {
      _pulse.stop();
      _pulse.value = 1;
    } else {
      _pulse.repeat(reverse: true);
    }
  }

  @override
  void dispose() {
    _pulse.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Semantics(
      label: widget.label ?? 'جارٍ التحميل',
      liveRegion: true,
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          AnimatedBuilder(
            animation: _pulse,
            builder: (context, child) => Transform.scale(
              scale: 0.92 + _pulse.value * 0.08,
              child: Opacity(opacity: 0.65 + _pulse.value * 0.35, child: child),
            ),
            child: Container(
              width: widget.size,
              height: widget.size,
              padding: EdgeInsets.all(widget.size * 0.1),
              decoration: BoxDecoration(
                color: Colors.white,
                borderRadius: BorderRadius.circular(widget.size * 0.24),
              ),
              child: Image.asset('assets/icons/LogoCompany.png',
                  fit: BoxFit.contain, excludeFromSemantics: true),
            ),
          ),
          if (widget.label != null) ...[
            const SizedBox(height: 12),
            ExcludeSemantics(child: Text(widget.label!,
                style: const TextStyle(color: AppColors.muted, fontSize: 14))),
          ],
        ],
      ),
    );
  }
}

