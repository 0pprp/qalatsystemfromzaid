# Bulk Assign Unassigned + Panel Perf — Design

**Date:** 2026-09-17  
**Status:** Approved for implementation (user: approach 1, unassigned only, per-request assign, partial success report)

## Goal
Sales Manager can multi-select unassigned requests and assign them to one employee in one action. Each request stays a separate SalesRequest. Failed items report without blocking successes. Reduce panel load latency.

## Scope
- FE: `sales-manager-requests.vue` — unassigned tab checkboxes + sticky bulk bar
- BE_Company: `POST sales-manager/sales-requests/assign-batch`
- Perf: unread-count use lightweight count; evaluate non-blocking / deferred after list paints
- No Flutter / filter / exception workflow changes beyond existing hold rules (held rows fail individually)

## API
```
POST /api/sales-manager/sales-requests/assign-batch
{ employeeId, employeeName?, cityValue?, requestIds: number[] }
→ { succeeded: number[], failed: [{ id, message }], total }
```
- Cap 50 ids
- Per id: build AssignDTO from existing SalesRequest row fields + employee
- Call existing AssignAsync (same rules: hold, already assigned, etc.)
- Continue on failure

## Out of scope
- Merging requests into one
- Bulk on other tabs
- New employee creation
