import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/theme/app_theme.dart';
import '../../app_config/data/app_config_repository.dart';

class SplashScreen extends ConsumerWidget {
  const SplashScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    // Kick off config loading (forced update check) while the session restores.
    ref.watch(remoteConfigProvider);
    return Scaffold(
      backgroundColor: Colors.white,
      body: Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Image.asset('assets/images/logo.png', cacheWidth: 400, width: 150, height: 150),
            const SizedBox(height: 18),
            Text(
              'قلعة الضمان',
              style: Theme.of(context).textTheme.headlineMedium?.copyWith(color: AppColors.primary),
            ),
            const SizedBox(height: 6),
            const Text('تسوّق نقداً أو بالأقساط اليومية', style: TextStyle(color: AppColors.textMuted)),
            const SizedBox(height: 32),
            const SizedBox(width: 26, height: 26, child: CircularProgressIndicator(strokeWidth: 2.6)),
          ],
        ),
      ),
    );
  }
}
