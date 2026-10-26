# Director Assigned Tasks — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.  
> **Do not start until the user explicitly asks to implement.**

**Goal:** Ship ClickUp-style personal tasks from director web → delegated manager app and/or branch manager app, with accept/reject, transfer, dual completion paths, attachments, and a director timeline.

**Architecture:** Central tables on Sales Gateway SQL (`dbo.DirectorAssignedTasks` + attachments + events). All traffic via `BE_SalesEmployee` (director / delegated-manager / branch-manager routes). FE_Company director page; Flutter inboxes in `delegated_manager_application` and `branch_manager_application`. Domain rules live in a pure service testable without SQL.

**Tech Stack:** .NET (BE_SalesEmployee), Dapper + SqlGatewayConnectionFactory, Vue FE_Company, Flutter (DM + BM apps), gateway disk file storage (mirror announcements pattern).

**Spec:** [`docs/superpowers/specs/2026-10-20-director-assigned-tasks-design.md`](../specs/2026-10-20-director-assigned-tasks-design.md)

## Global Constraints

- Creator = `المدير` only (not monitor / sales manager).
- BM inbox = branch_manager app only; DM inbox = delegated_manager app only.
- Priority order: عالية → متوسطة → ضعيفة, then sooner DueDate.
- Overdue badge does not auto-close the task.
- Reject → `معادة` + mandatory note; director may update fields then re-send to `مرسلة`.
- Completion paths exactly as spec (direct BM vs Ayman self vs Ayman+transfer).
- Times: store UTC; display Iraq local 12-hour صباح/مساء in UI.
- No BE_Company / branch DB changes.
- No OTA publish unless user requests.
- Do not confuse with `AppCommitteeTasks` / مهام اللجنة.

## File map (planned)

