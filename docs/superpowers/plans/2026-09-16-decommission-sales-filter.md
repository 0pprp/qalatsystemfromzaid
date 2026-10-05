# Decommission Sales Filter — Implementation Plan

> **For agentic workers:** Execute task-by-task. Do not delete `sales_filter_application` until migration verification.

**Goal:** Migrate filter-queue data into sales requests and restore direct assign → employee visibility.

**Architecture:** SQL backfill all FilterStatus rows → code bypass gate → disable filter APIs via config.

**Tech Stack:** SQL Server branch DBs, BE_Company SalesRequestService, BE_SalesEmployee gateway, xUnit.

## Global Constraints

- No Production apply/deploy/push unless explicitly ordered later.
- Do not drop Filter* columns or delete Flutter filter app in this change set.
- Migration must be idempotent and safe to re-run.

---

### Task 1: Migration SQL

- [ ] Add `DatabaseScripts/Migrations/20260916_DecommissionSalesFilter_MigrateToSalesRequests.sql`
- [ ] Map PendingFilter / OnHold / Rejected / leave ReadyForSale as designed
- [ ] Include verification SELECT counts

### Task 2: Backend new path (TDD)

- [ ] Update failing expectations: Assign → ReadyForSale; PendingFilter visible to employee after gate removal
- [ ] Implement Assign / ListForEmployee / RequireOwned / IsVisibleToSalesEmployee
- [ ] Pass SalesFilterTests + Sales request tests

### Task 3: Disable filter APIs

- [ ] Config flag `SalesManagement:FilterAppEnabled` (default false)
- [ ] BE_Company SalesFilterController + BE_SalesEmployee SalesFilterController return 410 when disabled

### Task 4: Docs + report

- [ ] Spec already written; note ops run order for Demo branches
- [ ] Do not delete Flutter app yet
