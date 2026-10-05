import 'package:flutter/material.dart';
import 'package:main_follower_application/core/config/province_labels.dart';
import 'package:main_follower_application/data/sales_repository.dart';
import 'package:main_follower_application/data/staff_repository.dart';
import 'package:main_follower_application/domain/branch_sale.dart';
import 'package:main_follower_application/domain/mission.dart';
import 'package:main_follower_application/domain/staff_member.dart';
import 'package:main_follower_application/presentation/city_history_screen.dart';
import 'package:main_follower_application/presentation/customer_profile_screen.dart';
import 'package:main_follower_application/presentation/sale_auth_dialog.dart';
import 'package:main_follower_application/presentation/staff_review_dialog.dart';

enum CityHubPage {
  sales,
  delegates,
  followers,
  employees,
  history,
}

/// City workspace — side menu (not bottom tabs).
class CityHubScreen extends StatefulWidget {
  const CityHubScreen({
    super.key,
    required this.mission,
    required this.cityValue,
  });

  final Mission mission;
  final String cityValue;

  @override
  State<CityHubScreen> createState() => _CityHubScreenState();
}

class _CityHubScreenState extends State<CityHubScreen> {
  CityHubPage _page = CityHubPage.sales;

  String get _title => switch (_page) {
        CityHubPage.sales => 'المبيعات',
        CityHubPage.delegates => 'المندوبون',
        CityHubPage.followers => 'المتابعون',
        CityHubPage.employees => 'موظفو المبيعات',
        CityHubPage.history => 'السجل',
      };

  @override
  Widget build(BuildContext context) {
    final cityLabel = ProvinceLabels.display(widget.cityValue);
    return Scaffold(
      appBar: AppBar(
        title: Text('$cityLabel — $_title'),
        backgroundColor: const Color(0xFF0F3D3E),
        foregroundColor: Colors.white,
      ),
      drawer: Drawer(
        child: SafeArea(
          child: ListView(
            padding: EdgeInsets.zero,
            children: [
              DrawerHeader(
                decoration: const BoxDecoration(color: Color(0xFF0F3D3E)),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Text(
                      'محافظة المهمة',
                      style: TextStyle(color: Colors.white70, fontSize: 13),
                    ),
                    const SizedBox(height: 8),
                    Text(
                      cityLabel,
                      style: const TextStyle(
                        color: Colors.white,
                        fontSize: 22,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                    const SizedBox(height: 4),
                    Text(
                      widget.mission.committeeName.isEmpty
                          ? 'مهمة #${widget.mission.id}'
                          : widget.mission.committeeName,
                      style: const TextStyle(color: Colors.white70),
                    ),
                  ],
                ),
              ),
              _item(CityHubPage.sales, Icons.storefront_outlined, 'المبيعات'),
              _item(CityHubPage.delegates, Icons.handshake_outlined, 'المندوبون'),
              _item(CityHubPage.followers, Icons.groups_outlined, 'المتابعون'),
              _item(CityHubPage.employees, Icons.badge_outlined, 'موظفو المبيعات'),
              _item(CityHubPage.history, Icons.history, 'السجل'),
            ],
          ),
        ),
      ),
      body: switch (_page) {
        CityHubPage.sales => _SalesPage(
            mission: widget.mission,
            cityValue: widget.cityValue,
          ),
        CityHubPage.delegates => _StaffPage(
            mission: widget.mission,
            cityValue: widget.cityValue,
            loader: () => StaffRepository.listDelegates(
              missionId: widget.mission.id,
              cityValue: widget.cityValue,
            ),
            emptyLabel: 'لا مندوبين في هذه المحافظة',
          ),
        CityHubPage.followers => _StaffPage(
            mission: widget.mission,
            cityValue: widget.cityValue,
            loader: () => StaffRepository.listFollowers(
              missionId: widget.mission.id,
              cityValue: widget.cityValue,
            ),
            emptyLabel: 'لا متابعين فرعيين في هذه المحافظة',
          ),
        CityHubPage.employees => _StaffPage(
            mission: widget.mission,
            cityValue: widget.cityValue,
            loader: () => StaffRepository.listEmployees(
              missionId: widget.mission.id,
              cityValue: widget.cityValue,
            ),
            emptyLabel: 'لا موظفي مبيعات في هذه المحافظة',
          ),
        CityHubPage.history =>
          CityHistoryScreen(cityValue: widget.cityValue, embedded: true),
      },
    );
  }

  Widget _item(CityHubPage page, IconData icon, String label) {
    final selected = _page == page;
    return ListTile(
      leading: Icon(icon, color: selected ? const Color(0xFF0F3D3E) : null),
      title: Text(
        label,
        style: TextStyle(
          fontWeight: selected ? FontWeight.bold : FontWeight.normal,
          color: selected ? const Color(0xFF0F3D3E) : null,
        ),
      ),
      selected: selected,
      selectedTileColor: const Color(0xFFE8F1F1),
      onTap: () {
        setState(() => _page = page);
        Navigator.of(context).pop();
      },
    );
  }
}

