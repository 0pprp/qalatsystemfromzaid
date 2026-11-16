import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/widgets/state_views.dart';
import '../data/work_controller.dart';

/// A sale request or a shop pin means the agent is out working: «الدوام» starts by itself if it is not running.
/// Tells the agent when it started, or why it could not (e.g. no location permission). Never blocks the caller's
/// own action: a failure here does not stop the order or the pin.
Future<void> autoStartWorkDay(BuildContext context, WidgetRef ref) async {
  final controller = ref.read(workControllerProvider.notifier);
  final wasWorking = ref.read(workControllerProvider).isWorking;
  if (wasWorking) return;
  final error = await controller.ensureWorking();
  if (!context.mounted) return;
  if (error != null) {
    context.showSnack('لم يبدأ الدوام تلقائياً: $error', error: true);
  } else if (ref.read(workControllerProvider).isWorking) {
    context.showSnack('بدأ الدوام تلقائياً ويتم تسجيل موقعك.');
  }
}
