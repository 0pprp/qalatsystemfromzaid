CREATE PROC [dbo].[Delegates_GetDashboardData]
  @FromDate DATETIME,
  @ToDate   DATETIME
AS
BEGIN
  SET NOCOUNT ON;;WITH
  SalesAgg AS (
    SELECT
      cs.DelegateID,
      COUNT(*)                             AS NumberOfSale,
      SUM(cs.NumberOfItemsSales)           AS NumberOfItemsSales,
      SUM(cs.AmountTotalSalesDenar)        AS AmountTotalSalesDenar,
      SUM(cs.AmountTotalCostDenar)        AS AmountTotalCostDenar,
      SUM(cs.AmountDaySalesDenar)          AS AmountDaySalesDenar
    FROM View_CustomersSalesDelegate_Final cs
    WHERE cs.DateCreate >= @FromDate
      AND cs.DateCreate <=  @ToDate
    GROUP BY cs.DelegateID
  ),
  DistinctCust AS (
    SELECT DISTINCT
      cs.DelegateID,
      cs.CustomerID
    FROM View_CustomersSalesDelegate_Final cs
    WHERE cs.DateCreate >= @FromDate
      AND cs.DateCreate <=  @ToDate
  ),
  PaymentAgg AS (
    SELECT
      dc.DelegateID,
      SUM(p.AmountDenar) AS AmountReceipt
    FROM DistinctCust dc
    INNER JOIN View_CustomersPayments p
      ON p.CustomerID = dc.CustomerID
    GROUP BY dc.DelegateID
  )
  SELECT
    d.DelegateID,
    d.DelegateName,
    COALESCE(s.NumberOfSale, 0)          AS NumberOfSale,
    COALESCE(s.NumberOfItemsSales, 0)    AS NumberOfItemsSales,
    COALESCE(s.AmountTotalSalesDenar, 0) AS AmountTotalSalesDenar,
    COALESCE(s.AmountTotalCostDenar, 0) AS AmountTotalCostDenar,
    COALESCE(s.AmountDaySalesDenar, 0)   AS AmountDaySalesDenar,
    COALESCE(p.AmountReceipt, 0)         AS AmountReceipt,
    COALESCE(s.AmountTotalSalesDenar, 0) - COALESCE(p.AmountReceipt, 0) AS AmountRemaining,
    CASE
      WHEN COALESCE(s.AmountTotalSalesDenar, 0) = 0 THEN 0
      ELSE CAST(
             ROUND(p.AmountReceipt * 100.0 / s.AmountTotalSalesDenar, 0)
           AS INT)
    END AS Rate
  FROM Delegates d
  LEFT JOIN SalesAgg   s ON s.DelegateID = d.DelegateID
  LEFT JOIN PaymentAgg p ON p.DelegateID = d.DelegateID
  where d.DelegateState=1;
END


