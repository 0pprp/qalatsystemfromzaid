create   view [dbo].[View_ReceiptCustomerDate]
AS
SELECT        dbo.Customers.CustomerID, dbo.CustomersPayments.CustomerPaymentID, ISNULL(dbo.AddToBox.Amount * 1448, 0) AS AmountDenar, dbo.CustomersPayments.PaymentDate
FROM            dbo.Customers LEFT OUTER JOIN
                         dbo.CustomersPayments ON dbo.Customers.CustomerID = dbo.CustomersPayments.CustomerID INNER JOIN
                         dbo.AddToBox ON dbo.CustomersPayments.CustomerPaymentID = dbo.AddToBox.CustomerPaymentID

