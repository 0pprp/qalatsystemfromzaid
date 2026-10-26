-- Migration: View_WeekFinal same-day SUM + 0..6 day window (today inclusive).
-- Apply per branch. Does not change historical FLOAT/1448 policy.
CREATE OR ALTER VIEW [dbo].[View_WeekFinal]
AS
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
        SUM(AmountDenar) FOR DaysAgo IN ([0], [1], [2], [3], [4], [5], [6])
    ) AS PivotTable
) P ON C.CustomerID = P.CustomerID;
GO

CREATE OR ALTER PROC [dbo].[GetDateWeek]
AS
SELECT
  CONVERT(nvarchar(10), CONVERT(date, GETDATE()), 120) AS Date1,
  CONVERT(nvarchar(10), CONVERT(date, GETDATE()-1), 120) AS Date2,
  CONVERT(nvarchar(10), CONVERT(date, GETDATE()-2), 120) AS Date3,
  CONVERT(nvarchar(10), CONVERT(date, GETDATE()-3), 120) AS Date4,
  CONVERT(nvarchar(10), CONVERT(date, GETDATE()-4), 120) AS Date5,
  CONVERT(nvarchar(10), CONVERT(date, GETDATE()-5), 120) AS Date6,
  CONVERT(nvarchar(10), CONVERT(date, GETDATE()-6), 120) AS Date7;
GO
