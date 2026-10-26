import 'package:flutter/material.dart';
import 'package:follower_application/utils/AppTheme.dart';

/// نص اسم واضح (بدون تعديل محتوى الاسم).
class PersonNameText extends StatelessWidget {
  const PersonNameText(
    this.raw, {
    super.key,
    this.style,
    this.maxLines = 2,
    this.textAlign,
    this.overflow = TextOverflow.ellipsis,
  });

  final Object? raw;
  final TextStyle? style;
  final int? maxLines;
  final TextAlign? textAlign;
  final TextOverflow overflow;

  @override
  Widget build(BuildContext context) {
    final text = '$raw'.trim();
    final base = style ?? const TextStyle();
    return Text(
      text == 'null' ? '' : text,
      textAlign: textAlign,
      maxLines: maxLines,
      overflow: overflow,
      style: base.copyWith(
        fontFamily: base.fontFamily ?? 'Cairo',
        fontWeight: base.fontWeight ?? FontWeight.w700,
        fontSize: base.fontSize ?? 16,
        height: base.height ?? 1.35,
      ),
    );
  }
}

/// خط فاصل جميل بين صفوف الأسماء.
class NameListSeparator extends StatelessWidget {
  const NameListSeparator({
    super.key,
    this.indent = 8,
    this.endIndent = 8,
  });

  final double indent;
  final double endIndent;

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final line = isDark
        ? Colors.white.withValues(alpha: 0.16)
        : AppTheme.primaryColor.withValues(alpha: 0.22);
    return Padding(
      padding: EdgeInsetsDirectional.only(
        start: indent,
        end: endIndent,
        top: 6,
        bottom: 6,
      ),
      child: Container(
        height: 1.2,
        decoration: BoxDecoration(
          borderRadius: BorderRadius.circular(2),
          gradient: LinearGradient(
            colors: [
              line.withValues(alpha: 0.05),
              line,
              line.withValues(alpha: 0.05),
            ],
          ),
        ),
      ),
    );
  }
}

/// يضع [NameListSeparator] بين عناصر القائمة دون خط بعد الأخير.
List<Widget> withNameSeparators(Iterable<Widget> children) {
  final list = children.toList(growable: false);
  if (list.isEmpty) return const [];
  final out = <Widget>[];
  for (var i = 0; i < list.length; i++) {
    out.add(list[i]);
    if (i < list.length - 1) out.add(const NameListSeparator());
  }
  return out;
}
