# Integration with the existing company system (qalatsystemfromzaid-main)

`BE_CustomerApp` never connects to branch databases directly. It only calls the **Sales Gateway**
(`BE_SalesEmployee`), using the header `X-Sales-Gateway-Key: <gateway InternalApiKey>`. The gateway then
calls each branch's `BE_Company` over HTTP, the same way existing features already work.

```
Mobile app / Web store ──► BE_CustomerApp (central DB: CustomerAppDb)
FE_Company dashboard  ──►        │   (admin/* endpoints, gateway JWT validated with the shared Jwt:Key)
                                 ▼
                     BE_SalesEmployee  api/internal/customer-app/*   (X-Sales-Gateway-Key)
                                 ▼
                 BE_Company (each Qalaa branch)  api/internal/customer-app/*  +  existing CRM intake
```

The branches in scope are قلعة الضمان only: cityValues 1–6 and 8–14.
Excluded: 7 الكوت, 15 الرصافة عقيل, 16 ميسان, 18 الكرخ عقيل. The rule is the existing `SalesOverviewBranches.IsAqeel` / `IsExcluded`.

## Gateway endpoints (new) — `BE_SalesEmployee/Controllers/InternalCustomerAppController.cs`
Auth: `X-Sales-Gateway-Key` must equal config `InternalApiKey` (same check as `InternalCrmSalesRequestsController`).

| Method & route | Purpose |
|---|---|
| `GET api/internal/customer-app/branches` | `[{ cityValue, name }]` Qalaa sales branches |
| `GET api/internal/customer-app/accounts-by-phone?phone=07…` | Asks every in-scope branch in parallel (8 s timeout per branch). Returns `{ partial, unavailableBranches:[name], accounts:[Account] }` |
| `GET api/internal/customer-app/accounts/{cityValue}/{customerId}?phone=07…` | Detail `{ account, sales:[…], payments:[…] }`. The branch returns 404 when the phone does not match |
| `POST api/internal/customer-app/sales-requests` | Body `{ orderUuid, cityValue, customerName, customerPhone, customerProvince, customerAddress, notes, productName }`. Forwards to that branch's existing `internal/crm/sales-requests` with `crmOrderUuid=orderUuid` and `source="CustomerApp"`. Returns `{ cityValue, cityName, salesRequestId, status, idempotentReplay }` |
| `POST api/internal/customer-app/sales-requests/status` | Body `{ items:[{ cityValue, orderUuid }] }` (≤ 200). Groups items by city. Returns `{ items:[{ orderUuid, cityValue, found, salesRequestId, status, isDeleted, rejectionReason, convertedToSaleId, updatedAtUtc }] }` |

`Account` = `{ cityValue, cityName, customerId, customerName, saleDate, items:[{name,quantity}], totalAmount, dailyInstallment, paidAmount, remainingAmount, paymentsCount, lastPaymentDate }`.
All amounts are IQD integers, taken from the branch's existing `*Denar` view columns.

## Branch endpoints (new) — `BE_Company/Sales/Controllers/InternalCustomerAppController.cs`
Auth: same key check as `BE_Company/Controllers/InternalCrmSalesRequestsController.cs`.

- `GET api/internal/customer-app/accounts-by-phone?phone=`
  - Phone match: digits-only, last 10 digits = `SalesPhoneNormalizer.ForMatch(phone)`, compared against `Customers.PhoneNumber`.
  - Excludes `IsFakeSale=1`.
  - One `Account` per `CustomerID`. Totals come from `View_CustomersDelegate` (`AmountTotalSales`, `AmountDaySales`, `ReceiptsTotal`, `AmountRemaining`); items come from `View_CustomersSales`.
- `GET api/internal/customer-app/accounts/{customerId}?phone=` → account + sales (`View_CustomersSales`) + payments (`View_CustomersPayments`: id, date, `AmountDenar`). Returns 404 if the phone does not match.
- `POST api/internal/customer-app/sales-requests/status` `{ orderUuids:[guid] }` → reads `SalesRequests` by `ExternalCrmOrderUuid`.

## Existing CRM intake change
`CrmSalesRequestIntakeService` accepts an optional `Source`. When `Source == "CustomerApp"`:
- `CustomerSourceType = "CustomerApp"`
- `CreatedByName = "تطبيق الزبون"`
- `CreatedByUserType = "CustomerApp"`

Everything else is unchanged: idempotency, evaluation, round-robin auto-assign. Because the CRM board-status forwarder only fires for `CustomerSourceType="Crm"`, the old CRM never receives these rows. `BE_CustomerApp` reads their status by polling the status endpoint every 2 minutes.

## Dashboard auth
`BE_CustomerApp` validates FE_Company's central gateway JWT (HS256) with config `GatewayJwt:Key`, which must be the same value as the gateway's `Jwt:Key`.
Allowed `UserType` values: `المدير`, `مدير مبيعات`. The actor name comes from the `UserName` claim.
