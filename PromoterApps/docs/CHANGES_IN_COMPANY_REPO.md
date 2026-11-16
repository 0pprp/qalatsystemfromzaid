# Changes made in the company repo for the promoter apps

The first change was 2026-11-03. Until 2026-11-16 the company repo was edited outside git and the originals were backed up in `Customer_website_app/company_repo_backup/`. Since then this log is kept in git, and that folder is no longer needed.

The gateway and branch endpoints are under `api/internal/customer-app/*`. Their code uses the `CustomerApp` names from v1; see the naming note in [../README.md](../README.md).

## Gateway & branch integration

### Branch (BE_Company)

New files:
- `BE_Company/Sales/Controllers/InternalCustomerAppController.cs` — gateway-only (X-Sales-Gateway-Key) `api/internal/customer-app/*`: accounts-by-phone, accounts/{customerId}?phone=, sales-requests/status.
- `BE_Company/Sales/Services/CustomerAppService.cs` — `ICustomerAppService`: phone validation, one account per customer, item merging, status response building (request order, found=false for unknown uuids, ≤200).
- `BE_Company/Sales/Services/CustomerAppRepository.cs` — `ICustomerAppRepository`: read-only Dapper queries over View_CustomersDelegate, View_SelectItemsSalesItemsNames, View_CustomersSalesDelegate, View_CustomersPaymentsDelegate, SalesRequests (by ExternalCrmOrderUuid).
- `BE_Company/Sales/Services/CustomerAppPhoneMatch.cs` — phone match key + T-SQL strip/RIGHT(…,10) predicate for legacy Customers.PhoneNumber values (with a C# mirror for tests).
- `BE_Company/Sales/Services/CrmIntakeSourceProfile.cs` — maps intake `Source` ("CustomerApp" vs default CRM) to CustomerSourceType / CreatedByName / CreatedByUserType / product note label / whether the CRM board-status forwarder fires.
- `BE_Company/Sales/DTO/CustomerAppDTOs.cs` — DTOs for the endpoints above.

Modified files (backed up):
- `BE_Company/Sales/Services/CrmSalesRequestIntakeService.cs` — optional `Source` on `CrmSalesRequestIntakeRequest`; row/history attribution comes from `CrmIntakeSourceProfile`; the "Processing" CRM board-status notify is skipped for CustomerApp. CRM behaviour unchanged when Source is empty.
- `BE_Company/Sales/Services/SalesManagerContracts.cs` — `SalesRequestSources.CustomerApp`, `CustomerAppArabicLabel` ("تطبيق الزبون"), `IsCustomerApp()`.
- `BE_Company/Sales/Services/ExceptionReviewCustomerMatcher.cs` — `ExceptionReviewSourceMapper` labels CustomerApp rows "تطبيق الزبون" (previously would show "غير متوفر").
- `BE_Company/Sales/SalesModuleExtensions.cs` — registers `ICustomerAppRepository` and `ICustomerAppService`.

The branch CRM intake controller (`BE_Company/Controllers/InternalCrmSalesRequestsController.cs`) is unchanged: it binds `CrmSalesRequestIntakeRequest`, so `source` passes through automatically.

### Gateway (BE_SalesEmployee)

New files:
- `BE_SalesEmployee/Controllers/InternalCustomerAppController.cs` — `api/internal/customer-app/*` (branches, accounts-by-phone, accounts/{cityValue}/{customerId}, sales-requests, sales-requests/status), key check reuses `InternalCrmSalesRequestsController.HasValidGatewayKey`.
- `BE_SalesEmployee/Sales/CustomerApp/CustomerAppGatewayService.cs` — fan-out (8 s per branch, partial results), detail proxy, order routing to the branch CRM intake with `source="CustomerApp"`, status grouping by branch.
- `BE_SalesEmployee/Sales/CustomerApp/CustomerAppRules.cs` — `CustomerAppBranchScope` (Qalaa only: `SalesOverviewBranches.IsExcluded` + numeric cityValue), `CustomerAppPhone`, `CustomerAppStatusGrouping`.
- `BE_SalesEmployee/Sales/CustomerApp/CustomerAppContracts.cs` — gateway request/response contracts (+ `LenientIntConverter`).

Modified files (backed up):
- `BE_SalesEmployee/Program.cs` — one line: `AddScoped<CustomerAppGatewayService>()`.

### Tests (new files)
- `BE_Company.Sales.Tests/CustomerAppIntegrationTests.cs` — phone match key + legacy formats + SQL helper; account/detail/status service logic; CustomerApp source mapping; end-to-end intake with Source=CustomerApp (no CRM notify, idempotent replay) and unchanged CRM path.
- `BE_SalesEmployee.Tests/CustomerAppGatewayTests.cs` — branch scope filtering; status grouping/merge; fan-out partial results with a slow and a dead branch; detail 404/stamping; create routing + 422 out-of-scope + error pass-through; status endpoint; controller key check and branches shape.

## FE_Company dashboard

All paths relative to `FE_Company/`. Backups of modified files: `company_repo_backup/FE_Company/<same path>`.

New files:
- `src/composables/customerAppApi.js` — BE_CustomerApp admin client (base `VITE_CUSTOMER_APP_API_URL`, default `/customer-app/api/v1/`; Bearer `localStorage.Token`; Arabic `{error}` extraction; helpers for every `admin/*` endpoint; gateway evaluate / evaluation-hits for app orders; labels, money/date formatting, commission-chain preview).
- `src/components/customer-app/caEvaluation.js` — evaluation chip label/colour helpers (copied rules from sales-manager-requests.vue).
- `src/components/customer-app/CaEvaluationSummary.vue` — overall score chip + 4 category counts (compact for table rows, full metric grid for the detail dialog).
- `src/components/customer-app/CaEvaluationHitsDialog.vue` — «تفاصيل التطابقات» dialog (tabs per category, gateway evaluation-hits).
- `src/components/customer-app/CaReasonDialog.vue` — reusable reason/note dialog (deny, approve notes, suspend, freeze, payout reject/paid, reverse).
- `src/pages/customer-app-orders.vue` — «فلترة طلبات التطبيق»: status tabs with counts (`admin/orders/counts`), filters, evaluation chips, detail dialog (auto-review of New), approve / deny / sync / edit public reason.
- `src/pages/customer-app-products.vue` — «منتجات التطبيق»: list, quick visibility, create/edit (pricing, commission chain preview, branches, specs, media upload/YouTube/reorder).
- `src/pages/customer-app-categories.vue` — categories CRUD (icon upload).
- `src/pages/customer-app-banners.vue` — banners CRUD (image upload, product/category/external link, schedule).
- `src/pages/customer-app-users.vue` — «مستخدمو التطبيق»: list/search, detail with upline + lazy network tree + earnings, suspend / freeze earnings.
- `src/pages/customer-app-commissions.vue` — «العمولات والصرف»: entries with filters, reverse sale, manual adjustment; payouts approve / mark-paid / reject.
- `src/pages/customer-app-settings.vue` — «إعدادات التطبيق»: settings form (incl. auto-reverse on branch cancel, min/latest app version) + broadcast notification + broadcast history.
- `src/pages/customer-app-reports.vue` — «تقارير التطبيق»: overview cards/tables, date range, CSV exports; audit log with before/after diff.

Modified files (backed up):
- `src/navigation/vertical/index.js` — added «تطبيق الزبون» group (8 children) after «إدارة المبيعات».
- `src/navigation/horizontal/index.js` — same group.
- `src/layouts/components/DefaultLayoutWithVerticalNav.vue` — title filter: show «تطبيق الزبون» for المدير and مدير مبيعات; hide for all other roles.
- `src/layouts/components/DefaultLayoutWithHorizontalNav.vue` — same filter change.
- `src/plugins/1.router/index.js` — `customerAppPaths` + guard (only المدير / مدير مبيعات reach `/customer-app-*`); paths added to `directorAllowed`.
- `src/pages/sales-manager-requests.vue` — `requestSourceLabel`: `CustomerSourceType === 'CustomerApp'` → «تطبيق الزبون» (2 lines).
- `vite.config.js` — dev proxy `/customer-app/` → `http://127.0.0.1:5380` (prefix stripped).
- `nginx.conf` — `location /customer-app/` → `http://127.0.0.1:5380/` (mirrors `/sales-gw/`, `client_max_body_size 110m` for video uploads).
- `.env.demo.example`, `.env.production.example` — `VITE_CUSTOMER_APP_API_URL=/customer-app/api/v1/`.


## v3 (2026-11-04): field agents

Files below were created in v1 by this project (no originals to back up).

Branch (BE_Company):
- `Sales/Controllers/InternalCustomerAppController.cs`: `GET api/internal/customer-app/sales/{saleId}/progress`. Returns 404 until the sales draft is posted.
- `Sales/Services/CustomerAppService.cs`: `GetSaleProgressAsync`.
- `Sales/Services/CustomerAppRepository.cs`: `GetAccountBySaleDraftAsync`, which reads `SalesDrafts.CustomerId` → `View_CustomersDelegate` (total, paid, remaining, payments, last payment).
- `Sales/DTO/CustomerAppDTOs.cs`: `CustomerAppSaleProgressDTO`.
- `BE_Company.Sales.Tests/CustomerAppIntegrationTests.cs`: progress test plus the fake repository member.

Gateway (BE_SalesEmployee):
- `Controllers/InternalCustomerAppController.cs`: `GET api/internal/customer-app/sales/{cityValue}/{saleId}/progress`.
- `Sales/CustomerApp/CustomerAppGatewayService.cs`: `GetSaleProgressAsync` (proxy to the city's branch, stamps `cityValue`).
- `Sales/CustomerApp/CustomerAppContracts.cs`: `CustomerAppSaleProgress`.
- `BE_SalesEmployee.Tests/CustomerAppGatewayTests.cs`: proxy and 404 tests.

The `accounts-by-phone` and `accounts/{…}` endpoints are no longer called by BE_CustomerApp. They can stay or be removed.

FE_Company dashboard:
- New `src/pages/customer-app-agents.vue` «المسوّقون»: list, create (username/password), edit, new password, suspend.
- New `src/pages/customer-app-attendance.vue` «دوام المسوّقين»: daily attendance, live map (Mapbox, existing token), each agent's route and shifts, 30-day history.
- `src/composables/customerAppApi.js`: agent and attendance helpers, `caMinutes`, and the `Finished` order status.
- `src/pages/customer-app-orders.vue`: shop photo and sale payment progress in the order dialog; `?q=` pre-fills the search.
- `src/navigation/vertical/index.js` and `src/navigation/horizontal/index.js`: group renamed «تطبيق المسوّقين»; «مستخدمو التطبيق» replaced by «المسوّقون» and «دوام المسوّقين».
- `src/plugins/1.router/index.js`: the two new paths added to the customer-app list.
- A test `vite build` was run, which regenerated `FE_Company/dist`.


## v3.1 (2026-11-05): pins, stationary alerts, roles, orders straight to «طلبات البيع»

None of this is deployed yet. The live server's company build is from 2026-11-04 07:48 Iraq time, so it predates both v3 and v3.1.

Branch (BE_Company):
- `Sales/Services/CrmIntakeSourceProfile.cs`: new `AutoAssign` (false for the agents' app) and `CreatedByNameFor(name)`. App orders stay in «غير المسندة» and store the agent's name in `CreatedByName`.
- `Sales/Services/CrmSalesRequestIntakeService.cs`: new `SubmittedByName` field. The round-robin auto-assign is skipped when `AutoAssign` is false.
- `Sales/Services/SalesManagerContracts.cs`: `CustomerAppArabicLabel` = «المروج».
- `BE_Company.Sales.Tests/CustomerAppIntegrationTests.cs`: expectations updated (no auto-assign, agent name).

Gateway (BE_SalesEmployee):
- `Sales/CustomerApp/CustomerAppContracts.cs` + `CustomerAppGatewayService.cs`: `SubmittedByName` passed through to the branch intake.

FE_Company dashboard:
- `src/pages/sales-manager-requests.vue`: the source of app orders shows «المروج: الاسم».
- The «تطبيق المسوّقين» section is now for المدير and المراقب only. It is removed from مدير مبيعات and every other role in both layouts' menus, the router guard and the monitor's allowed paths. This also fixes a v3 bug: the renamed group was hidden from المدير and shown to other roles.
- New `src/components/CustomerAppAlertsBell.vue`, added to both layouts' navbars for المدير and المراقب. It is a bell for «بقي في نفس المكان» alerts: it polls every minute and shows a toast, a sound and a browser notification.
- New `src/pages/customer-app-prospects.vue` «المحلات المحتملة»: a map and list of agents' pins with filters (branch, agent, interest, dates, search), details, and delete.
- `src/pages/customer-app-attendance.vue`: shows each day's alerts (a chip, an orange ring on the map, and the place on the agent's route with «تم الاطلاع»). It also honours `?date=`.
- `src/pages/customer-app-orders.vue`: renamed «طلبات المروّجين». It is now for tracking; «إرسال الآن» and «إلغاء» remain only for orders the branch could not receive yet.
- `src/composables/customerAppApi.js`: alert and prospect helpers and `CA_INTEREST_LEVELS`; `Transferred` now reads «في قسم المبيعات».
- `eslint --fix` was run on the touched files. The test `vite build` went to a scratch folder, so `dist` was not touched this time.

Pre-existing test failures not caused by these changes:
- BE_Company.Sales.Tests: `EmployeeSalesRatingTests.PeriodSnapshot_Only_Sales_Inside_From_To` and `ListRatingTrendCalculatorTests.PeriodAggregate_Uses_Level_Points_Over_Customer_Count`.
- BE_SalesEmployee.Tests: `DelegatedManagerRoleTests.UnknownUserType_MapsToNull` and `CentralComplaintsServiceTests.SenderRole_RecordsDelegatedManagerAndFallsBackToRawUserType`.

## 2026-11-05: agents belong to HQ, dashboard build fix

- `FE_Company/src/pages/customer-app-agents.vue`: removed the branch field, column and filter; subtitle says agents belong to the HQ.
- `customer-app-attendance.vue`, `customer-app-prospects.vue`: removed the branch filter (agents and their pins carry no branch). The attendance list shows the agent's phone where the branch was.
- Build note: build the dashboard from PowerShell (or with `MSYS_NO_PATHCONV=1`). Git Bash rewrites env values that start with `/` (e.g. `VITE_CUSTOMER_APP_API_URL=/customer-app/api/v1/` became `C:/Program Files/Git/customer-app/api/v1/`), which broke the agents pages with "Unsupported protocol C:". Leaving that variable unset also works: the code defaults to `/customer-app/api/v1/`.

## v4 (2026-11-06): dashboard clean-up (FE_Company only)

- Deleted pages: `customer-app-products`, `-categories`, `-banners`, `-users`, `-commissions`, `-reports`. New: `customer-app-overview.vue` (performance + action log). `customer-app-settings.vue` rewritten (support, app versions, messages to agents).
- `customer-app-orders.vue`: no prices, product images or «لنفسه/لشخص آخر»; items are the names the agent typed.
- `navigation/{vertical,horizontal}/index.js` and `plugins/1.router/index.js`: menu and allowed paths match the 6 remaining pages.
- `composables/customerAppApi.js`: catalog, users, commission and payout helpers removed.
- No backend change in the company system: the agent's typed item already travels as the request's product line.

## 2026-11-06: «طلبات المروّجين» removed

- Agents' orders already arrive in «طلبات البيع» (source «المروج: الاسم»), so the separate page was dropped: deleted `pages/customer-app-orders.vue` and the components only it used (`components/customer-app/CaEvaluationHitsDialog.vue`, `CaEvaluationSummary.vue`, `caEvaluation.js`); removed its menu entries, allowed path, the «طلباته» shortcut on the agents page, and the order/matching helpers in `composables/customerAppApi.js`.
- The menu is now: نظرة عامة · المسوّقون · دوام المسوّقين · المحلات المحتملة · إعدادات التطبيق. Deployed 2026-11-06 (backup `AdminFrontend.prev-noorders-20261106173806`).


## 2026-11-07: Arabic map labels

- Mapbox drew Arabic street/area names reversed and with unjoined letters. New `src/utils/mapbox.js` loads Mapbox's RTL text plugin (v0.2.3, lazily) once and re-exports `mapboxgl`; the 8 map pages (`customer-app-attendance`, `customer-app-prospects`, `follower-tracking-map`, `follower-tracking-routes`, `sales-manager-map`, `sales-manager-routes`, `sales-manager-followers-map`, `sales-manager-followers-routes`) import `mapboxgl` from `@/utils/mapbox` instead of `mapbox-gl`.
- Plugin v0.3.0 does not work lazily with mapbox-gl 3.1.2 (Arabic labels disappear); keep v0.2.3.
- Deployed 2026-11-07 (backup `AdminFrontend.prev-rtl1-20261107022321`; `AdminFrontend.prev-rtl-20261107021810` is the brief v0.3.0 build, not to be restored).

## 2026-11-07: dashboard maps had no Mapbox key

- The 03:03 dashboard build (version settings removed) went out without `VITE_MAPBOX_TOKEN`, so every map showed «مفتاح الخريطة غير مهيأ». The key was read with a relative path that came back empty and the build did not stop. Rebuilt and redeployed at 11:25 (backup of the keyless build: `AdminFrontend.prev-notoken-20261107112530`).
- Build check: read the key from the absolute path `E:\Repos\qalatsystemfromzaid-main\handover-secrets\FE_Company\.env`, refuse to build if it is empty or not `pk.…`, and after the build confirm `dist/assets/*.js` contains `pk.eyJ` before deploying.

## 2026-11-07: «المروّجون أونلاين» (online promoters app) dashboard section

A separate section for the new BE_PromotersApp (see docs/PROMOTERS_APP.md). Nothing in «تطبيق المسوّقين» changed. Backups of modified files: `company_repo_backup/_promoters_20261107/FE_Company/`.

- New `src/composables/promotersAppApi.js` (`pa*` helpers, base `/promoters-app/api/v1/`).
- New pages: `promoters-app-overview`, `-users`, `-products`, `-orders`, `-payouts`, `-materials`, `-settings`.
- `src/navigation/{vertical,horizontal}/index.js`: new group «المروّجون أونلاين».
- `src/layouts/components/DefaultLayoutWith{Vertical,Horizontal}Nav.vue`: the group is shown to المدير and المراقب only.
- `src/plugins/1.router/index.js`: `promotersAppPaths` with the same guard; added to the director's and monitor's allowed paths.
- `vite.config.js` (dev proxy) and `nginx.conf`: `/promoters-app/` → 127.0.0.1:5430.
- No company backend change: orders use the existing gateway intake (source «المروج: أونلاين - username»).
- Deployed 2026-11-07 (backup `AdminFrontend.prev-promoters-20261107214608`). The same build carried the «هيكل الأدوار والصلاحيات» session's pending edits (approved by the user).

## 2026-11-08: minutes at the shop, daily pin target (FE_Company)

- `customer-app-prospects.vue`: «الوقت المستغرق عند المحل» in the list, map popup and details.
- New `pages/customer-app-targets.vue` («الأهداف اليومية») and `components/customer-app/CaTargetHistoryDialog.vue`; menu entries in `navigation/{vertical,horizontal}/index.js`; allowed path `/customer-app-targets` in `plugins/1.router/index.js`.
- `customer-app-agents.vue`: row menu «سجل الهدف اليومي». `customer-app-settings.vue`: «الهدف اليومي لتثبيت المحلات». `composables/customerAppApi.js`: `caTargetsDay`, `caAgentTargets`, `caSetAgentTarget`, `CA_TARGET_STATUS`.
- Deployed 2026-11-08 03:24 (backup `AdminFrontend.prev-targets-20261108032408`). The build also carries the sales-requests Excel-import changes of another task, which that task had already deployed at 02:09.
- 2026-11-09: `promoters-app-payouts.vue` + `promotersAppApi.js`: «تم التحويل» requires a receipt picture (with a preview), the receipt shows on paid requests, and the chat can send and show photos and documents (fetched with the token, not public). Deployed (backup `AdminFrontend.prev-receipts-20261109075327`).

## 2026-11-09: map key missing again — now built in by default

- Another task's dashboard deploy (2026-11-09 15:55, backup `AdminFrontend.bak-assistant-history-20261109_155549.tar.gz`) was built without `VITE_MAPBOX_TOKEN`, so the maps showed «مفتاح الخريطة غير مهيأ» again. Rebuilt and redeployed at 16:02 (server time; keyless build kept as `AdminFrontend.prev-notoken2-20261109160207`).
- Lasting fix: `FE_Company/.env.production.local` holds `VITE_MAPBOX_TOKEN` (copied from `handover-secrets/FE_Company/.env`). Vite reads it on every `vite build`, so any build from this folder gets the key without passing it. The file is git-ignored (`.env*`, `*.local`). Still check `dist/assets/*.js` contains `pk.eyJ` before deploying.
