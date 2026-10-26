# Excel APIs Documentation / توثيق واجهات إكسل

Bilingual guide for the Excel-related APIs shared with collaborators.  
دليل عربي–إنجليزي لواجهات إكسل حتى يفهم المبرمج المستلم المسار كاملاً.

---

## 1) Overview / نظرة عامة

| Feature | EN | AR |
|--------|----|----|
| Sales Manager Excel Search | Bulk customer lookup from an Excel name list (read-only). UI parses the file, API searches by names. | بحث جماعي عن زبائن من قائمة أسماء في إكسل (قراءة فقط). الواجهة تقرأ الملف والـ API يبحث بالأسماء. |
| Sales Requests Excel Import | Import sales-request rows (after client-side parse) into a branch. | استيراد صفوف طلبات بيع (بعد قراءة الملف في الواجهة) إلى محافظة. |
| Legal Excel Search | Accountant/legal path: name + phone match against the branch customer catalog. | مسار المحاسب/القانونية: تطابق اسم + هاتف على كتالوج زبائن الفرع. |

**Primary UI pages**

| Route | File |
|-------|------|
| `/sales-manager-excel-search` | `FE_Company/src/pages/sales-manager-excel-search.vue` |
| Sales requests board (import Excel) | `FE_Company/src/pages/sales-manager-requests.vue` |
| `/accountant-legal-search` (LegalExcelSearch) | `FE_Company/src/pages/accountant-legal-search.vue` |

---

## 2) Auth & base paths / المصادقة والمسارات الأساسية

| Layer | Base path | Auth |
|-------|-----------|------|
| **Gateway** (FE calls this) | `POST /api/sales-manager/...` on **BE_SalesEmployee** | JWT — policy `SalesManagerOrDirector` (director writes denied where applicable) |
| **Branch backend** | `POST /api/sales-manager/...` on **BE_Company** | JWT — policy `SalesManager` (+ internal gateway key when called from aggregator) |
| **Company Customers** | `POST /api/Customers/LegalExcelSearch` | Company auth used by accountant legal page |

Gateway fans out Excel search to one or all sales branches, merges matches per query row, stamps `cityValue` / `resultKey`.

---

## 3) Endpoint A — Sales Manager Excel Search (main)

### Gateway

```
POST /api/sales-manager/customers/excel-search
```

**Code:** `BE_SalesEmployee/Controllers/SalesManagerController.cs` → `ExcelSearchCustomers`  
**Aggregator:** `SalesManagerBranchAggregator.ExcelSearchAsync`  
**Timeout per branch:** 120 seconds

### Branch (BE_Company)

```
POST /api/sales-manager/customers/excel-search
```

**Code:** `BE_Company/Sales/Controllers/SalesManagerController.cs` → `ExcelSearchCustomers`  
**Service:** `SalesExcelCustomerSearchService`  
**Catalog:** `SalesExcelCustomerSearchCatalog`  
**DTOs:** `BE_Company/Sales/DTO/SalesExcelSearchDTOs.cs`  
**Match rules:** `SalesCustomerNameMatch` in `SalesArabicText.cs`

### Query / body

```json
{
  "names": ["حسين محمد", "علي جاسم"],
  "cityValue": "DatabaseCompanyBasra"
}
```

| Field | Type | Required | EN | AR |
|-------|------|----------|----|----|
| `names` | `string[]` | yes | Customer names from Excel (order preserved). Max **1500**. | أسماء الزبائن من الإكسل (نفس الترتيب). الحد الأقصى **1500**. |
| `cityValue` | `string?` | no | Target branch key. Empty/omit = search **all** allowed sales branches (gateway merge). | مفتاح المحافظة. فارغ = بحث في **كل** الفروع المسموحة (دمج عبر الـ Gateway). |

Also accepted as query on gateway when FE uses `withCityQuery(...)`.

### Limits & matching / الحدود ومنطق التطابق

| Constant | Value | Meaning |
|----------|-------|---------|
| `MaxNames` | 1500 | Max names per request |
| `MinimumLength` | 2 | After Arabic normalization |
| `MaxMatchesPerName` | 80 | Cap per name; `truncated: true` when exceeded |

| Query shape | Behavior EN | السلوك AR |
|-------------|--------------|-----------|
| &lt; 3 words | Logical match: exact normalized name, or stored name starts with `query + " "` | تطابق منطقي: اسم مطابق بعد التطبيع، أو الاسم المخزّن يبدأ بـ الاستعلام + مسافة |
| ≥ 3 words | **Family search**: drop first word; match on remaining “family” key | **بحث عائلي**: إسقاط الاسم الأول؛ المطابقة على بقية الاسم |

Normalization: Arabic diacritics stripped, alef variants unified, etc. (`SalesArabicText.Normalize`).

### Success response (shape)

```json
{
  "readOnly": true,
  "nameCount": 2,
  "foundCount": 1,
  "missingCount": 1,
  "queries": [
    {
      "rowNumber": 2,
      "requestedName": "حسين محمد",
      "searchKey": "حسين محمد",
      "usedFamilySearch": false,
      "found": true,
      "matchCount": 1,
      "truncated": false,
      "warning": null,
      "matches": [
        {
          "resultKey": "DatabaseCompanyBasra:123",
          "cityValue": "DatabaseCompanyBasra",
          "cityName": "البصرة",
          "customerId": 123,
          "fullName": "حسين محمد علي",
          "phone": "0780...",
          "province": "...",
          "address": "...",
          "delegateName": "...",
          "delegateId": 1,
          "amountTotalSales": 1000000,
          "receiptsTotal": 200000,
          "amountRemaining": 800000,
          "sales": [
            {
              "saleId": 9,
              "saleDate": "2026-01-15T00:00:00",
              "saleAmount": 1000000,
              "receiptsTotal": 200000,
              "amountRemaining": 800000,
              "accountZero": false
            }
          ]
        }
      ]
    }
  ]
}
```

