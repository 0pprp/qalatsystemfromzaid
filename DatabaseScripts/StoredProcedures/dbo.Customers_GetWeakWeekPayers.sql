CREATE OR ALTER PROC [dbo].[Customers_GetWeakWeekPayers]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        V.*,
        (ISNULL(V.Amount1, 0) + ISNULL(V.Amount2, 0) + ISNULL(V.Amount3, 0)
            + ISNULL(V.Amount4, 0) + ISNULL(V.Amount5, 0) + ISNULL(V.Amount6, 0)
            + ISNULL(V.Amount7, 0)) AS WeekPaid,
        CASE
            WHEN ISNULL(V.AmountTotalSales, 0) = 0 THEN 0
            ELSE ROUND(
                ((ISNULL(V.Amount1, 0) + ISNULL(V.Amount2, 0) + ISNULL(V.Amount3, 0)
                    + ISNULL(V.Amount4, 0) + ISNULL(V.Amount5, 0) + ISNULL(V.Amount6, 0)
                    + ISNULL(V.Amount7, 0)) * 100.0) / V.AmountTotalSales
            , 2)
        END AS PaidPercent
    FROM View_CustomerWeekPaymentDevice V
    WHERE ISNULL(V.AmountRemaining, 0) > 0
      AND ISNULL(V.AmountTotalSales, 0) > 0
      AND ISNULL(V.IsLegal, 0) = 0
      AND ISNULL(V.IsFakeSale, 0) = 0
      AND (
            (ISNULL(V.Amount1, 0) + ISNULL(V.Amount2, 0) + ISNULL(V.Amount3, 0)
                + ISNULL(V.Amount4, 0) + ISNULL(V.Amount5, 0) + ISNULL(V.Amount6, 0)
                + ISNULL(V.Amount7, 0)) * 100.0
          ) / V.AmountTotalSales <= 2
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
