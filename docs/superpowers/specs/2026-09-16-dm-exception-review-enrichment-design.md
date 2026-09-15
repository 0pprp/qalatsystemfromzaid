# Delegated Manager Exception Review Enrichment

**Date:** 2026-09-16  
**Status:** Proposed — awaiting approval  
**Scope:** DM exception detail (classification, same-province matches, current request, source, history/rating) + complaint UI cleanup  
**Out of scope:** Redesign of SM evaluation UI, new rating thresholds, push/deploy, Flutter redesign of unrelated tabs

---

## 1. Goal

Delegated Manager must decide Approve/Reject from one detail screen without asking anyone else. Detail answers: New vs Existing (same province only), why, matches + history/rating, full current SalesRequest, authoritative request source, and clean business UI (no GUIDs).

---

## 2. Approaches considered

### A — Enrich gateway `GET delegated-manager/exceptions/{id}` (recommended)

- Keep list lightweight.
- On detail: gateway loads exception + audit, then S2S to branch (`CityValue`) for: SalesRequest by id, phone/triple-name matches in that branch catalog, profile/rating/history for match hits.
- Flutter consumes one enriched payload.

**Pros:** Matches existing DM auth (gateway JWT only); reuses branch evaluation/catalog/rating; no FE/Flutter secrets; one round-trip for UI.  
**Cons:** Detail latency higher (orchestrated); must handle missing SalesRequest / branch down clearly.

### B — New BE_Company DM-facing endpoints + Flutter multi-call

- Flutter calls gateway exception, then separately evaluate/profile APIs.

**Pros:** Smaller gateway change.  
**Cons:** DM has no branch JWT; would need new proxies anyway; N+1 from client; harder consistency/security; worse UX on flaky network.

### C — Duplicate matching in gateway SQL / in-memory

**Pros:** Single process.  
**Cons:** No authoritative catalog/rating; cross-province risk; forbidden duplication.

**Recommendation: A.**

---

## 3. Architecture (recommended)

```
DM Flutter
  GET /sales-gw/api/delegated-manager/exceptions/{id}
       │
       ▼
BE_SalesEmployee (Policy: Sales.DelegatedManager)
  SalesExceptionService.GetReviewDetailAsync
       │
       ├─ Gateway SQL: exception + audit (existing)
       │
       └─ BranchProxy / Aggregator (X-Sales-Gateway-Key)
              city = exception.CityValue
              │
              ├─ GET/POST sales-manager/… SalesRequest by SalesRequestId
              ├─ Same-province match service (new thin orchestrator on BE_Company)
              │     catalog LoadCustomersAsync (branch DB = province)
              │     SalesPhoneNormalizer + SalesRequestNameSimilarity.IsTripleNameMatch
              │     NO fatherGrandfather for classification
              └─ For each match (capped): profile + CustomerRatingCalculator + recent sales
```

**Matching runs only in BE_Company branch DB.** Gateway never fans out customer search across cities.  
**Classification rule:** Existing iff ≥1 match with (same branch tenancy) AND (phone OR triple-name). Kinship may appear only as optional supporting context on a card that already matched by phone/name — never alone.

---

## 4. Customer matching algorithm

### Inputs (authoritative)

- Current request `CityValue` (from loaded SalesRequest; fallback exception.CityValue only if request missing city but id valid — prefer request).
- Current `CustomerName`, `CustomerPhone` from SalesRequest (not client-spoofed).

### Province enforcement

- Branch DB connection **is** the province boundary (catalog already stamped with branch `CityValue`).
- Gateway routes proxy **only** to `exception.CityValue` / request.CityValue.
- After load, drop any catalog row whose comparable `CityValue` differs when both sides are comparable keys; if historical `CityValue` missing and no authoritative branch stamp → **exclude**.
- **Never** company-wide / multi-city search for this feature.

### Match predicates (Existing)

1. **Phone:** `SalesPhoneNormalizer.Matches(current, candidate)`  
2. **Triple/full name:** `SalesRequestNameSimilarity.IsTripleNameMatch` — requires ≥3 tokens each side; exact equality of tokens [0],[1],[2] after normalize (diacritics, أ/إ/آ→ا, ة→ه, ى→ي, collapse spaces, drop trailing numeric tokens).

### Explicit non-matches

