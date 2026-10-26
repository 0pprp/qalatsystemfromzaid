-- Amount1 = today, Amount2 = yesterday, … Amount7 = 6 days ago
-- Buckets use Asia/Baghdad business day (03:00). PaymentDate is CreatedAtUtc (UTC).
-- Matches GetDateWeek Date1..Date7.
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
