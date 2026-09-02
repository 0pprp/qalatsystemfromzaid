# Local publish output for Linux Demo (not Production).

Do not deploy from this folder until `SELECT DB_NAME()` = `DatabaseCompanyNajaf_DEMO`.

| Path | Content |
| --- | --- |
| `backend/` | `dotnet publish` BE_Company |
| `gateway/` | `dotnet publish` BE_SalesEmployee |
| `frontend/` | FE_Company Vite `dist` |
| `sql/` | Demo-only scripts in apply order |
| `docs/` | Templates, Linux checklist, smoke tests |
| `flutter/` | Debug APK if the local Android build succeeded |

See `docs/LINUX_DEPLOY_CHECKLIST.md`.