- Two-part names (e.g. علي حسن vs علي حسن محمد) → not Existing.  
- Same phone or same triple name in **another** province → not a match (never queried).  
- `IsFatherOrGrandfatherMatch` alone → **does not** classify Existing.  
- No fuzzy / family / document matcher.

### Match reasons (Arabic)

- تطابق رقم الهاتف  
- تطابق الاسم الثلاثي  
- تطابق الاسم ورقم الهاتف (both)

### Classification

- `matchCount == 0` → `New` / زبون جديد  
- `matchCount >= 1` → `Existing` / زبون قديم  

---

## 5. Why two-part names are excluded

Business rule: identity for “old customer” requires full three-part Arabic name equality (or phone). Two-part overlap is common and produces false positives. Existing `IsTripleNameMatch` already encodes ≥3 tokens — reuse it; do not loosen.

---

## 6. Data sources (reuse, do not duplicate)

| Concern | Source |
|--------|--------|
| Phone normalize | `SalesPhoneNormalizer` |
| Triple name | `SalesRequestNameSimilarity` |
| Catalog | `ISalesExcelCustomerSearchCatalog.LoadCustomersAsync` (branch) |
| Rating | `CustomerRatingCalculator` / `IRatingDataSource` (same as SM) |
| Profile / sales history | `SalesShopProfileService` (or equivalent existing profile aggregate) |
| Current request | Branch `SalesRequests` by `SalesRequestId` |
| Exception snapshot | Gateway `SalesExceptionRequests` (audit only; detail prefers live request) |
| Source person/list | SalesRequest: `CustomerSourceType`, `CreatedByName`, `CreatedByUserType`, `SourceListId` + list name lookup if exists; else غير متوفر |
| Hold / consume | Unchanged |

Limitation to document if profile lacks a field: show غير متوفر; do not invent thresholds.

---

## 7. Source-of-sale mapping

Map `CustomerSourceType` (+ CreatedBy*) server-side:

| Type | Arabic label | Person | List |
|------|--------------|--------|------|
| `Delegate` | مندوب | CreatedByName | list name from SourceListId if resolvable |
| `Follower` | متابع | CreatedByName | list name |
| `EmployeeSubmitted` | موظف مبيعات | CreatedByName | — |
| Manager create / NewCustomer / ExistingCustomer (SM intake) | مدير مبيعات or intake label | CreatedByName | — |
| Unknown / empty | غير متوفر | غير متوفر | — |

Never trust Flutter for source identity.

---

## 8. DTO / API changes

### Keep

`GET api/delegated-manager/exceptions` — lightweight (no matching).  
`PUT …/decision` — unchanged.  
Hold/consume/assign — unchanged.

### Extend

`GET api/delegated-manager/exceptions/{id}` response:

```json
{
  "request": { /* existing ToExceptionDetail */ },
  "audit": [ /* existing */ ],
  "review": {
    "customerClassification": {
      "type": "New|Existing",
      "labelArabic": "زبون جديد|زبون قديم",
      "matchCount": 0,
      "explanationArabic": "…"
    },
    "currentRequest": {
      "salesRequestId": 123,
      "customerName": "…",
      "customerPhone": "…",
      "cityValue": "…",
      "cityName": "…",
      "address": "…",
      "province": "…",
      "occupation": null,
      "notes": "…",
      "saleTypeOrProduct": "…",
      "status": "…",
      "createdAtUtc": "…",
      "createdByName": "…",
      "customerSourceType": "…"
      /* only meaningful business fields; no raw junk */
    },
    "source": {
      "type": "Delegate|Follower|EmployeeSubmitted|…",
      "displayLabel": "مندوب",
      "personName": "…",
      "listName": "…|null|غير متوفر",
      "branchName": "…"
    },
    "matchingCustomers": [
      {
        "customerId": 1,
        "fullName": "…",
        "phone": "…",
        "cityValue": "…",
        "cityName": "…",
        "address": "…",
        "occupation": "…",
        "matchReasons": ["تطابق رقم الهاتف"],
        "rating": { "labelArabic": "جيد", /* existing shape */ },
        "financialSummary": {
          "totalSales": 0,
          "totalPaid": 0,
          "remaining": 0,
          "currentDebt": 0,
          "lastSaleDate": null,
          "lastPaymentDate": null,
          "fullyPaid": false,
          "legalStatus": "…"
        },
        "previousSales": [ /* newest first, capped e.g. 10 */ ]
      }
    ],
    "decisionHistory": [ /* optional alias of audit with friendly labels */ ]
  }
}
```

