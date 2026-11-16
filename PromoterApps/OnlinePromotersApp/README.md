# تطبيق مروجو قلعة الضمان (online promoters app)

The app for online promoters, shown in the dashboard as «المروّجون أونلاين». A promoter signs in with a username and password, and submits sale orders from the product list. Each completed sale credits their wallet. Full accounts can create sub-accounts and keep a cut of their commission. Promoters withdraw to Qi Card or Zain Cash, with a chat per request, and can download and share marketing material. There is no location tracking or work day.

Full feature list and change log: [docs/FEATURES.md](docs/FEATURES.md).

| Part | Folder |
|---|---|
| API (.NET 10 + Dapper + SQL Server) | `backend/BE_PromotersApp` |
| API tests | `backend/BE_PromotersApp.Tests` |
| Android app (Flutter) | `mobile/` |
| Dashboard pages | `FE_Company/src/pages/promoters-app-*.vue`, menu «المروّجون أونلاين» |

## Run locally

```bash
dotnet run --project backend/BE_PromotersApp --launch-profile http
```

```bash
dotnet test backend/OnlinePromotersApp.slnx
```

```bash
cd mobile && flutter test
```

## Release build

Release builds use `http://promoters.alsaaeidy.com/api/v1/` by default:

```bash
cd mobile && flutter build apk --release
```

Sign with `promoters-release.jks` and its `key.properties` (kept in `handover-secrets/promoters_app/`, not in git). Every update must use the same key. Publishing the APK as the mandatory update uses `deploy/publish-apk.sh`, which is also kept outside git.
