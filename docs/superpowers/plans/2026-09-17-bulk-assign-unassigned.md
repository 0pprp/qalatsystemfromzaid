# Bulk Assign Unassigned — Implementation Plan

> **For agentic workers:** Implement task-by-task. TDD for BE batch.

**Goal:** Multi-select unassigned → assign-batch to one employee; lighten SM panel load.

**Architecture:** Reuse AssignAsync per id; new batch DTO/endpoint; FE checkboxes on unassigned only.

**Tech Stack:** BE_Company ASP.NET, FE_Company Vue, xUnit

## Global Constraints
- Unassigned tab only; each request remains separate
- Partial success + failure report
- Cap 50; held Pending/Rejected fail individually
- No Flutter / filter app changes
- No git push unless asked

### Task 1: BE AssignBatch + tests
### Task 2: Controller endpoint + unread-count fix
### Task 3: FE multi-select + bulk bar + report
### Task 4: FE load() defer evaluations
### Task 5: Run regression tests
