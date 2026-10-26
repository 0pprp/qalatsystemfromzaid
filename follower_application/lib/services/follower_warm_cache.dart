import 'dart:convert';

import 'package:follower_application/config/app_env.dart';
import 'package:follower_application/services/follower_json_cache.dart';
import 'package:follower_application/services/follower_list_cards_cache.dart';
import 'package:follower_application/utils/iraq_datetime.dart';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';

/// Fire-and-forget warm of every major follower endpoint after login / home boot.
class FollowerWarmCache {
  static bool _running = false;

  static Future<void> warmAll({bool force = false}) async {
    if (_running && !force) return;
    _running = true;
    try {
      final prefs = await SharedPreferences.getInstance();
      final asyncId = prefs.getString('AsyncId') ?? '';
      final link =
          AppEnv.apiBase(fallback: prefs.getString('LinkDelegate') ?? '');
      if (asyncId.isEmpty || link.isEmpty) return;

      final cardsFut = _get(link, 'Followers/ListCards', asyncId);
      final statsFut = _get(link, 'Followers/ListStats', asyncId);
      final decliningFut = _get(link, 'Followers/DecliningCustomers', asyncId);
      final collectionFut =
          _get(link, 'Followers/CollectionLists', asyncId);
      final pendingFut = _get(link, 'Followers/PendingSales', asyncId);

      final results = await Future.wait([
        cardsFut,
        statsFut,
        decliningFut,
        collectionFut,
        pendingFut,
      ]);

      final cardsBody = results[0];
      final statsBody = results[1];

      final listIds = <int>[];
      if (cardsBody != null) {
        final data = json.decode(cardsBody) as Map;
        final raw = data['items'] ?? data['Items'] ?? [];
        final cards = (raw as List).map((e) {
          final m = Map<String, dynamic>.from(e as Map);
          return {
            'listId': int.tryParse('${m['listId'] ?? m['ListId'] ?? 0}') ?? 0,
            'listName': '${m['listName'] ?? m['ListName'] ?? 'قائمة'}',
            'score': double.tryParse('${m['score'] ?? m['Score'] ?? 0}') ?? 0.0,
            'ratingLabel': '${m['ratingLabel'] ?? m['RatingLabel'] ?? ''}',
            'scoreWeekAgo': m['scoreWeekAgo'] ?? m['ScoreWeekAgo'],
            'trend': '${m['trend'] ?? m['Trend'] ?? 'flat'}'.toLowerCase(),
            'scoreMonthAgo': m['scoreMonthAgo'] ?? m['ScoreMonthAgo'],
            'monthTrend':
                '${m['monthTrend'] ?? m['MonthTrend'] ?? 'flat'}'.toLowerCase(),
          };
        }).where((c) => (c['listId'] as int) > 0).toList();
        await FollowerListCardsCache.save(cards);
        for (final c in cards) {
          listIds.add(c['listId'] as int);
        }
      }

      if (statsBody != null) {
        final data = json.decode(statsBody) as Map;
        final raw = data['items'] ?? data['Items'] ?? [];
        final list = (raw as List)
            .map((e) => Map<String, dynamic>.from(e as Map))
            .toList();
        await FollowerJsonCache.save(FollowerJsonCache.listStatsAll(), {
          'items': list,
          'yearMonth': data['yearMonth'] ?? data['YearMonth'],
        });
      }

      if (results[2] != null) {
        await FollowerJsonCache.save('follower_declining_v1', results[2]!);
      }
      if (results[3] != null) {
        await FollowerJsonCache.save('follower_collection_lists_v1', results[3]!);
      }
      if (results[4] != null) {
        await FollowerJsonCache.save('follower_pending_sales_v1', results[4]!);
      }

      final ymd = IraqDateTime.formatYmd(
          DateTime.now().subtract(const Duration(days: 1)));
      // Prefetch each list detail hot endpoints in parallel (bounded).
      await Future.wait(listIds.map((id) => _warmList(link, asyncId, id, ymd)));
    } catch (_) {
    } finally {
      _running = false;
    }
  }

  static Future<void> _warmList(
      String link, String asyncId, int listId, String ymd) async {
    try {
      final statsFut = _get(link, 'Followers/ListStats/$listId', asyncId);
      final custFut = _get(
        link,
        'Followers/ListStats/$listId/rating-customers',
        asyncId,
        extra: {'level': 'all'},
      );
      final rem100Fut = _get(
        link,
        'Followers/ListStats/$listId/remainder-customers',
        asyncId,
        extra: {'minRemaining': '0', 'maxRemaining': '100000'},
      );
      final rem200Fut = _get(
        link,
        'Followers/ListStats/$listId/remainder-customers',
        asyncId,
        extra: {'minRemaining': '100000', 'maxRemaining': '200000'},
      );
      final paidFut = _get(link, 'Followers/Daily', asyncId, extra: {
        'childId': '$listId',
        'date': ymd,
        'showType': 'المسددين',
      });
      final unpaidFut = _get(link, 'Followers/Daily', asyncId, extra: {
        'childId': '$listId',
        'date': ymd,
        'showType': 'الغير مسددين',
      });

      final parts = await Future.wait([
        statsFut,
        custFut,
        rem100Fut,
        rem200Fut,
        paidFut,
        unpaidFut,
      ]);

      final statsBody = parts[0];
      if (statsBody != null) {
        final data = json.decode(statsBody) as Map;
        final item = data['item'] ?? data['Item'];
        if (item is Map) {
          List<Map<String, dynamic>> parseRem(String? body) {
            if (body == null) return [];
            final m = json.decode(body) as Map;
            final raw = m['items'] ?? m['Items'] ?? [];
            return (raw as List)
                .map((e) => Map<String, dynamic>.from(e as Map))
                .toList();
          }

          await FollowerJsonCache.save(
            FollowerJsonCache.listDetailStats(listId),
            {
              'stats': Map<String, dynamic>.from(item),
              'r100': parseRem(parts[2]),
              'r200': parseRem(parts[3]),
            },
          );
        }
      }

      final custBody = parts[1];
      if (custBody != null) {
        final data = json.decode(custBody) as Map;
        final raw = data['items'] ?? data['Items'] ?? [];
        final rows = (raw as List)
            .map((e) => Map<String, dynamic>.from(e as Map))
            .toList();
        await FollowerJsonCache.save(
            FollowerJsonCache.listDetailCustomers(listId), rows);
      }

      Future<void> saveDaily(String showType, String? body) async {
        if (body == null) return;
        final decoded = json.decode(body);
        if (decoded is! List) return;
        final rows = decoded
            .map((e) => Map<String, dynamic>.from(e as Map))
            .toList();
        await FollowerJsonCache.save(
          FollowerJsonCache.listDetailDaily(listId, showType, ymd),
          rows,
        );
      }

      await saveDaily('المسددين', parts[4]);
      await saveDaily('الغير مسددين', parts[5]);
    } catch (_) {}
  }

  static Future<String?> _get(
    String link,
    String path,
    String asyncId, {
    Map<String, String>? extra,
  }) async {
    try {
      final qp = <String, String>{'asyncId': asyncId, ...?extra};
      final uri = Uri.parse('$link$path').replace(queryParameters: qp);
      final res = await http.get(uri).timeout(const Duration(seconds: 35));
      if (res.statusCode != 200) return null;
      return res.body;
    } catch (_) {
      return null;
    }
  }
}
