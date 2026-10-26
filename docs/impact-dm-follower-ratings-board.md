# Impact: DM web ratings → follower board

## Change
Delegated Manager web «التقييمات» shows follower cards + detail KPIs (rating from lists, buckets, remaining bands, collection imports, legal), proxied via existing `sales-manager/director-follower-ops/*` (same as عمل المتابعين).

Clickable list cards use `director-delegate-followup` summary/customers (same as متابعة المندوبين). Customer profile uses PaymentSummary + Payments + OfficialSales (name+phone match).

## Impact
| Surface | Impact |
|---------|--------|
| BE_SalesEmployee | Yes — DM routes under `followers/ops/*`, `delegate-followup/lists/*`, customer profile |
| BE_Company | No schema / no service change |
| FE_Company | Yes — `dm-stats.vue`, `dm-customer-profile.vue`, `delegatedManagerApi.js` |
| Flutter DM/BM | No |
| DB | No |

## Auth
Same aggregator + cityValue fan-out as director follower ops; policy `DelegatedManager` only.

## Regression notes
- City choice persisted in `localStorage` (`dm-stats-city`) + route query
- Initial board load: follower cards first; summaries deferred (faster paint)
- List/legal/imports/KPI cards open detail panels; customer row → `/dm-customer-profile`
