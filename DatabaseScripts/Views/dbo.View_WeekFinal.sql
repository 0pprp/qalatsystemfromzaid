CREATE OR ALTER VIEW [dbo].[View_WeekFinal]
AS
-- Amount1 = today, Amount2 = yesterday, … Amount7 = 6 days ago
-- Matches delegate receipt labels («اليوم» / «أمس») and GetDateWeek Date1..Date7.
SELECT
    C.CustomerID,
    ISNULL(P.Amount1, 0) AS Amount1,
    ISNULL(P.Amount2, 0) AS Amount2,
    ISNULL(P.Amount3, 0) AS Amount3,
    ISNULL(P.Amount4, 0) AS Amount4,
    ISNULL(P.Amount5, 0) AS Amount5,
    ISNULL(P.Amount6, 0) AS Amount6,
    ISNULL(P.Amount7, 0) AS Amount7
FROM dbo.Customers C
LEFT JOIN (
    SELECT
        CustomerID,
        [0] AS Amount1,
        [1] AS Amount2,
        [2] AS Amount3,
        [3] AS Amount4,
        [4] AS Amount5,
        [5] AS Amount6,
        [6] AS Amount7
    FROM (
        SELECT
            CustomerID,
            AmountDenar,
            DATEDIFF(DAY, CONVERT(date, PaymentDate), CONVERT(date, GETDATE())) AS DaysAgo
        FROM dbo.View_ReceiptCustomerDate
        WHERE CONVERT(date, PaymentDate) >= CONVERT(date, DATEADD(day, -6, GETDATE()))
    ) AS SourceData
    PIVOT (
        MAX(AmountDenar) FOR DaysAgo IN ([0], [1], [2], [3], [4], [5], [6])
    ) AS PivotTable
) P ON C.CustomerID = P.CustomerID;
