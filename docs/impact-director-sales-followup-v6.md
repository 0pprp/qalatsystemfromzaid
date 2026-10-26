# Impact: متابعة المبيعات V6

## Change
- Detail: auth/collector ratings ≤6 only (anonymized); system 30-day average history; zeroed count
- Gateway: `SalesEmployeeAppBlocks` — director can stop sales-employee app (blocks Login + StartShift)
- FE: all-provinces cards, 3-card rating line, customers container + zeroed + focus mode
- **V6 zeroed fix:** مصفر = `AmountTotalSales > 0` و`ReceiptsTotal >= AmountTotalSales` و`AmountRemaining <= 0` (ليست صفوفاً مالية فارغة). Marker: `DirectorSalesFollowup_V6_ZeroedPaidEqualsSale`. Faster FE focus/card transitions.

## Impact
| Surface | Impact |
|---------|--------|
| BE_Company | Yes — `DirectorSalesFollowupService` / Rules marker V6 (+ zeroed paid=sale) |
| BE_SalesEmployee | Yes — app-block store, Director/Auth/SalesController gates |
| FE_Company | Yes — `director-sales-followup.vue`, `directorApi.js` |
| Flutter | No code change (receives 403 messages) |
| DB | Gateway table `dbo.SalesEmployeeAppBlocks` (auto-created) |

## Auth
DirectorOrMonitor for followup + app-block; sales employee Login/StartShift check block store.
