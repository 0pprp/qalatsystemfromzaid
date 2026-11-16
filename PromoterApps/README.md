# Promoter apps — قلعة الضمان

This folder holds two separate apps. Each has its own API, database, Android app and dashboard section in `FE_Company`.

| | **تطبيق المروج الميداني** | **تطبيق مروجو قلعة الضمان** |
|---|---|---|
| Folder | [`FieldPromoterApp/`](FieldPromoterApp/) | [`OnlinePromotersApp/`](OnlinePromotersApp/) |
| Who uses it | The company's field promoters, who visit shops in person | Online promoters, who sell remotely for a commission |
| What it does | Sale orders with a shop photo and GPS pin; follows each order to sale and full payment; work day («الدوام») with live location; pinned shops and daily targets | Sale orders from a product list; wallet with commission per sale; sub-accounts with a cut; withdrawals (Qi Card / Zain Cash) with chat; marketing material |
| API | `backend/BE_CustomerApp` (.NET 10), port 5380 | `backend/BE_PromotersApp` (.NET 10), port 5430 |
| Live API | `http://agents.alsaaeidy.com/api/v1/` | `http://promoters.alsaaeidy.com/api/v1/` |
| Android app | `mobile/` (package `iq.qalaa.customer_app`) | `mobile/` (package `iq.qalaa.promoters_app`) |
| Dashboard menu | «تطبيق المسوّقين» → `/customer-app-*` pages | «المروّجون أونلاين» → `/promoters-app-*` pages |
| Dashboard API client | `FE_Company/src/composables/customerAppApi.js` | `FE_Company/src/composables/promotersAppApi.js` |
| Features & change log | [FieldPromoterApp/docs/FEATURES.md](FieldPromoterApp/docs/FEATURES.md) | [OnlinePromotersApp/docs/FEATURES.md](OnlinePromotersApp/docs/FEATURES.md) |

Both apps send their orders to the company system through the Sales Gateway (`BE_SalesEmployee`), never to branch databases directly. See [FieldPromoterApp/docs/INTEGRATION.md](FieldPromoterApp/docs/INTEGRATION.md). Every change made for them in `BE_Company`, `BE_SalesEmployee` and `FE_Company` is listed in [docs/CHANGES_IN_COMPANY_REPO.md](docs/CHANGES_IN_COMPANY_REPO.md).

## Why the code still says "CustomerApp"

تطبيق المروج الميداني started as a customer shopping app («تطبيق الزبون», v1), then became a commission-network app (v2), then a field-agents app (v3, from 2026-11-04). The online promoters app was added later as a separate app (2026-11-07).

The folders and docs use the current names. Some identifiers keep the old names on purpose, because renaming them would break things that are already live:

| Old name kept | Where | Why it stays |
|---|---|---|
| `BE_CustomerApp`, `CustomerAppDb` | API project and database | The live service and database use these names |
| `iq.qalaa.customer_app` | Android package id | A new id would be a different app: installed phones could not update |
| `customer_app` | Flutter package name | Used by every `package:customer_app/...` import |
| `/customer-app/`, `api/internal/customer-app/*` | nginx path, gateway and branch endpoints | Used by the live dashboard and servers |
| `customer-app-*` pages, `customerAppApi.js` | FE_Company | Page routes people already use |

Read `CustomerApp` / `customer-app` in code as **تطبيق المروج الميداني**.

## Layout

```
PromoterApps/
  FieldPromoterApp/          تطبيق المروج الميداني
    backend/                 BE_CustomerApp + tests, FieldPromoterApp.slnx
    mobile/                  Flutter app
    docs/                    FEATURES.md, INTEGRATION.md
  OnlinePromotersApp/        تطبيق مروجو قلعة الضمان
    backend/                 BE_PromotersApp + tests, OnlinePromotersApp.slnx
    mobile/                  Flutter app
    docs/                    FEATURES.md
  docs/
    CHANGES_IN_COMPANY_REPO.md
    history/                 v1 customer store and v2 commission network (no longer in use)
```

Not in git (keep local copies and share them privately): `appsettings*.json`, `android/key.properties` and the signing keystores, `google-services.json`, uploaded media in `App_Data/`, and the APK publish scripts in each app's `deploy/` folder.
