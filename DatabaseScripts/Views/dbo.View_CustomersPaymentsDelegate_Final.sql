create   view [dbo].[View_CustomersPaymentsDelegate_Final]
AS
WITH PaymentsSummary AS (SELECT        CustomerPaymentID, SUM(Amount * 1448) AS TotalAmountDenar
                                                                 FROM            dbo.AddToBox
                                                                 GROUP BY CustomerPaymentID)
    SELECT        dbo.CustomersPayments.CustomerPaymentID, dbo.CustomersPayments.PaymentDate, dbo.CustomersPayments.DelegateID, dbo.CustomersPayments.CustomerID, ISNULL(PaymentsSummary_1.TotalAmountDenar, 0) 
                              AS AmountDenar
     FROM            dbo.CustomersPayments LEFT OUTER JOIN
                              PaymentsSummary AS PaymentsSummary_1 ON dbo.CustomersPayments.CustomerPaymentID = PaymentsSummary_1.CustomerPaymentID

