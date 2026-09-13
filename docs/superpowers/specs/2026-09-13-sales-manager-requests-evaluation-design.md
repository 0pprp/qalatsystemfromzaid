# Sales Manager Requests — Automatic Evaluation + CRUD

Date: 2026-09-13
Updated: 2026-09-13 (production blockers)

## Locked decisions
- Reuse CustomerRatingCalculator scores (+10/+5/0/-5/-10). Do not change rating algorithm.
- Evaluation is on-the-fly (no derived rating table).
- Soft delete: IsDeleted + audit columns (idempotent EnsureSchema + Migration SQL).
- Soft delete / province transfer blocked when Completed or ConvertedToSaleId set.
- Excel search UI removed; backend Excel search kept for reuse.

## ReceiptCount
- Source: `COUNT(CustomerPaymentID)` on `dbo.View_CustomersPaymentsDelegate` via OUTER APPLY in `RatingDataSource` (same view as receipt sums).
- Aggregation is per CustomerID — join duplication avoided by OUTER APPLY aggregate, not row joins.
- Carried through `CustomerRatingFacts` → `CustomerRatingResult` → evaluation hits.

## Cross-branch evaluation
- FE posts payload items (requestId, sourceCityValue, name, phone) to gateway `POST sales-requests/evaluate`.
- Gateway fans out to all `GetSalesBranchesAsync` targets (ACL), merges summaries by `key=sourceCity:requestId`.
- Worst score wins; ResultCount sums profiles across branches.
- Hits: `POST sales-requests/evaluation-hits` fan-out; dedupe `cityValue:customerId`.
- Per-city evaluate/hits endpoints retained for backward compatibility.

## Province transfer
- Not metadata-only. Gateway `POST sales-requests/{fromCity}/{id}/transfer-province`.
- Flow: create destination (`accept-province-transfer`) → archive source (`mark-province-transferred-out` soft-delete).
- No distributed transaction; compensation: if archive fails after create, return 502 with both IDs (source kept).
- Assignee cleared unless destination employee provided.
- FilterStatus on destination = PendingFilter.
- Audit event: `ProvinceTransferred` with FromCity/ToCity/OriginalRequestId/DestinationRequestId.