| Field | EN | AR |
|-------|----|----|
| `readOnly` | Always true — search does not mutate DB | دائماً true — البحث لا يعدّل قاعدة البيانات |
| `rowNumber` | Excel-oriented row (header ≈ row 1 → first data row = 2) | رقم صف تقريبي للإكسل (البيانات تبدأ من 2) |
| `usedFamilySearch` | True when query had ≥ 3 tokens | true عند بحث عائلي (≥ 3 مقاطع) |
| `resultKey` | `{cityValue}:{customerId}` unique across branches | مفتاح فريد عبر الفروع |
| `warning` | Empty name, too short, or truncated results | اسم فارغ / قصير / نتائج مقطوعة |

### Errors

| Status | When |
|--------|------|
| 400 | Too many names; no branches; invalid city |
| 401/403 | Auth / policy |
| 4xx/5xx from branch | Propagated / skipped in fan-out (failed branches omitted from merge) |

### Excel file contract (UI) / عقد ملف الإكسل في الواجهة

- First sheet only.
- Header cell recognized as name: `اسم الزبون`, `اسم العميل`, `name`, `fullname`, `الاسم`, `customername`.
- FE rejects &gt; 1500 names before calling API.
- Template download: column `اسم الزبون`.

---

## 4) Endpoint B — Sales Requests Excel Import

Used by the sales-manager requests board after client-side Excel parse (`parseSalesRequestExcel`).

### Gateway

```
POST /api/sales-manager/sales-requests/import
```

**Requires** `cityValue` (body or query).  
**Code:** `BE_SalesEmployee/Controllers/SalesManagerController.cs` → `Import`

### Branch

```
POST /api/sales-manager/sales-requests/import
```

**Code:** `BE_Company/Sales/Controllers/SalesManagerController.cs` → `ImportRequests`  
**Service:** `ISalesRequestService.ImportRowsAsync`  
**DTO:** `SalesRequestImportDTO`

```json
{
  "cityValue": "DatabaseCompanyNajaf",
  "rows": [ /* parsed request rows from Excel */ ]
}
```

| EN | AR |
|----|----|
| Creates sales requests in the **selected** province only. | ينشئ طلبات بيع في المحافظة **المحددة** فقط. |
| Not a customer catalog search — different from excel-search. | ليس بحث كتالوج زبائن — مختلف عن excel-search. |

---

## 5) Endpoint C — Legal Excel Search

Separate product path (accountant / legal), not the Sales Manager Excel page.

```
POST /api/Customers/LegalExcelSearch
```

**Code:** `BE_Company/Controllers/CustomersController.cs`  
**FE:** `accountant-legal-search.vue` → `${apiUrl}Customers/LegalExcelSearch`

### Body

```json
{
  "queries": [
    { "name": "...", "phone": "..." }
  ]
}
```

(Exact property names follow `LegalSearchQueryInput` on the company API.)

### Behavior

| EN | AR |
|----|----|
| Matches **full triple name** and/or **phone** within the **branch** catalog. | تطابق **اسم ثلاثي كامل** و/أو **هاتف** داخل كتالوج **الفرع**. |
| Returns per-query name/phone hits + `exportRows` for download. | يعيد نتائج الاسم/الهاتف لكل صف + `exportRows` للتصدير. |

---

## 6) Related (not Excel upload, same catalog)

These reuse `ISalesExcelCustomerSearchCatalog` / name similarity but are **not** the Excel upload API:

| Endpoint (gateway / company) | Purpose |
|------------------------------|---------|
| `POST .../sales-manager/sales-requests/evaluate` | Program evaluation chips (triple / phone / fatherGrandfather / **sonFather**) |
| `POST .../sales-manager/sales-requests/evaluation-hits` | Paged hits for one evaluation category |
| Exception review matchers | Existing/New classification (phone + triple only) |

---

## 7) Source map / خريطة الملفات

| Concern | Path |
|---------|------|
| Gateway controller | `BE_SalesEmployee/Controllers/SalesManagerController.cs` |
| Fan-out + merge | `BE_SalesEmployee/Sales/Services/SalesManagerBranchAggregator.cs` |
| Branch controller | `BE_Company/Sales/Controllers/SalesManagerController.cs` |
| Search service | `BE_Company/Sales/Services/SalesExcelCustomerSearchService.cs` |
| DB catalog | `BE_Company/Sales/Services/SalesExcelCustomerSearchCatalog.cs` |
| DTOs | `BE_Company/Sales/DTO/SalesExcelSearchDTOs.cs` |
| Name match | `BE_Company/Sales/Services/SalesArabicText.cs` (`SalesCustomerNameMatch`) |
| FE Excel search page | `FE_Company/src/pages/sales-manager-excel-search.vue` |
| FE API helper | `FE_Company/src/composables/salesManagerApi.js` (`smPost`, `withCityQuery`) |
| Legal Excel | `BE_Company/Controllers/CustomersController.cs` + `accountant-legal-search.vue` |

---

## 8) Quick test checklist / قائمة تحقق سريعة

1. Auth as Sales Manager (or Director read).
2. `POST /api/sales-manager/customers/excel-search` with 2–3 Arabic names and optional `cityValue`.
3. Confirm `foundCount` / `missingCount` and `matches[].sales`.
4. Omit `cityValue` and confirm multi-branch `resultKey` prefixes.
5. Send &gt; 1500 names → 400.
6. (Optional) Legal: `POST /api/Customers/LegalExcelSearch` with name+phone rows.
7. (Optional) Import: parse Excel in UI → `POST .../sales-requests/import` with `cityValue`.
