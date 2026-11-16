import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/config/app_config.dart';
import '../../../core/providers.dart';
import '../../../core/theme/app_theme.dart';

class AboutScreen extends ConsumerWidget {
  const AboutScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final info = ref.watch(packageInfoProvider);
    return Scaffold(
      appBar: AppBar(title: const Text('عن التطبيق')),
      body: ListView(
        padding: const EdgeInsets.all(24),
        children: [
          Center(child: Image.asset('assets/images/logo.png', cacheWidth: 400, height: 120)),
          const SizedBox(height: 12),
          Center(child: Text('قلعة الضمان', style: Theme.of(context).textTheme.headlineSmall?.copyWith(color: AppColors.primary))),
          Center(
            child: Text('الإصدار ${info.version} (${info.buildNumber})', style: const TextStyle(color: AppColors.textMuted)),
          ),
          const SizedBox(height: 20),
          const Text(
            'تطبيق مسوّقي قلعة الضمان الميدانيين: تسجيل الدوام اليومي، إرسال طلبات البيع مع صورة المحل وموقعه إلى قسم المبيعات، '
            'متابعتها حتى اكتمال تسديدها، وتثبيت المحلات المحتملة على الخريطة.',
            style: TextStyle(height: 1.8),
          ),
          const SizedBox(height: 20),
          ListTile(
            leading: const Icon(Icons.article_outlined),
            title: const Text('تراخيص البرمجيات مفتوحة المصدر'),
            onTap: () => showLicensePage(
              context: context,
              applicationName: 'قلعة الضمان',
              applicationVersion: info.version,
              applicationIcon: Image.asset('assets/images/logo.png', cacheWidth: 400, height: 64),
            ),
          ),
          if (AppConfig.useFakeApi)
            const Padding(
              padding: EdgeInsets.only(top: 16),
              child: Text('الوضع التجريبي مفعّل (بيانات وهمية)', textAlign: TextAlign.center, style: TextStyle(color: AppColors.warning)),
            ),
        ],
      ),
    );
  }
}
