CREATE proc [dbo].[GetDelegateMonthAmount]
as
WITH PaymentSums AS (
    SELECT 
        DelegateID, 
        SUM(AmountDenar) AS TotalAmount
    FROM 
        View_CustomersPayments
    WHERE 
        CONVERT(date, PaymentDate) >= CONVERT(date, GETDATE() - 30) and 
	  CONVERT(date, PaymentDate) <= CONVERT(date, GETDATE())
    GROUP BY 
        DelegateID
)
SELECT 
    d.DelegateID,d.DelegateName, 
    ps.TotalAmount AS Amount
FROM 
    Delegates d
JOIN 
    PaymentSums ps ON d.DelegateID = ps.DelegateID
WHERE 
    d.DelegateState = 'true' AND ps.TotalAmount > 0
ORDER BY 
    ps.TotalAmount DESC;