class _SalesPage extends StatefulWidget {
  const _SalesPage({required this.mission, required this.cityValue});
  final Mission mission;
  final String cityValue;

  @override
  State<_SalesPage> createState() => _SalesPageState();
}

class _SalesPageState extends State<_SalesPage> {
  late Future<List<BranchSale>> _future;
  Set<int> _authenticatedSaleIds = {};

  @override
  void initState() {
    super.initState();
    _future = _load();
  }

  Future<List<BranchSale>> _load() async {
    final rows = await SalesRepository.listForMissionCity(
      missionId: widget.mission.id,
      cityValue: widget.cityValue,
    );
    try {
      final reviews = await SalesRepository.listReviews(widget.mission.id);
      _authenticatedSaleIds = reviews
          .where((r) =>
              r.cityValue.toLowerCase() == widget.cityValue.toLowerCase())
          .map((r) => r.saleId)
          .toSet();
    } catch (_) {
      _authenticatedSaleIds = {};
    }
    return rows;
  }

  Future<void> _reload() async {
    setState(() {
      _future = _load();
    });
    await _future;
  }

  Future<void> _authenticate(BranchSale sale) async {
    final saved = await showSaleReviewDialog(
      context,
      sale: sale,
      onSubmit: (score, note, shopStock, dailyRevenue) async {
        await SalesRepository.submitReview(
          missionId: widget.mission.id,
          cityValue: widget.cityValue,
          saleId: sale.saleId,
          customerName: sale.customerName,
          score: score,
          note: note,
          shopStockEstimatedValue: shopStock,
          estimatedDailyRevenue: dailyRevenue,
        );
      },
    );
    if (saved && mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('تمت مصادقة المبيع بنجاح')),
      );
      await _reload();
    }
  }

  void _openProfile(BranchSale sale) {
    Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => CustomerProfileScreen(
          missionId: widget.mission.id,
          cityValue: widget.cityValue,
          customerId: sale.customerId,
          customerName: sale.customerName,
          phone: sale.phone,
          saleId: sale.saleId,
          showAuthenticate: !_authenticatedSaleIds.contains(sale.saleId),
          onAuthenticated: _reload,
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return FutureBuilder<List<BranchSale>>(
      future: _future,
      builder: (context, snap) {
        if (snap.connectionState != ConnectionState.done) {
          return const Center(child: CircularProgressIndicator());
        }
        if (snap.hasError) {
          return Center(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Text(snap.error.toString()),
            ),
          );
        }
        final rows = snap.data ?? [];
        if (rows.isEmpty) {
          return const Center(child: Text('لا توجد مبيعات في هذه المحافظة'));
        }
        final pending =
            rows.where((s) => !_authenticatedSaleIds.contains(s.saleId)).toList();
        final done =
            rows.where((s) => _authenticatedSaleIds.contains(s.saleId)).toList();
        final ordered = [...pending, ...done];
        return RefreshIndicator(
          onRefresh: _reload,
          child: ListView.separated(
            padding: const EdgeInsets.fromLTRB(12, 12, 12, 32),
            itemCount: ordered.length + (pending.isNotEmpty ? 1 : 0),
            separatorBuilder: (_, __) => const SizedBox(height: 8),
            itemBuilder: (context, i) {
              if (pending.isNotEmpty && i == 0) {
                return Padding(
                  padding: const EdgeInsets.only(bottom: 4),
                  child: Text(
                    'بانتظار المصادقة: ${pending.length} · مصادق: ${done.length}',
                    style: const TextStyle(
                      fontWeight: FontWeight.w600,
                      color: Color(0xFF0F3D3E),
                    ),
                  ),
                );
              }
              final s = ordered[pending.isNotEmpty ? i - 1 : i];
              final authed = _authenticatedSaleIds.contains(s.saleId);
              return Card(
                child: ListTile(
                  title: Text(s.customerName),
                  subtitle: Text(
                    [
                      'مبيع #${s.saleId}',
                      if (s.phone != null && s.phone!.isNotEmpty) s.phone!,
                      if (s.employeeName != null && s.employeeName!.isNotEmpty)
                        'موظف: ${s.employeeName}',
                      if (s.status != null && s.status!.isNotEmpty) s.status!,
                      if (authed) 'مصادق',
                    ].join(' · '),
                  ),
                  onTap: () => _openProfile(s),
                  trailing: Wrap(
                    spacing: 4,
                    crossAxisAlignment: WrapCrossAlignment.center,
                    children: [
                      IconButton(
                        tooltip: 'ملف الزبون',
                        onPressed: () => _openProfile(s),
                        icon: const Icon(Icons.person_outline),
                      ),
                      if (authed)
                        const Chip(
                          label: Text('مصادق'),
                          avatar: Icon(Icons.verified, size: 16),
                          visualDensity: VisualDensity.compact,
                        )
                      else
                        FilledButton(
                          onPressed: () => _authenticate(s),
                          style: FilledButton.styleFrom(
                            backgroundColor: const Color(0xFF0F3D3E),
                          ),
                          child: const Text('تمت المصادقة'),
                        ),
                    ],
                  ),
                ),
              );
            },
          ),
        );
      },
    );
  }
}

