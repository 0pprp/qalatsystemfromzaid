import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/theme/app_theme.dart';
import '../../../core/utils/validators.dart';
import '../../orders/data/orders_repository.dart';

/// Converts Arabic-Indic digits to ASCII and keeps only digits.
class AsciiDigitsFormatter extends TextInputFormatter {
  AsciiDigitsFormatter({this.maxLength});

  final int? maxLength;

  @override
  TextEditingValue formatEditUpdate(TextEditingValue oldValue, TextEditingValue newValue) {
    var text = Validators.normalizeDigits(newValue.text).replaceAll(RegExp(r'[^\d]'), '');
    // Pasted international numbers: +9647XXXXXXXXX → 07XXXXXXXXX
    if (text.length > 11) text = Validators.normalizePhone(text);
    if (maxLength != null && text.length > maxLength!) text = text.substring(0, maxLength);
    return TextEditingValue(text: text, selection: TextSelection.collapsed(offset: text.length));
  }
}

/// Iraqi mobile number field with a fixed +964 hint.
class PhoneField extends StatelessWidget {
  const PhoneField({
    super.key,
    required this.controller,
    this.label = 'رقم الهاتف',
    this.errorText,
    this.onChanged,
    this.readOnly = false,
    this.autofocus = false,
    this.onSubmitted,
  });

  final TextEditingController controller;
  final String label;
  final String? errorText;
  final ValueChanged<String>? onChanged;
  final bool readOnly;
  final bool autofocus;
  final ValueChanged<String>? onSubmitted;

  @override
  Widget build(BuildContext context) {
    return TextField(
      controller: controller,
      readOnly: readOnly,
      autofocus: autofocus,
      keyboardType: TextInputType.phone,
      textDirection: TextDirection.ltr,
      textAlign: TextAlign.left,
      autofillHints: const [AutofillHints.telephoneNumber],
      inputFormatters: [AsciiDigitsFormatter(maxLength: 11)],
      onChanged: onChanged,
      onSubmitted: onSubmitted,
      style: const TextStyle(fontSize: 17, letterSpacing: 1.2, fontWeight: FontWeight.w600),
      decoration: InputDecoration(
        labelText: label,
        hintText: '07XX XXX XXXX',
        hintTextDirection: TextDirection.ltr,
        errorText: errorText,
        filled: true,
        fillColor: readOnly ? const Color(0xFFF1F4F4) : Colors.white,
        suffixIcon: const Padding(
          padding: EdgeInsetsDirectional.only(end: 12),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text('🇮🇶', style: TextStyle(fontSize: 18)),
              SizedBox(width: 6),
              Text('+964', textDirection: TextDirection.ltr, style: TextStyle(color: AppColors.textMuted, fontWeight: FontWeight.w700)),
            ],
          ),
        ),
      ),
    );
  }
}

/// The four Arabic name parts: first / father / grandfather (required, one word) + surname (optional).
class NameFields extends StatelessWidget {
  const NameFields({
    super.key,
    required this.first,
    required this.father,
    required this.grandfather,
    required this.surname,
    this.errors = const {},
    this.onChanged,
  });

  final TextEditingController first;
  final TextEditingController father;
  final TextEditingController grandfather;
  final TextEditingController surname;
  final Map<String, String> errors;
  final void Function(String key, String value)? onChanged;

  Widget _field(String key, String label, TextEditingController c, {bool optional = false}) {
    return TextField(
      controller: c,
      textInputAction: TextInputAction.next,
      keyboardType: TextInputType.name,
      autofillHints: key == 'firstName'
          ? const [AutofillHints.givenName]
          : key == 'surname'
              ? const [AutofillHints.familyName]
              : null,
      inputFormatters: [
        if (!optional) FilteringTextInputFormatter.deny(RegExp(r'\s')),
        LengthLimitingTextInputFormatter(30),
      ],
      onChanged: (v) => onChanged?.call(key, v),
      decoration: InputDecoration(labelText: label, errorText: errors[key], errorMaxLines: 2),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Expanded(child: _field('firstName', 'الاسم', first)),
            const SizedBox(width: 10),
            Expanded(child: _field('fatherName', 'اسم الأب', father)),
          ],
        ),
        const SizedBox(height: 12),
        Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Expanded(child: _field('grandfatherName', 'اسم الجد', grandfather)),
            const SizedBox(width: 10),
            Expanded(child: _field('surname', 'اللقب (اختياري)', surname, optional: true)),
          ],
        ),
      ],
    );
  }
}

/// Branch / governorate picker backed by `GET branches`.
class BranchDropdown extends ConsumerWidget {
  const BranchDropdown({super.key, required this.value, required this.onChanged, this.errorText, this.label = 'الفرع'});

  final int? value;
  final ValueChanged<int?> onChanged;
  final String? errorText;
  final String label;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final branches = ref.watch(branchesProvider);
    return branches.when(
      loading: () => const InputDecorator(
        decoration: InputDecoration(labelText: 'جارٍ تحميل الفروع…', prefixIcon: Icon(Icons.location_city_rounded)),
        child: LinearProgressIndicator(minHeight: 2),
      ),
      error: (e, _) => InputDecorator(
        decoration: InputDecoration(labelText: label, errorText: 'تعذّر تحميل الفروع', prefixIcon: const Icon(Icons.location_city_rounded)),
        child: TextButton(onPressed: () => ref.invalidate(branchesProvider), child: const Text('إعادة المحاولة')),
      ),
      data: (list) => DropdownButtonFormField<int>(
        initialValue: list.any((b) => b.cityValue == value) ? value : null,
        isExpanded: true,
        decoration: InputDecoration(labelText: label, errorText: errorText, prefixIcon: const Icon(Icons.location_city_rounded)),
        items: [for (final b in list) DropdownMenuItem(value: b.cityValue, child: Text(b.name))],
        onChanged: onChanged,
      ),
    );
  }
}
