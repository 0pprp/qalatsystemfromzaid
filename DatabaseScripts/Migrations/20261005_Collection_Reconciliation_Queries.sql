-- Reconciliation helpers for isolated branch DBs only.
-- Do NOT run against production. Do NOT DELETE pending payments to "clean" queues.

-- 1) Request vs official payment (idempotency check)
-- SELECT r.ClientPaymentId, r.CustomersPaymentsRequestID, r.ReceivedAtUtc, r.EligibleForPostingAtUtc,
--        cp.CustomerPaymentID, cp.PaymentDate, cp.Amount
-- FROM dbo.CustomersPaymentsRequest r
-- LEFT JOIN dbo.CustomersPayments cp ON cp.ClientPaymentId = r.ClientPaymentId
-- WHERE r.ReceivedAtUtc >= DATEADD(day, -7, SYSUTCDATETIME())
-- ORDER BY r.ReceivedAtUtc DESC;

-- 2) Eligible but not posted (stuck)
-- SELECT *
-- FROM dbo.CustomersPaymentsRequest r
-- WHERE r.EligibleForPostingAtUtc <= SYSUTCDATETIME()
--   AND NOT EXISTS (SELECT 1 FROM dbo.CustomersPayments cp WHERE cp.ClientPaymentId = r.ClientPaymentId);

-- 3) Duplicate ClientPaymentId in requests (should be zero with unique index)
-- SELECT ClientPaymentId, COUNT(1) c
-- FROM dbo.CustomersPaymentsRequest
-- WHERE ClientPaymentId IS NOT NULL
-- GROUP BY ClientPaymentId HAVING COUNT(1) > 1;

PRINT N'Reconciliation queries are commented. Uncomment on isolated DB for inspection only.';
GO
