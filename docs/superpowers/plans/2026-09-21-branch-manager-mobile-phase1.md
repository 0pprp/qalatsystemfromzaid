# Branch Manager Mobile Phase 1 — Implementation Plan

> **For agentic workers:** Implement task-by-task. Checkboxes for tracking.

**Goal:** Ship Flutter app «مدير الفرع» Phase 1: secure login (Users from المحاسب الرئيسي), weak-payers decisions, decision history, in-app updates, polished RTL UI — testable on Najaf demo.

**Architecture:** New `branch_manager_application` Flutter app. Login via city BE `Users/Users_LoginAdmin` (UserType must be مدير فرع). JWT Bearer + LinkCity like FE_Company. Reuse `CustomerDecisions` APIs. Copy update-gate pattern from follower with appKey `branch-manager`.

**Tech Stack:** Flutter 3.x, http, shared_preferences, flutter_secure_storage (token), package_info_plus, Cairo font, Material 3.

## Global Constraints
- Phase 1 only: login + decisions + history + updates
- Reject login if UserType ≠ مدير فرع
- Never log passwords/tokens
- RTL Arabic UI

---

### Task 1: Scaffold Flutter app
- [ ] Create `branch_manager_application` with pubspec, Android package `com.qalaat.branch_manager`, Cairo fonts, themes
- [ ] Config `app_env.dart` (demo Najaf URL)

### Task 2: Auth session
- [ ] Secure token storage + Session model
- [ ] Login page: city picker + username/password → Users_LoginAdmin
- [ ] Guard: only مدير فرع

### Task 3: Decisions API client
- [ ] GET WeakWeekPayers, POST Decide, GET decisions history (existing endpoints)
- [ ] Decision board UI + history UI

### Task 4: Update gate
- [ ] AppUpdateGate + publish key `branch-manager` when ready

### Task 5: Najaf demo smoke
- [ ] Build APK; login BM; decide one case if data exists
