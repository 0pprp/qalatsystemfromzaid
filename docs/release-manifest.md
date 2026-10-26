# Release manifest (template)

Fill before any staging/demo publish. **Production publish is blocked until explicit approval.**

| Field | Value |
|---|---|
| Git commit | _(sha)_ |
| Branch | `security/audit-remediation-f01-f20` |
| Built at UTC | |
| BE_Company | version / image tag |
| BE_SalesEmployee | version / image tag |
| BE_DelegateWebApplication | version / image tag |
| SQL migration set | list MigrationIds from `__SchemaMigrations` |
| FE_Company | build hash |
| FE_ShortCompany | build hash |
| CustomerPortal | build hash |
| delegate_application APK | versionName / versionCode / signing cert SHA-256 |
| Other Flutter apps | … |

## Smoke checklist (staging only)

1. Login Director / SM / Delegate / Portal token
2. Create sale → complete → stock deduct (no cross-user temp items)
3. Record payment → PostPaymentIdempotent → retry same ClientPaymentId → one effect
4. Delegate receipt: today + 6 prior days amounts visible
5. Branch aggregate count with one failed branch → `partial=true`, not silent zero
6. Collection readiness `GET /api/CollectionPostingReadiness` → 200 or intentional 503
7. Logout does not wipe unresolved payments (delegate / aqeel)

## Rollback

1. Redeploy previous commit binaries
2. Do **not** DELETE pending payments to “fix” sync
3. SQL: restore prior SP from backup only after verifying no mid-flight identity issues
4. Mobile: sideload previous APK signed with **same** certificate

## Explicitly untested here

- Real field devices / production certificates
- Isolated SQL concurrency under load
- Backup restore drill
- Query Store plans on production-sized data
- Full 16-component release APK builds
