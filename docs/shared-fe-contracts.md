# Shared FE contracts (F24 — extract after behavior match)

Do **not** merge FE_Company and FE_ShortCompany trees wholesale.

Proven shared defects already fixed in both trees:
- refresh token client isolation (`tokenService` / axios interceptor)
- ratings cache clear on logout

Recommended next shared packages (only after byte/behavior diff):
1. `packages/auth-refresh` — refresh mutex + single-flight
2. `packages/session-dates` — business day / IraqDate parity with Flutter `IraqDate`
3. `packages/api-errors` — correlation id header + generic user messages

Until a package lands, change contracts in **both** FE trees in the same PR and link tests.
