# Director Assigned Tasks (ClickUp-style) — Design

**Date:** 2026-10-20  
**Status:** Approved for planning (build plan only; not started)  
**Related:** Distinct from «مهام اللجنة» (broadcast text to followers).

## Goal

Director (حيدر) creates personal assigned tasks with title, due date, priority, assignee, and attachments. Assignees accept/reject; delegated manager (أيمن) may transfer to branch management. Completion paths differ by assignment route. Director sees timeline columns (date/time 12h AM/PM, transferor/transferee names and roles, completion times).

## Decisions (locked)

| Topic | Decision |
|-------|----------|
| Storage | Central Sales Gateway SQL (`ConnectionStrings:SalesGateway`), same pattern as announcements / committee tasks / BM users |
| Creator | Director (`المدير`) only on FE_Company |
| Assignees (phase 1) | Delegated manager **or** a specific Branch Manager app user (يوزر إدارة الفرع) |
| DM inbox | `delegated_manager_application` only |
| BM inbox | `branch_manager_application` only (not web) |
| Priority | `عالية` / `متوسطة` / `ضعيفة` — sort high → medium → low, then sooner due date |
| Overdue | Badge «تجاوزت المدة»; task stays active |
| Reject | Status `معادة` to same person; rejection note required; director can update title/due/priority/assignee/attachments |
| Attachments | Multiple files: image, PDF, Word, Excel — stored on gateway disk |
| Direct BM assign | BM «تم الإنجاز» → visible to director immediately (no DM confirm) |
| Via Ayman, self-complete | One «تم الإنجاز» → director |
| Via Ayman, transferred | BM completes → Ayman confirms «تم الإنجاز» → director; log transfer + both completion times |
| Time display | Date + time only, 12-hour صباح/مساء; separate columns |
| Out of scope v1 | Comments thread, subtasks, ClickUp full parity, monitor/sales-manager creators, OTA unless requested |

## Actors

| Role | App | Actions |
|------|-----|---------|
| المدير (حيدر) | FE_Company | Create, assign, attach, edit/return (`معادة`), view board + timeline |
| المدير المفوض (أيمن) | delegated_manager_application | Accept/reject, transfer to BM user, self-complete, confirm after BM complete |
| إدارة الفرع | branch_manager_application | Accept/reject, complete |

## Status machine

```
مرسلة
  ├─(قبول)→ مقبولة
  │           ├─(تم الإنجاز — مسار مباشر أو ذاتي)→ منجزة
  │           └─(تم الإنجاز — بعد نقل من المفوض)→ بانتظار_تصديق_المفوض
  │                                               └─(تصديق المفوض)→ منجزة
  └─(رفض + ملاحظة)→ معادة  → (تحديث بيانات + إعادة إرسال)→ مرسلة
```

Optional terminal: director may leave `معادة` until re-sent. Overdue flag is computed (`DueDate < Iraq today`) and does not change status by itself.

## Completion rules (authoritative)

1. **AssigneeRole = BranchManager** (direct from director): on BM complete → `منجزة`; record `BranchCompletedAtUtc`.
2. **AssigneeRole = DelegatedManager** and **no transfer**: on DM complete → `منجزة`; record `DelegatedCompletedAtUtc`.
3. **AssigneeRole = DelegatedManager** and **transfer exists**: on BM complete → `بانتظار_تصديق_المفوض` + `BranchCompletedAtUtc`; on DM confirm → `منجزة` + `DelegatedConfirmedAtUtc`.

## Data model (gateway SQL)

### `dbo.DirectorAssignedTasks`

