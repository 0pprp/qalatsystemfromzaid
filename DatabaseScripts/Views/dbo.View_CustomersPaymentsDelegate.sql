create   view [dbo].[View_CustomersPaymentsDelegate]
AS
WITH AmountData AS (SELECT        CustomerPaymentID, ISNULL(SUM(Amount * 1448), 0) AS AmountDenar
                                                 FROM            dbo.AddToBox
                                                 GROUP BY CustomerPaymentID)
    SELECT        dbo.CustomersPayments.CustomerPaymentID, dbo.CustomersPayments.PaymentDate, dbo.CustomersPayments.DelegateID, dbo.CustomersPayments.CustomerID, AmountData_1.AmountDenar
     FROM            dbo.CustomersPayments LEFT OUTER JOIN
                              AmountData AS AmountData_1 ON dbo.CustomersPayments.CustomerPaymentID = AmountData_1.CustomerPaymentID

