CREATE OR ALTER PROC [dbo].[Customers_GetWeakWeekPayers]
AS
BEGIN
    SET NOCOUNT ON;

    -- نافذة متحركة: كل يوم تُحسب تسديدات آخر 7 أيام من الأمس رجوعاً (بدون اليوم)
    DECLARE @FromDate DATE = CAST(DATEADD(DAY, -7, GETDATE()) AS DATE);
    DECLARE @ToDate DATE = CAST(DATEADD(DAY, -1, GETDATE()) AS DATE);

    ;WITH Last7DaysPaid AS (
        SELECT
            CustomerID,
            ROUND(SUM(ISNULL(AmountDenar, 0)), -3) AS WeekPaid
        FROM View_ReceiptCustomerDate
        WHERE CAST(PaymentDate AS DATE) >= @FromDate
          AND CAST(PaymentDate AS DATE) <= @ToDate
        GROUP BY CustomerID
    )
    SELECT
        V.*,
        ISNULL(L.WeekPaid, 0) AS WeekPaid,
        CASE
            WHEN ISNULL(V.AmountTotalSales, 0) = 0 THEN 0
            ELSE ROUND((ISNULL(L.WeekPaid, 0) * 100.0) / V.AmountTotalSales, 2)
        END AS PaidPercent
    FROM View_CustomerWeekPaymentDevice V
    LEFT JOIN Last7DaysPaid L
        ON L.CustomerID = V.CustomerID
    WHERE ISNULL(V.AmountRemaining, 0) > 0
      AND ISNULL(V.AmountTotalSales, 0) > 0
      AND ISNULL(V.IsLegal, 0) = 0
      AND ISNULL(V.IsFakeSale, 0) = 0
      AND (ISNULL(L.WeekPaid, 0) * 100.0) / V.AmountTotalSales <= 3.8
      AND NOT EXISTS (
            SELECT 1
            FROM dbo.Customers C
            WHERE C.CustomerID = V.CustomerID
              AND (ISNULL(C.IsLegal, 0) = 1 OR ISNULL(C.IsFakeSale, 0) = 1)
      )
      AND NOT EXISTS (
            SELECT 1
            FROM CustomerWeekDecisions D
            WHERE D.CustomerID = V.CustomerID
              AND (
                    D.DecisionType IN (N'قانونية', N'وهمي')
                    OR (
                        D.DecisionType = N'متواصل'
                        AND D.SnoozeUntil IS NOT NULL
                        AND D.SnoozeUntil > GETDATE()
                    )
              )
      )
    ORDER BY PaidPercent ASC, V.CustomerName;
END
