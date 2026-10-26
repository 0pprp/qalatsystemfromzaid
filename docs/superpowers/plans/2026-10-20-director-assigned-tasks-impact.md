# Director Assigned Tasks — Impact / regression note

**Date:** 2026-10-20  
**Feature:** ClickUp-style personal tasks (المهام) — distinct from «مهام اللجنة» (`AppCommitteeTasks`).

## Impact record

| Surface | Impact |
|---------|--------|
| **BE_Company** | None |
| **BE_SalesEmployee** | New models, store (SQL + in-memory), file storage, service, 3 controllers; BM login now also issues gateway JWT for assigned-tasks; `SalesPolicies.BranchManager` |
| **FE_Company** | Page `/director-assigned-tasks`, nav «المهام», director-only allowlist |
| **delegated_manager_application** | Inbox tab «مهامي» |
| **branch_manager_application** | Inbox «مهامي» + stores gateway token from login |
| **DB** | Gateway SQL only: `dbo.DirectorAssignedTasks`, `DirectorAssignedTaskAttachments`, `DirectorAssignedTaskEvents` (auto `EnsureSchema`) |
| **Branch DBs** | None |
| **OTA** | Not published in this work |

## Deploy (only when explicitly requested)

1. Gateway: publish `BE_SalesEmployee` DLL + ensure `ConnectionStrings:SalesGateway`
2. FE: AdminFrontend build with director page
3. Install DM/BM APKs on devices (OTA only if asked)

## Regression checklist (automated)

- `dotnet test BE_SalesEmployee.Tests --filter FullyQualifiedName~DirectorAssignedTask` — service paths: BM direct complete, DM self-complete, transfer+confirm, reject/return, overdue, sort
- Flutter: `assigned_tasks_*` unit tests in DM and BM apps
- Committee tasks / announcements unchanged by design (separate tables and routes)
