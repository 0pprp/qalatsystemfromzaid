CREATE VIEW [dbo].[View_CustomersPaymentsRequestFinal]
AS
SELECT        dbo.CustomersPaymentsRequest.CustomersPaymentsRequestID, dbo.CustomersPaymentsRequest.CustomerID, dbo.CustomersPaymentsRequest.PaymentDate, dbo.CustomersPaymentsRequest.DelegateID, 
                         dbo.CustomersPaymentsRequest.Amount, dbo.CustomersPaymentsRequest.Location, dbo.View_CustomersInfoSimple.CustomerName, dbo.View_CustomersInfoSimple.DelegateName, 
                         dbo.View_CustomersInfoSimple.AmountTotalSales, dbo.View_CustomersInfoSimple.AmountDaySales, dbo.View_CustomersInfoSimple.ReceiptsTotal, dbo.View_CustomersInfoSimple.AmountRemaining
FROM            dbo.CustomersPaymentsRequest LEFT OUTER JOIN
                         dbo.View_CustomersInfoSimple ON dbo.CustomersPaymentsRequest.CustomerID = dbo.View_CustomersInfoSimple.CustomerID

