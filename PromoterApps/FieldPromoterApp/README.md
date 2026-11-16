# تطبيق المروج الميداني (field promoter app)

The app for the company's field promoters. A promoter signs in with a username and password created in the dashboard. They show products, submit a sale order for a shop (a camera photo of the shop and a GPS pin are required), and follow it until it becomes a sale and is fully paid. They start and finish their work day («الدوام»), and while working the app sends their live location to the dashboard.

Full feature list and change log: [docs/FEATURES.md](docs/FEATURES.md). How it talks to the company system: [docs/INTEGRATION.md](docs/INTEGRATION.md).

The code still uses the old name `CustomerApp`; see [../README.md](../README.md#why-the-code-still-says-customerapp).

| Part | Folder |
|---|---|
| API (.NET 10 + Dapper + SQL Server) | `backend/BE_CustomerApp` |
| API tests | `backend/BE_CustomerApp.Tests` |
| Android app (Flutter) | `mobile/` |
| Dashboard pages | `FE_Company/src/pages/customer-app-*.vue`, menu «تطبيق المسوّقين» |

## Run locally

Needs .NET 10 and a local SQL Server Express. Development mode uses a fake gateway and seeds a demo catalog. Create an agent from the dashboard (or `POST admin/agents`) to sign in.

```bash
dotnet run --project backend/BE_CustomerApp --launch-profile http
```

```bash
dotnet test backend/FieldPromoterApp.slnx
```

The app (debug builds use `http://10.0.2.2:5380/api/v1/`, the emulator's address for your PC):

```bash
cd mobile && flutter run
```

## Release build

Always pass the live API address:

```bash
cd mobile && flutter build apk --release --dart-define=API_BASE_URL=http://agents.alsaaeidy.com/api/v1/
```

Version 1.4.9 was built without it and every phone that updated lost the server (see FEATURES.md, 2026-11-15). Sign with the field app's keystore, which is not in git. Publishing the APK as the mandatory update uses `deploy/publish-apk.sh`, which is also kept outside git.
