# Main Follower Committees — Phase 1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Central committee + mission APIs for المدير المفوض, main-follower JWT login, and a Flutter app that shows assigned multi-province missions.

**Architecture:** Committee/mission data lives in gateway SQL (BE_SalesEmployee). DM creates committees (2 main followers) and missions (1+ cities). Main followers authenticate centrally and read only their active missions. Clean Architecture Flutter layers: domain → data → presentation.

**Tech Stack:** .NET 8 (`BE_SalesEmployee`), Flutter 3.5+, xUnit, flutter_test, Cairo RTL.

**Spec:** `docs/superpowers/specs/2026-09-22-main-follower-committees-design.md`

## Global Constraints

- Phase 1 only: no sale ratings, no staff ratings.
- Do not break existing DelegatedManager / SalesManager login.
- Central DB only for committees (gateway connection string).
- Arabic UI strings for Flutter.
- TDD: failing test → implement → green before next task.

---

## File map (Phase 1)

### BE_SalesEmployee (new)
- `MainFollower/Domain/CommitteeModels.cs` — entities + status enums
- `MainFollower/Domain/CommitteeRules.cs` — min 2 members, ≥1 city on mission
- `MainFollower/Stores/ICommitteeStore.cs` + `SqlCommitteeStore.cs` + `InMemoryCommitteeStore.cs`
- `MainFollower/Services/CommitteeService.cs`
- `MainFollower/Services/MainFollowerAccountService.cs` — config-based accounts (like DM)
- `Controllers/MainFollowerController.cs` — member APIs
- `Controllers/DelegatedManagerCommitteesController.cs` — DM APIs
- Extend `TokenService` + auth policies for `متابع رئيسي` / `MainFollower`
- `Program.cs` DI registration

### Tests
- `BE_SalesEmployee.Tests/CommitteeRulesTests.cs`
- `BE_SalesEmployee.Tests/CommitteeServiceTests.cs`
- `BE_SalesEmployee.Tests/MainFollowerAuthTests.cs`

### Flutter `main_follower_application/` (new)
- Clean Architecture folders: `lib/domain`, `lib/data`, `lib/presentation`, `lib/application`
- Login + missions list + mission detail (cities)
- Tests: domain parsing / session

### DM app (minimal)
- `delegated_manager_application`: screen or section to create committee + mission (can be simple form hitting new APIs)

---

### Task 1: Committee domain rules (TDD)

**Files:**
- Create: `BE_SalesEmployee/MainFollower/Domain/CommitteeRules.cs`
- Create: `BE_SalesEmployee.Tests/CommitteeRulesTests.cs`

- [ ] **Step 1:** Write failing tests: committee needs ≥2 distinct member user ids; mission needs ≥1 cityValue; empty note allowed.
- [ ] **Step 2:** Run tests — expect fail.
- [ ] **Step 3:** Implement `CommitteeRules` minimal.
- [ ] **Step 4:** Run tests — expect pass.

### Task 2: In-memory store + CommitteeService (TDD)

**Files:**
- Create domain models, `ICommitteeStore`, `InMemoryCommitteeStore`, `CommitteeService`
- Create `CommitteeServiceTests.cs`

- [ ] **Step 1:** Failing tests: DM creates committee with 2 members; creates mission with cities; member lists only own active missions; non-member gets empty.
- [ ] **Step 2:** Implement service + in-memory store.
- [ ] **Step 3:** Green tests.

### Task 3: Auth + HTTP controllers

**Files:**
- `MainFollowerAccountService`, Token claim, policies, controllers
- `MainFollowerAuthTests.cs`

- [ ] **Step 1:** Test account authenticate from config section `MainFollowerAccounts`.
- [ ] **Step 2:** Wire `POST Auth/LoginMainFollower`, DM committee endpoints, member `GET missions`.
- [ ] **Step 3:** Register DI + policies in `Program.cs`.
- [ ] **Step 4:** Green auth/service tests.

### Task 4: SQL store schema

**Files:**
- `SqlCommitteeStore.cs` with EnsureSchema (Committees, CommitteeMembers, Missions, MissionCities)

- [ ] **Step 1:** EnsureSchema idempotent SQL.
- [ ] **Step 2:** Swap DI to SQL when connection configured; keep InMemory for tests.
- [ ] **Step 3:** Smoke: create committee via service against InMemory still green.

### Task 5: Flutter app scaffold (Clean Architecture)

**Files:**
- New `main_follower_application/`

- [ ] **Step 1:** `flutter create` / manual pubspec (Cairo, http, shared_preferences).
- [ ] **Step 2:** Domain entities + use cases (login, list missions).
- [ ] **Step 3:** Gateway API client + session.
- [ ] **Step 4:** Login page + missions page (RTL).
- [ ] **Step 5:** Widget/unit test for mission list mapping.

### Task 6: DM minimal UI to assign committee

**Files:**
- Extend `delegated_manager_application` with committees page calling DM APIs.

- [ ] **Step 1:** API client methods.
- [ ] **Step 2:** Simple form: name, member1, member2, cities (comma/multi), create.
- [ ] **Step 3:** List active missions.

### Task 7: Phase 1 verification gate

- [ ] Run all `Committee*` / `MainFollower*` xUnit tests.
- [ ] Run Flutter `flutter test` in `main_follower_application`.
- [ ] Manual or scripted: login main follower → see mission (demo config accounts).
- [ ] Confirm DM login unchanged.
- [ ] Only then start Phase 2 plan.

---

## Phase 2–4 (planned later, not in this file’s tasks)

- **2:** Sale reviews 1–10 + note rules + branch sales fan-out ✅  
- **3:** Staff reviews → notes streams ✅ (2026-09-22 smoke)  
- **4:** History UI + DM summary + release APK + force/optional update ✅  
