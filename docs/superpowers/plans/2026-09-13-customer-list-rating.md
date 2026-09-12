# Customer & List Rating Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver deterministic customer and list ratings in BE_Company with FE_Company display on customers-list, customer-profile, and agents-list.

**Architecture:** Pure rating calculator + SQL-backed data access + RatingService + RatingsController; FE calls batch summary and detail endpoints. No snapshots, no Flutter/Gateway.

**Tech Stack:** ASP.NET Core (`BE_Company`), xUnit (`BE_Company.Sales.Tests`), Vue 3 (`FE_Company`)

## Global Constraints

- Scope: BE_Company + FE_Company only; no Flutter, no BE_SalesEmployee, no SalesRequests
- No rating snapshot tables / migrations / cache in phase 1
- `IsSettled` iff `AmountRemaining == 0` (rounded remaining as system)
- Active rate: `ReceiptsTotal * 1000 / (AmountTotalSales * Days)` with `Days = DATEDIFF + 1`
- Recent list cutoff: `DateSaleDevice >= 2025-09-01`
- Include all customers by `DelegateID` regardless of `CustomerState`
- `IsFakeSale` does not alter rating
- No N+1 HTTP or SQL; list aggregates in SQL or single set query then pure aggregate
- Do not commit unless user asks

## File map

| Path | Responsibility |
|------|----------------|
| `BE_Company/Sales/Rating/CustomerRatingModels.cs` | Inputs/results/enums/labels |
| `BE_Company/Sales/Rating/CustomerRatingCalculator.cs` | Pure business rules |
| `BE_Company/Sales/Rating/ListRatingAggregator.cs` | Pure list aggregation from customer results |
| `BE_Company/Sales/Rating/IRatingDataSource.cs` | Load facts for customers/lists |
| `BE_Company/Sales/Rating/RatingDataSource.cs` | SQL against branch DB |
| `BE_Company/Sales/Rating/IRatingService.cs` | Application service |
| `BE_Company/Sales/Rating/RatingService.cs` | Orchestration |
| `BE_Company/Sales/Rating/RatingDtos.cs` | API contracts |
| `BE_Company/Controllers/RatingsController.cs` | HTTP endpoints |
| `BE_Company/Sales/SalesModuleExtensions.cs` | DI registration |
| `BE_Company.Sales.Tests/CustomerRatingCalculatorTests.cs` | Unit tests customer rules |
| `BE_Company.Sales.Tests/ListRatingAggregatorTests.cs` | Unit tests list rules |
| `FE_Company/src/pages/customers-list.vue` | Summary badges |
| `FE_Company/src/pages/customer-profile.vue` | Detail panel |
| `FE_Company/src/pages/agents-list.vue` | List summaries |

---

### Task 1: CustomerRatingCalculator (TDD)

**Files:**
- Create: `BE_Company/Sales/Rating/CustomerRatingModels.cs`
- Create: `BE_Company/Sales/Rating/CustomerRatingCalculator.cs`
- Test: `BE_Company.Sales.Tests/CustomerRatingCalculatorTests.cs`

**Produces:** `CustomerRatingCalculator.Evaluate(CustomerRatingFacts facts, DateTime asOfDate)` → `CustomerRatingResult`

- [ ] **Step 1:** Write failing tests covering legal override, settled boundaries (100,140,141,160,161,179,180), active rates (7.1,6.25,5.5,<5.5), negative remaining, zero sales, incomplete dates, fake sale ignored
- [ ] **Step 2:** Run `dotnet test --filter FullyQualifiedName~CustomerRatingCalculator` → FAIL
- [ ] **Step 3:** Implement calculator + models
- [ ] **Step 4:** Run tests → PASS

---

### Task 2: ListRatingAggregator (TDD)

**Files:**
- Create: `BE_Company/Sales/Rating/ListRatingAggregator.cs`
- Test: `BE_Company.Sales.Tests/ListRatingAggregatorTests.cs`

**Produces:** `ListRatingAggregator.Aggregate(IEnumerable<CustomerRatingResult> ratings, Func<CustomerRatingResult,bool>? include)` and helpers for FinalRating / RiskIndicator / recent filter by `DateSaleDevice`

- [ ] **Step 1:** Failing tests: counts, totals, average, empty list, legal %, risk bands, recent cutoff includes 2025-09-01
- [ ] **Step 2:** Run filter → FAIL
- [ ] **Step 3:** Implement aggregator
- [ ] **Step 4:** PASS

---

### Task 3: Data source + service + API

**Files:**
- Create rating data source, service, DTOs, controller
- Modify: `BE_Company/Sales/SalesModuleExtensions.cs`

**Endpoints:**
- `POST api/Ratings/customers/summary`
- `GET api/Ratings/customers/{customerId}`
- `POST api/Ratings/lists/summary`
- `GET api/Ratings/lists/{listId}`

SQL facts must match View_CustomersDelegate aggregates (sales max date, payment sums, remaining rounded -3).

- [ ] **Step 1:** Controller/auth tests or service tests with fake data source
- [ ] **Step 2:** Implement data source + service + controller + DI
- [ ] **Step 3:** `dotnet test` Sales.Tests related filters PASS

---

### Task 4: FE_Company wiring

**Files:**
- Modify: `customers-list.vue`, `customer-profile.vue`, `agents-list.vue`

- [ ] **Step 1:** After customers load, one batch POST for summaries; show colored chip
- [ ] **Step 2:** customer-profile loads detail GET
- [ ] **Step 3:** agents-list batch list summaries (overall + recent + risk)
- [ ] **Step 4:** FE build / smoke

---

### Task 5: Verification

- [ ] `dotnet test BE_Company.Sales.Tests`
- [ ] FE build if tooling available
- [ ] Deliver summary: design, API, rules, files, tests, perf/security notes

## Spec coverage check

| Spec item | Task |
|-----------|------|
| Customer rules + boundaries | 1 |
| List average/risk/recent | 2 |
| Hybrid on-the-fly API | 3 |
| FE pages a | 4 |
| No Flutter/Gateway/migrations | Global |
| Performance/SQL aggregate | 3 |
