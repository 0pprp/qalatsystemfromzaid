import 'package:branch_manager_application/config/app_env.dart';
import 'package:branch_manager_application/core/api.dart';
import 'package:branch_manager_application/core/session.dart';
import 'package:branch_manager_application/theme/bm_theme.dart';
import 'package:branch_manager_application/ui/branch_visit_page.dart';
import 'package:branch_manager_application/ui/home_shell.dart';
import 'package:branch_manager_application/ui/login_page.dart';
import 'package:branch_manager_application/ui/province_picker_page.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  // Always light UI — OS dark mode must not drive Material or leave gray chrome.
  SystemChrome.setSystemUIOverlayStyle(
    const SystemUiOverlayStyle(
      statusBarColor: Color(0xFF0F5C5C),
      statusBarIconBrightness: Brightness.light,
      systemNavigationBarColor: Colors.white,
      systemNavigationBarIconBrightness: Brightness.dark,
    ),
  );
  AppEnv.logIfDebug();
  // Release builds otherwise render a blank gray box for build errors.
  ErrorWidget.builder = (details) => _BuildErrorView(details: details);
  // Paint first frame immediately; load session after so splash never sticks.
  runApp(const BranchManagerApp());
}

class _BuildErrorView extends StatelessWidget {
  const _BuildErrorView({required this.details});

  final FlutterErrorDetails details;

