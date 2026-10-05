-- READ-ONLY diagnostics for duplicate / amountless CustomersPayments.
-- Do NOT run UPDATE/DELETE from this script against production automatically.

SET NOCOUNT ON;

PRINT '=== CustomersPayments with ZERO AddToBox rows (amountless) ===';
SELECT TOP 200
    p.CustomerPaymentID,
    p.CustomerID,
    p.DelegateID,
    p.ClientPaymentId,
    p.PaymentDate,
    p.Location,
    p.CreatedDate
FROM dbo.CustomersPayments p
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.AddToBox a WHERE a.CustomerPaymentID = p.CustomerPaymentID
)
ORDER BY p.CustomerPaymentID DESC;

PRINT '=== CustomersPayments with MULTIPLE AddToBox rows ===';
SELECT
    a.CustomerPaymentID,
    COUNT(*) AS AddToBoxCount,
    CAST(SUM(ISNULL(a.Amount,0)*1448) AS bigint) AS TotalIqd
FROM dbo.AddToBox a
GROUP BY a.CustomerPaymentID
HAVING COUNT(*) > 1
ORDER BY AddToBoxCount DESC;

PRINT '=== Near-time pairs same customer / same ClientPaymentId NULL ===';
SELECT TOP 200
    p1.CustomerPaymentID AS PayA,
    p2.CustomerPaymentID AS PayB,
    p1.CustomerID,
    p1.DelegateID,
    p1.PaymentDate AS PayADate,
    p2.PaymentDate AS PayBDate,
    p1.ClientPaymentId AS ClientA,
    p2.ClientPaymentId AS ClientB
FROM dbo.CustomersPayments p1
JOIN dbo.CustomersPayments p2
  ON p1.CustomerID = p2.CustomerID
 AND p1.DelegateID = p2.DelegateID
 AND p1.CustomerPaymentID < p2.CustomerPaymentID
 AND ABS(DATEDIFF(SECOND, p1.PaymentDate, p2.PaymentDate)) <= 120
WHERE (p1.ClientPaymentId IS NULL OR p2.ClientPaymentId IS NULL)
ORDER BY p1.PaymentDate DESC;
