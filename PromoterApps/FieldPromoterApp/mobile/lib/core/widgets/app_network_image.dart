import 'dart:io';

import 'package:cached_network_image/cached_network_image.dart';
import 'package:flutter/material.dart';

import '../theme/app_theme.dart';
import 'skeleton.dart';

/// Demo mode (USE_FAKE_API) bundles its sample images; those URLs use the `asset:` scheme.
const demoAssetScheme = 'asset:';

/// Demo mode keeps photos taken on the phone (shop photos) as local files.
const localFileScheme = 'file://';

/// Image provider for a remote URL, a bundled demo asset or a local file.
ImageProvider appImageProvider(String url) => url.startsWith(demoAssetScheme)
    ? AssetImage(url.substring(demoAssetScheme.length)) as ImageProvider
    : url.startsWith(localFileScheme)
        ? FileImage(File(url.substring(localFileScheme.length)))
        : CachedNetworkImageProvider(url);

class AppNetworkImage extends StatelessWidget {
  const AppNetworkImage({
    super.key,
    required this.url,
    this.fit = BoxFit.cover,
    this.width,
    this.height,
    this.radius = 0,
  });

  final String? url;
  final BoxFit fit;
  final double? width;
  final double? height;
  final double radius;

  @override
  Widget build(BuildContext context) {
    final u = url;
    Widget child;
    if (u == null || u.isEmpty) {
      child = const _Placeholder();
    } else if (u.startsWith(demoAssetScheme)) {
      child = Image.asset(u.substring(demoAssetScheme.length), fit: fit, width: width, height: height,
          errorBuilder: (_, _, _) => const _Placeholder());
    } else if (u.startsWith(localFileScheme)) {
      child = Image.file(File(u.substring(localFileScheme.length)), fit: fit, width: width, height: height,
          errorBuilder: (_, _, _) => const _Placeholder());
    } else {
      child = CachedNetworkImage(
        imageUrl: u,
        fit: fit,
        width: width,
        height: height,
        fadeInDuration: const Duration(milliseconds: 200),
        placeholder: (_, _) => const Shimmer(child: SkeletonBox(width: double.infinity, height: double.infinity, radius: 0)),
        errorWidget: (_, _, _) => const _Placeholder(),
      );
    }
    child = SizedBox(width: width, height: height, child: child);
    if (radius > 0) {
      child = ClipRRect(borderRadius: BorderRadius.circular(radius), child: child);
    }
    return child;
  }
}

class _Placeholder extends StatelessWidget {
  const _Placeholder();

  @override
  Widget build(BuildContext context) {
    return Container(
      color: AppColors.primaryLight,
      alignment: Alignment.center,
      child: const Icon(Icons.image_outlined, color: AppColors.primary, size: 36),
    );
  }
}
