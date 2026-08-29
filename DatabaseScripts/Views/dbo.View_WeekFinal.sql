create   view [dbo].[View_WeekFinal]
as
WITH ReceiptData AS (
    SELECT 
        CustomerID, 
        AmountDenar, 
        CAST(PaymentDate AS DATE) AS PaymentDate,
        DATEDIFF(DAY, PaymentDate, GETDATE()) AS DaysAgo
    FROM View_ReceiptCustomerDate
    WHERE PaymentDate >= CAST(GETDATE() - 7 AS DATE) -- استرجاع فقط آخر 7 أيام
)
SELECT 
    C.CustomerID,
    ISNULL(P.Amount1, 0) AS Amount1,
    ISNULL(P.Amount2, 0) AS Amount2,
    ISNULL(P.Amount3, 0) AS Amount3,
    ISNULL(P.Amount4, 0) AS Amount4,
    ISNULL(P.Amount5, 0) AS Amount5,
    ISNULL(P.Amount6, 0) AS Amount6,
    ISNULL(P.Amount7, 0) AS Amount7
FROM Customers C
LEFT JOIN (
    SELECT 
        CustomerID,
        [1] AS Amount1, 
        [2] AS Amount2, 
        [3] AS Amount3, 
        [4] AS Amount4, 
        [5] AS Amount5, 
        [6] AS Amount6, 
        [7] AS Amount7
    FROM (
        SELECT 
            CustomerID, 
            AmountDenar, 
            DATEDIFF(DAY, PaymentDate, GETDATE()) AS DaysAgo
        FROM View_ReceiptCustomerDate
        WHERE PaymentDate >= CAST(GETDATE() - 7 AS DATE)
    ) AS SourceData
    PIVOT (
        MAX(AmountDenar) FOR DaysAgo IN ([1], [2], [3], [4], [5], [6], [7])
    ) AS PivotTable
) P ON C.CustomerID = P.CustomerID;



