# Phase 3 verification matrix (F14, F17–F20, F22–F26)

Branch: `security/audit-remediation-f01-f20` — **no publish**.

## 16-component matrix

| # | Component | Status | Evidence | Untested |
|---|---|---|---|---|
| 1 | BE_Company | build OK; Portal date filter SQL; payments repo overload | build log | isolated SQL plans |
| 2 | BE_SalesEmployee | AdminCities IMemoryCache; correlation; BranchAggregateCountResult | code + build | live GetAdmin load metrics |
| 3 | BE_DelegateWebApplication | correlation middleware; readiness prior | build | live posting queue |
| 4 | FE_Company | prior F15/F16 retained | remediation-status | e2e browser |
| 5 | FE_ShortCompany | prior F15/F16 retained | same | e2e browser |
| 6 | CustomerPortal | date aria-labels; reduced-motion | PortalView.vue | screen reader |
| 7 | delegate_application | refresh TTL/batch/concurrency; F14 signing fail-closed | services + gradle | field device perf |
| 8 | delegate_application_aqeel | prior F09 retained | prior docs | full flutter suite |
| 9 | follower_application | unchanged this phase | — | — |
| 10 | sales_employee_application | F14 still debug-signed (open) | gradle | signing inventory |
| 11 | delegated_manager_application | F14 open; list test via subagent | — | — |
| 12 | main_follower_application | F14 open | — | — |
| 13 | branch_manager_application | F14 open | — | — |
| 14 | sales_filter_application | F14 open | — | — |
| 15 | mobile_app | LogInterceptor debug-only | api_service.dart | Timer test via subagent |
| 16 | MA_ShortCompany | zoom allowed; WebView error+retry; ShortCompanyApp | main.dart | WebView unit pump |

## Findings F01–F26

| ID | State | Notes |
|---|---|---|
| F01–F06, F13, F15–F16, F20-sec | Prior phase; retained | See remediation-status |
| F07–F12, F21, F25 | Prior financial phase; retained | ÷1448 policy still open |
| F14 | Partial | delegate_application fail-closed; other apps still debug release — inventory required before flip |
| F17 | Partial | TTL 4m, batch SQLite, max 3 concurrent fetches; **no delta sync** without versioned delete contract |
| F18 | Code ready | Explicit-column SP + Portal SQL filter; **no measured %** without Query Store |
| F19 | Fixed | IMemoryCache + stampede gate |
| F20 | Partial | correlation IDs; mobile body logs gated; full OTel/rate-limit not added |
| F22 | Partial | `.github/workflows/ci.yml` added; baseline subagent running |
| F23 | In progress | Flutter/DI tests via subagent |
| F24 | Incremental | BranchAggregateCountResult extracted; shared-fe-contracts.md; no big-bang folder move |
| F25 | Documented | no silent FLOAT/1448 change |
| F26 | Partial | portal labels + reduced motion; MA zoom + retry |

## Re-test commands

```powershell
dotnet test BE_Company.Tests
dotnet test BE_Company.Sales.Tests
dotnet test BE_SalesEmployee.Tests
dotnet test BE_DelegateWebApplication/BE_DelegateWebApplication.Tests
cd delegate_application; flutter test
```

## Explicitly not claimed green

- Real devices, production certs, backup restore
- Isolated SQL concurrency / Query Store before/after numbers
- Full release APK builds for all Flutter apps
- “Everything fixed”