Errors:

- Missing/invalid `SalesRequestId` or request not found on branch → **404/409 business message** (Arabic): لا يمكن تحميل تفاصيل طلب البيع الأصلي.
- Branch unreachable → clear error; do not silently classify New.

### BE_Company (internal / SM path)

Add focused endpoint used only via gateway key, e.g.:

`POST api/sales-manager/sales-requests/{id}/exception-review-context`  
or  
`POST api/sales-manager/exception-review/context` body `{ salesRequestId, cityValue }`

Returns: currentRequest snapshot + matches (phone/triple only) + enriched match cards.  
Auth: existing Sales Manager JWT **or** gateway key (same as other SM endpoints). **Not** callable as Delegated Manager role on branch.

Optional: extract shared matcher helper from evaluation that filters to phone+triple only for this DTO (do not change SM evaluation categories).

---

## 9. UI changes (delegated_manager_application)

### Exception detail hierarchy (RTL)

1. Classification banner: زبون جديد / زبون قديم (+ count / explanation)  
2. تفاصيل الطلب الحالي  
3. مصدر الطلب  
4. سبب الاستثناء  
5. الحالات المطابقة (expandable cards: بيانات الزبون، السجل المالي، المبيعات السابقة، التقييم، حالة الحساب)  
6. موافقة / رفض  

Hide from UI: exception GUID, complaint GUID, raw customerId (unless needed as business number for Existing — prefer hide), raw sourceApp/sourceType, metadata JSON, internal enums.

Keep IDs in memory for API calls only.

### Complaint list/detail

Show: sender name, role, province, body, friendly datetime, read/unread, friendly source label.  
Remove: GUID, metadataJson dump, raw sourceApp keys (map to مندوب / …).

### List screens

Remain lightweight — no match fetch.

---

## 10. Performance

- List: unchanged SQL page query.  
- Detail: 1 gateway read + 1 branch context call; inside branch: 1 catalog load + batch rating + capped history (avoid N+1 profile-per-row; batch where infrastructure allows; max matches e.g. 20, sales per match e.g. 10).  
- No cross-branch fan-out.

---

## 11. Security

- Detail requires `Sales.DelegatedManager` only.  
- Sales Manager cannot call DM review endpoints.  
- Matching + source derived server-side from SalesRequest + branch catalog.  
- Province = routed branch; no client CityValue override for search scope.  
- Gateway key stays server-side.  
- IDs are not authorization.  
- Parameterized SQL only.

---

## 12. Workflow integrity

Pending → DM Approve/Reject; hold/consume/assign unchanged. Rejected stays non-assignable. No return to unassigned.

---

## 13. Tests (planned)

**Matching (BE_Company):** cases 1–10 from requirements (New/Existing, province, two-part, whitespace, multi-match, dual reasons, no fuzzy).  

**Source:** 11–15.  

**Detail/API:** 16–23 (load request, history, rating, newest-first, legal, missing request error, list lightweight, DM auth).  

**Flutter:** 24–32 (labels, no cross-province, no two-part badge, source section, no GUIDs, approve/reject, RTL).  

Run: BE_Company.Sales.Tests, BE_SalesEmployee.Tests, relevant customer/search tests, `flutter test`, `flutter analyze`.

---

## 14. Migrations

**None required** if SalesRequest already has CreatedBy*, CustomerSourceType, SourceListId, CityValue, and exception has SalesRequestId (current schema).  

Optional later: denormalize list name onto SalesRequest — **out of scope**; resolve live or show غير متوفر.

---

## 15. Files likely touched

- `BE_Company`: new exception-review context service/controller; tests  
- `BE_SalesEmployee`: `SalesExceptionService` detail enrichment; aggregator proxy; tests  
- `delegated_manager_application`: exception detail UI/models/repo; complaint cleanup; tests  
- Spec: this file  

No FE_Company redesign required for this task.

---

## 16. Open non-blocking notes

- If `SourceListId` has no name resolver today → listName = غير متوفر (documented).  
- Kinship: not used for classification; omit from matchReasons; do not show kinship-only cards.  
- `salesRequestId` display: treat as business request number only if product already does; otherwise hide numeric id in UI and keep internal.
