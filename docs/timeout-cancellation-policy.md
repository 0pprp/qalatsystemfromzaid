# Timeout and cancellation policy (F17)

Unified guidance — **do not raise timeouts to hide slowness**; surface incomplete branches.

| Hop | Limit | Source |
|---|---|---|
| BranchProxy HttpClient | 120s | `BranchProxyTimeouts.HttpClientSeconds` |
| Per-branch CancelAfter (short) | 8s | `BranchProxyTimeouts.DefaultBranch` / Aggregator |
| Multi-branch soft fan-out | 1.4s | `FanoutSoftDeadline.MultiBranchTimeout` |
| Long list fan-out | 25s | `FanoutSoftDeadline.LongListBranchTimeout` |
| Excel / evaluate | 90s | Aggregator `ExcelSearchTimeout` |
| Delegate refresh HTTP (select/week) | 30s / 20s | `DelegateDataRefreshService` |
| Delegate customers fetch | 60s each, max 3 concurrent | same |
| Payment sync HTTP | existing payment_sync timeouts | `PaymentSyncService` |
| SQL guidance | 30s | ops / connection default |

**Cancellation:** linked CTS from request → branch CancelAfter. Failed hops must appear in `failedBranches` / `partial=true`, never as a silent zero total.

**SQL:** command timeouts must stay below HttpClient timeout with margin.
