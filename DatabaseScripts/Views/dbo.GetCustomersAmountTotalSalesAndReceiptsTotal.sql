create   view [dbo].[GetCustomersAmountTotalSalesAndReceiptsTotal]
AS
SELECT        c.CustomerID, c.CustomerName, c.AsyncID, ROUND(ISNULL(SUM(s.AmountTotalSalesDenar), 0), - 3) AS AmountTotalSales, ROUND(ISNULL(SUM(p.AmountDenar), 0), - 3) AS ReceiptsTotal
FROM            dbo.Customers AS c LEFT OUTER JOIN
                         dbo.View_CustomersSalesDelegate AS s ON c.CustomerID = s.CustomerID LEFT OUTER JOIN
                         dbo.View_CustomersPaymentsDelegate AS p ON c.CustomerID = p.CustomerID
GROUP BY c.CustomerID, c.CustomerName, c.AsyncID

