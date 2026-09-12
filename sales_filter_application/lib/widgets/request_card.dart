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
    return Card(
      margin: const EdgeInsets.only(bottom: 10),
      child: InkWell(
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.all(14),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(request.customerName, style: const TextStyle(fontFamily: 'Cairo', fontWeight: FontWeight.bold, fontSize: 16)),
              const SizedBox(height: 6),
              if ((request.customerPhone ?? '').isNotEmpty)
                InkWell(
                  onTap: () => onDial(request.customerPhone!),
                  child: Text(
                    request.customerPhone!,
                    style: const TextStyle(fontFamily: 'Cairo', color: AppTheme.primary, decoration: TextDecoration.underline),
                  ),
                ),
              const SizedBox(height: 4),
              Text(request.cityName ?? request.customerProvince ?? request.cityValue ?? '—', style: const TextStyle(fontFamily: 'Cairo')),
              Text(request.customerAddress ?? '—', style: const TextStyle(fontFamily: 'Cairo', color: Colors.black87)),
              const SizedBox(height: 6),
              Text(request.wantedDescription ?? '—', style: const TextStyle(fontFamily: 'Cairo', color: Colors.black54)),
            ],
          ),
        ),
      ),
    );
  }
}