class _StaffPage extends StatefulWidget {
  const _StaffPage({
    required this.mission,
    required this.cityValue,
    required this.loader,
    required this.emptyLabel,
  });

  final Mission mission;
  final String cityValue;
  final Future<List<StaffMember>> Function() loader;
  final String emptyLabel;

  @override
  State<_StaffPage> createState() => _StaffPageState();
}

class _StaffPageState extends State<_StaffPage> {
  late Future<List<StaffMember>> _future;

  @override
  void initState() {
    super.initState();
    _future = widget.loader();
  }

  Future<void> _reload() async {
    setState(() => _future = widget.loader());
    await _future;
  }

  Future<void> _rate(StaffMember member) async {
    final saved = await showStaffReviewDialog(
      context,
      member: member,
      onSubmit: (score, note) async {
        await StaffRepository.submitReview(
          missionId: widget.mission.id,
          cityValue: widget.cityValue,
          targetType: member.targetType,
          targetId: member.id,
          targetName: member.name,
          listId: member.listId,
          score: score,
          note: note,
        );
      },
    );
    if (saved && mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('تم حفظ تقييم الموظف وملاحظته')),
      );
    }
  }

  Future<void> _openNotes(StaffMember member) async {
    await Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => StaffNotesScreen(
          member: member,
          cityValue: widget.cityValue,
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return FutureBuilder<List<StaffMember>>(
      future: _future,
      builder: (context, snap) {
        if (snap.connectionState != ConnectionState.done) {
          return const Center(child: CircularProgressIndicator());
        }
        if (snap.hasError) {
          return Center(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Text(snap.error.toString()),
            ),
          );
        }
        final rows = snap.data ?? [];
        if (rows.isEmpty) {
          return Center(child: Text(widget.emptyLabel));
        }
        return RefreshIndicator(
          onRefresh: _reload,
          child: ListView.separated(
            padding: const EdgeInsets.fromLTRB(12, 12, 12, 32),
            itemCount: rows.length,
            separatorBuilder: (_, __) => const SizedBox(height: 8),
            itemBuilder: (context, i) {
              final m = rows[i];
              return Card(
                child: ListTile(
                  title: Text(m.name),
                  subtitle: Text(
                    [
                      if (m.subtitle != null && m.subtitle!.isNotEmpty) m.subtitle!,
                      'معرّف ${m.id}',
                    ].join(' · '),
                  ),
                  onTap: () => _openNotes(m),
                  trailing: FilledButton(
                    onPressed: () => _rate(m),
                    style: FilledButton.styleFrom(
                      backgroundColor: const Color(0xFF0F3D3E),
                    ),
                    child: const Text('تقييم'),
                  ),
                ),
              );
            },
          ),
        );
      },
    );
  }
}

class StaffNotesScreen extends StatefulWidget {
  const StaffNotesScreen({
    super.key,
    required this.member,
    required this.cityValue,
  });

  final StaffMember member;
  final String cityValue;

  @override
  State<StaffNotesScreen> createState() => _StaffNotesScreenState();
}

class _StaffNotesScreenState extends State<StaffNotesScreen> {
  late Future<List<StaffReview>> _future;

  @override
  void initState() {
    super.initState();
    _future = StaffRepository.listTargetNotes(
      targetType: widget.member.targetType,
      targetId: widget.member.id,
      cityValue: widget.cityValue,
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: Text('سجل ${widget.member.name}'),
        backgroundColor: const Color(0xFF0F3D3E),
        foregroundColor: Colors.white,
      ),
      body: FutureBuilder<List<StaffReview>>(
        future: _future,
        builder: (context, snap) {
          if (snap.connectionState != ConnectionState.done) {
            return const Center(child: CircularProgressIndicator());
          }
          if (snap.hasError) {
            return Center(child: Text(snap.error.toString()));
          }
          final rows = snap.data ?? [];
          if (rows.isEmpty) {
            return const Center(child: Text('لا ملاحظات تقييم بعد'));
          }
          return ListView.separated(
            padding: const EdgeInsets.fromLTRB(12, 12, 12, 32),
            itemCount: rows.length,
            separatorBuilder: (_, __) => const Divider(height: 1),
            itemBuilder: (context, i) {
              final r = rows[i];
              return ListTile(
                title: Text('${r.score}/10'),
                subtitle: Text(r.feedNote ?? r.note ?? ''),
              );
            },
          );
        },
      ),
    );
  }
}
