# DatabaseScripts & deploy index (F22/ops)

Purpose: map scripts without deleting history. **Do not run write scripts on production from this index.**

## Migrations (ordered — apply on isolated branch DB)

| File | Purpose | Env | R/W | Idempotent | Depends on |
|---|---|---|---|---|---|
| `Migrations/20261005_SchemaMigrations_Ledger.sql` | `__SchemaMigrations` table | all branches | W | yes (IF NOT EXISTS) | none |
| `Migrations/20261005_View_WeekFinal_SumTodayInclusive.sql` | Week SUM + today inclusive | branch | W | CREATE OR ALTER | View deps |
| `Migrations/20261005_CustomersSales_Create_ScopeIdentity_Txn.sql` | F07 sales create | branch | W | CREATE OR ALTER | temp item SPs |
| `Migrations/20261005_Buys_Create_ScopeIdentity_Txn.sql` | F07 buys | branch | W | CREATE OR ALTER | buy temp SPs |
| `Migrations/20261005_CustomersPaymentsApprove_Create_ScopeIdentity_Txn.sql` | F07 approve | branch | W | CREATE OR ALTER | payments/box |
| `Migrations/20261005_GetCustomersDelegateAll_ExplicitColumns.sql` | F18 columns | branch | W | CREATE OR ALTER | View_Customers |
| `Migrations/20261005_Collection_Reconciliation_Queries.sql` | Read-only helpers | isolated | R | n/a | request tables |

After each apply: `INSERT INTO dbo.__SchemaMigrations (MigrationId, Checksum, Notes) …`

## deploy/ folder

~688 files / ~167MB including snapshots and diagnostics. Treat as **historical ops archive**.

Rules:
1. Do not delete until ownership + purpose documented.
2. Never commit live customer PII or secrets into new artifacts.
3. Prefer Migrations/ + StoredProcedures/ as source of truth for schema going forward.
4. Deploy scripts that write money require dual control + reconciliation plan.

## Readiness

- API up ≠ posting ready → `GET /api/CollectionPostingReadiness`
- Schema mismatch → fail release / readiness, do not soft-warn forever
