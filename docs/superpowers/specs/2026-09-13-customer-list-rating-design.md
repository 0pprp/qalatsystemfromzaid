# Customer & List Rating — Design Spec

**Date:** 2026-09-13  
**Status:** Approved baseline (brainstorming complete)  
**Scope (phase 1):** `BE_Company` + `FE_Company` only

## Out of scope (phase 1)

- Flutter apps (including `sales_filter_application`)
- `BE_SalesEmployee` / Gateway
- `SalesRequests` / `ExistingCustomerId` / `SourceListId`
- Snapshot tables / rating migrations / cache
- Pages: `customers-archived`, `sales-list`, others

## Data sources (discovered)

| Concept | Source |
|--------|--------|
| Customer | `dbo.Customers` |
| Legal | `Customers.IsLegal` |
| Fake sale | `Customers.IsFakeSale` (does **not** change rating) |
| List | `Delegates.DelegateID` via `Customers.DelegateID` |
| Sale date | `DateSaleDevice` = `MAX(CustomersSales.DateCreate)` |
| Last payment | `LastPaymentDate` = `MAX(PaymentDate)` |
| Totals / remaining | Same rounding as `View_CustomersDelegate` (`ROUND(..., -3)`) |
| Settled | `AmountRemaining = 0` only |

## Customer rating rules

Priority order:

1. **`IsLegal = 1`** → `قانونية` / `-10` (absolute override)
2. **`AmountRemaining < 0`** → `ضعيف` / `0` — reason: `الباقي سالب ويحتاج مراجعة`
3. **No sale or `AmountTotalSales <= 0`** → `ضعيف` / `0` — reason: `لا توجد بيانات بيع كافية للتقييم`
4. **Settled (`AmountRemaining = 0`)** — days = `DATEDIFF(DAY, DateSaleDevice, LastPaymentDate) + 1`  
   - Missing dates that block calc → `ضعيف` / `0` — `بيانات التسديد غير مكتملة`  
   - `Days <= 140` → ممتاز / +10  
   - `Days <= 160` → جيد / +5  
   - `Days < 180` → ضعيف / 0  
   - `Days >= 180` → مرفوض / -5  
5. **Active (`AmountRemaining > 0`)** — days = `DATEDIFF(DAY, DateSaleDevice, Today) + 1`  
   - `Rate = ReceiptsTotal × 1000 / (AmountTotalSales × DaysSinceSale)`  
   - Incomplete calc → `ضعيف` / `0` — `بيانات التسديد غير مكتملة`  
   - `Rate >= 7.1` → ممتاز / +10  
   - `Rate >= 6.25` → جيد / +5  
   - `Rate >= 5.5` → ضعيف / 0  
   - `Rate < 5.5` → مرفوض / -5  

Every row in `dbo.Customers` must resolve to one of: ممتاز / جيد / ضعيف / مرفوض / قانونية.

## List rating rules

- Membership: **all** customers with `DelegateID = listId` (ignore `CustomerState`)
- Historical: all members
- Recent: members with `DateSaleDevice >= 2025-09-01`
- Metrics: counts per rating, `TotalPoints`, `AverageScore = TotalPoints / CustomerCount`, `LegalCount`, `LegalPercentage`, `RiskIndicator`, `FinalRating`
- Empty set: no division by zero; `CustomerCount = 0`, `AverageScore = 0`, `FinalRating = ضعيف`, `RiskIndicator = None`

### FinalRating from AverageScore

- `>= 7` ممتاز  
- `>= 3` جيد  
- `>= 0` ضعيف  
- `< 0` مرفوض  

### RiskIndicator from LegalPercentage

- `None`: 0%  
- `Low`: `> 0 && < 5`  
- `Medium`: `>= 5 && < 15`  
- `High`: `>= 15`  

Legal does **not** make the list FinalRating = قانونية.

## Architecture

**Hybrid compute (no snapshots phase 1):**

- Pure `CustomerRatingCalculator` (deterministic, unit-tested)
- SQL aggregation for list ratings (no N+1, no load-all-then-loop when SQL can aggregate)
- Customer detail: on-the-fly
- Table summaries: batch enrichment or post-fetch batch by IDs (no per-row HTTP)

### API (BE_Company)

- `POST /api/ratings/customers/summary` — body: customer IDs → summaries  
- `GET /api/ratings/customers/{customerId}` — full detail  
- `POST /api/ratings/lists/summary` — body: list/delegate IDs → historical+recent summaries  
- `GET /api/ratings/lists/{listId}` — full historical + recent detail  

Authorize like other company board endpoints (`[Authorize]`).

Optional later: short-lived cache with invalidation on payment/legal/sale/list-move — **not in phase 1**.

## FE_Company phase 1

1. `customers-list` — badge: Rating + Score + color  
2. `customer-profile` — full rating explanation  
3. `agents-list` — overall + recent AverageScore + RiskIndicator  

## Performance

- Prefer set-based SQL using same aggregates as `View_CustomersDelegate`  
- Indexes to verify/suggest (no destructive migration required phase 1):  
  - `Customers(DelegateID)`  
  - `CustomersSales(CustomerID, DateCreate)`  
  - `CustomersPayments(CustomerID, PaymentDate)` / payment views keys  

## Security

- Server-side `[Authorize]`  
- No cross-branch leakage beyond existing company connection model  
- Do not expose more accounting fields than needed for rating explanation on detail endpoint  

## Rollback

- Feature is additive (new endpoints + FE UI). Rollback = disable FE calls / remove routes; no schema changes in phase 1.
