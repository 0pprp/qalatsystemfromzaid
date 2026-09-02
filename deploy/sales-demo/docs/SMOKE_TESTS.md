# Sales Demo Smoke Tests

Run after Linux Demo deploy. Do not run against Production.

Gate: `SELECT DB_NAME()` = `DatabaseCompanyNajaf_DEMO`.

## Employee app (Flutter → BE_SalesEmployee :5280)

1. LOGIN موظف المبيعات — JWT, city `najaf-demo`.
2. START SHIFT — `POST /api/sales/shifts/start`.
3. CUSTOMER SEARCH — `GET /api/sales/customers/search?q=`.
4. INVENTORY — `GET /api/sales/inventory`.
5. CREATE SALE DRAFT — `POST /api/sales` with evaluation and items.
6. REJECTED SALE — evaluation `1` / مرفوض → status `Rejected`, no complete, no inventory deduct.
7. ACCEPTED ×2 — evaluation `2` / مقبول → final price = 2 × base.
8. COMPLETE SALE — `POST /api/sales/{id}/complete` (employee only).
9. INVENTORY DEDUCT — stock decreased once; retry complete is idempotent.
10. PDF CONTRACT — list documents then authorized download.
11. PDF PROMISSORY NOTE — second document; company name **قلعة الضمان**.
12. OFFLINE GPS SYNC — store points offline, `POST /api/sales/location/batch`; stale older points must not move the live marker backward.

## Sales requests

13. SALES REQUEST CREATE — manager `POST /api/sales-manager/sales-requests`.
14. EMPLOYEE RECEIVES REQUEST — `GET /api/sales/requests` shows only assigned rows; New badge.
15. REQUEST → SALE — view / start-processing optional; create sale with `salesRequestId`; status `ConvertedToSale`; complete sale → request `Completed`.

## SalesManager (FE_Company)

16. MANAGER EMPLOYEES — `GET /api/sales-manager/employees` (401 without token, 403 for employee role).
17. LIVE LOCATION — REST employees then SignalR `locationUpdated`; stale `capturedAt` ignored.
18. ROUTE HISTORY — `GET /api/sales-manager/employees/{id}/route?date=` ordered `capturedAt` ASC.
19. 03:00 CUTOFF — Iraq business day 03:00 → 03:00; auto-close uses shift cutoff.

## Negative checks

- Sales employee cannot GET GPS / sales-manager APIs / another employee's request.
- Manager cannot complete sale, edit GPS, or delete route from manager APIs.
- PDF URL `/Sale_{id}_Contract.pdf` is 404; download requires JWT.