| Column | Notes |
|--------|--------|
| Id | UNIQUEIDENTIFIER PK |
| Title | nvarchar |
| DueDate | date (Iraq calendar day) |
| Priority | tinyint: 1=عالية, 2=متوسطة, 3=ضعيفة |
| Status | nvarchar: مرسلة / مقبولة / معادة / بانتظار_تصديق_المفوض / منجزة |
| CreatedByUserId / CreatedByUserName | director |
| AssigneeKind | `DelegatedManager` \| `BranchManager` |
| AssigneeKey | stable key: DM identity or `BranchManagerAppUsers.Id` |
| AssigneeDisplayName | denormalized |
| AssigneeCityValue | optional for BM (province) |
| RejectNote | last reject note |
| CurrentHolderKind / CurrentHolderKey | who must act now (after transfer may differ from original assignee) |
| CreatedAtUtc, UpdatedAtUtc | |
| BranchCompletedAtUtc, DelegatedCompletedAtUtc, DelegatedConfirmedAtUtc | nullable |

### `dbo.DirectorAssignedTaskAttachments`

Id, TaskId, FileName, StoredFileName, ContentType, SizeBytes, UploadedAtUtc, UploadedBy

### `dbo.DirectorAssignedTaskEvents`

Audit/timeline rows: `Created`, `Accepted`, `Rejected`, `Returned`, `Updated`, `Transferred`, `Completed`, `Confirmed`  
Columns: Id, TaskId, EventType, AtUtc, ActorKind, ActorKey, ActorDisplayName, ActorRoleLabel, TargetKind, TargetKey, TargetDisplayName, TargetRoleLabel, Note

Transfer event fills actor = الناقل, target = المنقول إليه — these drive director table columns.

## API (SalesGateway / BE_SalesEmployee)

### Director (`api/sales-manager/assigned-tasks`) — policy: Director only

- `GET /` — list (sort priority then due); include overdue flag, current holder, last transfer summary
- `POST /` — create (multipart or JSON + separate uploads)
- `PUT /{id}` — update fields when `معادة` or before accept (title, due, priority, assignee, clear/replace attachments rules TBD in impl)
- `POST /{id}/attachments` — upload
- `GET /{id}/attachments/{fileId}` — download
- `GET /assignee-options` — delegated manager identity + BM users list (from `BranchManagerAppUsers`)

### Delegated manager (`api/delegated-manager/assigned-tasks`)

- `GET /inbox`
- `POST /{id}/accept` | `reject` (body note required)
- `POST /{id}/transfer` — body: BM user id (+ city if needed)
- `POST /{id}/complete` — self-complete **or** confirm when status is `بانتظار_تصديق_المفوض`

### Branch manager (`api/branch-manager/assigned-tasks`)

- `GET /inbox` — only tasks where current holder is this BM user
- `POST /{id}/accept` | `reject`
- `POST /{id}/complete`

Auth: reuse existing DM / BM token policies.

## FE (director)

- New nav item «المهام» visible to `المدير` only (same allowlist pattern as الإعلانات / مهام اللجنة).
- Board: cards or table sorted by priority; badge تجاوزت المدة; filters by status.
- Create/edit dialog: title, due date, priority, assignee picker (مفوض / مستخدم فرع), multi-file attach.
- Detail: timeline table — تاريخ | وقت (ص/م) | الناقل | دور الناقل | المنقول | دور المنقول | ملاحظة؛ plus completion timestamps for branch and Ayman.

## Mobile

- **DM app:** tab/inbox «مهامي» — sort by priority; actions accept/reject/transfer/complete/confirm; show attachments.
- **BM app:** same inbox pattern scoped to assignee; no transfer action.

## Impact

| Surface | Impact |
|---------|--------|
| BE_Company | None (gateway-central) |
| BE_SalesEmployee | New stores, services, controllers |
| FE_Company | New page + nav |
| delegated_manager_application | Inbox UI + API |
| branch_manager_application | Inbox UI + API |
| DB | Gateway SQL tables only |
| Flutter OTA | Not in this plan unless explicitly requested later |

## Non-goals

- Replacing مهام اللجنة
- Push notifications v1 (optional later)
- Full ClickUp (comments, subtasks, boards multi-column drag)
