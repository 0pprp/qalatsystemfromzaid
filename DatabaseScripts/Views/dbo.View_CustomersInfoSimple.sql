create   view [dbo].[View_CustomersInfoSimple]
as
WITH SalesData AS (SELECT        CustomerID, ROUND(ISNULL(SUM(AmountTotalSalesDenar), 0), - 3) AS AmountTotalSales, ROUND(ISNULL(SUM(AmountDaySalesDenar), 0), - 3) AS AmountDaySales
                                             FROM            dbo.View_CustomersSalesDelegate
                                             GROUP BY CustomerID), PaymentData AS
    (SELECT        CustomerID, ROUND(ISNULL(SUM(AmountDenar), 0), - 3) AS ReceiptsTotal
      FROM            dbo.View_CustomersPaymentsDelegate
      GROUP BY CustomerID)
    SELECT        C.CustomerID, C.CustomerName, C.DelegateID, D.DelegateName, ISNULL(SalesData_1.AmountTotalSales, 0) AS AmountTotalSales, ISNULL(SalesData_1.AmountDaySales, 0) AS AmountDaySales, 
                              ISNULL(PaymentData_1.ReceiptsTotal, 0) AS ReceiptsTotal, ROUND(ISNULL(SalesData_1.AmountTotalSales, 0) - ISNULL(PaymentData_1.ReceiptsTotal, 0), - 3) AS AmountRemaining
     FROM            dbo.Customers AS C LEFT OUTER JOIN
                              dbo.Delegates AS D ON C.DelegateID = D.DelegateID LEFT OUTER JOIN
                              SalesData AS SalesData_1 ON C.CustomerID = SalesData_1.CustomerID LEFT OUTER JOIN
                              PaymentData AS PaymentData_1 ON C.CustomerID = PaymentData_1.CustomerID

