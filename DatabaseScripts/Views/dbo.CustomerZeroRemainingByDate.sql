CREATE VIEW [dbo].[CustomerZeroRemainingByDate]
AS
WITH LastPayment AS (SELECT        CustomerID, MAX(PaymentDate) AS LastPaymentDate
                                                   FROM            dbo.CustomersPayments
                                                   GROUP BY CustomerID), TotalSales AS
    (SELECT        CustomerID, ROUND(ISNULL(SUM(AmountTotalSalesDenar), 0), - 3) AS AmountTotalSales
      FROM            dbo.ViewAmountTotalSales
      GROUP BY CustomerID), DaySales AS
    (SELECT        CustomerID, ROUND(ISNULL(SUM(AmountDaySalesDenar), 0), - 3) AS AmountDaySales
      FROM            dbo.ViewAmountDaySales
      GROUP BY CustomerID), TotalPayments AS
    (SELECT        CustomerID, ROUND(ISNULL(SUM(AmountDenar), 0), - 3) AS TotalPayments
      FROM            dbo.ViewCustomersPaymentsSingle
      GROUP BY CustomerID)
    SELECT        c.CustomerID, c.DelegateID, lp.LastPaymentDate, ts.AmountTotalSales, ds.AmountDaySales, ROUND(ISNULL(ts.AmountTotalSales - tp.TotalPayments, 0), - 3) AS AmountRemaining
     FROM            dbo.Customers AS c LEFT OUTER JOIN
                              LastPayment AS lp ON c.CustomerID = lp.CustomerID LEFT OUTER JOIN
                              TotalSales AS ts ON c.CustomerID = ts.CustomerID LEFT OUTER JOIN
                              DaySales AS ds ON c.CustomerID = ds.CustomerID LEFT OUTER JOIN
                              TotalPayments AS tp ON c.CustomerID = tp.CustomerID

