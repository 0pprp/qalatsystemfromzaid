import 'package:branch_manager_application/theme/bm_theme.dart';
import 'package:branch_manager_application/core/session.dart';
import 'package:flutter/material.dart';

/// After login: pick which assigned province to open.
class ProvincePickerPage extends StatelessWidget {
  const ProvincePickerPage({
    super.key,
    required this.onPicked,
    required this.onLogout,
    required this.onOpenCity,
  });

  final VoidCallback onPicked;
  final VoidCallback onLogout;
  final Future<void> Function(BmAssignedCity city) onOpenCity;

  @override
  Widget build(BuildContext context) {
    final cities = BmSession.instance.assignedCities;
    final user = BmSession.instance.greetingName;

    return Directionality(
      textDirection: TextDirection.rtl,
      child: Scaffold(
        body: Container(
          decoration: const BoxDecoration(
            gradient: LinearGradient(
              begin: Alignment.topRight,
              end: Alignment.bottomLeft,
              colors: [
                Color(0xFF0A3F3F),
                Color(0xFF0F5C5C),
                Color(0xFF1A7A6D),
              ],
            ),
          ),
          child: SafeArea(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Padding(
                  padding: const EdgeInsets.fromLTRB(20, 16, 20, 8),
                  child: Row(
                    children: [
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            const Text(
                              'تطبيق إدارة الفرع',
                              style: TextStyle(
                                fontFamily: 'Cairo',
                                color: Colors.white,
                                fontSize: 22,
                                fontWeight: FontWeight.w700,
                              ),
                            ),
                            const SizedBox(height: 4),
                            Text(
                              user.isEmpty ? 'اختر المحافظة' : 'مرحباً، $user',
                              style: const TextStyle(
                                fontFamily: 'Cairo',
                                color: Colors.white70,
                                fontSize: 14,
                              ),
                            ),
                          ],
                        ),
                      ),
                      IconButton(
                        tooltip: 'خروج',
                        onPressed: onLogout,
                        icon: const Icon(Icons.logout, color: Colors.white70),
                      ),
                    ],
                  ),
                ),
                const Padding(
                  padding: EdgeInsets.symmetric(horizontal: 20),
                  child: Text(
                    'المحافظات المكلّف بها',
                    style: TextStyle(
                      fontFamily: 'Cairo',
                      color: Colors.white,
                      fontSize: 16,
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                ),
                const SizedBox(height: 12),
                Expanded(
                  child: cities.isEmpty
                      ? const Center(
                          child: Text(
                            'لا توجد محافظات مرتبطة بهذا الحساب',
                            style: TextStyle(
                              fontFamily: 'Cairo',
                              color: Colors.white70,
                            ),
                          ),
                        )
                      : ListView.separated(
                          padding: const EdgeInsets.fromLTRB(16, 0, 16, 24),
                          itemCount: cities.length,
                          separatorBuilder: (_, __) =>
                              const SizedBox(height: 12),
                          itemBuilder: (context, i) {
                            final city = cities[i];
                            return _ProvinceCard(
                              city: city,
                              onTap: () async {
                                await onOpenCity(city);
                                onPicked();
                              },
                            );
                          },
                        ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _ProvinceCard extends StatefulWidget {
  const _ProvinceCard({required this.city, required this.onTap});

  final BmAssignedCity city;
  final Future<void> Function() onTap;

  @override
  State<_ProvinceCard> createState() => _ProvinceCardState();
}

class _ProvinceCardState extends State<_ProvinceCard> {
  bool _loading = false;
  String? _error;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.white,
      elevation: 2,
      shadowColor: Colors.black26,
      borderRadius: BorderRadius.circular(16),
      child: InkWell(
        borderRadius: BorderRadius.circular(16),
        onTap: _loading
            ? null
            : () async {
                setState(() {
                  _loading = true;
                  _error = null;
                });
                try {
                  await widget.onTap();
                } catch (e) {
                  if (mounted) {
                    setState(() {
                      _loading = false;
                      _error = '$e';
                    });
                  }
                  return;
                }
                if (mounted) setState(() => _loading = false);
              },
        child: Padding(
          padding: const EdgeInsets.all(18),
          child: Row(
            children: [
              Container(
                width: 48,
                height: 48,
                decoration: BoxDecoration(
                  color: BmTheme.primary.withValues(alpha: 0.12),
                  borderRadius: BorderRadius.circular(12),
                ),
                child: const Icon(Icons.location_city, color: BmTheme.primary),
              ),
              const SizedBox(width: 14),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      widget.city.name,
                      style: const TextStyle(
                        fontFamily: 'Cairo',
                        fontSize: 17,
                        fontWeight: FontWeight.w700,
                        color: Color(0xFF0A3F3F),
                      ),
                    ),
                    if (_error != null) ...[
                      const SizedBox(height: 4),
                      Text(
                        _error!,
                        style: const TextStyle(
                          fontFamily: 'Cairo',
                          fontSize: 12,
                          color: Colors.red,
                        ),
                      ),
                    ] else ...[
                      const SizedBox(height: 2),
                      const Text(
                        'اضغط لفتح بيانات هذه المحافظة',
                        style: TextStyle(
                          fontFamily: 'Cairo',
                          fontSize: 12,
                          color: Colors.black54,
                        ),
                      ),
                    ],
                  ],
                ),
              ),
              if (_loading)
                const SizedBox(
                  width: 22,
                  height: 22,
                  child: CircularProgressIndicator(strokeWidth: 2),
                )
              else
                const Icon(Icons.chevron_left, color: Colors.black45),
            ],
          ),
        ),
      ),
    );
  }
}
