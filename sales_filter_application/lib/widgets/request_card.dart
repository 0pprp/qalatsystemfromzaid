import 'package:flutter/material.dart';
import 'package:sales_filter_application/models/filter_models.dart';
import 'package:sales_filter_application/theme/app_theme.dart';

class RequestCard extends StatelessWidget {
  const RequestCard({
    super.key,
    required this.request,
    required this.onTap,
    required this.onDial,
  });

  final FilterRequest request;
  final VoidCallback onTap;
  final Future<void> Function(String phone) onDial;

  @override
  Widget build(BuildContext context) {
    final city = request.cityName ?? request.customerProvince ?? request.cityValue ?? '—';
    return Card(
      margin: const EdgeInsets.only(bottom: AppSpacing.sm),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(AppRadius.md),
        child: Padding(
          padding: const EdgeInsets.all(AppSpacing.md),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                request.customerName,
                style: const TextStyle(fontFamily: 'Cairo', fontWeight: FontWeight.w800, fontSize: 17, color: AppColors.text),
              ),
              const SizedBox(height: AppSpacing.sm),
              _labeledRow(
                icon: Icons.phone_outlined,
                label: 'رقم الهاتف',
                child: (request.customerPhone ?? '').isEmpty
                    ? const Text('—', style: TextStyle(fontFamily: 'Cairo'))
                    : InkWell(
                        onTap: () => onDial(request.customerPhone!),
                        child: Text(
                          request.customerPhone!,
                          style: const TextStyle(
                            fontFamily: 'Cairo',
                            color: AppColors.darkGreen,
                            fontWeight: FontWeight.w700,
                            decoration: TextDecoration.underline,
                          ),
                        ),
                      ),
              ),
              _labeled('المحافظة', city),
              _labeled('العنوان', request.customerAddress ?? '—'),
              _labeled('نوع المبيع', request.wantedDescription ?? '—'),
              if (request.filterStatus == FilterStatuses.onHold && (request.filterNote ?? '').trim().isNotEmpty) ...[
                const SizedBox(height: 6),
                _labeled('ملاحظة التعليق', _short(request.filterNote!), emphasize: true),
              ],
              if (request.filterStatus == FilterStatuses.rejected && (request.rejectReason ?? '').trim().isNotEmpty) ...[
                const SizedBox(height: 6),
                _labeled('سبب الرفض', request.rejectReason!, emphasize: true, color: AppColors.danger),
              ],
            ],
          ),
        ),
      ),
    );
  }

  static String _short(String value) {
    final t = value.trim();
    if (t.length <= 80) return t;
    return '${t.substring(0, 80)}…';
  }

  Widget _labeled(String label, String value, {bool emphasize = false, Color? color}) {
    return Padding(
      padding: const EdgeInsets.only(top: 4),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text('$label:', style: TextStyle(fontFamily: 'Cairo', fontWeight: FontWeight.w700, color: color ?? AppColors.muted, fontSize: 13)),
          Text(
            value,
            style: TextStyle(
              fontFamily: 'Cairo',
              fontWeight: emphasize ? FontWeight.w700 : FontWeight.w500,
              fontSize: 14,
              color: color ?? AppColors.text,
              height: 1.35,
            ),
          ),
        ],
      ),
    );
  }

  Widget _labeledRow({required IconData icon, required String label, required Widget child}) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 2),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(icon, size: 18, color: AppColors.darkGreen),
          const SizedBox(width: 6),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text('$label:', style: const TextStyle(fontFamily: 'Cairo', fontWeight: FontWeight.w700, color: AppColors.muted, fontSize: 13)),
                child,
              ],
            ),
          ),
        ],
      ),
    );
  }
}
