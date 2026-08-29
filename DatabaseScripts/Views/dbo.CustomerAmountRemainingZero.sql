create   view [dbo].[CustomerAmountRemainingZero]
AS
SELECT        c.CustomerID, ROUND(ISNULL(SUM(s.AmountTotalSalesDenar) - SUM(p.AmountDenar), 0), - 3) AS AmountRemaining
FROM            dbo.Customers AS c LEFT OUTER JOIN
                         dbo.View_CustomersSalesDelegate AS s ON c.CustomerID = s.CustomerID LEFT OUTER JOIN
                         dbo.View_CustomersPaymentsDelegate AS p ON c.CustomerID = p.CustomerID
GROUP BY c.CustomerID

