# Financial integrity remediation (F07–F12, F21, F25)

Last updated: 2026-10-05. **No production scripts executed. No publish.**

## Impact analysis

| Surface | Impact |
|---|---|
| `BE_Company` | CustomersSales_Create SCOPE_IDENTITY+txn; SalesComplete/Purchase no IDENT_CURRENT fallback; Buys + PaymentsApprove migrations |
| `BE_DelegateWebApplication` | Collection posting readiness (`GET /api/CollectionPostingReadiness`); idempotent intake unchanged |
| `BE_SalesEmployee` | SQL MF stores (committees/reviews/shifts); Production fails closed without SalesGateway; SumCountAsync completeness metadata |
| `delegate_application` | Receipt week SUM+today; payment AwaitingSession; legacy-local ClientPaymentId; ownership guard |
| `delegate_application_aqeel` | Fetch-then-replace sync; logout wipe blocked; payment send never deletes on error; receipt 7-day helper; ClientPaymentId |
| Database | Migration scripts under `DatabaseScripts/Migrations/` + `__SchemaMigrations` ledger — **apply only on isolated branch DBs** |

## End-to-end payment cycle (documented bounds)

1. Device insert `CustomerPayment` with stable `ClientPaymentId` (UUID or `legacy-local-{id}`).
2. HTTP `PostPaymentIdempotent` → `CustomersPaymentsRequest` unique on ClientPaymentId.
3. Eligibility window then `CustomersPaymentsRequest_PostEligible` → official `CustomersPayments` + box.
4. Retry after accept: same ClientPaymentId → AlreadyExists (one financial effect).
5. `SESSION_EXPIRED` → `AwaitingSession` (retry after login), not PermanentFailure; OwnerAsyncId ownership preserved.
6. Network after server accept → client may retry; server idempotency prevents double box credit.

Mid-flight failure on CustomersSales_Create rolls back customer+sale+stock withdrawal (single SP transaction).

## Currency / rounding (F25) — observe only

| Rule | Status |
|---|---|
| IQD display: whole dinars, comma thousands (`ReceiptWeekAmounts.formatExactIqd`) | Documented; no change to FLOAT columns |
| Discount ÷ 1448 on CustomersSales_Create path | **Historical factor — do not change without balance reconciliation** |
| FLOAT money columns in classic schema | Isolated; gradual DECIMAL migration requires matching ops + inventory of balances |
| Business day Baghdad 03:00 / sync gate 16:00 | Documented in `IraqDate` |

**Open policy ask:** Confirm whether ÷1448 is still required for new sales or only legacy discount parity. Until answered, code keeps current behavior.

## Migrations tracking (F21)

- Ledger table script: `DatabaseScripts/Migrations/20261005_SchemaMigrations_Ledger.sql`
- Pair each `CREATE OR ALTER` apply with INSERT into `__SchemaMigrations` (MigrationId + checksum)
- Schema changes must not ship via ad-hoc request handlers

## Collection readiness alerts (F21)

- `GET /api/CollectionPostingReadiness` → 200 only when PostEligible proc exists and stuck queue = 0; else 503 with reason
- Monitor `stuckOlderThan6h` and `oldestEligibleAgeMinutes`

## Rollback plan

1. Do not delete pending `CustomerPayment` / `CustomersPaymentsRequest` rows to “fix” sync.
2. Revert app binaries to prior build; keep SQLite ClientPaymentId columns (additive).
3. SQL SP rollback: restore prior `CREATE OR ALTER` from backup only after verifying no mid-txn identity mismatch reports.
4. Gateway: if SalesGateway SQL unavailable, Production refuses InMemory (fail closed) — restore connection string, not memory fallback.

## Test results (this pass)

| Suite | Result |
|---|---|
| Flutter `receipt_week_amounts_test` (main) | **6 passed** |
| Flutter `receipt_week_amounts_test` (aqeel) | **6 passed** |
| Collection* + readiness contract (`dotnet test --filter Collection`) | **31 passed** |
| Backend builds (Company / SalesEmployee / Delegate) | **succeeded** |
| Isolated SQL concurrency for CustomersSales_Create | **Not executed** — no isolated SQL Server here; migration scripts ready |
| Production balances | **Not touched** |

## Findings status

| ID | Status |
|---|---|
| F07 | Code + migration scripts; apply on isolated DB before prod |
| F08 | SQL MF + Production fail-closed without gateway |
| F09 | aqeel sync/logout/payment wipe fixed |
| F10/F11 | Main app + aqeel ClientPaymentId / session / legacy migration |
| F12 | SumCountAsync returns complete/partial/failedBranches |
| F21 | Ledger + readiness endpoint |
| F25 | Documented; no silent FLOAT/1448 change |
