# Qalaa API endpoint registry generator

Static analysis only. Does not change application behavior, deploy, or push.

## Usage

From the repository root (`SalesHaider-main`):

```bat
py -3 -m pip install openpyxl
py -3 tools/api_registry/generate_api_registry.py
```

Outputs:

- `docs/Qalaa_API_Endpoint_Registry.xlsx`
- `docs/Qalaa_API_Endpoint_Registry.json`

The Excel workbook is generated from the JSON so a later refresh is repeatable.

## What it scans

- `BE_Company`, `BE_SalesEmployee`, `BE_DelegateWebApplication` controllers, hubs, `MapHub`
- Web: `FE_Company`, `FE_ShortCompany`, `CustomerPortal`
- Flutter: delegate, delegated manager, sales employee, sales filter, follower, `mobile_app`, `MA_ShortCompany`, `delegate_application_aqeel`
- Backend-to-backend `HttpClient` relative paths

Business-function names and duplicate classifications combine route/action evidence with curated mappings in `enrich.py`. Where static analysis cannot see a runtime caller, the registry records `NO STATIC CONSUMER FOUND` instead of marking the endpoint unused.
