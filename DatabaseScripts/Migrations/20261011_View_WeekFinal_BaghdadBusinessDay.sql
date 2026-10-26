-- Align last-7-day receipt buckets with Asia/Baghdad business day (03:00 boundary).
-- PaymentDate from collection intake is CreatedAtUtc (UTC wall clock in DATETIME).
-- GetDateWeek labels: dd/MM for Date1=today … Date7=6 days ago.
SET NOCOUNT ON;
GO

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
            r.CustomerID,
            r.AmountDenar,
            DATEDIFF(
                DAY,
                CASE
                    WHEN DATEPART(HOUR, DATEADD(HOUR, 3, r.PaymentDate)) < 3
                        THEN DATEADD(DAY, -1, CONVERT(date, DATEADD(HOUR, 3, r.PaymentDate)))
                    ELSE CONVERT(date, DATEADD(HOUR, 3, r.PaymentDate))
                END,
                CASE
                    WHEN DATEPART(HOUR, DATEADD(HOUR, 3, SYSUTCDATETIME())) < 3
                        THEN DATEADD(DAY, -1, CONVERT(date, DATEADD(HOUR, 3, SYSUTCDATETIME())))
                    ELSE CONVERT(date, DATEADD(HOUR, 3, SYSUTCDATETIME()))
                END
            ) AS DaysAgo
        FROM dbo.View_ReceiptCustomerDate r
        WHERE CASE
                  WHEN DATEPART(HOUR, DATEADD(HOUR, 3, r.PaymentDate)) < 3
                      THEN DATEADD(DAY, -1, CONVERT(date, DATEADD(HOUR, 3, r.PaymentDate)))
                  ELSE CONVERT(date, DATEADD(HOUR, 3, r.PaymentDate))
              END
              >=
              DATEADD(
                  DAY,
                  -6,
                  CASE
                      WHEN DATEPART(HOUR, DATEADD(HOUR, 3, SYSUTCDATETIME())) < 3
                          THEN DATEADD(DAY, -1, CONVERT(date, DATEADD(HOUR, 3, SYSUTCDATETIME())))
                      ELSE CONVERT(date, DATEADD(HOUR, 3, SYSUTCDATETIME()))
                  END
              )
    ) AS SourceData
    PIVOT (
        SUM(AmountDenar) FOR DaysAgo IN ([0], [1], [2], [3], [4], [5], [6])
    ) AS PivotTable
) P ON C.CustomerID = P.CustomerID;
GO

CREATE OR ALTER PROC [dbo].[GetDateWeek]
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @BaghdadNow DATETIME2(3) = DATEADD(HOUR, 3, SYSUTCDATETIME());
    DECLARE @Today DATE =
        CASE
            WHEN DATEPART(HOUR, @BaghdadNow) < 3
                THEN DATEADD(DAY, -1, CONVERT(date, @BaghdadNow))
            ELSE CONVERT(date, @BaghdadNow)
        END;

    SELECT
        CONVERT(nvarchar(5), @Today, 3) AS Date1,
        CONVERT(nvarchar(5), DATEADD(DAY, -1, @Today), 3) AS Date2,
        CONVERT(nvarchar(5), DATEADD(DAY, -2, @Today), 3) AS Date3,
        CONVERT(nvarchar(5), DATEADD(DAY, -3, @Today), 3) AS Date4,
        CONVERT(nvarchar(5), DATEADD(DAY, -4, @Today), 3) AS Date5,
        CONVERT(nvarchar(5), DATEADD(DAY, -5, @Today), 3) AS Date6,
        CONVERT(nvarchar(5), DATEADD(DAY, -6, @Today), 3) AS Date7;
END
GO
