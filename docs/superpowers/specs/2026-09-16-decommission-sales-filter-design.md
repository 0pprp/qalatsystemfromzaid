# Decommission Sales Filter — Design

**Date:** 2026-09-16  
**Status:** Approved (Approach A)  
**Constraint:** Do not delete filter app until data migration verified on each branch.

## Goal

Remove the filter stage entirely so workflow is:

**Create/assign → Sales app → طلبات البيع → Sales employee**

Migrate all existing filter-queue rows (all provinces; live data today: النجف، البصرة، الكرخ، الرصافة) into the sales-request path without data loss.

## Architecture

Filter is not a separate request store. It is a `FilterStatus` gate on `dbo.SalesRequests` plus `SalesFilterHistory` / `SalesFilterUserCities`.

1. **Data phase:** Idempotent SQL per branch DB maps every filter state into sales `Status` + preserves notes/reasons, then sets visibility for the sales employee inbox.
2. **Code phase:** Assignment no longer queues `PendingFilter`; employee list/detail no longer require `ReadyForSale`.
3. **Disable phase:** Filter APIs return gone/disabled when `SalesManagement:FilterAppEnabled=false`.
4. **Delete phase (ops):** After count verification, remove Flutter app and leftover filter code (separate change).

## Migration mapping

| FilterStatus (Arabic) | SalesRequest after migrate | Preserved fields |
|----------------------|----------------------------|------------------|
| PendingFilter (طلبات البيع) | Keep `Status` (usually Assigned); `FilterStatus=ReadyForSale` | All customer/assign fields |
| OnHold (معلق) | `Status=Assigned` (طلبات البيع tab); append `[تعليق فلترة]` + `FilterNote` into `Notes` if present; `FilterStatus=ReadyForSale` | FilterNote content |
| ReadyForSale (جاهز للبيع) | No change | As-is |
| Rejected (مرفوض) | `Status=Rejected`; `RejectionReason = COALESCE(RejectionReason, FilterRejectReason)`; `FilterStatus=ReadyForSale` (gate obsolete) | FilterRejectReason |

Scope: all rows with a filter state on every branch catalog (script is province-agnostic).

## New request path

- `AssignAsync` → `FilterStatus=ReadyForSale` (not PendingFilter).
- `ListForEmployeeAsync` → no filter-status filter (assigned employee rows).
- `RequireOwned` → no ReadyForSale visibility gate.
- Create may still stamp a default FilterStatus for schema compatibility until columns are dropped later.

## Disable / delete

- Config: `SalesManagement:FilterAppEnabled` default `false` → SalesFilter controllers return 410.
- Keep `sales_filter_application` in repo until ops confirms migration counts; then delete app + ACL UI + gateway routes in a follow-up.

## Impact

- **BE_Company:** SalesRequestService, SalesFilterStatuses/controllers, migration script
- **BE_SalesEmployee:** gateway SalesFilter proxy respects disable / 410
- **FE_Company:** filter-user ACL can remain until delete phase
- **Flutter:** sales_employee sees assigned work immediately; filter app unused
- **DB:** per-branch SalesRequests updates; history tables retained until delete phase

## Verification

- Pre/post counts by FilterStatus per branch
- New assign appears in employee طلبات البيع without filter action
- OnHold notes visible on migrated rows
- Filter reject reasons on Rejected rows
- Filter API disabled
- Tests updated for Assign → ReadyForSale and employee visibility
