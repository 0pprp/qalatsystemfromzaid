# Sales Request Name Transfer — Design (executed)

**Date:** 2026-09-13

## Chosen design
Ownership lives on `SalesRequests.TargetEmployeeId`. Transfer **updates the same row** (no duplicate request/customer). Visibility still requires `FilterStatus = ReadyForSale`. After transfer: status → `Assigned`, `ViewedAtUtc` cleared, filter stays ReadyForSale so the peer sees it under «طلبات البيع».

Audit: `dbo.SalesRequestNameTransfers` (append-only) + `SalesRequestHistory` event `NameTransferred`.

## Security
JWT identity only; owner-only; active `موظف مبيعات` peers in branch DB (`ISNULL(UserState,1)=1`); no self-transfer; CityValue mismatch rejected; Completed/Rejected/ConvertedToSale/Inspected blocked; optimistic `UPDLOCK` update.
