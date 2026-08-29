create   view [dbo].[CustomerZeroRemainingByDate_Final]
AS
WITH LastPaymentDateCTE AS (SELECT        CustomerID, MAX(PaymentDate) AS LastPaymentDate
                                                                      FROM            dbo.CustomersPayments
                                                                      GROUP BY CustomerID), TotalSalesCTE AS
    (SELECT        CustomerID, ROUND(ISNULL(SUM(AmountTotalSalesDenar), 0), - 3) AS TotalSales
      FROM            dbo.ViewAmountTotalSales_Final
      GROUP BY CustomerID), DaySalesCTE AS
    (SELECT        CustomerID, ROUND(ISNULL(SUM(AmountDaySalesDenar), 0), - 3) AS DaySales
      FROM            dbo.ViewAmountDaySales_Final
      GROUP BY CustomerID), PaymentsSummaryCTE AS
    (SELECT        CustomerID, ROUND(ISNULL(SUM(AmountDenar), 0), - 3) AS TotalPayments
      FROM            dbo.ViewCustomersPaymentsSingle_Final
      GROUP BY CustomerID)
    SELECT        C.CustomerID, C.DelegateID, LP.LastPaymentDate, TS.TotalSales AS AmountTotalSales, DS.DaySales AS AmountDaySales, ROUND(TS.TotalSales - PS.TotalPayments, - 3) AS AmountRemaining
     FROM            dbo.Customers AS C LEFT OUTER JOIN
                              LastPaymentDateCTE AS LP ON C.CustomerID = LP.CustomerID LEFT OUTER JOIN
                              TotalSalesCTE AS TS ON C.CustomerID = TS.CustomerID LEFT OUTER JOIN
                              DaySalesCTE AS DS ON C.CustomerID = DS.CustomerID LEFT OUTER JOIN
                              PaymentsSummaryCTE AS PS ON C.CustomerID = PS.CustomerID

