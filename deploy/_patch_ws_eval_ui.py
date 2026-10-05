from pathlib import Path

p = Path(r"C:\GitHub\SalesHaider-main\SalesHaider-main\delegated_manager_application\lib\features\exceptions\presentation\wrong_sale_detail_screen.dart")
t = p.read_text(encoding="utf-8")

old_vars = """    final del = _asMap(p?['delegateShopEval'] ?? p?['DelegateShopEval']);
    final follower = _asMap(p?['followerShopEval'] ?? p?['FollowerShopEval']);
    final latestShop = _asMap(p?['latestShop'] ?? p?['LatestShop']);
    final sales = (p?['sales'] ?? p?['Sales'] ?? []) as List? ?? [];
    final payments = (p?['payments'] ?? p?['Payments'] ?? []) as List? ?? [];
    final selfId = request.summary.customerId;
    final matches = (review?.matchingCustomers ?? const <ExceptionReviewMatchCustomer>[])
        .where((m) {
      if (m.isCurrentCustomer) return false;
      if (selfId != null && m.customerId != null && m.customerId == selfId) {
        return false;
      }
      return true;
    }).toList();

    final employeeName = _firstNonEmpty([
      for (final s in sales.take(3))
        '${_asMap(s)?['employeeName'] ?? _asMap(s)?['EmployeeName'] ?? ''}',
      '${del?['evaluatorName'] ?? del?['EvaluatorName'] ?? ''}',
      '${p?['delegateName'] ?? p?['DelegateName'] ?? ''}',
      request.summary.requestingManagerDisplayName ?? '',
    ]);
"""

new_vars = """    final collectionDelegateEval =
        _asMap(p?['delegateShopEval'] ?? p?['DelegateShopEval']);
    final followerEval =
        _asMap(p?['followerShopEval'] ?? p?['FollowerShopEval']);
    final sales = (p?['sales'] ?? p?['Sales'] ?? []) as List? ?? [];
    final payments = (p?['payments'] ?? p?['Payments'] ?? []) as List? ?? [];
    final selfId = request.summary.customerId;
    final matches = (review?.matchingCustomers ??
            const <ExceptionReviewMatchCustomer>[])
        .where((m) {
      if (m.isCurrentCustomer) return false;
      if (selfId != null && m.customerId != null && m.customerId == selfId) {
        return false;
      }
      return true;
    }).toList();

    // One eval container: follower OR collection-list delegate (not sales employee).
    final fromFollower = _isWrongSaleFromFollower(
      request: request,
      followerEval: followerEval,
      collectionDelegateEval: collectionDelegateEval,
    );
    final senderEval = fromFollower ? followerEval : collectionDelegateEval;
    final evalTitle =
        fromFollower ? 'تقييم المتابع' : 'تقييم مندوب الجباية';
    final evaluatorLabel =
        fromFollower ? 'اسم المتابع' : 'اسم مندوب الجباية';
    final evaluatorName = _firstNonEmpty([
      '${senderEval?['evaluatorName'] ?? senderEval?['EvaluatorName'] ?? ''}',
      request.summary.requestingManagerDisplayName ?? '',
    ]);
    final listName = _firstNonEmpty([
      '${p?['customerListName'] ?? p?['CustomerListName'] ?? ''}',
      '${p?['delegateName'] ?? p?['DelegateName'] ?? ''}',
    ]);
"""

if old_vars not in t:
    raise SystemExit("vars block not found")
t = t.replace(old_vars, new_vars, 1)

old_cards = """      const SizedBox(height: AppSpacing.md),
      // Always show — empty blanks when not evaluated yet.
      SectionCard(
        title: 'تقييم المتابع',
        children: _shopEvalRows(follower, emptyHint: 'لم يُقيَّم بعد'),
      ),
      const SizedBox(height: AppSpacing.md),
      SectionCard(
        title: 'تقييم المندوب',
        children: [
          InfoRow(label: 'اسم المندوب / الموظف', value: employeeName),
          ..._shopEvalRows(del, emptyHint: 'لم يُقيَّم بعد'),
          if (latestShop != null) ...[
            InfoRow(
              label: 'اسم المحل',
              value: '${latestShop['shopName'] ?? latestShop['ShopName'] ?? '-'}',
            ),
            InfoRow(
              label: 'نوع النشاط',
              value:
                  '${latestShop['shopBusinessType'] ?? latestShop['ShopBusinessType'] ?? '-'}',
            ),
          ],
        ],
      ),
    ];
"""

new_cards = """      const SizedBox(height: AppSpacing.md),
      // Single container: whoever sent the WrongSale (follower or collection delegate).
      SectionCard(
        title: evalTitle,
        children: [
          InfoRow(label: evaluatorLabel, value: evaluatorName),
          InfoRow(label: 'القائمة', value: listName),
          ..._shopEvalRows(senderEval, emptyHint: 'لم يُقيَّم بعد'),
        ],
      ),
    ];
"""

if old_cards not in t:
    raise SystemExit("cards block not found")
t = t.replace(old_cards, new_cards, 1)

