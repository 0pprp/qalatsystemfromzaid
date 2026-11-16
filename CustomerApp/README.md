# قلعة الضمان — Field agents app (v3)

An Android app (distributed as an APK) for the company's field advertisers, with its API and dashboard pages:
- agents sign in with a username and password created in the dashboard;
- they show products and submit sale orders for customers, with a mandatory shop photo and GPS location;
- they follow each order until it becomes a sale in the company system, then until the sale is fully paid;
- they start and finish their working day («الدوام»); while working, the app sends their live location, and the dashboard shows attendance, hours and routes.

Commissions are recorded but hidden until the scheme is reworked. Earlier versions are in `backups/` (v1 store-ready, v2 commission network). See [docs/V3_FIELD_AGENTS.md](docs/V3_FIELD_AGENTS.md).

| Folder | What |
|---|---|
| `backend/BE_CustomerApp` | .NET 10 + Dapper + SQL Server API: OTP auth, catalog, orders, commission ledger, referrals, notifications, admin |
| `backend/BE_CustomerApp.Tests` | Unit tests (commission math, phone/name rules, status mapping) and end-to-end tests against a throw-away SQL DB |
| `web/store` | Vue 3 + Vuetify web store (Arabic RTL) |
| `mobile/customer_app` | Flutter app, shipped as an Android APK (Arabic RTL) |
| `backend/BE_PromotersApp` + `mobile/promoters_app` | Separate online promoters app (wallet, team, materials) — see [docs/PROMOTERS_APP.md](docs/PROMOTERS_APP.md) |
| `docs/` | API contract, integration design, deployment, v2 changes, list of company-repo changes |
| `backups/` | Zipped source of earlier versions |
| `company_repo_backup/` | Originals of every file modified in `qalatsystemfromzaid-main` |

Changes made inside the company repo (gateway, branch API, FE_Company dashboard pages) are listed in [docs/CHANGES_IN_COMPANY_REPO.md](docs/CHANGES_IN_COMPANY_REPO.md).

## Run locally

Requirements: .NET 10, a local SQL Server Express instance, and Node 20+.

Backend: Development mode uses a fake gateway and seeds a demo catalog. Create an agent from the dashboard (or `POST admin/agents`) to sign in.

```bash
dotnet run --project backend/BE_CustomerApp --launch-profile http
```

Web store, using the real local API on port 5380:

```bash
npm --prefix web/store install
```

```bash
npm --prefix web/store run dev
```

Tests:

```bash
dotnet test backend/CustomerApp.slnx
```

```bash
npm --prefix web/store test
```

## Docs
- [API contract](docs/API_CONTRACT.md)
- [Integration with the company system](docs/INTEGRATION.md)
- [Deployment](docs/DEPLOYMENT.md)
- [v3 field agents (current)](docs/V3_FIELD_AGENTS.md)
- [v2 changes (APK, commissions, network)](docs/V2_CHANGES.md)
- [Services needed](docs/STORE_POLICY_AND_SERVICES.md) (section B; section A applied to the store-ready v1 only)