| Area | Create / Modify |
|------|-----------------|
| Models | `BE_SalesEmployee/DelegatedManager/Models/DirectorAssignedTask*.cs` |
| Store | `.../Stores/IDirectorAssignedTaskStore.cs`, `SqlDirectorAssignedTaskStore.cs`, `InMemoryDirectorAssignedTaskStore.cs` |
| Files | `.../Services/DirectorAssignedTaskFileStorage.cs` (like announcements) |
| Domain | `.../Services/DirectorAssignedTaskRules.cs` + workflow service |
| API | Controllers under sales-manager / delegated-manager / branch-manager |
| DI | `BE_SalesEmployee/Program.cs` |
| Tests | `BE_SalesEmployee.Tests/DirectorAssignedTask*Tests.cs` |
| FE | `FE_Company/src/pages/director-assigned-tasks.vue`, nav + router allowlist, `directorApi.js` |
| DM Flutter | features/assigned_tasks/* + shell tab |
| BM Flutter | ui/assigned_tasks_* + home shell entry |

---

### Task 1: Domain rules + status machine (TDD)

**Files:**
- Create: `BE_SalesEmployee/DelegatedManager/Services/DirectorAssignedTaskRules.cs`
- Create: `BE_SalesEmployee.Tests/DirectorAssignedTaskRulesTests.cs`

**Produces:** Pure functions for sort order, overdue flag, legal transitions (`Accept`, `Reject`, `Transfer`, `Complete`, `Confirm`), and which completion path applies.

- [ ] **Step 1:** Write failing tests for: priority sort; overdue; reject requires note; BM direct complete → منجزة; DM self complete → منجزة; DM transfer then BM complete → بانتظار_تصديق_المفوض; DM confirm → منجزة; illegal confirm without transfer fails.
- [ ] **Step 2:** Run tests — expect FAIL.
- [ ] **Step 3:** Implement minimal `DirectorAssignedTaskRules`.
- [ ] **Step 4:** Run tests — expect PASS.
- [ ] **Step 5:** Commit (only when user asked to implement and commit).

---

### Task 2: SQL store + in-memory store

**Files:**
- Create: models + `IDirectorAssignedTaskStore`
- Create: `SqlDirectorAssignedTaskStore` with `EnsureSchema` for three tables
- Create: `InMemoryDirectorAssignedTaskStore`
- Modify: `Program.cs` DI (SQL when gateway CS configured, else in-memory)

**Produces:** CRUD task, list sorted, add event, add attachment metadata, query inbox by holder.

- [ ] **Step 1:** Tests against InMemory: create → list order; append Transfer event; inbox filter by CurrentHolderKey.
- [ ] **Step 2:** Implement InMemory + interface.
- [ ] **Step 3:** Implement Sql store (`CREATE TABLE IF NOT EXISTS` pattern from `SqlAppAnnouncementStore`).
- [ ] **Step 4:** Register DI.
- [ ] **Step 5:** Commit when allowed.

---

### Task 3: File storage for attachments

**Files:**
- Create: `DirectorAssignedTaskFileStorage.cs`
- Test: content-type allow-list (image/*, pdf, doc/docx, xls/xlsx); reject others

- [ ] **Step 1:** Failing tests for allow/deny extensions and safe stored file names.
- [ ] **Step 2:** Implement save under gateway data dir (e.g. `assigned-tasks/{taskId}/...`).
- [ ] **Step 3:** Pass tests.

---

### Task 4: Application service (orchestration)

**Files:**
- Create: `DirectorAssignedTaskService.cs`

**Produces:** `CreateAsync`, `UpdateReturnedAsync`, `AcceptAsync`, `RejectAsync`, `TransferAsync`, `CompleteAsync`, `ConfirmAsync`, `ListForDirectorAsync`, `InboxForDmAsync`, `InboxForBmAsync`, attachment upload/download.

- [ ] **Step 1:** Service tests with InMemory store covering all three completion paths + reject/return/update.
- [ ] **Step 2:** Implement service calling Rules + Store + FileStorage; write Events rows for timeline.
- [ ] **Step 3:** Pass tests.

---

### Task 5: HTTP controllers + auth

**Files:**
- Create: `Controllers/SalesManagerAssignedTasksController.cs` — Director policy only
- Create: `Controllers/DelegatedManagerAssignedTasksController.cs`
- Create: `Controllers/BranchManagerAssignedTasksController.cs`
- Tests: auth/policy smoke if existing test harness allows; else manual smoke checklist

**Routes (lock names):**
- `api/sales-manager/assigned-tasks`
- `api/delegated-manager/assigned-tasks`
- `api/branch-manager/assigned-tasks`

- [ ] **Step 1:** Wire endpoints to service; assignee-options from DM account + `IBranchManagerAppUserStore`.
- [ ] **Step 2:** Ensure BM/DM can only mutate tasks they currently hold.
- [ ] **Step 3:** Manual/API smoke on demo gateway.

---

### Task 6: FE director board

**Files:**
- Create: `FE_Company/src/pages/director-assigned-tasks.vue`
- Modify: vertical/horizontal nav — title `المهام`, director-only
- Modify: router allowlist + `DefaultLayoutWith*Nav.vue` filters (same pattern as الإعلانات)
- Modify: `composables/directorApi.js` — assigned-tasks helpers

**UI:**
- Sorted list/cards; overdue badge; create dialog; detail with timeline columns (تاريخ، وقت ص/م، الناقل، دور الناقل، المنقول، دور المنقول، ملاحظة) + completion times.
- Multi-file upload.

- [ ] **Step 1:** Page + API client against gateway.
- [ ] **Step 2:** Nav/router gating for `المدير` only.
- [ ] **Step 3:** Manual UI check (create → see in list).

---

### Task 7: Delegated manager Flutter inbox

**Files:**
- Create: `delegated_manager_application/lib/features/assigned_tasks/...`
- Modify: home shell / tabs to add «مهامي»

- [ ] **Step 1:** Repository + models parsing gateway JSON.
- [ ] **Step 2:** Inbox UI sorted by priority; accept/reject (note dialog); transfer picker (BM users API); complete/confirm.
- [ ] **Step 3:** Attachment open/download.
- [ ] **Step 4:** Widget/unit tests for sorting + action visibility by status.

---

### Task 8: Branch manager Flutter inbox

**Files:**
- Create: `branch_manager_application/lib/...` assigned tasks feature
- Modify: `home_shell.dart` entry

- [ ] **Step 1:** Repository scoped to current BM user.
- [ ] **Step 2:** Inbox: accept/reject/complete only (no transfer).
- [ ] **Step 3:** Tests for action gating.

---

### Task 9: End-to-end verification (no prod deploy unless asked)

**Checklist:**
- [ ] Director → BM direct → BM accept → complete → director sees منجزة + BranchCompleted time only.
- [ ] Director → Ayman → accept → complete → director sees DelegatedCompleted time.
- [ ] Director → Ayman → transfer BM → BM complete → status بانتظار تصديق → Ayman confirm → director timeline shows transfer row + both times.
- [ ] Reject → معادة + note → director updates due/priority → re-send → مرسلة.
- [ ] Overdue task still active with badge.
- [ ] Sort: عالية before متوسطة before ضعيفة.

**Deploy (separate explicit user ask):**
- Gateway DLL + FE AdminFrontend; install DM/BM APKs on devices (OTA only if requested).

---

### Task 10: Docs / regression note

- [ ] Short note in deploy script or README snippet: gateway-only feature; committee tasks unchanged.
- [ ] Impact record: BE_Company none; FE_Company director page; Flutter DM+BM; Gateway DB tables.

---

## Out of order / do not do

- Do not add monitor as creator in v1.
- Do not put task tables in branch `DatabaseCompany*`.
- Do not reuse `AppCommitteeTasks` rows for this feature.
- Do not implement until user says to execute this plan.