# Replace _shopEvalRows and add helper before it
old_fn = """  List<Widget> _shopEvalRows(Map<String, dynamic>? e, {String? emptyHint}) {
    if (e == null) {
      return [
        InfoRow(label: 'الحالة', value: emptyHint ?? 'غير متوفر'),
        const InfoRow(label: 'المقيّم', value: '-'),
        const InfoRow(label: 'التقييم', value: '-'),
        const InfoRow(label: 'الملاحظة', value: '-'),
        const InfoRow(label: 'قيمة بضاعة المحل', value: '-'),
        const InfoRow(label: 'واردات المحل اليومية', value: '-'),
      ];
    }
    final rating = e['rating'] ?? e['Rating'];
    return [
      InfoRow(
        label: 'المقيّم',
        value: '${e['evaluatorName'] ?? e['EvaluatorName'] ?? '-'}',
      ),
      InfoRow(
        label: 'التقييم',
        value: rating == null ? '-' : '$rating / 10',
      ),
      InfoRow(
        label: 'الملاحظة',
        value: '${e['note'] ?? e['Note'] ?? '-'}',
      ),
      InfoRow(
        label: 'قيمة بضاعة المحل',
        value: _num(
            e['shopStockEstimatedValue'] ?? e['ShopStockEstimatedValue']),
      ),
      InfoRow(
        label: 'واردات المحل اليومية',
        value: _num(
            e['estimatedDailyRevenue'] ?? e['EstimatedDailyRevenue']),
      ),
      InfoRow(
        label: 'سعر البيع (عند التقييم)',
        value: _num(e['saleAmount'] ?? e['SaleAmount']),
      ),
    ];
  }
"""

new_fn = """  /// True when WrongSale was sent by the follower (not collection-list delegate).
  bool _isWrongSaleFromFollower({
    required ExceptionRequest request,
    required Map<String, dynamic>? followerEval,
    required Map<String, dynamic>? collectionDelegateEval,
  }) {
    final reason = request.reason ?? '';
    if (reason.contains('المصدر: متابع')) return true;
    if (reason.contains('المصدر: مندوب')) return false;

    final requester =
        (request.summary.requestingManagerDisplayName ?? '').trim();
    final fName =
        '${followerEval?['evaluatorName'] ?? followerEval?['EvaluatorName'] ?? ''}'
            .trim();
    final dName =
        '${collectionDelegateEval?['evaluatorName'] ?? collectionDelegateEval?['EvaluatorName'] ?? ''}'
            .trim();

    if (requester.isNotEmpty && fName.isNotEmpty && requester == fName) {
      return true;
    }
    if (requester.isNotEmpty && dName.isNotEmpty && requester == dName) {
      return false;
    }
    if (requester == 'متابع') return true;
    if (requester == 'مندوب' || requester.contains('مندوب الجباية')) {
      return false;
    }

    final fSrc =
        '${followerEval?['source'] ?? followerEval?['Source'] ?? ''}'.trim();
    final dSrc =
        '${collectionDelegateEval?['source'] ?? collectionDelegateEval?['Source'] ?? ''}'
            .trim();
    if (fSrc.toLowerCase() == 'follower' && dSrc.toLowerCase() != 'delegate') {
      return true;
    }
    if (dSrc.toLowerCase() == 'delegate' && followerEval == null) {
      return false;
    }

    if (followerEval != null && collectionDelegateEval == null) return true;
    if (collectionDelegateEval != null && followerEval == null) return false;

    // New-sale WrongSale from collection delegate carries SalesRequestId.
    if (request.summary.salesRequestId != null) return false;

    // Default: follower authentication path.
    return followerEval != null;
  }

  List<Widget> _shopEvalRows(Map<String, dynamic>? e, {String? emptyHint}) {
    if (e == null) {
      return [
        InfoRow(label: 'الحالة', value: emptyHint ?? 'غير متوفر'),
        const InfoRow(label: 'التقييم', value: '-'),
        const InfoRow(label: 'الملاحظة', value: '-'),
        const InfoRow(label: 'قيمة بضاعة المحل', value: '-'),
        const InfoRow(label: 'واردات المحل اليومية', value: '-'),
      ];
    }
    final rating = e['rating'] ?? e['Rating'];
    return [
      InfoRow(
        label: 'التقييم',
        value: rating == null ? '-' : '$rating / 10',
      ),
      InfoRow(
        label: 'الملاحظة',
        value: '${e['note'] ?? e['Note'] ?? '-'}',
      ),
      InfoRow(
        label: 'قيمة بضاعة المحل',
        value: _num(
            e['shopStockEstimatedValue'] ?? e['ShopStockEstimatedValue']),
      ),
      InfoRow(
        label: 'واردات المحل اليومية',
        value: _num(
            e['estimatedDailyRevenue'] ?? e['EstimatedDailyRevenue']),
      ),
      InfoRow(
        label: 'سعر البيع (عند التقييم)',
        value: _num(e['saleAmount'] ?? e['SaleAmount']),
      ),
    ];
  }
"""

if old_fn not in t:
    raise SystemExit("shopEvalRows not found")
t = t.replace(old_fn, new_fn, 1)

p.write_text(t, encoding="utf-8")
print("Flutter OK", p.stat().st_size)
