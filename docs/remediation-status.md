# Security remediation status (audit e95e96b → branch security/audit-remediation-f01-f20)

Last updated: 2026-10-05. External rotation / production deploy are **not** marked complete.

## Impact analysis (workspace gate)

| Surface | Impact |
|---|---|
| `BE_Company` | Portal tokens, password hashing, refresh token, UserLevelMiddleware ClaimsPrincipal, Swagger/CORS |
| `BE_SalesEmployee` | Director/SM bootstrap removed, Jwt:Key required, Swagger Dev-only, CORS production gate |
| `BE_DelegateWebApplication` | TrustReceipts + Customers + Payments authZ ownership |
| `FE_Company` / `FE_ShortCompany` | Refresh client isolation, logout cache clear |
| `CustomerPortal` / `MA_ShortCompany` | HTTPS + header portal token |
| Flutter `delegate_application` | AsyncId headers on TrustReceipts/Customers/Payments/refresh |
| Database | No schema migration applied. Password upgrade happens in-app on login. Connection-string placeholders only. |

## Findings

| ID | Finding | Code evidence of fix | Tests | Still requires publish / ops |
|---|---|---|---|---|
| F01 | Hardcoded Director/SM bootstrap logins | Removed bootstrap; `HasConfiguredAccounts` fail-closed | `SecurityBootstrapAuthTests` (4 passed) | Set Director/SM passwords via secrets; restart gateway |
| F02 | TrustReceipts unauthenticated | AsyncId + session actor + ownership | `DelegateComplaintAuthSecurityRemediationTests` (4 passed) | Deploy BE_Delegate + Flutter headers |
| F03 | Portal CBC token tampering | AES-GCM + expiry + purpose + branch + revoke; no CBC fallback | `PortalTokenSecurityTests` | Rotate portal key; regenerate links |
| F04 | Customers/Payments IDOR | AsyncId + ownership | Auth tests + Flutter headers | Deploy BE_Delegate + remaining clients |
| F05 | Plaintext passwords | PBKDF2 + upgrade on login; Password cleared from DTOs | `PasswordHasherTests` | Upgrade on login; ops rollback plan |
| F06 | Embedded secrets + JWT default | `RequireJwtKey`; scrubbed literals; `tools/secret-scan.ps1` | JWT bootstrap tests | **Rotate** live secrets; history rewrite not done |
| F13 | HTTP + token in URL | Kept working **HTTP** hosts for portals (HTTPS deferred); header portal token preferred | Manual | Switch to HTTPS only when explicitly approved for publish |
| F15 | Refresh missing + deadlock | `Users_RefreshToken` + FE refreshClient | FE node tests | Deploy BE_Company with FE |
| F16 | Ratings cache after logout | Clears session caches on logout | `tokenService.cache.test.mjs` (2 passed) | FE deploy |
| F20 (security) | ex.Message / Swagger / CORS | Generic errors; Swagger Dev-only; CORS origins in Production | Middleware trust tests | Set `Cors:AllowedOrigins` |

## Changed contracts

1. Delegate TrustReceipts/Customers/Payments require `X-Async-Id` (or `asyncId`). Optional `X-Delegate-Id` must match.
2. Portal tokens: AES-GCM only; legacy CBC → 401.
3. `POST /api/Users/Users_RefreshToken` `{ token }` → `{ token, expiration }`.
4. Gateway fails without `Jwt:Key` (≥32). Director/SM fail without configured passwords.
5. User passwords stored as `pbkdf2$…`.

## Local test results

- SecurityBootstrapAuthTests: 4 passed
- Portal/Password/Middleware tests: 9 passed
- DelegateComplaintAuthSecurityRemediationTests: 4 passed
- FE cache/refresh marker tests: 2 passed
- secret-scan.ps1: clean
- Collection* + readiness contract tests: **31 passed**
- Flutter `receipt_week_amounts_test` (delegate_application): **6 passed**
- Flutter `receipt_week_amounts_test` (delegate_application_aqeel): **6 passed**
- Isolated SQL Server concurrency for CustomersSales_Create: **not run** (no isolated SQL in this environment)
- Production balances / live posting: **not touched**
- Phase 3 .NET baseline (all four suites): **1002/1002** — [baseline-dotnet-tests.md](baseline-dotnet-tests.md)
- Phase 3 Flutter smokes (mutex/refresh, aqeel, MA, DM, mobile_app): see [baseline-flutter-tests.md](baseline-flutter-tests.md)

## Ops verification (not completed here)

Configure secrets, deploy three backends + FE/Portal/Flutter together, smoke F01–F04/F15, then **externally rotate** any credential that ever appeared in git. Deleting text is not rotation.

---

# Financial integrity (F07–F12 / F21 / F25) — same branch

See also: [docs/financial-integrity-remediation.md](financial-integrity-remediation.md).

| ID | Finding | Code evidence | Tests / ops |
|---|---|---|---|
| F07 | CustomersSales_Create IDENT_CURRENT / race | SCOPE_IDENTITY+txn+UPDLOCK BoundNumber; SalesComplete throws instead of IDENT_CURRENT; Buys/PaymentsApprove migrations | Isolated SQL not run here |
| F08 | InMemory MF/DM in prod | SQL stores + Production requires SalesGateway | Restart survives with SQL |
| F09 | aqeel delete-before-fetch / logout wipe | Sync fetch-then-replace; logout guard; send never deletes on error | Flutter tests when SDK available |
| F10/F11 | Session / legacy ClientPaymentId / idempotency | AwaitingSession; legacy-local-{id}; ownership retained | Payment sync unit tests |
| F12 | Branch sum incomplete as full | SumCountAsync `complete`/`partial`/`failedBranches` | Contract in aggregator |
| F21 | Migrations + collection readiness | `__SchemaMigrations` ledger; `GET /api/CollectionPostingReadiness` | 503 when PostEligible missing |
| F25 | Currency FLOAT / 1448 | Documented; no silent change | Policy confirmation needed for 1448 |

**Publish:** still blocked unless explicitly requested.

---

# Phase 3 — Performance / quality / CI (F14, F17–F20, F22–F26)

See [phase3-verification-matrix.md](phase3-verification-matrix.md), [release-signing.md](release-signing.md), [timeout-cancellation-policy.md](timeout-cancellation-policy.md), [release-manifest.md](release-manifest.md).

| ID | Status | Evidence |
|---|---|---|
| F14 | Partial | `delegate_application` release fails without `key.properties`; continuity doc; other apps still debug-signed |
| F17 | Partial | Soft TTL, concurrent fetch cap=3, SQLite batch; no delta sync yet |
| F18 | Code | `GetCustomersDelegateAll` explicit columns; Portal payments date filter in SQL |
| F19 | Fixed | `AdminCitiesService` → `IMemoryCache` + reload semaphore |
| F20 | Partial | Correlation middleware; mobile LogInterceptor `kDebugMode` only |
| F22 | Partial | `.github/workflows/ci.yml`; **dotnet baseline 1002/1002** ([baseline-dotnet-tests.md](baseline-dotnet-tests.md)) |
| F23 | Partial | Real PaymentSync + DelegateDataRefresh mutex tests; aqeel/DM/MA widget fixes; mobile_app harness |
| F24 | Incremental | `BranchAggregateCountResult`; shared FE contract notes — no Clean Architecture rename |
| F25 | Unchanged policy | Docs only; ÷1448 open question |
| F26 | Partial | Portal aria-labels + reduced-motion; MA zoom + WebView retry |

Security (F01–F06…) and financial (F07–F12…) fixes from prior waves are **retained** (not weakened).
