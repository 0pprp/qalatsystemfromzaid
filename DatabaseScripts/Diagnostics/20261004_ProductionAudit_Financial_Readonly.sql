-- READ-ONLY production financial diagnostics.
-- Do NOT run UPDATE/DELETE against production from this script.
-- Correlate candidates manually before any remediation.

SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

PRINT N'=== CustomersPayments with zero AddToBox amount rows ===';
SELECT TOP 500
    cp.CustomerPaymentID,
    cp.CustomerID,
    cp.DelegateID,
    cp.PaymentDate,
    cp.ClientPaymentId,
    cp.BoundNumber
FROM dbo.CustomersPayments cp
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.AddToBox a WHERE a.CustomerPaymentID = cp.CustomerPaymentID
)
ORDER BY cp.CustomerPaymentID DESC;

PRINT N'=== CustomersPayments with multiple AddToBox rows ===';
SELECT TOP 500
    a.CustomerPaymentID,
    COUNT(1) AS AddToBoxRows,
    SUM(CAST(ISNULL(a.Amount, 0) AS FLOAT)) AS AmountSum
FROM dbo.AddToBox a
WHERE a.CustomerPaymentID IS NOT NULL
GROUP BY a.CustomerPaymentID
HAVING COUNT(1) > 1
ORDER BY AddToBoxRows DESC, a.CustomerPaymentID DESC;

PRINT N'=== Duplicate ClientPaymentId on CustomersPayments ===';
SELECT TOP 200
    ClientPaymentId,
    COUNT(1) AS PaymentRows
FROM dbo.CustomersPayments
WHERE ClientPaymentId IS NOT NULL AND LTRIM(RTRIM(ClientPaymentId)) <> N''
GROUP BY ClientPaymentId
HAVING COUNT(1) > 1;

PRINT N'=== Duplicate ClientPaymentId on request queue ===';
IF COL_LENGTH(N'dbo.CustomersPaymentsRequest', N'ClientPaymentId') IS NOT NULL
BEGIN
    SELECT TOP 200
        ClientPaymentId,
        COUNT(1) AS RequestRows
    FROM dbo.CustomersPaymentsRequest
    WHERE ClientPaymentId IS NOT NULL AND LTRIM(RTRIM(ClientPaymentId)) <> N''
    GROUP BY ClientPaymentId
    HAVING COUNT(1) > 1;
END

PRINT N'=== Near-time duplicate payments (same customer, same amount, <= 120 seconds) ===';
;WITH P AS (
    SELECT
        cp.CustomerPaymentID,
        cp.CustomerID,
        cp.PaymentDate,
        ISNULL(v.AmountDenar, 0) AS AmountDenar
    FROM dbo.CustomersPayments cp
    LEFT JOIN dbo.View_CustomersPayments v ON v.CustomerPaymentID = cp.CustomerPaymentID
)
SELECT TOP 500
    a.CustomerID,
    a.CustomerPaymentID AS PaymentA,
    b.CustomerPaymentID AS PaymentB,
    a.AmountDenar,
    a.PaymentDate AS DateA,
    b.PaymentDate AS DateB,
    DATEDIFF(SECOND, a.PaymentDate, b.PaymentDate) AS SecondsApart
FROM P a
INNER JOIN P b
    ON a.CustomerID = b.CustomerID
   AND a.CustomerPaymentID < b.CustomerPaymentID
   AND ABS(a.AmountDenar - b.AmountDenar) < 0.0001
   AND ABS(DATEDIFF(SECOND, a.PaymentDate, b.PaymentDate)) <= 120
ORDER BY a.PaymentDate DESC;

PRINT N'=== Waiver applications without payment linkage (if table exists) ===';
IF OBJECT_ID(N'dbo.SalesWaiverApplications', N'U') IS NOT NULL
BEGIN
    SELECT TOP 200 *
    FROM dbo.SalesWaiverApplications
    WHERE CustomerPaymentID IS NULL OR CustomerPaymentID <= 0
    ORDER BY AppliedAtUtc DESC;
END

PRINT N'Done (read-only).';
