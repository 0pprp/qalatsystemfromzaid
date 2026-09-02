# Linux Demo Deploy Checklist

Do not run these steps until the target catalog is proven.

## Gate 0 — STOP unless DEMO

On the SQL instance that the sales module will use:

```sql
SELECT DB_NAME();
```

Required result:

```
DatabaseCompanyNajaf_DEMO
```

If the name is `DatabaseCompany`, `DatabaseCompanyNajaf`, or anything else: **STOP**.

No Production database. No remote execution from this repository session.

## Placeholders (fill on the server only)

- Nginx upstream BE_Company: `__BE_COMPANY_UPSTREAM__` (example `http://127.0.0.1:__BE_COMPANY_PORT__`)
- Nginx upstream BE_SalesEmployee: `__BE_SALES_GATEWAY_UPSTREAM__` (example `http://127.0.0.1:5280`)
- systemd / service name BE_Company: `__BE_COMPANY_SERVICE__`
- systemd / service name BE_SalesEmployee: `__BE_SALES_GATEWAY_SERVICE__`
- SQL host: `__SQL_HOST__`
- Frontend root: `__FE_DIST_PATH__`
- App_Data path: `__APP_DATA_SALES__` (must not be served by nginx)

## 1. Backup

Backup `DatabaseCompanyNajaf_DEMO` only. Do not backup or restore Production catalogs for this module.

## 2. SQL scripts (order)

Run only after Gate 0. Demo-only scripts. No DROP / DELETE / TRUNCATE.

1. `sql/01_SalesDrafts_DemoOnly.sql`
2. `sql/02_SalesComplete_DemoOnly.sql`
3. `sql/03_SalesTracking_DemoOnly.sql`
4. `sql/04_SalesRequests_DemoOnly.sql`

## 3. Backend publish

Copy `backend/` (BE_Company publish output) to the demo host.

Copy `docs/appsettings.demo.template.json` values into the running `appsettings` **without committing secrets**.

Confirm `AllowedDemoDatabase` = `DatabaseCompanyNajaf_DEMO`.

Confirm Cairo fonts exist under `Sales/Fonts/` in the published folder.

## 4. Gateway publish

Copy `gateway/` (BE_SalesEmployee publish output).

JWT key must match the intended demo gateway signing key.

## 5. Frontend deploy

Copy `frontend/` (Vite `dist`) behind nginx.

Set `VITE_MAPBOX_TOKEN` at **build time** only (see `docs/env.demo.example`). Missing token must not break the app; the live map shows a fallback list.

## 6. App_Data permissions

Create `__APP_DATA_SALES__` writable by the BE_Company process.

Nginx must **not** alias or serve `App_Data` or `*.pdf` from that folder.

PDF download is only via:

`GET /api/sales/{id}/documents/{documentId}/download`

## 7. Restart

Restart `__BE_COMPANY_SERVICE__` then `__BE_SALES_GATEWAY_SERVICE__`.

## 8. Nginx

Point `/api` employee traffic to the gateway upstream.

Point SalesManager FE `LinkCity` / company API to BE_Company (or gateway `/api/sales-manager` if the manager logs in through the gateway).

SignalR:

- `/hubs/sales-tracking` — WebSockets + negotiate

Test: `nginx -t` then reload.

## 9. Smoke tests

Follow `docs/SMOKE_TESTS.md`. Do not skip LOGIN + START SHIFT + COMPLETE SALE + SALES REQUEST.

## Flutter APK (optional)

Debug APK is under `flutter/` if the local debug build succeeded.

Later release (not this hardening run):

```bash
flutter build apk --release --dart-define=APP_ENV=demo
```

`APP_ENV=demo` never uses the mock repository.
