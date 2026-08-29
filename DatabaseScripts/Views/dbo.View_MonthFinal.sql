
create   view [dbo].[View_MonthFinal]
as
WITH ReceiptData AS (
    SELECT 
        CustomerID, 
        AmountDenar, 
        CAST(PaymentDate AS DATE) AS PaymentDate,
        DATEDIFF(DAY, PaymentDate, GETDATE()) AS DaysAgo
    FROM View_ReceiptCustomerDate
    WHERE PaymentDate >= CAST(GETDATE() - 30 AS DATE) -- استرجاع فقط آخر 30 يومًا
)
SELECT 
    C.CustomerID,
    ISNULL(P.[1], 0) AS Amount1,
    ISNULL(P.[2], 0) AS Amount2,
    ISNULL(P.[3], 0) AS Amount3,
    ISNULL(P.[4], 0) AS Amount4,
    ISNULL(P.[5], 0) AS Amount5,
    ISNULL(P.[6], 0) AS Amount6,
    ISNULL(P.[7], 0) AS Amount7,
    ISNULL(P.[8], 0) AS Amount8,
    ISNULL(P.[9], 0) AS Amount9,
    ISNULL(P.[10], 0) AS Amount10,
    ISNULL(P.[11], 0) AS Amount11,
    ISNULL(P.[12], 0) AS Amount12,
    ISNULL(P.[13], 0) AS Amount13,
    ISNULL(P.[14], 0) AS Amount14,
    ISNULL(P.[15], 0) AS Amount15,
    ISNULL(P.[16], 0) AS Amount16,
    ISNULL(P.[17], 0) AS Amount17,
    ISNULL(P.[18], 0) AS Amount18,
    ISNULL(P.[19], 0) AS Amount19,
    ISNULL(P.[20], 0) AS Amount20,
    ISNULL(P.[21], 0) AS Amount21,
    ISNULL(P.[22], 0) AS Amount22,
    ISNULL(P.[23], 0) AS Amount23,
    ISNULL(P.[24], 0) AS Amount24,
    ISNULL(P.[25], 0) AS Amount25,
    ISNULL(P.[26], 0) AS Amount26,
    ISNULL(P.[27], 0) AS Amount27,
    ISNULL(P.[28], 0) AS Amount28,
    ISNULL(P.[29], 0) AS Amount29,
    ISNULL(P.[30], 0) AS Amount30
FROM Customers C
LEFT JOIN (
    SELECT 
        CustomerID,
        [1], [2], [3], [4], [5], [6], [7], [8], [9], [10],
        [11], [12], [13], [14], [15], [16], [17], [18], [19], [20],
        [21], [22], [23], [24], [25], [26], [27], [28], [29], [30]
    FROM (
        SELECT 
            CustomerID, 
            AmountDenar, 
            DATEDIFF(DAY, PaymentDate, GETDATE()) AS DaysAgo
        FROM View_ReceiptCustomerDate
        WHERE PaymentDate >= CAST(GETDATE() - 30 AS DATE)
    ) AS SourceData
    PIVOT (
        MAX(AmountDenar) FOR DaysAgo IN (
            [1], [2], [3], [4], [5], [6], [7], [8], [9], [10],
            [11], [12], [13], [14], [15], [16], [17], [18], [19], [20],
            [21], [22], [23], [24], [25], [26], [27], [28], [29], [30]
        )
    ) AS PivotTable
) P ON C.CustomerID = P.CustomerID;