  @override
  Widget build(BuildContext context) {
    return Directionality(
      textDirection: TextDirection.rtl,
      child: ColoredBox(
        color: Colors.white,
        child: Center(
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: Text(
              'حدث خطأ في عرض الصفحة\n\n${details.exceptionAsString()}',
              textAlign: TextAlign.center,
              style: const TextStyle(
                fontFamily: 'Cairo',
                fontSize: 14,
                color: Color(0xFFB42318),
                decoration: TextDecoration.none,
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class BranchManagerApp extends StatefulWidget {
  const BranchManagerApp({super.key});

  @override
  State<BranchManagerApp> createState() => _BranchManagerAppState();
}

class _BranchManagerAppState extends State<BranchManagerApp> {
  Map<String, dynamic>? _pendingVisit;
  bool _openingCity = false;
  bool _bootReady = false;

  @override
  void initState() {
    super.initState();
    BmSession.revision.addListener(_onSessionChanged);
    _boot();
  }

  Future<void> _boot() async {
    try {
      await BmSession.instance.load();
    } catch (e, st) {
      debugPrint('BmSession.load failed: $e\n$st');
    }
    // Never trap on incomplete visit flags from older installs.
    final s = BmSession.instance;
    if ((s.activeVisitId ?? '').isEmpty) {
      s.visitEntryComplete = true;
    }
    if (mounted) setState(() => _bootReady = true);
  }

  @override
  void dispose() {
    BmSession.revision.removeListener(_onSessionChanged);
    super.dispose();
  }

  void _onSessionChanged() {
    if (mounted) setState(() {});
  }

  String _visitIdOf(Map<String, dynamic> visit) =>
      '${visit['id'] ?? visit['Id'] ?? ''}'.trim();

  Future<void> _logout() async {
    _pendingVisit = null;
    await BmSession.instance.clear();
    if (mounted) setState(() {});
  }

  Future<void> _backToProvinces() async {
    _pendingVisit = null;
    await BmSession.instance.setActiveVisitId(null);
    await BmSession.instance.clearActiveCity();
    if (mounted) setState(() {});
  }

  Future<void> _openCity(BmAssignedCity city) async {
    if (!mounted) return;
    setState(() => _openingCity = true);
    try {
      await BmApi.instance.openAssignedCity(city);
      final cityValue = (city.value ?? city.database ?? city.name).trim();
      final visit = await BmApi.instance.openBranchVisit(
        cityValue: cityValue.isEmpty ? city.name : cityValue,
        cityName: city.name,
      );
      final id = _visitIdOf(visit);
      if (id.isEmpty) {
        await BmSession.instance.clearActiveCity();
        throw BmApiException('تعذر فتح استمارة الزيارة');
      }
      await BmSession.instance.setActiveVisitId(id, entryComplete: false);
      if (!mounted) return;
      setState(() {
        _pendingVisit = visit;
        _openingCity = false;
      });
    } catch (e) {
      try {
        await BmSession.instance.clearActiveCity();
      } catch (_) {}
      if (!mounted) return;
      setState(() => _openingCity = false);
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('$e', style: const TextStyle(fontFamily: 'Cairo')),
          duration: const Duration(seconds: 5),
        ),
      );
    }
  }

  /// Leaving province: entry form must be acknowledged; ratings happen on depart sheet.
  Future<bool> _ensureVisitReadyForDepart() async {
    final session = BmSession.instance;
    try {
      var visitId = (session.activeVisitId ?? '').trim();
      var visit = await BmApi.instance.activeBranchVisit();
      if (visit == null) {
        final cityValue =
            (session.activeCityValue ?? session.cityName ?? '').trim();
        if (cityValue.isEmpty) {
          if (mounted) {
            ScaffoldMessenger.of(context).showSnackBar(
              const SnackBar(
                content: Text('لا محافظة مفتوحة',
                    style: TextStyle(fontFamily: 'Cairo')),
              ),
            );
          }
          return false;
        }
        visit = await BmApi.instance.openBranchVisit(
          cityValue: cityValue,
          cityName: session.cityName,
        );
      }
      visitId = _visitIdOf(visit);
      if (visitId.isEmpty) return false;

      if (!session.visitEntryComplete) {
        await session.setActiveVisitId(visitId, entryComplete: false);
        if (!mounted) return false;
        setState(() => _pendingVisit = visit);
        return false;
      }
      await session.setActiveVisitId(visitId, entryComplete: true);
      return true;
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('$e', style: const TextStyle(fontFamily: 'Cairo'))),
        );
      }
      return false;
    }
  }

  Widget _wrap(Widget home) {
    return MaterialApp(
      title: 'تطبيق إدارة الفرع',
      debugShowCheckedModeBanner: false,
      theme: BmTheme.light(),
      themeMode: ThemeMode.light,
      home: home,
    );
  }

  @override
  Widget build(BuildContext context) {
    if (!_bootReady) {
      return _wrap(
        const Scaffold(
          backgroundColor: Color(0xFF0F5C5C),
          body: Center(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                CircularProgressIndicator(color: Colors.white),
                SizedBox(height: 16),
                Text(
                  'تطبيق إدارة الفرع',
                  style: TextStyle(
                    fontFamily: 'Cairo',
                    color: Colors.white,
                    fontSize: 18,
                    fontWeight: FontWeight.bold,
                  ),
                ),
              ],
            ),
          ),
        ),
      );
    }

    final session = BmSession.instance;

    // 1) Not logged in
    if (!session.isAuthenticated) {
      return _wrap(
        LoginPage(onLoggedIn: () {
          // Single-city login may have opened a city without visit form.
          // If so, force province picker until user opens via _openCity.
          final s = BmSession.instance;
          if (s.isLoggedIn && (s.activeVisitId ?? '').isEmpty) {
            s.clearActiveCity().then((_) {
              if (mounted) setState(() {});
            });
          } else if (mounted) {
            setState(() {});
          }
        }),
      );
    }

    // 2) Entry form after opening a province
    if (_pendingVisit != null) {
      return _wrap(
        BranchVisitPage(
          visit: _pendingVisit!,
          onContinue: () async {
            await BmSession.instance.markVisitEntryComplete();
            if (!mounted) return;
            setState(() => _pendingVisit = null);
          },
        ),
      );
    }

    // 3) Pick province (or still opening)
    if (session.needsCityPick || !session.isLoggedIn) {
      return _wrap(
        Stack(
          children: [
            ProvincePickerPage(
              onPicked: () {
                if (mounted) setState(() {});
              },
              onLogout: _logout,
              onOpenCity: _openCity,
            ),
            if (_openingCity)
              const ColoredBox(
                color: Color(0x99000000),
                child: Center(
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      CircularProgressIndicator(color: Colors.white),
                      SizedBox(height: 16),
                      Text(
                        'جاري فتح المحافظة واستمارة الزيارة…',
                        style: TextStyle(
                          fontFamily: 'Cairo',
                          color: Colors.white,
                          fontSize: 15,
                        ),
                      ),
                    ],
                  ),
                ),
              ),
          ],
        ),
      );
    }

    // 4) Inside province with completed entry
    return _wrap(
      HomeShell(
        onLogout: _logout,
        onDepartBranch: _backToProvinces,
        onEnsureVisitReadyForDepart: _ensureVisitReadyForDepart,
      ),
    );
  }
}
