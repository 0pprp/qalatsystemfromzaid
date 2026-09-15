# Delegated Manager MVP — Design Spec

**Date:** 2026-09-15  
**Status:** Approved for implementation by product request (build after design; no wait unless destructive conflict)  
**App:** تطبيق المدير المفوض (`delegated_manager_application`)

## 1. Architecture decision

```
Flutter Delegated Manager  ──JWT──►  BE_SalesEmployee (sales-gw)
                                         │
                    ┌────────────────────┼────────────────────┐
                    ▼                    ▼                    ▼
           Gateway SQL DB        Branch proxy           AdminCities
        (complaints /          (customer profile,       (province
         exceptions /           notes, sales req)        filter)
         mobile updates)
```

- **No direct branch URLs/keys in the mobile app.**
- Reuse existing gateway JWT + `SalesRoleHandler` pattern.
- New Arabic `UserType`: `مدير مفوض` → module role `DelegatedManager`.
- Central login (no province): `POST api/Auth/LoginDelegatedManager` (config accounts, like Sales Manager).
- Branch-specific reads for exception detail go through existing `SalesManagerBranchAggregator` / `BranchProxyService` with gateway key (never expose to client).

## 2. Why gateway SQL (new)

`BE_SalesEmployee` today is proxy-only (no SqlClient). Cross-branch inbox + exception state machine require a **central store**.  
Add `ConnectionStrings:SalesGateway` (demo points at DEMO SQL catalog; never auto-run against Production).

## 3. Domain

### 3.1 Central complaints (`CentralComplaints`)
Generic inbox for all future apps. Status: `Unread` | `Read`.  
Intake: `POST api/complaints` — identity from auth (JWT or trusted gateway headers), never trust body identity.  
DM inbox: `GET api/delegated-manager/complaints` with pagination/filters; mark-read endpoints.

### 3.2 Sales exception requests (`SalesExceptionRequests` + `SalesExceptionAudit`)
States: `Pending` | `Approved` | `Rejected` | `Cancelled`.  
`TargetApproverType`: `DelegatedManager` | `BranchManager` (schema ready; MVP UI only DelegatedManager).  
Optimistic concurrency: `UPDATE … WHERE Id=@Id AND Status='Pending'`.  
On Approve: append shared customer note via branch `sales-manager/customers/notes`.

### 3.3 Mobile updates (`MobileAppReleases`)
`GET api/mobile-updates/{appKey}` — versionCode comparison, forceUpdate, sha256, apkUrl.

## 4. FE_Company hooks

- Card + detail: **طلب استثناء** on `sales-manager-requests.vue`.
- New tab/section: exception list (Pending / Approved / Rejected).
- APIs via `salesManagerApi.js` → gateway.

## 5. Flutter app structure

Feature-based clean architecture under `delegated_manager_application/lib/` (core + features: auth, dashboard, complaints, exceptions, settings/update).  
AppEnv mirrors `sales_filter_application` (demo `:8080/sales-gw/api/`, prod `admincompany…/sales-gw/api/`, no city selector).

## 6. Non-goals / limits

- No Branch Manager approval UI.
- No Production DB apply / deploy / push.
- Do not revive reverted Global Sales Manager feature.
- Existing Delegate branch complaints remain; central intake is additive for extensibility.

## 7. Security

- Server policies for all DM endpoints.
- Parameterized SQL; no secrets in repo beyond existing placeholder patterns.
- SHA-256 APK verify before install; no silent install.
