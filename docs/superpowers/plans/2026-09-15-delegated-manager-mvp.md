# Delegated Manager MVP — Implementation Plan

> **For agentic workers:** Complete tasks in order. Each task ends with verification.

**Goal:** Runnable DEMO+PRODUCTION Flutter app + gateway APIs + FE Sales Manager exception UX + docs/tests. No deploy/push/prod DB.

**Tech:** BE_SalesEmployee (+ Dapper), FE_Company Vue, new Flutter app, idempotent SQL under `DatabaseScripts/Migrations/`.

---

### Task 1: Gateway role + auth + connection

**Files:** `SalesRoles.cs`, `TokenService.cs`, `AuthController.cs`, `Program.cs`, `DelegatedManagerAccountService.cs`, `appsettings*.json`  
**Verify:** unit tests for role mapping + login policy.

### Task 2: Migrations

**File:** `DatabaseScripts/Migrations/20260915_DelegatedManager_CentralInbox_Exceptions_MobileUpdates.sql`  
Tables: CentralComplaints, SalesExceptionRequests, SalesExceptionAudit, MobileAppReleases + indexes/checks.

### Task 3: Complaints APIs + tests

Repositories/services/controllers: intake + DM inbox + mark-read.  
**Verify:** `dotnet test` authz, pagination, filters, mark-read.

### Task 4: Exception APIs + tests

Create (SalesManager), list/detail/decide (DelegatedManager), 409 on double decide, note on approve via branch proxy.  
**Verify:** state machine + concurrency tests.

### Task 5: Mobile updates API + tests

### Task 6: FE_Company exception UX

`sales-manager-requests.vue` + `salesManagerApi.js` + nav if needed.

### Task 7: Flutter app scaffold + AppEnv/tests

### Task 8: Flutter features (auth, dashboard, complaints, exceptions, updater)

### Task 9: `docs/delegated-manager-api.md` + Swagger coverage

### Task 10: Verification loop — analyze/test/build report (no push)
