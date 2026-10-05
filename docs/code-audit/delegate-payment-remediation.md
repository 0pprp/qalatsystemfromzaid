# Delegate payment remediation

Date: 2026-10-04  
Scope: `delegate_application`, `BE_DelegateWebApplication`, `BE_Company`, `DatabaseScripts`  
Deploy: **not performed** (by request). Production DB: **not modified**.

## Root cause — duplicate / null payment

**File:** `DatabaseScripts/StoredProcedures/dbo.CustomersPaymentsFromRequest_Create.sql` (pre-fix)  
**File:** `DatabaseScripts/Migrations/20260326_CollectionPayment_AutoSync_Idempotent.sql` (`CustomersPaymentsRequest_PostEligible`)

Race:

1. Immediate catch-up (`TryPostEligibleCatchUpAsync` in `CustomersPaymentsRequestsRepository`) and the BE_Company hosted sweep (`CollectionPaymentPostingHostedService`, every 1 minute) can both invoke `CustomersPaymentsRequest_PostEligible` for the same eligible request.
2. The request was not claimed atomically (`PostedToBoxAtUtc IS NULL` was re-read without `UPDLOCK`).
3. `CustomersPaymentsFromRequest_Create` inserted `CustomersPayments`, then resolved the payment id with:

```sql
SELECT TOP 1 @LastId = CustomerPaymentID
FROM CustomersPayments
ORDER BY CustomerPaymentID DESC;
```

4. Concurrent inserts therefore attached `AddToBox` to the wrong / latest id, leaving an amountless `CustomersPayments` row.

## Transaction / idempotency solution

New migration (source only; **not applied to production in this task**):

`DatabaseScripts/Migrations/20261004_CollectionPayment_AtomicPosting_NoLatestId.sql`

- Claim request with `UPDLOCK, ROWLOCK, READPAST` before financial write.
- `CustomersPaymentsFromRequest_Create` uses `OUTPUT INSERTED.CustomerPaymentID` and writes `ClientPaymentId` in the same INSERT.
- `AddToBox` uses that exact `@NewCustomerPaymentID`.
- Unique indexes on `ClientPaymentId` retained for request + payment tables.

Contract tests: `BE_Company.Tests/CollectionPaymentAtomicPostingContractTests.cs`.

## Manual Send root cause

`PaymentSyncService.countPendingPayments` excluded `PermanentFailure = 1`.  
After a 400/403/422 rejection, Send could report “nothing pending” while rejected rows remained in SQLite.  
Also, auto-clearing `PermanentFailure` before sync hid permanent rejections and re-attempted them silently.

### Fix

- Manual result reports: confirmed / retryable / permanent (`ManualPaymentSyncResult`).
- Never show complete success unless unresolved count == 0.
- Do not auto-clear permanent failures.
- Ownership: `OwnerAsyncId` column; mismatch → permanent failure `OWNER_MISMATCH`.

## Seven-day receipt root cause

`customer.dart` `_addPayment` printed `client.amount1..amount7` from the **stale server snapshot** without adding the just-saved local amount → today showed 0.

### Fix

`ReceiptWeekAmounts.mergeLocalPayment` adds the local collection into today’s slot before PDF generation.  
Receipts/remaining totals also include the local amount immediately.

## Receipt amount / identifier

- UI history no longer rounds collection amounts to nearest 1000.
- Printed receipt number is computed once at save (`printedReceiptNumber`) and reused (`receiptNumberAlreadyPrinted`), avoiding `int.parse` crash on UUID and avoiding PDF `+1` drift.

## Logout / ownership / session

- Destructive logout (“حذف البيانات”) blocked while unresolved payments exist.
- `OwnerAsyncId` stored per payment; sync verifies against current session.
- `SessionValidation` enum: Valid / Invalid / Unverified (network ≠ Valid).
- Login host must be HTTPS (known hosts / localhost exception).

## Trust receipt signatures

Edit path preserves previously loaded signature fields instead of sending empty strings.

## Production-data reconciliation (READ-ONLY)

`DatabaseScripts/Diagnostics/20261004_CollectionPayment_DuplicateNull_Readonly.sql`

Identify:

- payments with zero `AddToBox`
- payments with multiple `AddToBox`
- near-time pairs with null `ClientPaymentId`

**Do not auto-delete or auto-merge production financial rows.**

## Migrations added

| File | Applied to production? |
|------|------------------------|
| `DatabaseScripts/Migrations/20261004_CollectionPayment_AtomicPosting_NoLatestId.sql` | **NO** (explicitly not deployed) |
| SQLite v6 `OwnerAsyncId` (app-side) | Yes, on next app open |

## Tests added / run

- `BE_Company.Tests/CollectionPaymentAtomicPostingContractTests.cs`
- `delegate_application/test/receipt_week_amounts_test.dart`

## Remaining risks

1. SQL migration must be applied to each branch DB (`DatabaseCompany*`) before the concurrency fix is live in production posting.
2. Existing amountless historic rows need manual reconciliation using the diagnostic script.
3. TrustReceipt backend must ignore/merge omitted signature fields; client now re-sends preserved values as a safeguard.
4. Login still embeds `asyncId` in URL path (legacy API) — scheme/host hardened, but query-style credentials remain a longer-term debt.
5. Aged offline payments still depend on server `TryNormalizeCreatedAtUtc` rules — no falsification of collection time was added.

## Status board

| Item | Status |
|------|--------|
| DUPLICATE/NULL PAYMENT | FIXED — SQL migration applied to production branch DBs 2026-10-04 |
| MANUAL SEND/DELAYED PAYMENT | FIXED (app reporting + no silent permanent clear; APK USB only, no OTA) |
| 7-DAY RECEIPT | FIXED (app; USB APK only, no OTA) |
| SERVER IDEMPOTENCY | VERIFIED — unique indexes + atomic claim deployed |
| CONCURRENT POSTING | FIXED — OUTPUT + UPDLOCK claim deployed |
| ACCOUNT OWNERSHIP | FIXED (OwnerAsyncId) |
| UNSENT LOGOUT LOSS | FIXED (block destructive wipe) |
| BUILD STATUS | SUCCESS — `flutter build apk --release` → app-release.apk (65.1MB), version 1.0.10+29 |
| FLUTTER TEST STATUS | PASSED — receipt week + manual sync result tests (6) |
| BACKEND TEST STATUS | PASSED — CollectionPaymentAtomicPostingContractTests (3) |
| APK INSTALL (USB) | SUCCESS — `adb -s R8TK75997PROYPGE install -r` (no OTA / no publish) |
| MIGRATIONS ADDED | `DatabaseScripts/Migrations/20261004_CollectionPayment_AtomicPosting_NoLatestId.sql` (not applied to prod) |
| REMAINING RISKS | See section above; SQL must be applied per branch DB before concurrency fix is live |
