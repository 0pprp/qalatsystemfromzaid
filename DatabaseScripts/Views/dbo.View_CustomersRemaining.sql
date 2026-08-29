create   view [dbo].[View_CustomersRemaining]
AS
WITH SalesData AS (SELECT        CustomerID, ISNULL(SUM(AmountTotalSalesDenar), 0) AS TotalSales
                                             FROM            dbo.View_CustomersSales
                                             GROUP BY CustomerID), PaymentsData AS
    (SELECT        CustomerIDPayment, ISNULL(SUM(AmountDenar), 0) AS TotalPayments
      FROM            dbo.View_AddToBox
      GROUP BY CustomerIDPayment)
    SELECT        dbo.Customers.CustomerID, dbo.Customers.DelegateID, ROUND(ISNULL(SalesData_1.TotalSales, 0) - ISNULL(PaymentsData_1.TotalPayments, 0), - 3) AS AmountRemaining
     FROM            dbo.Customers LEFT OUTER JOIN
                              SalesData AS SalesData_1 ON dbo.Customers.CustomerID = SalesData_1.CustomerID LEFT OUTER JOIN
                              PaymentsData AS PaymentsData_1 ON dbo.Customers.CustomerID = PaymentsData_1.CustomerIDPayment

